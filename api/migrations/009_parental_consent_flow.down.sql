DROP TABLE IF EXISTS parental_consents;

CREATE TABLE parental_consents (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id         UUID NOT NULL REFERENCES users(id) ON DELETE NO ACTION,
    verified_at     TIMESTAMPTZ NOT NULL,
    consent_version VARCHAR(20) NOT NULL
);
