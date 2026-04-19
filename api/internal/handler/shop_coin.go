package handler

import (
	"encoding/json"
	"net/http"
	"strconv"
	"time"

	"github.com/go-chi/chi/v5"
	mw "github.com/nibankougen/LowPolyWorld/api/internal/middleware"
	"github.com/nibankougen/LowPolyWorld/api/internal/response"
)

// ── Coin Balance ──────────────────────────────────────────────────────────────

// GET /me/coins
// Returns current balance and per-lot breakdown with expiry dates.
func (h *Handler) GetCoinBalance(w http.ResponseWriter, r *http.Request) {
	userID := mw.UserIDFromContext(r.Context())

	// Per-lot valid coin amounts
	rows, err := h.DB.Query(r.Context(), `
		SELECT id, coins_amount, valid_until
		FROM coin_purchases
		WHERE user_id = $1 AND valid_until > now()
		ORDER BY valid_until ASC`, userID)
	if err != nil {
		h.Logger.Error("get coin lots", "error", err)
		response.Error(w, r, http.StatusInternalServerError, "DB_ERROR", "database error")
		return
	}
	defer rows.Close()

	type lot struct {
		PurchaseID string `json:"purchase_id"`
		Coins      int    `json:"coins"`
		ValidUntil string `json:"valid_until"`
	}
	lots := []lot{}
	rawTotal := 0
	for rows.Next() {
		var l lot
		var validUntil time.Time
		if err := rows.Scan(&l.PurchaseID, &l.Coins, &validUntil); err != nil {
			h.Logger.Error("scan coin lot", "error", err)
			response.Error(w, r, http.StatusInternalServerError, "DB_ERROR", "database error")
			return
		}
		l.ValidUntil = validUntil.UTC().Format(time.RFC3339)
		rawTotal += l.Coins
		lots = append(lots, l)
	}

	// Subtract cancellations on still-valid lots
	var deducted int
	_ = h.DB.QueryRow(r.Context(), `
		SELECT COALESCE(SUM(cc.coins_deducted), 0)
		FROM coin_purchase_cancellations cc
		JOIN coin_purchases cp ON cp.id = cc.coin_purchase_id
		WHERE cp.user_id = $1`, userID).Scan(&deducted)

	// Subtract spent coins
	var spent int
	_ = h.DB.QueryRow(r.Context(),
		`SELECT COALESCE(SUM(coins_spent), 0) FROM coin_transactions WHERE buyer_id = $1`, userID).Scan(&spent)

	balance := rawTotal - deducted - spent

	response.JSON(w, http.StatusOK, map[string]any{
		"balance": balance,
		"lots":    lots,
	})
}

// ── Record Coin Purchase (IAP verified by client, server records result) ──────

