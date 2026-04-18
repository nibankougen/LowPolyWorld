package handler

import (
	"encoding/json"
	"net/http"
	"regexp"
	"time"
	"unicode/utf8"

	"github.com/go-chi/chi/v5"
	"github.com/nibankougen/LowPolyWorld/api/internal/middleware"
	"github.com/nibankougen/LowPolyWorld/api/internal/plan"
	"github.com/nibankougen/LowPolyWorld/api/internal/response"
	"golang.org/x/text/unicode/norm"
)

var nameRegexp = regexp.MustCompile(`^[a-z0-9_]+$`)

type meResponse struct {
	ID                string `json:"id"`
	DisplayName       string `json:"displayName"`
	Name              string `json:"name"`
	NameSetupRequired bool   `json:"nameSetupRequired"`
	Language          string `json:"language"`
	SubscriptionTier  string `json:"subscriptionTier"`
	VivoxID           string `json:"vivoxId"`
	CreatedAt         string `json:"createdAt"`
}

// GetMe handles GET /api/v1/me.
func (h *Handler) GetMe(w http.ResponseWriter, r *http.Request) {
	userID := middleware.UserIDFromContext(r.Context())

	var me meResponse
	var displayName *string
	var name *string
	var createdAt time.Time
	err := h.DB.QueryRow(r.Context(),
		`SELECT au.user_id, au.display_name, au.name, au.name_setup_required,
		        au.language, au.subscription_tier, au.vivox_id::text, u.created_at
		 FROM active_users au
		 JOIN users u ON u.id = au.user_id
		 WHERE au.user_id = $1 AND au.deleted_at IS NULL`,
		userID,
	).Scan(&me.ID, &displayName, &name, &me.NameSetupRequired,
		&me.Language, &me.SubscriptionTier, &me.VivoxID, &createdAt)
	if err != nil {
		response.Error(w, r, http.StatusNotFound, "not_found", "user not found")
		return
	}
	if displayName != nil {
		me.DisplayName = *displayName
	}
	if name != nil {
		me.Name = *name
	}
	me.CreatedAt = createdAt.UTC().Format(time.RFC3339)

	response.JSON(w, http.StatusOK, me)
}

type setNameRequest struct {
	Name string `json:"name"`
}

// SetupName handles PUT /api/v1/me/name.
// First-time setup is open to all users; subsequent changes require premium + 90-day cooldown.
func (h *Handler) SetupName(w http.ResponseWriter, r *http.Request) {
	userID := middleware.UserIDFromContext(r.Context())

	var req setNameRequest
	if err := json.NewDecoder(r.Body).Decode(&req); err != nil {
		response.Error(w, r, http.StatusBadRequest, "validation_error", "invalid request body")
		return
	}

	// NFKC normalize then lowercase
	normalized := norm.NFKC.String(req.Name)
	lower := toLower(normalized)

	// Validate format
	details := validateName(lower)
	if len(details) > 0 {
		response.ValidationError(w, r, details)
		return
	}

	// Fetch current user state
	var nameSetupRequired bool
	var subscriptionTier string
	var lastNameChangeAt *time.Time
	err := h.DB.QueryRow(r.Context(),
		`SELECT name_setup_required, subscription_tier, last_name_change_at
		 FROM active_users WHERE user_id = $1 AND deleted_at IS NULL`,
		userID,
	).Scan(&nameSetupRequired, &subscriptionTier, &lastNameChangeAt)
	if err != nil {
		response.Error(w, r, http.StatusNotFound, "not_found", "user not found")
		return
	}

	caps := plan.GetCapabilities(plan.Tier(subscriptionTier))

	if !nameSetupRequired {
		// This is a name change, not initial setup
		if !caps.NameChange {
			response.Error(w, r, http.StatusForbidden, "forbidden", "name change requires premium subscription")
			return
		}
		if lastNameChangeAt != nil {
			cooldownEnd := lastNameChangeAt.Add(90 * 24 * time.Hour)
			if time.Now().Before(cooldownEnd) {
				response.Error(w, r, http.StatusConflict, "cooldown", "name can only be changed once every 90 days")
				return
			}
		}
	}

	// Check uniqueness
	var exists bool
	_ = h.DB.QueryRow(r.Context(),
		`SELECT EXISTS(SELECT 1 FROM active_users WHERE name = $1 AND user_id != $2)`, lower, userID,
	).Scan(&exists)
	if exists {
		response.Error(w, r, http.StatusConflict, "name_taken", "this name is already in use")
		return
	}

	now := time.Now()
	_, err = h.DB.Exec(r.Context(),
		`UPDATE active_users
		 SET name = $1, name_setup_required = FALSE, last_name_change_at = $2, updated_at = $2
		 WHERE user_id = $3`,
		lower, now, userID,
	)
	if err != nil {
		h.Logger.Error("update name", "error", err)
		response.InternalError(w, r, h.Cfg.IsProduction())
		return
	}

	response.JSON(w, http.StatusOK, map[string]string{"name": lower})
}

