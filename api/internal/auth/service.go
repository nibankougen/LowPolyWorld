package auth

import (
	"context"
	"crypto/rand"
	"crypto/rsa"
	"crypto/sha256"
	"encoding/hex"
	"fmt"
	"math/big"
	"os"
	"time"

	"github.com/lestrrat-go/jwx/v2/jwa"
	"github.com/lestrrat-go/jwx/v2/jwk"
	"github.com/lestrrat-go/jwx/v2/jwt"
)

const (
	accessTokenTTL  = 7 * 24 * time.Hour
	refreshTokenTTL = 90 * 24 * time.Hour

	googleJWKSURL = "https://www.googleapis.com/oauth2/v3/certs"
	appleJWKSURL  = "https://appleid.apple.com/auth/keys"
)

// Claims holds the verified claims extracted from our own access token.
type Claims struct {
	UserID   string
	Revision int
}

// Service handles JWT issuance/verification and OAuth ID token verification.
type Service struct {
	privateKey     *rsa.PrivateKey
	publicKey      *rsa.PublicKey
	jwksCache      *jwk.Cache
	GoogleClientID string
	AppleClientID  string
}

// NewService loads RSA keys from disk and initialises the JWKS cache.
func NewService(ctx context.Context, privateKeyPath, publicKeyPath, googleClientID, appleClientID string) (*Service, error) {
	privPEM, err := os.ReadFile(privateKeyPath)
	if err != nil {
		return nil, fmt.Errorf("read private key: %w", err)
	}
	privKey, err := jwk.ParseKey(privPEM, jwk.WithPEM(true))
	if err != nil {
		return nil, fmt.Errorf("parse private key: %w", err)
	}
	var rawPriv rsa.PrivateKey
	if err := privKey.Raw(&rawPriv); err != nil {
		return nil, fmt.Errorf("extract private key: %w", err)
	}

	pubPEM, err := os.ReadFile(publicKeyPath)
	if err != nil {
		return nil, fmt.Errorf("read public key: %w", err)
	}
	pubKey, err := jwk.ParseKey(pubPEM, jwk.WithPEM(true))
	if err != nil {
		return nil, fmt.Errorf("parse public key: %w", err)
	}
	var rawPub rsa.PublicKey
	if err := pubKey.Raw(&rawPub); err != nil {
		return nil, fmt.Errorf("extract public key: %w", err)
	}

	cache := jwk.NewCache(ctx)
	_ = cache.Register(googleJWKSURL, jwk.WithMinRefreshInterval(15*time.Minute))
	_ = cache.Register(appleJWKSURL, jwk.WithMinRefreshInterval(15*time.Minute))
	// Pre-fetch (ignore errors — will retry on first use)
	_, _ = cache.Refresh(ctx, googleJWKSURL)
	_, _ = cache.Refresh(ctx, appleJWKSURL)

	return &Service{
		privateKey:     &rawPriv,
		publicKey:      &rawPub,
		jwksCache:      cache,
		GoogleClientID: googleClientID,
		AppleClientID:  appleClientID,
	}, nil
}

// IssueAccessToken creates a signed RS256 JWT for the given user.
func (s *Service) IssueAccessToken(userID string, tokenRevision int) (string, error) {
	tok, err := jwt.NewBuilder().
		Subject(userID).
		IssuedAt(time.Now()).
		Expiration(time.Now().Add(accessTokenTTL)).
		Claim("rev", tokenRevision).
		Build()
	if err != nil {
		return "", fmt.Errorf("build token: %w", err)
	}
	signed, err := jwt.Sign(tok, jwt.WithKey(jwa.RS256, s.privateKey))
	if err != nil {
		return "", fmt.Errorf("sign token: %w", err)
	}
	return string(signed), nil
}

// ParseAccessToken verifies our own JWT and returns the claims.
func (s *Service) ParseAccessToken(tokenStr string) (*Claims, error) {
	tok, err := jwt.Parse([]byte(tokenStr), jwt.WithKey(jwa.RS256, s.publicKey), jwt.WithValidate(true))
	if err != nil {
		return nil, fmt.Errorf("parse token: %w", err)
	}
	rev, _ := tok.Get("rev")
	var revision int
	switch v := rev.(type) {
	case float64:
		revision = int(v)
	case int:
		revision = v
	}
	return &Claims{UserID: tok.Subject(), Revision: revision}, nil
}

// IssueRefreshToken generates a random opaque token and returns (plaintext, sha256hash).
func IssueRefreshToken() (plain, hash string, err error) {
	b := make([]byte, 16)
	if _, err = rand.Read(b); err != nil {
		return "", "", fmt.Errorf("rand.Read: %w", err)
	}
	plain = hex.EncodeToString(b)
	sum := sha256.Sum256([]byte(plain))
	hash = hex.EncodeToString(sum[:])
	return plain, hash, nil
}

// HashRefreshToken returns the SHA-256 hex hash of the given plaintext token.
func HashRefreshToken(plain string) string {
	sum := sha256.Sum256([]byte(plain))
	return hex.EncodeToString(sum[:])
}

// RefreshTokenExpiresAt returns the standard expiry time for a new refresh token.
func RefreshTokenExpiresAt() time.Time { return time.Now().Add(refreshTokenTTL) }

// VerifyGoogleIDToken verifies a Google ID token and returns the provider sub.
func (s *Service) VerifyGoogleIDToken(ctx context.Context, idToken string) (string, error) {
	keyset, err := s.jwksCache.Get(ctx, googleJWKSURL)
	if err != nil {
		return "", fmt.Errorf("fetch google jwks: %w", err)
	}
	tok, err := jwt.Parse([]byte(idToken), jwt.WithKeySet(keyset), jwt.WithValidate(true))
	if err != nil {
		return "", fmt.Errorf("parse google id token: %w", err)
	}
	if s.GoogleClientID != "" {
		audOK := false
		for _, a := range tok.Audience() {
			if a == s.GoogleClientID {
				audOK = true
				break
			}
		}
		if !audOK {
			return "", fmt.Errorf("google id token audience mismatch")
		}
	}
	return tok.Subject(), nil
}

// VerifyAppleIDToken verifies an Apple ID token and returns the provider sub.
func (s *Service) VerifyAppleIDToken(ctx context.Context, idToken string) (string, error) {
	keyset, err := s.jwksCache.Get(ctx, appleJWKSURL)
	if err != nil {
		return "", fmt.Errorf("fetch apple jwks: %w", err)
	}
	tok, err := jwt.Parse([]byte(idToken), jwt.WithKeySet(keyset), jwt.WithValidate(true))
	if err != nil {
		return "", fmt.Errorf("parse apple id token: %w", err)
	}
	if s.AppleClientID != "" {
		audOK := false
		for _, a := range tok.Audience() {
			if a == s.AppleClientID {
				audOK = true
				break
			}
		}
		if !audOK {
			return "", fmt.Errorf("apple id token audience mismatch")
		}
	}
	return tok.Subject(), nil
}

const tempNameAlphabet = "abcdefghijklmnopqrstuvwxyz0123456789"

// GenerateTempName creates a temporary @name for new users of the form "user_XXXXXXXX".
func GenerateTempName() string {
	const suffixLen = 8
	b := make([]byte, suffixLen)
	alphabetLen := big.NewInt(int64(len(tempNameAlphabet)))
	for i := range b {
		n, _ := rand.Int(rand.Reader, alphabetLen)
		b[i] = tempNameAlphabet[n.Int64()]
	}
	return "user_" + string(b)
}