// POST /me/coins/purchases
func (h *Handler) RecordCoinPurchase(w http.ResponseWriter, r *http.Request) {
	userID := mw.UserIDFromContext(r.Context())

	var req struct {
		Platform              string  `json:"platform"`
		PlatformTransactionID string  `json:"platform_transaction_id"`
		StorefrontCountry     string  `json:"storefront_country"`
		CoinsAmount           int     `json:"coins_amount"`
		LocalAmount           float64 `json:"local_amount"`
		LocalCurrency         string  `json:"local_currency"`
		FxRateToJpy           float64 `json:"fx_rate_to_jpy"`
	}
	if err := json.NewDecoder(r.Body).Decode(&req); err != nil {
		response.Error(w, r, http.StatusBadRequest, "INVALID_BODY", "invalid request body")
		return
	}
	if req.Platform != "ios" && req.Platform != "android" {
		response.Error(w, r, http.StatusBadRequest, "INVALID_PLATFORM", "platform must be ios or android")
		return
	}
	if req.PlatformTransactionID == "" || req.CoinsAmount <= 0 {
		response.Error(w, r, http.StatusBadRequest, "INVALID_PARAMS", "missing required fields")
		return
	}

	// Lookup active platform fee rate
	var feeRateID int64
	var feeRate float64
	err := h.DB.QueryRow(r.Context(), `
		SELECT id, fee_rate FROM platform_fee_rates
		WHERE platform = $1 AND start_date <= CURRENT_DATE AND (end_date IS NULL OR end_date >= CURRENT_DATE)
		ORDER BY start_date DESC LIMIT 1`, req.Platform,
	).Scan(&feeRateID, &feeRate)
	if err != nil {
		// Fall back to 30% if no rate configured
		feeRateID = 0
		feeRate = 0.30
	}

	convertedJPY := req.LocalAmount * req.FxRateToJpy
	estimatedNet := convertedJPY * (1 - feeRate)
	validUntil := time.Now().UTC().AddDate(0, 6, 0)

	tx, err := h.DB.Begin(r.Context())
	if err != nil {
		response.Error(w, r, http.StatusInternalServerError, "DB_ERROR", "database error")
		return
	}
	defer tx.Rollback(r.Context())

	var purchaseID string
	err = tx.QueryRow(r.Context(), `
		INSERT INTO coin_purchases
		  (user_id, platform, platform_transaction_id, storefront_country,
		   purchase_timestamp, valid_until, coins_amount,
		   local_amount, local_currency, fx_rate_to_jpy,
		   converted_jpy_amount, platform_fee_rate_id, estimated_net_revenue_jpy)
		VALUES ($1,$2,$3,$4,now(),$5,$6,$7,$8,$9,$10,$11,$12)
		ON CONFLICT (platform_transaction_id) DO NOTHING
		RETURNING id`,
		userID, req.Platform, req.PlatformTransactionID, req.StorefrontCountry,
		validUntil, req.CoinsAmount,
		req.LocalAmount, req.LocalCurrency, req.FxRateToJpy,
		convertedJPY, feeRateID, estimatedNet,
	).Scan(&purchaseID)
	if err != nil || purchaseID == "" {
		// Idempotent: duplicate transaction — return existing
		var existingID string
		_ = h.DB.QueryRow(r.Context(),
			`SELECT id FROM coin_purchases WHERE platform_transaction_id = $1`,
			req.PlatformTransactionID).Scan(&existingID)
		response.JSON(w, http.StatusOK, map[string]string{"purchase_id": existingID})
		return
	}

	// Update avg_coin_value_jpy
	var currentBalance int
	var currentAvg float64
	_ = tx.QueryRow(r.Context(),
		`SELECT COALESCE(avg_coin_value_jpy, 0) FROM user_coin_values WHERE user_id = $1`, userID).Scan(&currentAvg)
	_ = tx.QueryRow(r.Context(), `
		SELECT COALESCE(SUM(cp.coins_amount), 0) - COALESCE(SUM(cc.coins_deducted), 0) - COALESCE(SUM(ct.coins_spent), 0)
		FROM coin_purchases cp
		LEFT JOIN coin_purchase_cancellations cc ON cc.coin_purchase_id = cp.id
		LEFT JOIN coin_transactions ct ON ct.buyer_id = cp.user_id
		WHERE cp.user_id = $1 AND cp.valid_until > now()`, userID).Scan(&currentBalance)

	var newAvg float64
	denominator := currentBalance + req.CoinsAmount
	if denominator == 0 {
		newAvg = estimatedNet / float64(req.CoinsAmount)
	} else {
		newAvg = (float64(currentBalance)*currentAvg + estimatedNet) / float64(denominator)
	}

	_, err = tx.Exec(r.Context(), `
		INSERT INTO user_coin_values (user_id, avg_coin_value_jpy)
		VALUES ($1, $2)
		ON CONFLICT (user_id) DO UPDATE SET avg_coin_value_jpy = $2, updated_at = now()`,
		userID, newAvg)
	if err != nil {
		h.Logger.Error("update user_coin_values", "error", err)
		response.Error(w, r, http.StatusInternalServerError, "DB_ERROR", "database error")
		return
	}

	// Daily balance snapshot (first event of the day)
	_, _ = tx.Exec(r.Context(), `
		INSERT INTO coin_balance_snapshots (user_id, snapshot_date, balance, change_reason)
		VALUES ($1, CURRENT_DATE, $2, 'purchase')
		ON CONFLICT (user_id, snapshot_date) DO NOTHING`,
		userID, currentBalance)

	if err := tx.Commit(r.Context()); err != nil {
		h.Logger.Error("commit coin purchase", "error", err)
		response.Error(w, r, http.StatusInternalServerError, "DB_ERROR", "database error")
		return
	}

	response.JSON(w, http.StatusOK, map[string]string{"purchase_id": purchaseID})
}

