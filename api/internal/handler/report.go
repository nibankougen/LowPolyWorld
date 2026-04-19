package handler

import (
	"context"
	"encoding/json"
	"net/http"
	"time"

	"github.com/go-chi/chi/v5"
	"github.com/nibankougen/LowPolyWorld/api/internal/middleware"
	"github.com/nibankougen/LowPolyWorld/api/internal/response"
	"github.com/nibankougen/LowPolyWorld/api/internal/trust"
)

// valid violation reasons (Phase 9 will extend this to cover world/product reports)
var validViolationReasons = map[string]bool{
	"spam":            true,
	"harassment":      true,
	"hate_speech":     true,
	"impersonation":   true,
	"inappropriate":   true,
	"violence":        true,
	"misinformation":  true,
	"other":           true,
}

type reportUserRequest struct {
	Reason string `json:"reason"`
	Detail string `json:"detail"`
}

// ReportUser handles POST /api/v1/users/{userID}/report.
// Records a violation report and fires the auto-restriction check.
func (h *Handler) ReportUser(w http.ResponseWriter, r *http.Request) {
	reporterID := middleware.UserIDFromContext(r.Context())
	targetID := chi.URLParam(r, "userID")

	if targetID == reporterID {
		response.Error(w, r, http.StatusBadRequest, "validation_error", "cannot report yourself")
		return
	}

	var req reportUserRequest
	if err := json.NewDecoder(r.Body).Decode(&req); err != nil {
		response.Error(w, r, http.StatusBadRequest, "validation_error", "invalid JSON")
		return
	}
	if !validViolationReasons[req.Reason] {
		response.Error(w, r, http.StatusBadRequest, "validation_error", "invalid reason")
		return
	}

	// Confirm target exists and is not deleted.
	var exists bool
	err := h.DB.QueryRow(r.Context(),
		`SELECT EXISTS(SELECT 1 FROM active_users WHERE user_id = $1 AND deleted_at IS NULL)`,
		targetID,
	).Scan(&exists)
	if err != nil || !exists {
		response.Error(w, r, http.StatusNotFound, "not_found", "user not found")
		return
	}

	if _, err := h.DB.Exec(r.Context(),
		`INSERT INTO user_violation_reports (reporter_id, target_id, reason, detail)
		 VALUES ($1, $2, $3, $4)
		 ON CONFLICT DO NOTHING`,
		reporterID, targetID, req.Reason, req.Detail,
	); err != nil {
		h.Logger.Error("report user: insert", "error", err)
		response.InternalError(w, r, h.Cfg.IsProduction())
		return
	}

	// Fire auto-restriction check asynchronously (detached context — request may finish first).
	db, logger := h.DB, h.Logger
	go func() {
		ctx, cancel := context.WithTimeout(context.Background(), 5*time.Second)
		defer cancel()
		trust.CheckAndApplyViolationRestriction(ctx, db, logger, targetID)
	}()

	w.WriteHeader(http.StatusNoContent)
}
