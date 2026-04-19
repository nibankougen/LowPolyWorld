package handler

import (
	"context"
	"encoding/json"
	"net/http"
	"time"

	"github.com/nibankougen/LowPolyWorld/api/internal/auth"
	"github.com/nibankougen/LowPolyWorld/api/internal/middleware"
	"github.com/nibankougen/LowPolyWorld/api/internal/response"
)

const maxActiveSessions = 3

type authCallbackRequest struct {
	IDToken    string `json:"id_token"`
	DeviceName string `json:"device_name"`
	BirthDate  string `json:"birth_date"` // YYYY-MM-DD, new accounts only
	Locale     string `json:"locale"`     // ja-JP etc.
}

type authResponse struct {
	AccessToken       string `json:"access_token"`
	RefreshToken      string `json:"refresh_token"`
	ExpiresIn         int    `json:"expires_in"`
	NameSetupRequired bool   `json:"name_setup_required"`
}

func (h *Handler) handleOAuthCallback(w http.ResponseWriter, r *http.Request, provider string) {
	var req authCallbackRequest
	if err := json.NewDecoder(r.Body).Decode(&req); err != nil {
		response.Error(w, r, http.StatusBadRequest, "validation_error", "invalid request body")
		return
	}
	if req.IDToken == "" {
		response.Error(w, r, http.StatusBadRequest, "validation_error", "id_token is required")
		return
	}

	var providerSub string
	var err error
	switch provider {
	case "google":
		providerSub, err = h.AuthSvc.VerifyGoogleIDToken(r.Context(), req.IDToken)
	case "apple":
		providerSub, err = h.AuthSvc.VerifyAppleIDToken(r.Context(), req.IDToken)
	default:
		response.Error(w, r, http.StatusBadRequest, "validation_error", "unsupported provider")
		return
	}
	if err != nil {
		h.Logger.Warn("oauth token verification failed", "provider", provider, "error", err)
		response.Error(w, r, http.StatusUnauthorized, "unauthorized", "invalid id_token")
		return
	}

	locale := req.Locale
	if locale == "" {
		locale = "ja-JP"
	}

	// Determine which column to query/insert
	var subColumn string
	switch provider {
	case "google":
		subColumn = "google_sub"
	case "apple":
		subColumn = "apple_sub"
	}

	// Find or create user
	var userID string
	var tokenRevision int
	var nameSetupRequired bool

	err = h.DB.QueryRow(r.Context(),
		`SELECT au.user_id, au.token_revision, au.name_setup_required
		 FROM active_users au
		 WHERE `+subColumn+` = $1 AND au.deleted_at IS NULL`,
		providerSub,
	).Scan(&userID, &tokenRevision, &nameSetupRequired)

	if err != nil {
		// New user — determine age group
		ageGroup := calcAgeGroup(req.BirthDate)
		if ageGroup == "" {
			response.Error(w, r, http.StatusForbidden, "age_restricted", "users under 13 cannot register")
			return
		}

		// Generate unique temp name with retries
		tempName := generateUniqueTempName(r.Context(), h)

		tx, txErr := h.DB.Begin(r.Context())
		if txErr != nil {
			h.Logger.Error("begin tx", "error", txErr)
			response.InternalError(w, r, h.Cfg.IsProduction())
			return
		}
		defer tx.Rollback(r.Context()) //nolint:errcheck

		if scanErr := tx.QueryRow(r.Context(),
			`INSERT INTO users (id) VALUES (gen_random_uuid()) RETURNING id`,
		).Scan(&userID); scanErr != nil {
			h.Logger.Error("insert user", "error", scanErr)
			response.InternalError(w, r, h.Cfg.IsProduction())
			return
		}

		if _, insertErr := tx.Exec(r.Context(),
			`INSERT INTO active_users
			 (user_id, name, `+subColumn+`, locale, age_group, name_setup_required)
			 VALUES ($1, $2, $3, $4, $5, TRUE)`,
			userID, tempName, providerSub, locale, ageGroup,
		); insertErr != nil {
			h.Logger.Error("insert active_users", "error", insertErr)
			response.InternalError(w, r, h.Cfg.IsProduction())
			return
		}

		if commitErr := tx.Commit(r.Context()); commitErr != nil {
			h.Logger.Error("commit tx", "error", commitErr)
			response.InternalError(w, r, h.Cfg.IsProduction())
			return
		}
		tokenRevision = 0
		nameSetupRequired = true
	}

	// Issue tokens
	accessToken, err := h.AuthSvc.IssueAccessToken(userID, tokenRevision)
	if err != nil {
		h.Logger.Error("issue access token", "error", err)
		response.InternalError(w, r, h.Cfg.IsProduction())
		return
	}
	plain, hash, err := auth.IssueRefreshToken()
	if err != nil {
		h.Logger.Error("issue refresh token", "error", err)
		response.InternalError(w, r, h.Cfg.IsProduction())
		return
	}

	// Enforce max sessions by revoking oldest active session if needed
	h.enforceMaxSessions(r.Context(), userID)

	if _, err := h.DB.Exec(r.Context(),
		`INSERT INTO refresh_tokens (user_id, token_hash, expires_at, device_name)
		 VALUES ($1, $2, $3, $4)`,
		userID, hash, auth.RefreshTokenExpiresAt(), req.DeviceName,
	); err != nil {
		h.Logger.Error("insert refresh token", "error", err)
		response.InternalError(w, r, h.Cfg.IsProduction())
		return
	}

	response.JSON(w, http.StatusOK, authResponse{
		AccessToken:       accessToken,
		RefreshToken:      plain,
		ExpiresIn:         int(7 * 24 * time.Hour / time.Second),
		NameSetupRequired: nameSetupRequired,
	})
}

