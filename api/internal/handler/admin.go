package handler

import (
	"net/http"
	"time"

	"github.com/go-chi/chi/v5"
	"github.com/nibankougen/LowPolyWorld/api/internal/response"
)

const restoreWindowDays = 30

// RestoreAccount handles PATCH /admin/users/{userID}/restore.
// Clears deleted_at if the account was soft-deleted within the last 30 days.
func (h *Handler) RestoreAccount(w http.ResponseWriter, r *http.Request) {
	if h.Cfg.BatchSecret != "" && r.Header.Get("X-Batch-Secret") != h.Cfg.BatchSecret {
		response.Error(w, r, http.StatusUnauthorized, "unauthorized", "invalid batch secret")
		return
	}

	userID := chi.URLParam(r, "userID")

	var deletedAt *time.Time
	err := h.DB.QueryRow(r.Context(),
		`SELECT deleted_at FROM active_users WHERE user_id = $1`,
		userID,
	).Scan(&deletedAt)
	if err != nil {
		response.Error(w, r, http.StatusNotFound, "not_found", "user not found")
		return
	}
	if deletedAt == nil {
		response.Error(w, r, http.StatusConflict, "not_deleted", "account is not in a deleted state")
		return
	}
	if time.Since(*deletedAt) > restoreWindowDays*24*time.Hour {
		response.Error(w, r, http.StatusGone, "expired", "restoration window has expired (30 days)")
		return
	}

	_, err = h.DB.Exec(r.Context(),
		`UPDATE active_users SET deleted_at = NULL, token_revision = token_revision + 1, updated_at = now()
		 WHERE user_id = $1`,
		userID,
	)
	if err != nil {
		h.Logger.Error("restore account", "error", err)
		response.InternalError(w, r, h.Cfg.IsProduction())
		return
	}

	response.JSON(w, http.StatusOK, map[string]string{"user_id": userID, "status": "restored"})
}
