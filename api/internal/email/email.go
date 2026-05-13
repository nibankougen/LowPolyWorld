package email

import (
	"context"
	"fmt"
	"log/slog"
)

// Sender is the interface for sending transactional emails.
type Sender interface {
	SendParentalConsentRequest(ctx context.Context, to, token, appBaseURL string) error
	SendParentalConsentReminder(ctx context.Context, to, token, appBaseURL string) error
}

// NoOp logs the email details but does not send anything.
// Used in development when RESEND_API_KEY is not configured.
type NoOp struct {
	Logger *slog.Logger
}

func (n *NoOp) SendParentalConsentRequest(_ context.Context, to, token, appBaseURL string) error {
	n.Logger.Info("parental consent email (no-op)",
		"to", to,
		"verify_url", fmt.Sprintf("%s/auth/parental-consent/verify?token=%s", appBaseURL, token),
	)
	return nil
}

func (n *NoOp) SendParentalConsentReminder(_ context.Context, to, token, appBaseURL string) error {
	n.Logger.Info("parental consent reminder (no-op)",
		"to", to,
		"verify_url", fmt.Sprintf("%s/auth/parental-consent/verify?token=%s", appBaseURL, token),
	)
	return nil
}
