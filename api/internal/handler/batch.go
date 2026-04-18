package handler

import (
	"net/http"

	"github.com/go-chi/chi/v5"
	"github.com/nibankougen/LowPolyWorld/api/internal/batch"
	"github.com/nibankougen/LowPolyWorld/api/internal/response"
)

// RunBatch handles POST /admin/internal/run-batch/{batchName}.
// Protected by X-Batch-Secret header matching cfg.BatchSecret.
func (h *Handler) RunBatch(w http.ResponseWriter, r *http.Request) {
	if h.Cfg.BatchSecret != "" && r.Header.Get("X-Batch-Secret") != h.Cfg.BatchSecret {
		response.Error(w, r, http.StatusUnauthorized, "unauthorized", "invalid batch secret")
		return
	}

	batchName := chi.URLParam(r, "batchName")
	switch batchName {
	case "delete-expired-accounts":
		count, err := batch.DeleteExpiredAccounts(r.Context(), h.DB, h.Storage, h.Logger)
		if err != nil {
			h.Logger.Error("batch failed", "batch", batchName, "error", err)
			response.InternalError(w, r, h.Cfg.IsProduction())
			return
		}
		h.Logger.Info("batch completed",
			"event", "batch_completed",
			"batch", "delete-expired-accounts",
			"affected_count", count,
		)
		response.JSON(w, http.StatusOK, map[string]any{"batch": batchName, "affected_count": count})
	default:
		response.Error(w, r, http.StatusNotFound, "not_found", "unknown batch name")
	}
}
