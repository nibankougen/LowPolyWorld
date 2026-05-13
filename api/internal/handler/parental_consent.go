package handler

import (
	"context"
	"crypto/rand"
	"crypto/sha256"
	"encoding/hex"
	"encoding/json"
	"net/http"
	"strings"

	"github.com/nibankougen/LowPolyWorld/api/internal/middleware"
	"github.com/nibankougen/LowPolyWorld/api/internal/response"
)

// generateConsentToken generates a cryptographically random 32-byte hex token.
func generateConsentToken() string {
	b := make([]byte, 32)
	_, _ = rand.Read(b)
	return hex.EncodeToString(b)
}

// hashEmail returns the lowercase hex-encoded SHA-256 of the lowercased trimmed email.
func hashEmail(email string) string {
	normalized := strings.ToLower(strings.TrimSpace(email))
	sum := sha256.Sum256([]byte(normalized))
	return hex.EncodeToString(sum[:])
}

// initiateParentalConsent creates or replaces the consent record for a user and sends
// the verification email. Safe to call from a goroutine.
func (h *Handler) initiateParentalConsent(ctx context.Context, userID, parentalEmail string) {
	token := generateConsentToken()
	emailHash := hashEmail(parentalEmail)

	_, err := h.DB.Exec(ctx,
		`INSERT INTO parental_consents (user_id, token, parental_email_hash)
		 VALUES ($1, $2, $3)
		 ON CONFLICT (user_id) DO UPDATE
		   SET token              = EXCLUDED.token,
		       parental_email_hash = EXCLUDED.parental_email_hash,
		       email_sent_at      = now(),
		       reminder_sent_at   = NULL,
		       verified_at        = NULL,
		       expired_at         = NULL`,
		userID, token, emailHash,
	)
	if err != nil {
		h.Logger.Error("create parental consent record", "user_id", userID, "error", err)
		return
	}

	_, _ = h.DB.Exec(ctx,
		`UPDATE active_users SET parental_email = $1 WHERE user_id = $2`,
		parentalEmail, userID,
	)

	if h.EmailSender != nil {
		if err := h.EmailSender.SendParentalConsentRequest(ctx, parentalEmail, token, h.Cfg.AppBaseURL); err != nil {
			h.Logger.Error("send parental consent email", "user_id", userID, "error", err)
		}
	}
}

// RequestParentalConsent handles POST /auth/parental-consent/request.
// Allows a young_teen user to submit a parent's email address, triggering the consent email.
func (h *Handler) RequestParentalConsent(w http.ResponseWriter, r *http.Request) {
	userID := middleware.UserIDFromContext(r.Context())

	// Only young_teen users need parental consent.
	ageGroup := middleware.AgeGroupFromContext(r.Context())
	if ageGroup != "young_teen" {
		response.Error(w, r, http.StatusBadRequest, "not_required",
			"parental consent is not required for this account")
		return
	}

	// Already verified — idempotent success.
	if middleware.ParentalConsentVerifiedFromContext(r.Context()) {
		w.WriteHeader(http.StatusNoContent)
		return
	}

	var req struct {
		ParentalEmail string `json:"parental_email"`
	}
	if err := json.NewDecoder(r.Body).Decode(&req); err != nil || req.ParentalEmail == "" {
		response.Error(w, r, http.StatusBadRequest, "validation_error", "parental_email is required")
		return
	}
	if !strings.Contains(req.ParentalEmail, "@") || len(req.ParentalEmail) > 254 {
		response.Error(w, r, http.StatusBadRequest, "validation_error", "invalid email address")
		return
	}

	h.initiateParentalConsent(r.Context(), userID, req.ParentalEmail)
	w.WriteHeader(http.StatusNoContent)
}