// ── Webhook: Apple Refund ─────────────────────────────────────────────────────

// POST /webhook/apple
func (h *Handler) WebhookApple(w http.ResponseWriter, r *http.Request) {
	var req struct {
		SignedPayload string `json:"signedPayload"`
	}
	if err := json.NewDecoder(r.Body).Decode(&req); err != nil || req.SignedPayload == "" {
		response.Error(w, r, http.StatusBadRequest, "INVALID_BODY", "missing signedPayload")
		return
	}

	// Record raw webhook event
	var eventID int64
	_ = h.DB.QueryRow(r.Context(), `
		INSERT INTO webhook_events (source, event_type, external_id, raw_payload, processing_status)
		VALUES ('apple', 'REFUND', '', $1, 'pending')
		RETURNING id`, req.SignedPayload).Scan(&eventID)

	// TODO: JWS signature verification using Apple public key (Phase 8 full implementation)
	// For now, mark as pending for async processing
	// The signed payload is stored raw; a background worker handles verification and processing
	w.WriteHeader(http.StatusOK)
}

// ── Webhook: Google Refund ────────────────────────────────────────────────────

// POST /webhook/google
func (h *Handler) WebhookGoogle(w http.ResponseWriter, r *http.Request) {
	var req struct {
		Message struct {
			Data string `json:"data"`
		} `json:"message"`
	}
	if err := json.NewDecoder(r.Body).Decode(&req); err != nil || req.Message.Data == "" {
		response.Error(w, r, http.StatusBadRequest, "INVALID_BODY", "missing message.data")
		return
	}

	// TODO: Pub/Sub push subscription bearer token validation
	// Record raw event for async processing
	_ = h.DB.QueryRow(r.Context(), `
		INSERT INTO webhook_events (source, event_type, external_id, raw_payload, processing_status)
		VALUES ('google', 'ONE_TIME_PRODUCT_VOIDED', '', $1, 'pending')
		RETURNING id`, req.Message.Data)

	w.WriteHeader(http.StatusOK)
}

// ── Admin: Coin Purchase Cancellations ────────────────────────────────────────

// GET /admin/coin-purchases/cancellations
func (h *Handler) AdminListCancellations(w http.ResponseWriter, r *http.Request) {
	q := r.URL.Query()
	after := q.Get("after")
	limit := 20
	if l, err := strconv.Atoi(q.Get("limit")); err == nil && l > 0 && l <= 50 {
		limit = l
	}

	args := []any{}
	argIdx := 1
	conds := []string{}

	if after != "" {
		if afterID, err := strconv.ParseInt(after, 10, 64); err == nil {
			conds = append(conds, "c.id < $"+strconv.Itoa(argIdx))
			args = append(args, afterID)
			argIdx++
		}
	}

	where := ""
	if len(conds) > 0 {
		where = "WHERE " + conds[0]
	}
	args = append(args, limit+1)

	rows, err := h.DB.Query(r.Context(), `
		SELECT c.id, c.coin_purchase_id, c.cancellation_type, c.platform,
		       c.coins_deducted, c.balance_before, c.balance_after, c.cancelled_at, c.notes
		FROM coin_purchase_cancellations c
		`+where+`
		ORDER BY c.id DESC
		LIMIT $`+strconv.Itoa(argIdx), args...)
	if err != nil {
		h.Logger.Error("list cancellations", "error", err)
		response.Error(w, r, http.StatusInternalServerError, "DB_ERROR", "database error")
		return
	}
	defer rows.Close()

	type row struct {
		ID               int64   `json:"id"`
		CoinPurchaseID   string  `json:"coin_purchase_id"`
		CancellationType string  `json:"cancellation_type"`
		Platform         *string `json:"platform"`
		CoinsDeducted    int     `json:"coins_deducted"`
		BalanceBefore    int     `json:"balance_before"`
		BalanceAfter     int     `json:"balance_after"`
		CancelledAt      string  `json:"cancelled_at"`
		Notes            *string `json:"notes"`
	}

	items := []row{}
	for rows.Next() {
		var it row
		var cancelledAt time.Time
		if err := rows.Scan(&it.ID, &it.CoinPurchaseID, &it.CancellationType, &it.Platform,
			&it.CoinsDeducted, &it.BalanceBefore, &it.BalanceAfter, &cancelledAt, &it.Notes); err != nil {
			h.Logger.Error("scan cancellation", "error", err)
			response.Error(w, r, http.StatusInternalServerError, "DB_ERROR", "database error")
			return
		}
		it.CancelledAt = cancelledAt.UTC().Format(time.RFC3339)
		items = append(items, it)
	}

	hasNext := len(items) > limit
	if hasNext {
		items = items[:limit]
	}
	var nextCursor *string
	if hasNext && len(items) > 0 {
		c := strconv.FormatInt(items[len(items)-1].ID, 10)
		nextCursor = &c
	}

	response.JSON(w, http.StatusOK, map[string]any{"cancellations": items, "next_cursor": nextCursor})
}

