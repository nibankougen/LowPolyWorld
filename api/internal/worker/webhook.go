package worker

import (
	"context"
	"crypto/ecdsa"
	"crypto/sha256"
	"crypto/x509"
	"encoding/base64"
	"encoding/json"
	"fmt"
	"log/slog"
	"math/big"
	"strings"
	"time"

	"github.com/jackc/pgx/v5/pgxpool"
)

const (
	maxRetries   = 5
	pollInterval = 30 * time.Second
	batchSize    = 10
)

// WebhookWorker polls the webhook_events table and processes pending Apple / Google webhook events.
type WebhookWorker struct {
	DB                *pgxpool.Pool
	Logger            *slog.Logger
	AppleBundleID     string // optional: validates bundle ID in Apple notifications
	GooglePackageName string // optional: validates package name in Google notifications
}

// Start runs the polling loop until ctx is cancelled.
func (w *WebhookWorker) Start(ctx context.Context) {
	ticker := time.NewTicker(pollInterval)
	defer ticker.Stop()
	w.processBatch(ctx) // process immediately on start
	for {
		select {
		case <-ctx.Done():
			return
		case <-ticker.C:
			w.processBatch(ctx)
		}
	}
}

type webhookEvent struct {
	id         int64
	source     string
	rawPayload string
	retryCount int
}

func (w *WebhookWorker) processBatch(ctx context.Context) {
	rows, err := w.DB.Query(ctx, `
		SELECT id, source, raw_payload, retry_count
		FROM webhook_events
		WHERE processing_status = 'pending' AND retry_count < $1
		ORDER BY id ASC LIMIT $2`, maxRetries, batchSize)
	if err != nil {
		w.Logger.Error("webhook worker: query pending events", "error", err)
		return
	}

	var events []webhookEvent
	for rows.Next() {
		var e webhookEvent
		if err := rows.Scan(&e.id, &e.source, &e.rawPayload, &e.retryCount); err != nil {
			rows.Close()
			w.Logger.Error("webhook worker: scan event", "error", err)
			return
		}
		events = append(events, e)
	}
	rows.Close()
	if err := rows.Err(); err != nil {
		w.Logger.Error("webhook worker: rows error", "error", err)
		return
	}

	for _, e := range events {
		w.processOne(ctx, e)
	}
}

func (w *WebhookWorker) processOne(ctx context.Context, e webhookEvent) {
	var eventType, externalID string
	var ignored bool
	var processErr error

	switch e.source {
	case "apple":
		eventType, externalID, ignored, processErr = w.processApple(ctx, e.rawPayload)
	case "google":
		eventType, externalID, ignored, processErr = w.processGoogle(ctx, e.rawPayload)
	default:
		_, _ = w.DB.Exec(ctx, `
			UPDATE webhook_events
			SET processing_status = 'ignored', event_type = 'unknown_source', processed_at = now()
			WHERE id = $1`, e.id)
		return
	}

	if processErr != nil {
		newRetry := e.retryCount + 1
		status := "failed"
		if newRetry >= maxRetries {
			status = "permanently_failed"
		}
		_, _ = w.DB.Exec(ctx, `
			UPDATE webhook_events
			SET processing_status = $1, retry_count = $2, error_message = $3
			WHERE id = $4`, status, newRetry, processErr.Error(), e.id)
		w.Logger.Error("webhook worker: event processing failed",
			"event_id", e.id, "source", e.source, "retry", newRetry, "error", processErr)
		return
	}

	finalStatus := "processed"
	if ignored {
		finalStatus = "ignored"
	}
	_, _ = w.DB.Exec(ctx, `
		UPDATE webhook_events
		SET processing_status = $1, event_type = $2, external_id = $3, processed_at = now()
		WHERE id = $4`, finalStatus, eventType, externalID, e.id)
	w.Logger.Info("webhook worker: event done",
		"event_id", e.id, "source", e.source, "event_type", eventType, "status", finalStatus)
}

// ── Apple ─────────────────────────────────────────────────────────────────────

type appleNotificationPayload struct {
	NotificationType string `json:"notificationType"`
	NotificationUUID string `json:"notificationUUID"`
	Data             struct {
		BundleID              string `json:"bundleId"`
		SignedTransactionInfo string `json:"signedTransactionInfo"`
	} `json:"data"`
}

type appleTransactionInfo struct {
	OriginalTransactionID string `json:"originalTransactionId"`
	BundleID              string `json:"bundleId"`
}

