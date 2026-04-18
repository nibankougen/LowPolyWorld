package middleware

import (
	"context"
	"log/slog"
	"net/http"
	"time"
)

type accessLogCtxKey struct{}

// SetAccessLogUserID writes the authenticated user ID into the shared holder placed by AccessLog.
// Call this from auth middleware after the user ID is known.
func SetAccessLogUserID(r *http.Request, userID string) {
	if holder, ok := r.Context().Value(accessLogCtxKey{}).(*string); ok {
		*holder = userID
	}
}

// AccessLog is a structured JSON access logging middleware that replaces chi's default Logger.
// It captures the user ID populated by downstream auth middleware via a shared *string in context.
func AccessLog(logger *slog.Logger) func(http.Handler) http.Handler {
	return func(next http.Handler) http.Handler {
		return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
			start := time.Now()

			userID := new(string)
			ctx := context.WithValue(r.Context(), accessLogCtxKey{}, userID)

			ww := &statusWriter{ResponseWriter: w, code: http.StatusOK}
			next.ServeHTTP(ww, r.WithContext(ctx))

			var uid any
			if *userID != "" {
				uid = *userID
			}

			logger.LogAttrs(r.Context(), slog.LevelInfo, "",
				slog.String("type", "access_log"),
				slog.String("method", r.Method),
				slog.String("path", r.URL.Path),
				slog.Int("status", ww.code),
				slog.Int64("latency_ms", time.Since(start).Milliseconds()),
				slog.String("ip", r.RemoteAddr),
				slog.Any("user_id", uid),
				slog.String("user_agent", r.UserAgent()),
			)
		})
	}
}

// statusWriter wraps http.ResponseWriter to capture the response status code.
type statusWriter struct {
	http.ResponseWriter
	code    int
	written bool
}

func (sw *statusWriter) WriteHeader(code int) {
	if !sw.written {
		sw.code = code
		sw.written = true
	}
	sw.ResponseWriter.WriteHeader(code)
}

func (sw *statusWriter) Write(b []byte) (int, error) {
	if !sw.written {
		sw.written = true
	}
	return sw.ResponseWriter.Write(b)
}