// DeleteAccount handles DELETE /api/v1/me — soft-deletes the account.
func (h *Handler) DeleteAccount(w http.ResponseWriter, r *http.Request) {
	userID := middleware.UserIDFromContext(r.Context())

	tx, err := h.DB.Begin(r.Context())
	if err != nil {
		h.Logger.Error("begin tx", "error", err)
		response.InternalError(w, r, h.Cfg.IsProduction())
		return
	}
	defer tx.Rollback(r.Context()) //nolint:errcheck

	now := time.Now()

	// Soft-delete: set deleted_at, increment token_revision, clear all PII, regenerate vivox_id.
	// name is nullable (partial unique index) so we can NULL it here per GDPR erasure spec.
	if _, err := tx.Exec(r.Context(),
		`UPDATE active_users
		 SET deleted_at = $1,
		     token_revision = token_revision + 1,
		     display_name = NULL,
		     name = NULL,
		     email = NULL,
		     google_sub = NULL,
		     apple_sub = NULL,
		     vivox_id = gen_random_uuid(),
		     updated_at = $1
		 WHERE user_id = $2`,
		now, userID,
	); err != nil {
		h.Logger.Error("soft-delete active_users", "error", err)
		response.InternalError(w, r, h.Cfg.IsProduction())
		return
	}

	// Set public worlds to non-public
	if _, err := tx.Exec(r.Context(),
		`UPDATE worlds SET is_public = FALSE, updated_at = $1
		 WHERE owner_user_id = $2 AND is_public = TRUE`,
		now, userID,
	); err != nil {
		h.Logger.Error("unpublish worlds", "error", err)
		response.InternalError(w, r, h.Cfg.IsProduction())
		return
	}

	// Revoke all refresh tokens
	if _, err := tx.Exec(r.Context(),
		`UPDATE refresh_tokens SET revoked_at = $1
		 WHERE user_id = $2 AND revoked_at IS NULL`,
		now, userID,
	); err != nil {
		h.Logger.Error("revoke refresh tokens", "error", err)
		response.InternalError(w, r, h.Cfg.IsProduction())
		return
	}

	if err := tx.Commit(r.Context()); err != nil {
		h.Logger.Error("commit tx", "error", err)
		response.InternalError(w, r, h.Cfg.IsProduction())
		return
	}

	w.WriteHeader(http.StatusNoContent)
}

type setLanguageRequest struct {
	Language string `json:"language"`
}

