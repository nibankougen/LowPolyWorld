package handler

import (
	"encoding/json"
	"errors"
	"net/http"
	"strings"

	"github.com/nibankougen/LowPolyWorld/api/internal/adminauth"
	"github.com/nibankougen/LowPolyWorld/api/internal/middleware"
	"github.com/nibankougen/LowPolyWorld/api/internal/response"
)

type adminLoginRequest struct {
	Email    string `json:"email"`
	Password string `json:"password"`
}

// AdminLogin handles POST /admin/auth/login.
// Returns an opaque bearer token on success.
func (h *Handler) AdminLogin(w http.ResponseWriter, r *http.Request) {
	var req adminLoginRequest
	if err := json.NewDecoder(r.Body).Decode(&req); err != nil {
		response.Error(w, r, http.StatusBadRequest, "validation_error", "invalid JSON")
		return
	}
	if req.Email == "" || req.Password == "" {
		response.Error(w, r, http.StatusBadRequest, "validation_error", "email and password required")
		return
	}

	token, err := adminauth.Login(r.Context(), h.DB, req.Email, req.Password)
	if errors.Is(err, adminauth.ErrInvalidCredentials) {
		response.Error(w, r, http.StatusUnauthorized, "unauthorized", "invalid email or password")
		return
	}
	if errors.Is(err, adminauth.ErrInactiveAccount) {
		response.Error(w, r, http.StatusForbidden, "forbidden", "admin account is inactive")
		return
	}
	if err != nil {
		h.Logger.Error("admin login", "error", err)
		response.InternalError(w, r, h.Cfg.IsProduction())
		return
	}

	response.JSON(w, http.StatusOK, map[string]string{"token": token})
}

// AdminLogout handles POST /admin/auth/logout.
// Invalidates the current session token.
func (h *Handler) AdminLogout(w http.ResponseWriter, r *http.Request) {
	raw := strings.TrimPrefix(r.Header.Get("Authorization"), "Bearer ")
	if raw != "" {
		_ = adminauth.Logout(r.Context(), h.DB, raw)
	}
	w.WriteHeader(http.StatusNoContent)
}

// AdminMe handles GET /admin/auth/me.
// Returns the current admin user's id and role.
func (h *Handler) AdminMe(w http.ResponseWriter, r *http.Request) {
	admin := middleware.AdminUserFromContext(r.Context())
	if admin == nil {
		response.Error(w, r, http.StatusUnauthorized, "unauthorized", "not authenticated")
		return
	}
	response.JSON(w, http.StatusOK, map[string]string{
		"id":   admin.ID,
		"role": admin.Role,
	})
}
