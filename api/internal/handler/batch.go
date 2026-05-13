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
		count, err := batch.DeleteExpiredAccounts(r.Context(), h.Pool, h.Storage, h.Logger)
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
	case "cleanup-access-logs":
		// Access logs are stored in Cloud Logging with automatic 1-year retention configured
		// at the bucket level. No DB records to purge.
		h.Logger.Info("batch completed",
			"event", "batch_completed",
			"batch", "cleanup-access-logs",
			"affected_count", 0,
		)
		response.JSON(w, http.StatusOK, map[string]any{"batch": batchName, "affected_count": 0})
	case "expire-coins":
		count, err := batch.ExpireCoins(r.Context(), h.Pool, h.Logger)
		if err != nil {
			h.Logger.Error("batch failed", "batch", batchName, "error", err)
			response.InternalError(w, r, h.Cfg.IsProduction())
			return
		}
		h.Logger.Info("batch completed",
			"event", "batch_completed",
			"batch", "expire-coins",
			"affected_count", count,
		)
		response.JSON(w, http.StatusOK, map[string]any{"batch": batchName, "affected_count": count})
	case "parental-consent-reminder":
		count, err := h.runParentalConsentReminder(r.Context())
		if err != nil {
			h.Logger.Error("batch failed", "batch", batchName, "error", err)
			response.InternalError(w, r, h.Cfg.IsProduction())
			return
		}
		h.Logger.Info("batch completed",
			"event", "batch_completed",
			"batch", "parental-consent-reminder",
			"affected_count", count,
		)
		response.JSON(w, http.StatusOK, map[string]any{"batch": batchName, "affected_count": count})
	case "parental-consent-timeout":
		count, err := h.runParentalConsentTimeout(r.Context())
		if err != nil {
			h.Logger.Error("batch failed", "batch", batchName, "error", err)
			response.InternalError(w, r, h.Cfg.IsProduction())
			return
		}
		h.Logger.Info("batch completed",
			"event", "batch_completed",
			"batch", "parental-consent-timeout",
			"affected_count", count,
		)
		response.JSON(w, http.StatusOK, map[string]any{"batch": batchName, "affected_count": count})
	case "expire-subscriptions":
		count, err := batch.ExpireSubscriptions(r.Context(), h.Pool, h.Logger)
		if err != nil {
			h.Logger.Error("batch failed", "batch", batchName, "error", err)
			response.InternalError(w, r, h.Cfg.IsProduction())
			return
		}
		h.Logger.Info("batch completed",
			"event", "batch_completed",
			"batch", "expire-subscriptions",
			"affected_count", count,
		)
		response.JSON(w, http.StatusOK, map[string]any{"batch": batchName, "affected_count": count})
	default:
		response.Error(w, r, http.StatusNotFound, "not_found", "unknown batch name")
	}
}