// SetLanguage handles PATCH /api/v1/me/language — updates the user's preferred language.
func (h *Handler) SetLanguage(w http.ResponseWriter, r *http.Request) {
	userID := middleware.UserIDFromContext(r.Context())

	var req setLanguageRequest
	if err := json.NewDecoder(r.Body).Decode(&req); err != nil {
		response.Error(w, r, http.StatusBadRequest, "validation_error", "invalid request body")
		return
	}

	if len(req.Language) < 2 || len(req.Language) > 10 {
		response.Error(w, r, http.StatusBadRequest, "validation_error", "invalid language code")
		return
	}

	_, err := h.DB.Exec(r.Context(),
		`UPDATE active_users SET language = $1, updated_at = now() WHERE user_id = $2`,
		req.Language, userID,
	)
	if err != nil {
		h.Logger.Error("update language", "error", err)
		response.InternalError(w, r, h.Cfg.IsProduction())
		return
	}

	response.JSON(w, http.StatusOK, map[string]string{"language": req.Language})
}

type updateDisplayNameRequest struct {
	DisplayName string `json:"displayName"`
}

// UpdateDisplayName handles PATCH /api/v1/me/display-name.
func (h *Handler) UpdateDisplayName(w http.ResponseWriter, r *http.Request) {
	userID := middleware.UserIDFromContext(r.Context())

	var req updateDisplayNameRequest
	if err := json.NewDecoder(r.Body).Decode(&req); err != nil {
		response.Error(w, r, http.StatusBadRequest, "validation_error", "invalid request body")
		return
	}

	if len(req.DisplayName) == 0 || len([]rune(req.DisplayName)) > 30 {
		response.Error(w, r, http.StatusBadRequest, "validation_error", "display name must be 1-30 characters")
		return
	}

	_, err := h.DB.Exec(r.Context(),
		`UPDATE active_users SET display_name = $1, updated_at = now() WHERE user_id = $2`,
		req.DisplayName, userID,
	)
	if err != nil {
		h.Logger.Error("update display name", "error", err)
		response.InternalError(w, r, h.Cfg.IsProduction())
		return
	}

	response.JSON(w, http.StatusOK, map[string]string{"displayName": req.DisplayName})
}

type publicUserResponse struct {
	ID          string `json:"id"`
	DisplayName string `json:"displayName"`
	Name        string `json:"name,omitempty"`
	CreatedAt   string `json:"createdAt"`
}

// GetPublicUser handles GET /api/v1/users/{userID} — public profile, returns 404 for deleted accounts.
func (h *Handler) GetPublicUser(w http.ResponseWriter, r *http.Request) {
	targetID := chi.URLParam(r, "userID")

	var resp publicUserResponse
	var displayName, name *string
	var createdAt time.Time

	err := h.DB.QueryRow(r.Context(),
		`SELECT au.user_id, au.display_name, au.name, u.created_at
		 FROM active_users au
		 JOIN users u ON u.id = au.user_id
		 WHERE au.user_id = $1 AND au.deleted_at IS NULL`,
		targetID,
	).Scan(&resp.ID, &displayName, &name, &createdAt)
	if err != nil {
		response.Error(w, r, http.StatusNotFound, "not_found", "user not found")
		return
	}
	if displayName != nil {
		resp.DisplayName = *displayName
	}
	if name != nil {
		resp.Name = *name
	}
	resp.CreatedAt = createdAt.UTC().Format(time.RFC3339)

	response.JSON(w, http.StatusOK, resp)
}

// validateName returns validation error details for the given (already-lowercased) name.
func validateName(name string) []response.ErrDetail {
	var details []response.ErrDetail
	length := utf8.RuneCountInString(name)
	if length < 3 || length > 15 {
		details = append(details, response.ErrDetail{
			Field:   "name",
			Code:    "invalid_length",
			Message: "name must be between 3 and 15 characters",
		})
	}
	if !nameRegexp.MatchString(name) {
		details = append(details, response.ErrDetail{
			Field:   "name",
			Code:    "invalid_format",
			Message: "name may only contain lowercase letters, digits, and underscores",
		})
	}
	return details
}

// toLower converts a string to lowercase using simple ASCII + unicode folding.
func toLower(s string) string {
	result := make([]rune, 0, len(s))
	for _, r := range s {
		if r >= 'A' && r <= 'Z' {
			result = append(result, r+32)
		} else {
			result = append(result, r)
		}
	}
	return string(result)
}
