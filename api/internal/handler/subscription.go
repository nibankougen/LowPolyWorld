package handler

import (
	"encoding/json"
	"net/http"
	"time"

	"github.com/nibankougen/LowPolyWorld/api/internal/middleware"
	"github.com/nibankougen/LowPolyWorld/api/internal/plan"
	"github.com/nibankougen/LowPolyWorld/api/internal/response"
)

// subscriptionDuration maps IAP product IDs to how long the subscription lasts.
var subscriptionDuration = map[string]time.Duration{
	"com.nibankougen.lowpolyworld.premium_monthly": 31 * 24 * time.Hour,
	"com.nibankougen.lowpolyworld.premium_yearly":  366 * 24 * time.Hour,
}

type subscriptionPurchaseRequest struct {
	Platform      string `json:"platform"`       // "ios" or "android"
	TransactionID string `json:"transaction_id"` // platform-issued transaction ID
	ProductID     string `json:"product_id"`
}

type subscriptionResponse struct {
	Tier      string  `json:"tier"`
	ExpiresAt *string `json:"expiresAt,omitempty"`
}

// GetSubscription handles GET /api/v1/me/subscription.
func (h *Handler) GetSubscription(w http.ResponseWriter, r *http.Request) {
	userID := middleware.UserIDFromContext(r.Context())

	var tier string
	var expiresAt *time.Time
	err := h.DB.QueryRow(r.Context(),
		`SELECT subscription_tier, subscription_expires_at FROM active_users WHERE user_id = $1`,
		userID,
	).Scan(&tier, &expiresAt)
	if err != nil {
		response.InternalError(w, r, h.Cfg.IsProduction())
		return
	}

	resp := subscriptionResponse{Tier: tier}
	if expiresAt != nil {
		s := expiresAt.UTC().Format(time.RFC3339)
		resp.ExpiresAt = &s
	}
	response.JSON(w, http.StatusOK, resp)
}

// RecordSubscriptionPurchase handles POST /api/v1/me/subscription/purchases.
// Called by the client after a successful IAP subscription purchase.
// Idempotent: re-submitting the same transaction extends the expiry rather than erroring.
func (h *Handler) RecordSubscriptionPurchase(w http.ResponseWriter, r *http.Request) {
	userID := middleware.UserIDFromContext(r.Context())

	var req subscriptionPurchaseRequest
	if err := json.NewDecoder(r.Body).Decode(&req); err != nil {
		response.Error(w, r, http.StatusBadRequest, "validation_error", "invalid request body")
		return
	}
	if req.Platform != "ios" && req.Platform != "android" {
		response.Error(w, r, http.StatusBadRequest, "validation_error", "platform must be ios or android")
		return
	}
	if req.TransactionID == "" || req.ProductID == "" {
		response.Error(w, r, http.StatusBadRequest, "validation_error", "transaction_id and product_id are required")
		return
	}

	dur, ok := subscriptionDuration[req.ProductID]
	if !ok {
		response.Error(w, r, http.StatusBadRequest, "validation_error", "unrecognized product_id")
		return
	}

	// Calculate expiry: extend from the current expiry if already premium, otherwise from now.
	var currentExpiry *time.Time
	_ = h.DB.QueryRow(r.Context(),
		`SELECT subscription_expires_at FROM active_users WHERE user_id = $1`, userID,
	).Scan(&currentExpiry)

	base := time.Now().UTC()
	if currentExpiry != nil && currentExpiry.After(base) {
		base = *currentExpiry
	}
	expiresAt := base.Add(dur)

	// Insert purchase record (idempotent via ON CONFLICT DO NOTHING).
	_, err := h.DB.Exec(r.Context(),
		`INSERT INTO subscription_purchases
		 (user_id, platform, platform_transaction_id, product_id, subscription_tier, expires_at)
		 VALUES ($1, $2, $3, $4, 'premium', $5)
		 ON CONFLICT (platform, platform_transaction_id) DO NOTHING`,
		userID, req.Platform, req.TransactionID, req.ProductID, expiresAt,
	)
	if err != nil {
		h.Logger.Error("insert subscription purchase", "error", err)
		response.InternalError(w, r, h.Cfg.IsProduction())
		return
	}

	// Activate / extend premium on active_users.
	if _, err := h.DB.Exec(r.Context(),
		`UPDATE active_users
		 SET subscription_tier = 'premium',
		     subscription_expires_at = GREATEST(COALESCE(subscription_expires_at, now()), $1)
		 WHERE user_id = $2`,
		expiresAt, userID,
	); err != nil {
		h.Logger.Error("update subscription tier", "error", err)
		response.InternalError(w, r, h.Cfg.IsProduction())
		return
	}

	h.Logger.Info("subscription purchased",
		"event", "subscription_purchased",
		"user_id", userID,
		"product_id", req.ProductID,
		"platform", req.Platform,
		"expires_at", expiresAt,
	)

	caps := plan.GetCapabilities(plan.TierPremium)
	response.ClientJSON(w, http.StatusOK, map[string]any{
		"tier":             "premium",
		"expiresAt":        expiresAt.Format(time.RFC3339),
		"planCapabilities": caps,
	})
}
