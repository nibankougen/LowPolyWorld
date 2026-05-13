-- subscription_purchases: records each IAP subscription transaction and serves as
-- the audit trail for tier changes. active_users.subscription_tier is the live state;
-- this table is the source of truth for billing history.
CREATE TABLE subscription_purchases (
    id                      UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id                 UUID        NOT NULL REFERENCES users(id) ON DELETE NO ACTION,
    platform                VARCHAR(10) NOT NULL CHECK (platform IN ('ios', 'android')),
    platform_transaction_id TEXT        NOT NULL,
    product_id              TEXT        NOT NULL,
    subscription_tier       VARCHAR(20) NOT NULL DEFAULT 'premium',
    started_at              TIMESTAMPTZ NOT NULL DEFAULT now(),
    expires_at              TIMESTAMPTZ NOT NULL,
    cancelled_at            TIMESTAMPTZ,
    created_at              TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE (platform, platform_transaction_id)
);

CREATE INDEX idx_subscription_purchases_user_id ON subscription_purchases(user_id);
CREATE INDEX idx_subscription_purchases_expires_at ON subscription_purchases(expires_at)
    WHERE cancelled_at IS NULL;