// POST /admin/coin-purchases/{purchaseID}/cancel
func (h *Handler) AdminCancelCoinPurchase(w http.ResponseWriter, r *http.Request) {
	purchaseID := chi.URLParam(r, "purchaseID")
	if purchaseID == "" {
		response.Error(w, r, http.StatusBadRequest, "MISSING_ID", "purchase ID required")
		return
	}
	adminUser := mw.AdminUserFromContext(r.Context())

	var req struct {
		Notes string `json:"notes"`
	}
	_ = json.NewDecoder(r.Body).Decode(&req)

	tx, err := h.DB.Begin(r.Context())
	if err != nil {
		response.Error(w, r, http.StatusInternalServerError, "DB_ERROR", "database error")
		return
	}
	defer tx.Rollback(r.Context())

	var userID string
	var coinsAmount int
	var validUntil time.Time
	err = tx.QueryRow(r.Context(),
		`SELECT user_id, coins_amount, valid_until FROM coin_purchases WHERE id = $1`, purchaseID,
	).Scan(&userID, &coinsAmount, &validUntil)
	if err != nil {
		response.Error(w, r, http.StatusNotFound, "NOT_FOUND", "purchase not found")
		return
	}

	// Compute current balance (before cancellation)
	var rawTotal, deducted, spent int
	_ = tx.QueryRow(r.Context(),
		`SELECT COALESCE(SUM(coins_amount), 0) FROM coin_purchases WHERE user_id = $1 AND valid_until > now()`, userID).Scan(&rawTotal)
	_ = tx.QueryRow(r.Context(), `
		SELECT COALESCE(SUM(cc.coins_deducted), 0)
		FROM coin_purchase_cancellations cc
		JOIN coin_purchases cp ON cp.id = cc.coin_purchase_id
		WHERE cp.user_id = $1`, userID).Scan(&deducted)
	_ = tx.QueryRow(r.Context(),
		`SELECT COALESCE(SUM(coins_spent), 0) FROM coin_transactions WHERE buyer_id = $1`, userID).Scan(&spent)
	balanceBefore := rawTotal - deducted - spent

	coinsDeducted := coinsAmount
	if validUntil.Before(time.Now().UTC()) {
		coinsDeducted = 0
	}
	balanceAfter := balanceBefore - coinsDeducted

	_, err = tx.Exec(r.Context(), `
		INSERT INTO coin_purchase_cancellations
		  (coin_purchase_id, cancellation_type, coins_deducted, balance_before, balance_after, admin_id, notes)
		VALUES ($1, 'manual_admin', $2, $3, $4, $5, $6)`,
		purchaseID, coinsDeducted, balanceBefore, balanceAfter, adminUser.ID, req.Notes)
	if err != nil {
		h.Logger.Error("insert cancellation", "error", err)
		response.Error(w, r, http.StatusInternalServerError, "DB_ERROR", "database error")
		return
	}

	if coinsDeducted > 0 {
		_, _ = tx.Exec(r.Context(), `
			INSERT INTO coin_balance_snapshots (user_id, snapshot_date, balance, change_reason)
			VALUES ($1, CURRENT_DATE, $2, 'cancel')
			ON CONFLICT (user_id, snapshot_date) DO NOTHING`, userID, balanceAfter)
	}

	if err := tx.Commit(r.Context()); err != nil {
		h.Logger.Error("commit cancellation", "error", err)
		response.Error(w, r, http.StatusInternalServerError, "DB_ERROR", "database error")
		return
	}

	response.JSON(w, http.StatusOK, map[string]any{
		"coins_deducted": coinsDeducted,
		"balance_before": balanceBefore,
		"balance_after":  balanceAfter,
	})
}