// runParentalConsentReminder sends a reminder email to parents who have not verified after 7 days.
// Returns the number of reminders sent.
func (h *Handler) runParentalConsentReminder(ctx context.Context) (int, error) {
	rows, err := h.Pool.Query(ctx,
		`SELECT pc.user_id::text, au.parental_email, pc.token
		 FROM parental_consents pc
		 JOIN active_users au ON au.user_id = pc.user_id
		 WHERE pc.verified_at IS NULL
		   AND pc.expired_at IS NULL
		   AND pc.reminder_sent_at IS NULL
		   AND pc.email_sent_at < now() - interval '7 days'
		   AND au.parental_email IS NOT NULL`,
	)
	if err != nil {
		return 0, err
	}
	defer rows.Close()

	type row struct {
		userID string
		email  string
		token  string
	}
	var pending []row
	for rows.Next() {
		var r row
		if err := rows.Scan(&r.userID, &r.email, &r.token); err != nil {
			continue
		}
		pending = append(pending, r)
	}
	if rows.Err() != nil {
		return 0, rows.Err()
	}

	count := 0
	for _, p := range pending {
		if h.EmailSender != nil {
			if err := h.EmailSender.SendParentalConsentReminder(ctx, p.email, p.token, h.Cfg.AppBaseURL); err != nil {
				h.Logger.Error("send parental consent reminder", "user_id", p.userID, "error", err)
				continue
			}
		}
		_, _ = h.Pool.Exec(ctx,
			`UPDATE parental_consents SET reminder_sent_at = now() WHERE token = $1`, p.token)
		count++
	}
	return count, nil
}

// runParentalConsentTimeout expires requests older than 14 days and soft-deletes the user.
// Returns the number of accounts expired.
func (h *Handler) runParentalConsentTimeout(ctx context.Context) (int, error) {
	rows, err := h.Pool.Query(ctx,
		`SELECT user_id::text
		 FROM parental_consents
		 WHERE verified_at IS NULL
		   AND expired_at IS NULL
		   AND email_sent_at < now() - interval '14 days'`,
	)
	if err != nil {
		return 0, err
	}
	defer rows.Close()

	var userIDs []string
	for rows.Next() {
		var uid string
		if err := rows.Scan(&uid); err != nil {
			continue
		}
		userIDs = append(userIDs, uid)
	}
	if rows.Err() != nil {
		return 0, rows.Err()
	}

	count := 0
	for _, uid := range userIDs {
		_, _ = h.Pool.Exec(ctx,
			`UPDATE parental_consents SET expired_at = now()
			 WHERE user_id = $1 AND verified_at IS NULL AND expired_at IS NULL`,
			uid,
		)
		_, _ = h.Pool.Exec(ctx,
			`UPDATE active_users SET deleted_at = now(), parental_email = NULL WHERE user_id = $1`,
			uid,
		)
		count++
	}
	return count, nil
}

// VerifyParentalConsent handles GET /auth/parental-consent/verify?token=...
// Parents click this link from the consent email. No authentication required.
func (h *Handler) VerifyParentalConsent(w http.ResponseWriter, r *http.Request) {
	token := r.URL.Query().Get("token")
	if token == "" {
		response.Error(w, r, http.StatusBadRequest, "validation_error", "token is required")
		return
	}

	var userID string
	var expiredAt *string
	var verifiedAt *string
	err := h.DB.QueryRow(r.Context(),
		`SELECT user_id::text, expired_at::text, verified_at::text
		 FROM parental_consents WHERE token = $1`,
		token,
	).Scan(&userID, &expiredAt, &verifiedAt)
	if err != nil {
		response.Error(w, r, http.StatusNotFound, "not_found", "invalid or expired token")
		return
	}

	if expiredAt != nil {
		response.Error(w, r, http.StatusGone, "token_expired",
			"this consent request has expired; please ask your child to request a new one")
		return
	}

	if verifiedAt != nil {
		// Idempotent — already verified.
		response.JSON(w, http.StatusOK, map[string]string{"status": "already_verified"})
		return
	}

	_, err = h.DB.Exec(r.Context(),
		`UPDATE parental_consents SET verified_at = now() WHERE token = $1`, token)
	if err != nil {
		h.Logger.Error("verify parental consent", "error", err)
		response.InternalError(w, r, h.Cfg.IsProduction())
		return
	}

	_, _ = h.DB.Exec(r.Context(),
		`UPDATE active_users
		 SET parental_consent_verified_at = now(), parental_email = NULL
		 WHERE user_id = $1`,
		userID,
	)

	response.JSON(w, http.StatusOK, map[string]string{"status": "verified"})
}
