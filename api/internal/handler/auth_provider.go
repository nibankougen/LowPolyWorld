package handler

import (
	"encoding/json"
	"net/http"

	"github.com/go-chi/chi/v5"
	"github.com/nibankougen/LowPolyWorld/api/internal/middleware"
	"github.com/nibankougen/LowPolyWorld/api/internal/response"
)

type authProviderItem struct {
	Provider string `json:"provider"`
}

// ListAuthProviders handles GET /api/v1/me/auth-providers.
func (h *Handler) ListAuthProviders(w http.ResponseWriter, r *http.Request) {
	userID := middleware.UserIDFromContext(r.Context())

	var googleSub, appleSub *string
	err := h.DB.QueryRow(r.Context(),
		`SELECT google_sub, apple_sub FROM active_users WHERE user_id = $1 AND deleted_at IS NULL`,
		userID,
	).Scan(&googleSub, &appleSub)
	if err != nil {
		response.Error(w, r, http.StatusNotFound, "not_found", "user not found")
		return
	}

	providers := []authProviderItem{}
	if googleSub != nil {
		providers = append(providers, authProviderItem{Provider: "google"})
	}
	if appleSub != nil {
		providers = append(providers, authProviderItem{Provider: "apple"})
	}
	response.JSON(w, http.StatusOK, providers)
}

type linkProviderRequest struct {
	IDToken string `json:"id_token"`
}

// LinkAuthProvider handles POST /api/v1/me/auth-providers/{provider} — links an OAuth provider to the account.
func (h *Handler) LinkAuthProvider(w http.ResponseWriter, r *http.Request) {
	userID := middleware.UserIDFromContext(r.Context())
	provider := chi.URLParam(r, "provider")

	var req linkProviderRequest
	if err := json.NewDecoder(r.Body).Decode(&req); err != nil || req.IDToken == "" {
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
		h.Logger.Warn("link provider: token verification failed", "provider", provider, "error", err)
		response.Error(w, r, http.StatusUnauthorized, "unauthorized", "invalid id_token")
		return
	}

	var subColumn string
	switch provider {
	case "google":
		subColumn = "google_sub"
	case "apple":
		subColumn = "apple_sub"
	}

	// Verify the provider account is not already linked to a different user
	var existingUserID string
	_ = h.DB.QueryRow(r.Context(),
		`SELECT user_id FROM active_users WHERE `+subColumn+` = $1 AND deleted_at IS NULL`,
		providerSub,
	).Scan(&existingUserID)
	if existingUserID != "" && existingUserID != userID {
		response.Error(w, r, http.StatusConflict, "provider_in_use", "this provider account is already linked to another user")
		return
	}

	_, err = h.DB.Exec(r.Context(),
		`UPDATE active_users SET `+subColumn+` = $1, updated_at = now() WHERE user_id = $2`,
		providerSub, userID,
	)
	if err != nil {
		h.Logger.Error("link auth provider", "error", err)
		response.InternalError(w, r, h.Cfg.IsProduction())
		return
	}

	response.JSON(w, http.StatusOK, authProviderItem{Provider: provider})
}

// UnlinkAuthProvider handles DELETE /api/v1/me/auth-providers/{provider}.
// Requires at least one remaining provider.
func (h *Handler) UnlinkAuthProvider(w http.ResponseWriter, r *http.Request) {
	userID := middleware.UserIDFromContext(r.Context())
	provider := chi.URLParam(r, "provider")

	if provider != "google" && provider != "apple" {
		response.Error(w, r, http.StatusBadRequest, "validation_error", "unsupported provider")
		return
	}

	var googleSub, appleSub *string
	err := h.DB.QueryRow(r.Context(),
		`SELECT google_sub, apple_sub FROM active_users WHERE user_id = $1 AND deleted_at IS NULL`,
		userID,
	).Scan(&googleSub, &appleSub)
	if err != nil {
		response.Error(w, r, http.StatusNotFound, "not_found", "user not found")
		return
	}

	// Verify the provider is actually linked
	var isLinked bool
	switch provider {
	case "google":
		isLinked = googleSub != nil
	case "apple":
		isLinked = appleSub != nil
	}
	if !isLinked {
		response.Error(w, r, http.StatusNotFound, "not_found", "provider not linked")
		return
	}

	// Require at least one remaining provider after unlinking
	linked := 0
	if googleSub != nil {
		linked++
	}
	if appleSub != nil {
		linked++
	}
	if linked <= 1 {
		response.Error(w, r, http.StatusForbidden, "last_provider", "cannot unlink the last auth provider")
		return
	}

	var subColumn string
	switch provider {
	case "google":
		subColumn = "google_sub"
	case "apple":
		subColumn = "apple_sub"
	}

	_, err = h.DB.Exec(r.Context(),
		`UPDATE active_users SET `+subColumn+` = NULL, updated_at = now() WHERE user_id = $1`,
		userID,
	)
	if err != nil {
		h.Logger.Error("unlink auth provider", "error", err)
		response.InternalError(w, r, h.Cfg.IsProduction())
		return
	}

	w.WriteHeader(http.StatusNoContent)
}
