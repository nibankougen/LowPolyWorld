// Package adminauth provides authentication for the admin management panel.
// Admin sessions use opaque bearer tokens stored in admin_sessions; the token
// itself is never stored — only its SHA-256 hash is kept in the DB.
package adminauth

import (
	"context"
	"crypto/rand"
	"crypto/sha256"
	"encoding/hex"
	"errors"
	"time"

	"github.com/jackc/pgx/v5"
	"github.com/jackc/pgx/v5/pgxpool"
	"golang.org/x/crypto/bcrypt"
)

const (
	RoleModerator  = "moderator"
	RoleAdmin      = "admin"
	RoleSuperAdmin = "super_admin"

	tokenLength = 32          // bytes of random data
	sessionTTL  = 24 * time.Hour
)

// RoleRank returns an ordinal for role comparison (higher = more privileged).
func RoleRank(role string) int {
	switch role {
	case RoleSuperAdmin:
		return 3
	case RoleAdmin:
		return 2
	case RoleModerator:
		return 1
	default:
		return 0
	}
}

// AtLeast reports whether actualRole meets or exceeds the required role level.
func AtLeast(actualRole, required string) bool {
	return RoleRank(actualRole) >= RoleRank(required)
}

// AdminUser holds the data of an authenticated admin user.
type AdminUser struct {
	ID   string
	Role string
}

var ErrInvalidCredentials = errors.New("invalid email or password")
var ErrInactiveAccount = errors.New("admin account is inactive")

// Login verifies email/password, creates a session, and returns the raw token.
func Login(ctx context.Context, db *pgxpool.Pool, email, password string) (string, error) {
	var id, hash, role string
	var isActive bool
	err := db.QueryRow(ctx,
		`SELECT id, password_hash, role, is_active FROM admin_users WHERE email = $1`,
		email,
	).Scan(&id, &hash, &role, &isActive)
	if errors.Is(err, pgx.ErrNoRows) {
		return "", ErrInvalidCredentials
	}
	if err != nil {
		return "", err
	}
	if !isActive {
		return "", ErrInactiveAccount
	}
	if err := bcrypt.CompareHashAndPassword([]byte(hash), []byte(password)); err != nil {
		return "", ErrInvalidCredentials
	}

	// Generate a random opaque token.
	rawBytes := make([]byte, tokenLength)
	if _, err := rand.Read(rawBytes); err != nil {
		return "", err
	}
	rawToken := hex.EncodeToString(rawBytes)
	tokenHash := hashToken(rawToken)
	expiresAt := time.Now().Add(sessionTTL)

	if _, err := db.Exec(ctx,
		`INSERT INTO admin_sessions (admin_id, token_hash, expires_at) VALUES ($1, $2, $3)`,
		id, tokenHash, expiresAt,
	); err != nil {
		return "", err
	}
	return rawToken, nil
}

// Authenticate looks up the raw bearer token and returns the AdminUser.
// Returns ErrInvalidCredentials if the token is unknown or expired.
func Authenticate(ctx context.Context, db *pgxpool.Pool, rawToken string) (*AdminUser, error) {
	tokenHash := hashToken(rawToken)

	var adminID, role string
	err := db.QueryRow(ctx,
		`SELECT au.id, au.role
		 FROM admin_sessions s
		 JOIN admin_users au ON au.id = s.admin_id
		 WHERE s.token_hash = $1
		   AND s.expires_at > now()
		   AND au.is_active = TRUE`,
		tokenHash,
	).Scan(&adminID, &role)
	if errors.Is(err, pgx.ErrNoRows) {
		return nil, ErrInvalidCredentials
	}
	if err != nil {
		return nil, err
	}
	return &AdminUser{ID: adminID, Role: role}, nil
}

// Logout invalidates the session for the given raw token.
func Logout(ctx context.Context, db *pgxpool.Pool, rawToken string) error {
	_, err := db.Exec(ctx,
		`DELETE FROM admin_sessions WHERE token_hash = $1`, hashToken(rawToken))
	return err
}

// HashPassword returns a bcrypt hash of the given password (cost 12).
func HashPassword(password string) (string, error) {
	h, err := bcrypt.GenerateFromPassword([]byte(password), 12)
	return string(h), err
}

func hashToken(raw string) string {
	sum := sha256.Sum256([]byte(raw))
	return hex.EncodeToString(sum[:])
}
