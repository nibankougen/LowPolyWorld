package handler

import (
	"encoding/json"
	"net/http"
	"strconv"
	"time"

	"github.com/go-chi/chi/v5"
	"github.com/nibankougen/LowPolyWorld/api/internal/adminauth"
	"github.com/nibankougen/LowPolyWorld/api/internal/middleware"
	"github.com/nibankougen/LowPolyWorld/api/internal/response"
	"github.com/nibankougen/LowPolyWorld/api/internal/trust"
)

type adminUserRow struct {
	UserID       string     `json:"user_id"`
	DisplayName  string     `json:"display_name"`
	AtName       string     `json:"at_name"`
	TrustLevel   string     `json:"trust_level"`
	TrustPoints  float64    `json:"trust_points"`
	Locked       bool       `json:"trust_level_locked"`
	IsRestricted bool       `json:"is_restricted"`
	SubTier      string     `json:"subscription_tier"`
	DeletedAt    *time.Time `json:"deleted_at,omitempty"`
	CreatedAt    time.Time  `json:"created_at"`
}

// AdminListUsers handles GET /admin/users.
// Query params: q (name/@name search), trust_level, is_restricted (true/false), after (cursor).
func (h *Handler) AdminListUsers(w http.ResponseWriter, r *http.Request) {
	q := r.URL.Query().Get("q")
	trustFilter := r.URL.Query().Get("trust_level")
	restrictedStr := r.URL.Query().Get("is_restricted")
	after := r.URL.Query().Get("after")
	limit := 50

	var restrictedFilter *bool
	if restrictedStr == "true" {
		v := true
		restrictedFilter = &v
	} else if restrictedStr == "false" {
		v := false
		restrictedFilter = &v
	}

	rows, err := h.DB.Query(r.Context(),
		`SELECT au.user_id, au.display_name, au.at_name,
		        au.trust_level, au.trust_points, au.trust_level_locked,
		        au.is_restricted, au.subscription_tier, au.deleted_at,
		        u.created_at
		 FROM active_users au
		 JOIN users u ON u.id = au.user_id
		 WHERE ($1 = '' OR au.display_name ILIKE '%'||$1||'%' OR au.at_name ILIKE '%'||$1||'%')
		   AND ($2 = '' OR au.trust_level = $2)
		   AND ($3::BOOLEAN IS NULL OR au.is_restricted = $3)
		   AND ($4 = '' OR u.created_at < (SELECT created_at FROM users WHERE id = $4::UUID))
		 ORDER BY u.created_at DESC
		 LIMIT $5`,
		q, trustFilter, restrictedFilter, after, limit+1,
	)
	if err != nil {
		h.Logger.Error("admin list users", "error", err)
		response.InternalError(w, r, h.Cfg.IsProduction())
		return
	}
	defer rows.Close()

	var users []adminUserRow
	for rows.Next() {
		var u adminUserRow
		if err := rows.Scan(&u.UserID, &u.DisplayName, &u.AtName,
			&u.TrustLevel, &u.TrustPoints, &u.Locked,
			&u.IsRestricted, &u.SubTier, &u.DeletedAt, &u.CreatedAt,
		); err != nil {
			h.Logger.Error("admin list users scan", "error", err)
			response.InternalError(w, r, h.Cfg.IsProduction())
			return
		}
		users = append(users, u)
	}

	var cursor response.Cursor
	if len(users) > limit {
		users = users[:limit]
		cursor.Next = users[len(users)-1].UserID
	}
	response.JSONCursor(w, http.StatusOK, users, cursor)
}

// AdminGetUser handles GET /admin/users/{userID}.
func (h *Handler) AdminGetUser(w http.ResponseWriter, r *http.Request) {
	userID := chi.URLParam(r, "userID")
	var u adminUserRow
	err := h.DB.QueryRow(r.Context(),
		`SELECT au.user_id, au.display_name, au.at_name,
		        au.trust_level, au.trust_points, au.trust_level_locked,
		        au.is_restricted, au.subscription_tier, au.deleted_at,
		        u.created_at
		 FROM active_users au
		 JOIN users u ON u.id = au.user_id
		 WHERE au.user_id = $1`,
		userID,
	).Scan(&u.UserID, &u.DisplayName, &u.AtName,
		&u.TrustLevel, &u.TrustPoints, &u.Locked,
		&u.IsRestricted, &u.SubTier, &u.DeletedAt, &u.CreatedAt,
	)
	if err != nil {
		response.Error(w, r, http.StatusNotFound, "not_found", "user not found")
		return
	}
	response.JSON(w, http.StatusOK, u)
}

