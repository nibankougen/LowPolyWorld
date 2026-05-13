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

const (
	UserIDKey              authCtxKey = "user_id"
	ageGroupKey            authCtxKey = "age_group"
	parentalConsentKey     authCtxKey = "parental_consent_verified"
)

// AgeGroupFromContext returns the user's age_group stored by Authenticate.
func AgeGroupFromContext(ctx context.Context) string {
	v, _ := ctx.Value(ageGroupKey).(string)
	return v
}

// ParentalConsentVerifiedFromContext returns true if parental consent has been verified.
func ParentalConsentVerifiedFromContext(ctx context.Context) bool {
	v, _ := ctx.Value(parentalConsentKey).(bool)
	return v
}

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
// It also stores age_group and parental consent status in the request context for
// downstream middleware (RequireParentalConsent).
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
		var ageGroup string
		var parentalConsentVerifiedAt *string
		err = m.db.QueryRow(r.Context(),
			`SELECT token_revision, deleted_at::text, age_group, parental_consent_verified_at::text
			 FROM active_users WHERE user_id = $1`,
			claims.UserID,
		).Scan(&tokenRevision, &deletedAt, &ageGroup, &parentalConsentVerifiedAt)
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

		SetAccessLogUserID(r, claims.UserID)
		ctx := context.WithValue(r.Context(), UserIDKey, claims.UserID)
		ctx = context.WithValue(ctx, ageGroupKey, ageGroup)
		ctx = context.WithValue(ctx, parentalConsentKey, parentalConsentVerifiedAt != nil)
		next.ServeHTTP(w, r.WithContext(ctx))
	})
}

// RequireParentalConsent blocks requests from young_teen users whose parental consent
// has not yet been verified. Apply after Authenticate on routes that must be protected.
func (m *AuthMiddleware) RequireParentalConsent(next http.Handler) http.Handler {
	return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		ageGroup := AgeGroupFromContext(r.Context())
		consentVerified := ParentalConsentVerifiedFromContext(r.Context())
		if ageGroup == "young_teen" && !consentVerified {
			response.Error(w, r, http.StatusForbidden, "parental_consent_required",
				"parental consent must be verified before you can use this service")
			return
		}
		next.ServeHTTP(w, r)
	})
}

// UserIDFromContext extracts the authenticated user ID from the request context.
func UserIDFromContext(ctx context.Context) string {
	id, _ := ctx.Value(UserIDKey).(string)
	return id
}