func (w *WebhookWorker) processApple(ctx context.Context, signedPayload string) (eventType, externalID string, ignored bool, err error) {
	outerBytes, err := verifyAppleJWS(signedPayload)
	if err != nil {
		return "", "", false, fmt.Errorf("outer JWS: %w", err)
	}

	var notif appleNotificationPayload
	if err := json.Unmarshal(outerBytes, &notif); err != nil {
		return "", "", false, fmt.Errorf("parse apple notification: %w", err)
	}

	// Reject events from a different app if configured
	if w.AppleBundleID != "" && notif.Data.BundleID != "" && notif.Data.BundleID != w.AppleBundleID {
		return notif.NotificationType, "bundle_mismatch", true, nil
	}

	// Only act on REFUND notifications; ignore everything else
	if notif.NotificationType != "REFUND" {
		return notif.NotificationType, notif.NotificationUUID, true, nil
	}

	if notif.Data.SignedTransactionInfo == "" {
		return "", "", false, fmt.Errorf("missing signedTransactionInfo in REFUND notification")
	}

	txBytes, err := verifyAppleJWS(notif.Data.SignedTransactionInfo)
	if err != nil {
		return "", "", false, fmt.Errorf("transaction JWS: %w", err)
	}

	var tx appleTransactionInfo
	if err := json.Unmarshal(txBytes, &tx); err != nil {
		return "", "", false, fmt.Errorf("parse transaction info: %w", err)
	}
	if tx.OriginalTransactionID == "" {
		return "", "", false, fmt.Errorf("missing originalTransactionId")
	}

	if err := w.handleRefund(ctx, tx.OriginalTransactionID, "ios"); err != nil {
		return "", "", false, err
	}
	return notif.NotificationType, tx.OriginalTransactionID, false, nil
}

// verifyAppleJWS verifies the ES256 JWS signature using the x5c leaf certificate
// embedded in the protected header. It does not validate the full certificate chain
// against Apple's root CA (acceptable for Phase 8 — add chain validation before
// production hardening if stricter security is needed).
func verifyAppleJWS(compact string) ([]byte, error) {
	parts := strings.SplitN(compact, ".", 3)
	if len(parts) != 3 {
		return nil, fmt.Errorf("invalid compact JWS: expected 3 parts")
	}

	// Decode protected header
	headerJSON, err := base64.RawURLEncoding.DecodeString(parts[0])
	if err != nil {
		return nil, fmt.Errorf("decode JWS header: %w", err)
	}
	var header struct {
		Alg string   `json:"alg"`
		X5c []string `json:"x5c"` // standard base64 (not base64url)
	}
	if err := json.Unmarshal(headerJSON, &header); err != nil {
		return nil, fmt.Errorf("parse JWS header: %w", err)
	}
	if header.Alg != "ES256" {
		return nil, fmt.Errorf("unexpected JWS algorithm: %s (expected ES256)", header.Alg)
	}
	if len(header.X5c) == 0 {
		return nil, fmt.Errorf("x5c not present in JWS header")
	}

	// Parse leaf certificate (x5c[0] uses standard base64, not base64url)
	leafDER, err := base64.StdEncoding.DecodeString(header.X5c[0])
	if err != nil {
		return nil, fmt.Errorf("decode leaf certificate from x5c: %w", err)
	}
	leafCert, err := x509.ParseCertificate(leafDER)
	if err != nil {
		return nil, fmt.Errorf("parse leaf certificate: %w", err)
	}
	ecKey, ok := leafCert.PublicKey.(*ecdsa.PublicKey)
	if !ok {
		return nil, fmt.Errorf("leaf certificate does not contain an ECDSA public key")
	}

	// Verify ES256 signature: SHA-256 over "base64url(header).base64url(payload)"
	signingInput := parts[0] + "." + parts[1]
	digest := sha256.Sum256([]byte(signingInput))

	sigBytes, err := base64.RawURLEncoding.DecodeString(parts[2])
	if err != nil {
		return nil, fmt.Errorf("decode JWS signature: %w", err)
	}
	// P-256 signature in JWS is raw r||s (64 bytes total)
	if len(sigBytes) != 64 {
		return nil, fmt.Errorf("unexpected ECDSA signature length: %d (expected 64)", len(sigBytes))
	}
	r := new(big.Int).SetBytes(sigBytes[:32])
	s := new(big.Int).SetBytes(sigBytes[32:])
	if !ecdsa.Verify(ecKey, digest[:], r, s) {
		return nil, fmt.Errorf("ECDSA signature verification failed")
	}

	// Decode and return the payload
	payload, err := base64.RawURLEncoding.DecodeString(parts[1])
	if err != nil {
		return nil, fmt.Errorf("decode JWS payload: %w", err)
	}
	return payload, nil
}

// ── Google ────────────────────────────────────────────────────────────────────

type googleDeveloperNotification struct {
	Version     string `json:"version"`
	PackageName string `json:"packageName"`
	OneTimePurchaseNotification *struct {
		Version          string `json:"version"`
		NotificationType int    `json:"notificationType"`
		PurchaseToken    string `json:"purchaseToken"`
		SKU              string `json:"sku"`
	} `json:"oneTimePurchaseNotification"`
}

// ONE_TIME_PRODUCT_CANCELED from Google Play Developer Notifications.
const googleNotifCanceled = 2