// AdminSetRestriction handles PATCH /admin/users/{userID}/restriction.
// Body: { "restricted": true/false }
func (h *Handler) AdminSetRestriction(w http.ResponseWriter, r *http.Request) {
	admin := middleware.AdminUserFromContext(r.Context())
	userID := chi.URLParam(r, "userID")

	var body struct {
		Restricted bool `json:"restricted"`
	}
	if err := json.NewDecoder(r.Body).Decode(&body); err != nil {
		response.Error(w, r, http.StatusBadRequest, "validation_error", "invalid JSON")
		return
	}

	tag, err := h.DB.Exec(r.Context(),
		`UPDATE active_users SET is_restricted = $1, updated_at = now()
		 WHERE user_id = $2 AND deleted_at IS NULL`,
		body.Restricted, userID,
	)
	if err != nil || tag.RowsAffected() == 0 {
		response.Error(w, r, http.StatusNotFound, "not_found", "user not found")
		return
	}

	action := "admin_unrestrict_user"
	if body.Restricted {
		action = "admin_restrict_user"
	}
	middleware.SetAdminAuditEntry(r.Context(), middleware.AdminAuditEntry{
		AdminID:    admin.ID,
		Action:     action,
		TargetType: "user",
		TargetID:   userID,
	})

	w.WriteHeader(http.StatusNoContent)
}

type patchTrustLevelRequest struct {
	TrustLevel string `json:"trust_level"`
	Locked     *bool  `json:"trust_level_locked,omitempty"`
	Notes      string `json:"notes"`
}

// AdminPatchTrustLevel handles PATCH /admin/users/{userID}/trust-level.
// Requires admin or super_admin role.
func (h *Handler) AdminPatchTrustLevel(w http.ResponseWriter, r *http.Request) {
	admin := middleware.AdminUserFromContext(r.Context())
	if !adminauth.AtLeast(admin.Role, adminauth.RoleAdmin) {
		response.Error(w, r, http.StatusForbidden, "forbidden", "admin role required")
		return
	}

	userID := chi.URLParam(r, "userID")

	var req patchTrustLevelRequest
	if err := json.NewDecoder(r.Body).Decode(&req); err != nil {
		response.Error(w, r, http.StatusBadRequest, "validation_error", "invalid JSON")
		return
	}

	validLevels := map[string]bool{
		trust.LevelVisitor: true, trust.LevelNewUser: true,
		trust.LevelUser: true, trust.LevelTrustedUser: true,
	}
	if req.TrustLevel != "" && !validLevels[req.TrustLevel] {
		response.Error(w, r, http.StatusBadRequest, "validation_error", "invalid trust_level")
		return
	}

	// Fetch current level for audit log.
	var beforeLevel string
	_ = h.DB.QueryRow(r.Context(),
		`SELECT trust_level FROM active_users WHERE user_id = $1`, userID,
	).Scan(&beforeLevel)

	// Build dynamic update.
	if req.TrustLevel != "" {
		if _, err := h.DB.Exec(r.Context(),
			`UPDATE active_users SET trust_level = $1, updated_at = now() WHERE user_id = $2 AND deleted_at IS NULL`,
			req.TrustLevel, userID,
		); err != nil {
			h.Logger.Error("admin patch trust level", "error", err)
			response.InternalError(w, r, h.Cfg.IsProduction())
			return
		}
		// Record in trust_level_logs.
		_, _ = h.DB.Exec(r.Context(),
			`INSERT INTO trust_level_logs (user_id, before_level, after_level, reason, admin_id)
			 VALUES ($1, $2, $3, 'admin_manual', $4)`,
			userID, beforeLevel, req.TrustLevel, admin.ID,
		)
	}
	if req.Locked != nil {
		if _, err := h.DB.Exec(r.Context(),
			`UPDATE active_users SET trust_level_locked = $1, updated_at = now() WHERE user_id = $2`,
			*req.Locked, userID,
		); err != nil {
			h.Logger.Error("admin patch trust lock", "error", err)
			response.InternalError(w, r, h.Cfg.IsProduction())
			return
		}
	}

	middleware.SetAdminAuditEntry(r.Context(), middleware.AdminAuditEntry{
		AdminID:    admin.ID,
		Action:     "admin_patch_trust_level",
		TargetType: "user",
		TargetID:   userID,
		Notes:      req.Notes,
	})

	w.WriteHeader(http.StatusNoContent)
}

