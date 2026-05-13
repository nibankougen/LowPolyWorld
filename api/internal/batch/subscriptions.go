package batch

import (
	"context"
	"log/slog"

	"github.com/jackc/pgx/v5/pgxpool"
)

// ExpireSubscriptions downgrades users whose subscription_expires_at is in the past.
// Returns the number of users downgraded.
func ExpireSubscriptions(ctx context.Context, db *pgxpool.Pool, logger *slog.Logger) (int, error) {
	tag, err := db.Exec(ctx,
		`UPDATE active_users
		 SET subscription_tier       = 'free',
		     subscription_expires_at = NULL
		 WHERE subscription_tier != 'free'
		   AND subscription_expires_at IS NOT NULL
		   AND subscription_expires_at < now()`,
	)
	if err != nil {
		return 0, err
	}
	n := int(tag.RowsAffected())
	if n > 0 {
		logger.Info("subscriptions expired", "count", n)
	}
	return n, nil
}