func (w *WebhookWorker) processGoogle(ctx context.Context, encodedData string) (eventType, externalID string, ignored bool, err error) {
	// message.data is standard base64; try URL-safe variant as fallback
	decoded, err := base64.StdEncoding.DecodeString(encodedData)
	if err != nil {
		decoded, err = base64.URLEncoding.DecodeString(encodedData)
		if err != nil {
			return "", "", false, fmt.Errorf("base64 decode message data: %w", err)
		}
	}

	var notif googleDeveloperNotification
	if err := json.Unmarshal(decoded, &notif); err != nil {
		return "", "", false, fmt.Errorf("parse google notification: %w", err)
	}

	if w.GooglePackageName != "" && notif.PackageName != "" && notif.PackageName != w.GooglePackageName {
		return "unknown", "package_mismatch", true, nil
	}

	if notif.OneTimePurchaseNotification == nil {
		// Subscription or other notification type — not relevant to coins
		return "subscription_or_other", notif.PackageName, true, nil
	}

	otp := notif.OneTimePurchaseNotification
	if otp.NotificationType != googleNotifCanceled {
		return fmt.Sprintf("one_time_%d", otp.NotificationType), otp.PurchaseToken, true, nil
	}

	if otp.PurchaseToken == "" {
		return "", "", false, fmt.Errorf("missing purchaseToken in cancellation notification")
	}

	if err := w.handleRefund(ctx, otp.PurchaseToken, "android"); err != nil {
		return "", "", false, err
	}
	return "ONE_TIME_PRODUCT_CANCELED", otp.PurchaseToken, false, nil
}

// ── Shared: Refund → Cancellation Record ─────────────────────────────────────

func (w *WebhookWorker) handleRefund(ctx context.Context, platformTransactionID, platform string) error {
	tx, err := w.DB.Begin(ctx)
	if err != nil {
		return fmt.Errorf("begin tx: %w", err)
	}
	defer tx.Rollback(ctx)

	// Look up the original coin purchase
	var purchaseID, userID string
	var coinsAmount int
	var validUntil time.Time
	err = tx.QueryRow(ctx, `
		SELECT id, user_id, coins_amount, valid_until
		FROM coin_purchases
		WHERE platform_transaction_id = $1 AND platform = $2`,
		platformTransactionID, platform,
	).Scan(&purchaseID, &userID, &coinsAmount, &validUntil)
	if err != nil {
		// Not found: the refund is for a non-coin IAP (e.g. subscription) — ignore silently
		return nil
	}

	// Idempotency: skip if already cancelled
	var alreadyCancelled bool
	_ = tx.QueryRow(ctx,
		`SELECT EXISTS(SELECT 1 FROM coin_purchase_cancellations WHERE coin_purchase_id = $1)`,
		purchaseID).Scan(&alreadyCancelled)
	if alreadyCancelled {
		return nil
	}

	// Compute balance before cancellation (valid lots − existing deductions − spent)
	var rawTotal, deducted, spent int
	_ = tx.QueryRow(ctx,
		`SELECT COALESCE(SUM(coins_amount), 0) FROM coin_purchases
		 WHERE user_id = $1 AND valid_until > now()`, userID).Scan(&rawTotal)
	_ = tx.QueryRow(ctx, `
		SELECT COALESCE(SUM(cc.coins_deducted), 0)
		FROM coin_purchase_cancellations cc
		JOIN coin_purchases cp ON cp.id = cc.coin_purchase_id
		WHERE cp.user_id = $1`, userID).Scan(&deducted)
	_ = tx.QueryRow(ctx,
		`SELECT COALESCE(SUM(coins_spent), 0) FROM coin_transactions WHERE buyer_id = $1`,
		userID).Scan(&spent)
	balanceBefore := rawTotal - deducted - spent

	// Expired lots yield 0 deducted coins (can't reclaim what already expired)
	coinsDeducted := coinsAmount
	if validUntil.Before(time.Now().UTC()) {
		coinsDeducted = 0
	}
	balanceAfter := balanceBefore - coinsDeducted

	_, err = tx.Exec(ctx, `
		INSERT INTO coin_purchase_cancellations
		  (coin_purchase_id, cancellation_type, platform, platform_transaction_id,
		   coins_deducted, balance_before, balance_after)
		VALUES ($1, 'platform_refund', $2, $3, $4, $5, $6)`,
		purchaseID, platform, platformTransactionID,
		coinsDeducted, balanceBefore, balanceAfter)
	if err != nil {
		return fmt.Errorf("insert cancellation record: %w", err)
	}

	if coinsDeducted > 0 {
		_, _ = tx.Exec(ctx, `
			INSERT INTO coin_balance_snapshots (user_id, snapshot_date, balance, change_reason)
			VALUES ($1, CURRENT_DATE, $2, 'cancel')
			ON CONFLICT (user_id, snapshot_date) DO NOTHING`, userID, balanceAfter)
	}

	return tx.Commit(ctx)
}