// AdminGetTrustLevelHistory handles GET /admin/users/{userID}/trust-level/history.
func (h *Handler) AdminGetTrustLevelHistory(w http.ResponseWriter, r *http.Request) {
	userID := chi.URLParam(r, "userID")
	limitStr := r.URL.Query().Get("limit")
	limit := 50
	if n, err := strconv.Atoi(limitStr); err == nil && n > 0 && n <= 100 {
		limit = n
	}

	rows, err := h.DB.Query(r.Context(),
		`SELECT before_level, after_level, reason, admin_id, created_at
		 FROM trust_level_logs
		 WHERE user_id = $1
		 ORDER BY created_at DESC
		 LIMIT $2`,
		userID, limit,
	)
	if err != nil {
		h.Logger.Error("admin trust history", "error", err)
		response.InternalError(w, r, h.Cfg.IsProduction())
		return
	}
	defer rows.Close()

	type historyRow struct {
		BeforeLevel string     `json:"before_level"`
		AfterLevel  string     `json:"after_level"`
		Reason      string     `json:"reason"`
		AdminID     *string    `json:"admin_id,omitempty"`
		CreatedAt   time.Time  `json:"created_at"`
	}
	var history []historyRow
	for rows.Next() {
		var h historyRow
		if err := rows.Scan(&h.BeforeLevel, &h.AfterLevel, &h.Reason, &h.AdminID, &h.CreatedAt); err != nil {
			continue
		}
		history = append(history, h)
	}
	response.JSON(w, http.StatusOK, history)
}

// AdminGetUserDataExport handles GET /admin/users/{userID}/data-export.
// Returns a JSON snapshot of the user's personal data (GDPR Art. 15/20).
func (h *Handler) AdminGetUserDataExport(w http.ResponseWriter, r *http.Request) {
	userID := chi.URLParam(r, "userID")

	var profile adminUserRow
	err := h.DB.QueryRow(r.Context(),
		`SELECT au.user_id, au.display_name, au.at_name,
		        au.trust_level, au.trust_points, au.trust_level_locked,
		        au.is_restricted, au.subscription_tier, au.deleted_at,
		        u.created_at
		 FROM active_users au JOIN users u ON u.id = au.user_id
		 WHERE au.user_id = $1`,
		userID,
	).Scan(&profile.UserID, &profile.DisplayName, &profile.AtName,
		&profile.TrustLevel, &profile.TrustPoints, &profile.Locked,
		&profile.IsRestricted, &profile.SubTier, &profile.DeletedAt, &profile.CreatedAt,
	)
	if err != nil {
		response.Error(w, r, http.StatusNotFound, "not_found", "user not found")
		return
	}

	// Avatar metadata.
	type avatarMeta struct {
		ID   string `json:"id"`
		Hash string `json:"hash"`
	}
	avatarRows, _ := h.DB.Query(r.Context(),
		`SELECT id, vrm_hash FROM avatars WHERE user_id = $1`, userID)
	var avatars []avatarMeta
	if avatarRows != nil {
		for avatarRows.Next() {
			var a avatarMeta
			_ = avatarRows.Scan(&a.ID, &a.Hash)
			avatars = append(avatars, a)
		}
		avatarRows.Close()
	}

	// World metadata.
	type worldMeta struct {
		ID   string `json:"id"`
		Name string `json:"name"`
	}
	worldRows, _ := h.DB.Query(r.Context(),
		`SELECT id, name FROM worlds WHERE owner_user_id = $1`, userID)
	var worlds []worldMeta
	if worldRows != nil {
		for worldRows.Next() {
			var wm worldMeta
			_ = worldRows.Scan(&wm.ID, &wm.Name)
			worlds = append(worlds, wm)
		}
		worldRows.Close()
	}

	w.Header().Set("Content-Disposition", `attachment; filename="user_data_export.json"`)
	response.JSON(w, http.StatusOK, map[string]any{
		"exported_at": time.Now().UTC(),
		"profile":     profile,
		"avatars":     avatars,
		"worlds":      worlds,
	})
}
