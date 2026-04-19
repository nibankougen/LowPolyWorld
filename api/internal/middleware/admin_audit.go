package middleware

import (
	"context"
	"log/slog"
	"net/http"
	"time"

	"github.com/jackc/pgx/v5/pgxpool"
)

type adminAuditKey struct{}

// AdminAuditEntry carries the mutable fields that the middleware writes to
// admin_audit_logs after the handler returns. Handlers can enrich it via
// SetAdminAuditEntry(ctx, ...) before writing their response.
type AdminAuditEntry struct {
	AdminID     string
	Action      string
	TargetType  string
	TargetID    string
	BeforeValue []byte // raw JSON, nil if unused
	AfterValue  []byte // raw JSON, nil if unused
	Notes       string
}

// SetAdminAuditEntry stores an entry in the request context so the middleware
// can retrieve it after the handler finishes.
func SetAdminAuditEntry(ctx context.Context, entry AdminAuditEntry) context.Context {
	return context.WithValue(ctx, adminAuditKey{}, &entry)
}

// adminAuditResponseWriter wraps http.ResponseWriter to capture the status code.
type adminAuditResponseWriter struct {
	http.ResponseWriter
	status int
}

func (a *adminAuditResponseWriter) WriteHeader(code int) {
	a.status = code
	a.ResponseWriter.WriteHeader(code)
}

func (a *adminAuditResponseWriter) Write(b []byte) (int, error) {
	return a.ResponseWriter.Write(b)
}

// AdminAuditLog returns a middleware that records every request to /admin/* into
// admin_audit_logs. The handler should call SetAdminAuditEntry to supply action/
// target details; if omitted the row is still written with empty fields so that
// every admin request (including 4xx/5xx) is captured.
func AdminAuditLog(db *pgxpool.Pool, logger *slog.Logger) func(http.Handler) http.Handler {
	return func(next http.Handler) http.Handler {
		return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
			arw := &adminAuditResponseWriter{ResponseWriter: w, status: http.StatusOK}
			next.ServeHTTP(arw, r)

			entry, _ := r.Context().Value(adminAuditKey{}).(*AdminAuditEntry)
			method, path := r.Method, r.URL.Path

			go func() {
				ctx, cancel := context.WithTimeout(context.Background(), 5*time.Second)
				defer cancel()

				adminID := ""
				action := method + " " + path
				targetType := ""
				targetID := ""
				var beforeValue, afterValue any
				notes := ""
				var errorCode *string

				if entry != nil {
					if entry.AdminID != "" {
						adminID = entry.AdminID
					}
					if entry.Action != "" {
						action = entry.Action
					}
					targetType = entry.TargetType
					targetID = entry.TargetID
					notes = entry.Notes
					if len(entry.BeforeValue) > 0 {
						beforeValue = entry.BeforeValue
					}
					if len(entry.AfterValue) > 0 {
						afterValue = entry.AfterValue
					}
				}

				status := arw.status
				if status >= 400 {
					code := http.StatusText(status)
					errorCode = &code
				}

				if _, err := db.Exec(ctx,
					`INSERT INTO admin_audit_logs
					 (admin_id, action, target_type, target_id, before_value, after_value, notes, response_status, error_code)
					 VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9)`,
					adminID, action, targetType, targetID,
					beforeValue, afterValue, notes,
					int16(status), errorCode,
				); err != nil {
					logger.Error("admin_audit: insert failed", "error", err,
						"action", action, "status", status)
				}
			}()
		})
	}
}
