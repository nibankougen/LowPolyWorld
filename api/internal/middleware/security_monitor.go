package middleware

import (
	"context"
	"encoding/json"
	"log/slog"
	"net/http"
	"time"

	"github.com/go-chi/httprate"
	"github.com/jackc/pgx/v5/pgxpool"
)

// insertSystemAlert inserts a row into system_alerts in a background goroutine.
// It is best-effort: failures are logged but do not affect the request.
func insertSystemAlert(db *pgxpool.Pool, logger *slog.Logger, alertType, severity, message string, details map[string]any) {
	var detailsJSON []byte
	if details != nil {
		detailsJSON, _ = json.Marshal(details)
	}
	go func() {
		ctx, cancel := context.WithTimeout(context.Background(), 5*time.Second)
		defer cancel()
		if _, err := db.Exec(ctx,
			`INSERT INTO system_alerts (alert_type, severity, message, details)
			 VALUES ($1, $2, $3, $4)`,
			alertType, severity, message, detailsJSON,
		); err != nil {
			logger.Error("insert system_alert failed", "alert_type", alertType, "error", err)
		}
	}()
}

// BruteForceLog wraps an httprate middleware and emits a structured
// brute_force_attempt log + system_alert whenever the rate limiter fires 429.
// Apply this to the auth route group around httprate.LimitByIP.
func BruteForceLog(logger *slog.Logger, db *pgxpool.Pool) func(http.Handler) http.Handler {
	return func(next http.Handler) http.Handler {
		return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
			sw := &statusWriter{ResponseWriter: w, code: http.StatusOK}
			next.ServeHTTP(sw, r)
			if sw.code == http.StatusTooManyRequests {
				ip := r.RemoteAddr
				logger.Warn("brute_force_attempt",
					"type", "brute_force_attempt",
					"ip", ip,
					"path", r.URL.Path,
					"method", r.Method,
				)
				insertSystemAlert(db, logger, "brute_force_attempt", "warning",
					"Auth endpoint rate limit reached — possible brute-force attack",
					map[string]any{"ip": ip, "path": r.URL.Path},
				)
			}
		})
	}
}

// HighAPIRateLog returns a middleware that enforces a per-user rate limit of
// maxReqPerMin requests per minute. When the limit is exceeded it responds
// 429 and emits a high_api_rate structured log + system_alert.
func HighAPIRateLog(logger *slog.Logger, db *pgxpool.Pool, maxReqPerMin int) func(http.Handler) http.Handler {
	rl := httprate.NewRateLimiter(maxReqPerMin, time.Minute,
		httprate.WithKeyFuncs(func(r *http.Request) (string, error) {
			uid := UserIDFromContext(r.Context())
			if uid == "" {
				return r.RemoteAddr, nil
			}
			return uid, nil
		}),
	)

	rateMW := rl.Handler

	return func(next http.Handler) http.Handler {
		inner := rateMW(next)
		return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
			sw := &statusWriter{ResponseWriter: w, code: http.StatusOK}
			inner.ServeHTTP(sw, r)
			if sw.code == http.StatusTooManyRequests {
				userID := UserIDFromContext(r.Context())
				logger.Warn("high_api_rate",
					"type", "high_api_rate",
					"user_id", userID,
					"ip", r.RemoteAddr,
					"path", r.URL.Path,
					"limit_per_min", maxReqPerMin,
				)
				insertSystemAlert(db, logger, "high_api_rate", "warning",
					"User exceeded API rate limit",
					map[string]any{
						"user_id":       userID,
						"ip":            r.RemoteAddr,
						"limit_per_min": maxReqPerMin,
					},
				)
			}
		})
	}
}

// CheckMassTokenRevocation queries the number of distinct users who had tokens
// revoked in the last hour. If above threshold, logs mass_token_revocation and
// inserts a system_alert. Call this after any token_revision increment.
func CheckMassTokenRevocation(ctx context.Context, db *pgxpool.Pool, logger *slog.Logger, threshold int) {
	go func() {
		qCtx, cancel := context.WithTimeout(context.Background(), 5*time.Second)
		defer cancel()

		var count int
		err := db.QueryRow(qCtx,
			`SELECT COUNT(DISTINCT user_id)
			 FROM refresh_tokens
			 WHERE revoked_at > now() - interval '1 hour'`,
		).Scan(&count)
		if err != nil {
			return
		}
		if count >= threshold {
			logger.Warn("mass_token_revocation",
				"type", "mass_token_revocation",
				"distinct_users_last_hour", count,
				"threshold", threshold,
			)
			insertSystemAlert(db, logger, "mass_token_revocation", "critical",
				"Mass token revocation detected — possible credential compromise",
				map[string]any{
					"distinct_users_last_hour": count,
					"threshold":                threshold,
				},
			)
		}
	}()
}