// GoogleCallback handles POST /auth/google/callback.
func (h *Handler) GoogleCallback(w http.ResponseWriter, r *http.Request) {
	h.handleOAuthCallback(w, r, "google")
}

// AppleCallback handles POST /auth/apple/callback.
func (h *Handler) AppleCallback(w http.ResponseWriter, r *http.Request) {
	h.handleOAuthCallback(w, r, "apple")
}

// RefreshToken handles POST /auth/refresh — rotates the refresh token.
func (h *Handler) RefreshToken(w http.ResponseWriter, r *http.Request) {
	var req struct {
		RefreshToken string `json:"refresh_token"`
	}
	if err := json.NewDecoder(r.Body).Decode(&req); err != nil || req.RefreshToken == "" {
		response.Error(w, r, http.StatusBadRequest, "validation_error", "refresh_token is required")
		return
	}

	hash := auth.HashRefreshToken(req.RefreshToken)
	var tokenID int64
	var userID string
	var revokedAt *time.Time
	var expiresAt time.Time

	err := h.DB.QueryRow(r.Context(),
		`SELECT id, user_id, revoked_at, expires_at FROM refresh_tokens WHERE token_hash = $1`,
		hash,
	).Scan(&tokenID, &userID, &revokedAt, &expiresAt)
	if err != nil {
		response.Error(w, r, http.StatusUnauthorized, "unauthorized", "refresh_token_invalid")
		return
	}
	if time.Now().After(expiresAt) {
		response.Error(w, r, http.StatusUnauthorized, "unauthorized", "refresh_token_expired")
		return
	}
	if revokedAt != nil {
		// Token reuse detected — revoke all sessions for this user
		h.Logger.Warn("refresh token reuse detected", "user_id", userID)
		_, _ = h.DB.Exec(r.Context(),
			`UPDATE refresh_tokens SET revoked_at = now() WHERE user_id = $1 AND revoked_at IS NULL`, userID)
		_, _ = h.DB.Exec(r.Context(),
			`UPDATE active_users SET token_revision = token_revision + 1 WHERE user_id = $1`, userID)
		middleware.CheckMassTokenRevocation(r.Context(), h.DB, h.Logger, 100)
		response.Error(w, r, http.StatusUnauthorized, "unauthorized", "refresh_token_invalid")
		return
	}

	var tokenRevision int
	var deletedAt *time.Time
	if err := h.DB.QueryRow(r.Context(),
		`SELECT token_revision, deleted_at FROM active_users WHERE user_id = $1`, userID,
	).Scan(&tokenRevision, &deletedAt); err != nil {
		response.Error(w, r, http.StatusUnauthorized, "unauthorized", "user not found")
		return
	}
	if deletedAt != nil {
		response.Error(w, r, http.StatusForbidden, "account_deleted", "this account has been deleted")
		return
	}

	// Revoke old token and issue a new one
	_, _ = h.DB.Exec(r.Context(),
		`UPDATE refresh_tokens SET revoked_at = now() WHERE id = $1`, tokenID)

	newPlain, newHash, err := auth.IssueRefreshToken()
	if err != nil {
		response.InternalError(w, r, h.Cfg.IsProduction())
		return
	}
	if _, err := h.DB.Exec(r.Context(),
		`INSERT INTO refresh_tokens (user_id, token_hash, expires_at) VALUES ($1, $2, $3)`,
		userID, newHash, auth.RefreshTokenExpiresAt(),
	); err != nil {
		response.InternalError(w, r, h.Cfg.IsProduction())
		return
	}

	newAccess, err := h.AuthSvc.IssueAccessToken(userID, tokenRevision)
	if err != nil {
		response.InternalError(w, r, h.Cfg.IsProduction())
		return
	}

	response.JSON(w, http.StatusOK, authResponse{
		AccessToken:  newAccess,
		RefreshToken: newPlain,
		ExpiresIn:    int(7 * 24 * time.Hour / time.Second),
	})
}

