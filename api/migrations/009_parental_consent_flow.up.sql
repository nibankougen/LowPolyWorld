-- 009: parental_consent_flow
-- Rebuild parental_consents as a full audit-trail workflow table.
-- The original table (created in 001) was a minimal "consent given" log;
-- this replaces it with a request table that tracks the full email-verify cycle.

DROP TABLE parental_consents;

CREATE TABLE parental_consents (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id             UUID NOT NULL UNIQUE REFERENCES users(id) ON DELETE NO ACTION,
    token               TEXT NOT NULL UNIQUE,
    parental_email_hash TEXT NOT NULL,
    email_sent_at       TIMESTAMPTZ NOT NULL DEFAULT now(),
    reminder_sent_at    TIMESTAMPTZ,
    verified_at         TIMESTAMPTZ,
    expired_at          TIMESTAMPTZ,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX idx_parental_consents_user_id ON parental_consents(user_id);
