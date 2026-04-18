package middleware

import (
	"context"
	"net/http"
	"strings"

	"github.com/jackc/pgx/v5/pgxpool"
	"github.com/nibankougen/LowPolyWorld/api/internal/auth"
	"github.com/nibankougen/LowPolyWorld/api/internal/response"
)

type authCtxKey string

const UserIDKey authCtxKey = "user_id"

// AuthMiddleware validates JWT tokens and enforces token_revision + deleted_at checks.
type AuthMiddleware struct {
	authSvc *auth.Service
	db      *pgxpool.Pool
}

// NewAuthMiddleware constructs an AuthMiddleware.
func NewAuthMiddleware(authSvc *auth.Service, db *pgxpool.Pool) *AuthMiddleware {
	return &AuthMiddleware{authSvc: authSvc, db: db}
}

// Authenticate validates the Bearer JWT and rejects deleted or revoked sessions.
func (m *AuthMiddleware) Authenticate(next http.Handler) http.Handler {
	return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		header := r.Header.Get("Authorization")
		if !strings.HasPrefix(header, "Bearer ") {
			response.Error(w, r, http.StatusUnauthorized, "unauthorized", "missing or invalid authorization header")
			return
		}
		tokenStr := strings.TrimPrefix(header, "Bearer ")
		claims, err := m.authSvc.ParseAccessToken(tokenStr)
		if err != nil {
			response.Error(w, r, http.StatusUnauthorized, "unauthorized", "invalid token")
			return
		}

		var tokenRevision int
		var deletedAt *string
		err = m.db.QueryRow(r.Context(),
			`SELECT token_revision, deleted_at::text FROM active_users WHERE user_id = $1`,
			claims.UserID,
		).Scan(&tokenRevision, &deletedAt)
		if err != nil {
			response.Error(w, r, http.StatusUnauthorized, "unauthorized", "user not found")
			return
		}
		if tokenRevision != claims.Revision {
			response.Error(w, r, http.StatusUnauthorized, "unauthorized", "token has been revoked")
			return
		}
		if deletedAt != nil {
			response.Error(w, r, http.StatusForbidden, "account_deleted", "this account has been deleted")
			return
		}

		ctx := context.WithValue(r.Context(), UserIDKey, claims.UserID)
		next.ServeHTTP(w, r.WithContext(ctx))
	})
}

// UserIDFromContext extracts the authenticated user ID from the request context.
func UserIDFromContext(ctx context.Context) string {
	id, _ := ctx.Value(UserIDKey).(string)
	return id
}