// Logout handles POST /auth/logout — revokes the given refresh token.
func (h *Handler) Logout(w http.ResponseWriter, r *http.Request) {
	userID := middleware.UserIDFromContext(r.Context())
	var req struct {
		RefreshToken string `json:"refresh_token"`
	}
	_ = json.NewDecoder(r.Body).Decode(&req)

	if req.RefreshToken != "" {
		hash := auth.HashRefreshToken(req.RefreshToken)
		_, _ = h.DB.Exec(r.Context(),
			`UPDATE refresh_tokens SET revoked_at = now()
			 WHERE token_hash = $1 AND user_id = $2 AND revoked_at IS NULL`,
			hash, userID)
	}
	w.WriteHeader(http.StatusNoContent)
}

// enforceMaxSessions revokes the oldest active session when the user exceeds maxActiveSessions.
func (h *Handler) enforceMaxSessions(ctx context.Context, userID string) {
	_, _ = h.DB.Exec(ctx, `
		UPDATE refresh_tokens SET revoked_at = now()
		WHERE id IN (
			SELECT id FROM refresh_tokens
			WHERE user_id = $1 AND revoked_at IS NULL AND expires_at > now()
			ORDER BY created_at ASC
			LIMIT GREATEST(0, (
				SELECT count(*) FROM refresh_tokens
				WHERE user_id = $1 AND revoked_at IS NULL AND expires_at > now()
			) - $2 + 1)
		)
	`, userID, maxActiveSessions)
}

// calcAgeGroup maps a birth date string to the DB age_group value.
// Returns "" if user is under 13 (registration must be rejected).
func calcAgeGroup(birthDate string) string {
	if birthDate == "" {
		return "adult"
	}
	bd, err := time.Parse("2006-01-02", birthDate)
	if err != nil {
		return "adult" // treat unparseable as adult — client should validate
	}
	now := time.Now()
	age := now.Year() - bd.Year()
	if now.Month() < bd.Month() || (now.Month() == bd.Month() && now.Day() < bd.Day()) {
		age--
	}
	switch {
	case age < 13:
		return "" // rejected
	case age <= 15:
		return "young_teen"
	case age <= 17:
		return "teen"
	default:
		return "adult"
	}
}

// generateUniqueTempName tries to produce a temp @name that doesn't already exist in active_users.
func generateUniqueTempName(ctx context.Context, h *Handler) string {
	for i := 0; i < 10; i++ {
		name := auth.GenerateTempName()
		var exists bool
		_ = h.DB.QueryRow(ctx, `SELECT EXISTS(SELECT 1 FROM active_users WHERE name = $1)`, name).Scan(&exists)
		if !exists {
			return name
		}
	}
	// Fallback: use a UUID-based name (always unique in practice)
	return auth.GenerateTempName()
}
