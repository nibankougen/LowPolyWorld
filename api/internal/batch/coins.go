package batch

import (
	"context"
	"log/slog"
	"time"

	"github.com/jackc/pgx/v5/pgxpool"
)

// ExpireCoins records balance snapshots for coin lots that expired since the last run,
// and sends 30-day / 7-day advance expiry notifications.
// Returns the total number of expiry events recorded.
func ExpireCoins(ctx context.Context, db *pgxpool.Pool, logger *slog.Logger) (int, error) {
	affected := 0

	// ── Step 1: record balance snapshots for newly-expired lots ──────────────
	// For each user with lots that expired and haven't had a snapshot recorded yet,
	// write one snapshot row per user per day with the post-expiry balance.
	//
	// We identify "newly expired" as: valid_until < now() AND no snapshot exists
	// for this user on today's date with change_reason='expire'.
	rows, err := db.Query(ctx,
		`SELECT DISTINCT cp.user_id
		 FROM coin_purchases cp
		 WHERE cp.valid_until < now()
		   AND NOT EXISTS (
		     SELECT 1 FROM coin_balance_snapshots cbs
		     WHERE cbs.user_id = cp.user_id
		       AND cbs.snapshot_date = CURRENT_DATE
		       AND cbs.change_reason = 'expire'
		   )`,
	)
	if err != nil {
		return 0, err
	}
	var expiredUserIDs []string
	for rows.Next() {
		var uid string
		if err := rows.Scan(&uid); err != nil {
			rows.Close()
			return 0, err
		}
		expiredUserIDs = append(expiredUserIDs, uid)
	}
	rows.Close()
	if err := rows.Err(); err != nil {
		return 0, err
	}

	for _, userID := range expiredUserIDs {
		// Calculate current balance (valid lots only − deductions − spent)
		var balance int
		err := db.QueryRow(ctx,
			`SELECT
			   (SELECT COALESCE(SUM(coins_amount), 0)
			    FROM coin_purchases
			    WHERE user_id = $1 AND valid_until > now())
			 - (SELECT COALESCE(SUM(cc.coins_deducted), 0)
			    FROM coin_purchase_cancellations cc
			    JOIN coin_purchases cp ON cp.id = cc.coin_purchase_id
			    WHERE cp.user_id = $1)
			 - (SELECT COALESCE(SUM(coins_spent), 0)
			    FROM coin_transactions
			    WHERE buyer_id = $1)`,
			userID,
		).Scan(&balance)
		if err != nil {
			logger.Error("expire coins: balance calc failed", "user_id", userID, "error", err)
			continue
		}
		if balance < 0 {
			balance = 0
		}

		_, err = db.Exec(ctx,
			`INSERT INTO coin_balance_snapshots (user_id, snapshot_date, balance, change_reason)
			 VALUES ($1, CURRENT_DATE, $2, 'expire')
			 ON CONFLICT (user_id, snapshot_date) DO NOTHING`,
			userID, balance,
		)
		if err != nil {
			logger.Error("expire coins: snapshot insert failed", "user_id", userID, "error", err)
			continue
		}
		affected++
	}

	// ── Step 2: send 30-day advance expiry notifications ─────────────────────
	notif30, err := sendExpiryNotifications(ctx, db, logger, "30day", "now() + interval '30 days'", "now() + interval '31 days'")
	if err != nil {
		logger.Error("expire coins: 30-day notifications failed", "error", err)
	}
	logger.Info("expire coins: 30-day notifications sent", "count", notif30)

	// ── Step 3: send 7-day advance expiry notifications ──────────────────────
	notif7, err := sendExpiryNotifications(ctx, db, logger, "7day", "now() + interval '7 days'", "now() + interval '8 days'")
	if err != nil {
		logger.Error("expire coins: 7-day notifications failed", "error", err)
	}
	logger.Info("expire coins: 7-day notifications sent", "count", notif7)

	return affected, nil
}

// sendExpiryNotifications inserts dedup rows for users with lots expiring in the
// given window, returning the number of new notification records inserted.
func sendExpiryNotifications(ctx context.Context, db *pgxpool.Pool, logger *slog.Logger, notifType, windowStart, windowEnd string) (int, error) {
	// Find lots expiring in the target window that haven't been notified yet.
	rows, err := db.Query(ctx,
		`SELECT DISTINCT cp.user_id, cp.valid_until
		 FROM coin_purchases cp
		 WHERE cp.valid_until >= `+windowStart+`
		   AND cp.valid_until <  `+windowEnd+`
		   AND NOT EXISTS (
		     SELECT 1 FROM coin_expiry_notifications cen
		     WHERE cen.user_id    = cp.user_id
		       AND cen.valid_until = cp.valid_until
		       AND cen.type        = $1
		   )`,
		notifType,
	)
	if err != nil {
		return 0, err
	}
	defer rows.Close()

	type notifTarget struct {
		userID     string
		validUntil time.Time
	}
	var targets []notifTarget
	for rows.Next() {
		var t notifTarget
		if err := rows.Scan(&t.userID, &t.validUntil); err != nil {
			return 0, err
		}
		targets = append(targets, t)
	}
	if err := rows.Err(); err != nil {
		return 0, err
	}

	count := 0
	for _, t := range targets {
		_, err := db.Exec(ctx,
			`INSERT INTO coin_expiry_notifications (user_id, valid_until, type)
			 VALUES ($1, $2, $3)
			 ON CONFLICT (user_id, valid_until, type) DO NOTHING`,
			t.userID, t.validUntil, notifType,
		)
		if err != nil {
			logger.Error("expiry notification insert failed",
				"user_id", t.userID, "type", notifType, "error", err)
			continue
		}
		count++
		// Push notification delivery would be wired here (Phase 9+).
		logger.Info("coin expiry notification recorded",
			"user_id", t.userID, "type", notifType, "valid_until", t.validUntil)
	}
	return count, nil
}
