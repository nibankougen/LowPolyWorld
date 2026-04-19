package middleware

import (
	"context"
	"log/slog"
	"net/http"
	"strings"

	"github.com/jackc/pgx/v5/pgxpool"
	"github.com/nibankougen/LowPolyWorld/api/internal/adminauth"
	"github.com/nibankougen/LowPolyWorld/api/internal/response"
)

type adminUserKey struct{}

// AdminUserFromContext retrieves the authenticated AdminUser from the context.
// Returns nil if the context was not populated by AdminAuthMiddleware.
func AdminUserFromContext(ctx context.Context) *adminauth.AdminUser {
	u, _ := ctx.Value(adminUserKey{}).(*adminauth.AdminUser)
	return u
}

// AdminAuth returns a middleware that validates the admin bearer token and
// stores the AdminUser in the request context. Returns 401 if missing/invalid.
func AdminAuth(db *pgxpool.Pool, logger *slog.Logger) func(http.Handler) http.Handler {
	return func(next http.Handler) http.Handler {
		return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
			raw := strings.TrimPrefix(r.Header.Get("Authorization"), "Bearer ")
			if raw == "" {
				response.Error(w, r, http.StatusUnauthorized, "unauthorized", "missing admin token")
				return
			}
			admin, err := adminauth.Authenticate(r.Context(), db, raw)
			if err != nil {
				response.Error(w, r, http.StatusUnauthorized, "unauthorized", "invalid or expired admin token")
				return
			}
			ctx := context.WithValue(r.Context(), adminUserKey{}, admin)
			next.ServeHTTP(w, r.WithContext(ctx))
		})
	}
}

// RequireAdminRole returns a middleware that rejects requests from admins
// whose role is below the required level (returns 403).
func RequireAdminRole(required string) func(http.Handler) http.Handler {
	return func(next http.Handler) http.Handler {
		return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
			admin := AdminUserFromContext(r.Context())
			if admin == nil || !adminauth.AtLeast(admin.Role, required) {
				response.Error(w, r, http.StatusForbidden, "forbidden", "insufficient admin role")
				return
			}
			next.ServeHTTP(w, r)
		})
	}
}
