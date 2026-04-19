-- ============================================================
-- Phase 8: Shop & Coin system
-- ============================================================

-- ----------------------------------------------------------
-- Creators
-- ----------------------------------------------------------
CREATE TABLE creators (
    id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id     UUID NOT NULL REFERENCES users(id) ON DELETE NO ACTION,
    display_name TEXT NOT NULL DEFAULT '',
    bio          TEXT NOT NULL DEFAULT '',
    created_at  TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at  TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE UNIQUE INDEX idx_creators_user_id ON creators(user_id);

-- ----------------------------------------------------------
-- Products
-- ----------------------------------------------------------
CREATE TABLE products (
    id                      UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    creator_id              UUID NOT NULL REFERENCES creators(id) ON DELETE NO ACTION,
    name                    TEXT NOT NULL,
    description             TEXT NOT NULL DEFAULT '',
    category                VARCHAR(20) NOT NULL CHECK (category IN ('avatar', 'accessory', 'world_object', 'stamp')),
    price_coins             INTEGER NOT NULL CHECK (price_coins >= 0),
    asset_hash              TEXT NOT NULL,
    thumbnail_hash          TEXT,
    texture_cost            SMALLINT,            -- world_object only: texture budget points (nullable for other categories)
    collider_size_category  VARCHAR(10) CHECK (collider_size_category IN ('small', 'medium', 'large')), -- world_object only
    edit_allowed            BOOLEAN NOT NULL DEFAULT TRUE,
    is_published            BOOLEAN NOT NULL DEFAULT FALSE,
    likes_count             INTEGER NOT NULL DEFAULT 0,
    recent_purchase_count   INTEGER NOT NULL DEFAULT 0,
    tags                    TEXT[] NOT NULL DEFAULT '{}',
    created_at              TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at              TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX idx_products_creator_id ON products(creator_id);
CREATE INDEX idx_products_category ON products(category);
CREATE INDEX idx_products_created_at ON products(created_at DESC);
CREATE INDEX idx_products_likes_count ON products(likes_count DESC);
CREATE INDEX idx_products_popularity ON products(recent_purchase_count DESC) WHERE recent_purchase_count >= 3;
CREATE INDEX idx_products_name_trgm ON products USING GIN (name gin_trgm_ops);

-- ----------------------------------------------------------
-- Product likes
-- ----------------------------------------------------------
CREATE TABLE product_likes (
    product_id UUID NOT NULL REFERENCES products(id) ON DELETE CASCADE,
    user_id    UUID NOT NULL REFERENCES users(id) ON DELETE NO ACTION,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (product_id, user_id)
);

CREATE INDEX idx_product_likes_user_id ON product_likes(user_id);

-- ----------------------------------------------------------
-- Platform fee rates
-- ----------------------------------------------------------
CREATE TABLE platform_fee_rates (
    id         BIGSERIAL PRIMARY KEY,
    platform   VARCHAR(10) NOT NULL CHECK (platform IN ('ios', 'android')),
    fee_rate   NUMERIC(5,4) NOT NULL,
    start_date DATE NOT NULL,
    end_date   DATE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX idx_platform_fee_rates_platform_start ON platform_fee_rates(platform, start_date DESC);

-- ----------------------------------------------------------
-- Coin purchases
-- ----------------------------------------------------------
CREATE TABLE coin_purchases (
    id                         UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id                    UUID NOT NULL REFERENCES users(id) ON DELETE NO ACTION,
    platform                   VARCHAR(10) NOT NULL CHECK (platform IN ('ios', 'android')),
    platform_transaction_id    TEXT NOT NULL UNIQUE,
    storefront_country         VARCHAR(10) NOT NULL,
    purchase_timestamp         TIMESTAMPTZ NOT NULL,
    valid_until                TIMESTAMPTZ NOT NULL,
    coins_amount               INTEGER NOT NULL CHECK (coins_amount > 0),
    local_amount               NUMERIC(12,2) NOT NULL,
    local_currency             VARCHAR(3) NOT NULL,
    fx_rate_to_jpy             NUMERIC(12,4) NOT NULL,
    converted_jpy_amount       NUMERIC(12,2) NOT NULL,
    platform_fee_rate_id       BIGINT REFERENCES platform_fee_rates(id),  -- NULL when no rate was configured at purchase time
    estimated_net_revenue_jpy  NUMERIC(12,2) NOT NULL,
    created_at                 TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX idx_coin_purchases_user_id ON coin_purchases(user_id);
CREATE INDEX idx_coin_purchases_valid_until ON coin_purchases(valid_until);
CREATE INDEX idx_coin_purchases_user_valid ON coin_purchases(user_id, valid_until);

-- ----------------------------------------------------------
-- User average coin value (per-user aggregate)
-- ----------------------------------------------------------
CREATE TABLE user_coin_values (
    user_id             UUID PRIMARY KEY REFERENCES users(id) ON DELETE NO ACTION,
    avg_coin_value_jpy  NUMERIC(12,4) NOT NULL DEFAULT 0,
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT now()
);

-- ----------------------------------------------------------
-- Coin transactions (consumption ledger)
-- ----------------------------------------------------------
CREATE TABLE coin_transactions (
    id                               UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    idempotency_key                  UUID UNIQUE,
    buyer_id                         UUID NOT NULL REFERENCES users(id) ON DELETE NO ACTION,
    product_id                       UUID NOT NULL REFERENCES products(id) ON DELETE NO ACTION,
    creator_id                       UUID NOT NULL REFERENCES creators(id) ON DELETE NO ACTION,
    coins_spent                      INTEGER NOT NULL CHECK (coins_spent > 0),
    avg_coin_value_jpy_at_time       NUMERIC(12,4) NOT NULL,
    estimated_consumption_value_jpy  NUMERIC(12,2) NOT NULL,
    final_consumption_value_jpy      NUMERIC(12,2),  -- filled by monthly batch
    created_at                       TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX idx_coin_transactions_buyer_id ON coin_transactions(buyer_id);
CREATE INDEX idx_coin_transactions_product_id ON coin_transactions(product_id);
CREATE INDEX idx_coin_transactions_creator_id ON coin_transactions(creator_id);
CREATE INDEX idx_coin_transactions_created_at ON coin_transactions(created_at);

-- ----------------------------------------------------------
-- User purchased products
-- ----------------------------------------------------------
CREATE TABLE user_products (
    id           UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id      UUID NOT NULL REFERENCES users(id) ON DELETE NO ACTION,
    product_id   UUID NOT NULL REFERENCES products(id) ON DELETE NO ACTION,
    transaction_id UUID NOT NULL REFERENCES coin_transactions(id) ON DELETE NO ACTION,
    purchased_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE (user_id, product_id)
);

CREATE INDEX idx_user_products_user_id ON user_products(user_id);

-- ----------------------------------------------------------
-- Coin purchase cancellations (refund ledger)
-- ----------------------------------------------------------
CREATE TABLE coin_purchase_cancellations (
    id                      BIGSERIAL PRIMARY KEY,
    coin_purchase_id        UUID NOT NULL REFERENCES coin_purchases(id) ON DELETE NO ACTION,
    cancellation_type       VARCHAR(20) NOT NULL CHECK (cancellation_type IN ('platform_refund', 'manual_admin')),
    platform                VARCHAR(10) CHECK (platform IN ('ios', 'android')),
    platform_transaction_id TEXT UNIQUE,          -- NULL for manual_admin
    coins_deducted          INTEGER NOT NULL DEFAULT 0,
    balance_before          INTEGER NOT NULL,
    balance_after           INTEGER NOT NULL,
    cancelled_at            TIMESTAMPTZ NOT NULL DEFAULT now(),
    admin_id                UUID REFERENCES admin_users(id) ON DELETE NO ACTION,
    notes                   TEXT
);

CREATE INDEX idx_coin_purchase_cancellations_purchase_id ON coin_purchase_cancellations(coin_purchase_id);
CREATE INDEX idx_coin_purchase_cancellations_admin_id ON coin_purchase_cancellations(admin_id);

-- ----------------------------------------------------------
-- Webhook events log
-- ----------------------------------------------------------
CREATE TABLE webhook_events (
    id                      BIGSERIAL PRIMARY KEY,
    source                  VARCHAR(10) NOT NULL CHECK (source IN ('apple', 'google')),
    event_type              TEXT NOT NULL,
    external_id             TEXT NOT NULL,
    raw_payload             TEXT NOT NULL,
    received_at             TIMESTAMPTZ NOT NULL DEFAULT now(),
    processing_status       VARCHAR(20) NOT NULL DEFAULT 'pending'
                                CHECK (processing_status IN ('pending', 'processed', 'failed', 'ignored', 'permanently_failed')),
    processed_at            TIMESTAMPTZ,
    error_message           TEXT,
    retry_count             SMALLINT NOT NULL DEFAULT 0,
    related_cancellation_id BIGINT REFERENCES coin_purchase_cancellations(id) ON DELETE NO ACTION
);

CREATE INDEX idx_webhook_events_external_id ON webhook_events(external_id);
CREATE INDEX idx_webhook_events_status ON webhook_events(processing_status) WHERE processing_status NOT IN ('processed', 'ignored');

-- ----------------------------------------------------------
-- Coin balance snapshots (daily audit snapshots)
-- ----------------------------------------------------------
CREATE TABLE coin_balance_snapshots (
    id             BIGSERIAL PRIMARY KEY,
    user_id        UUID NOT NULL REFERENCES users(id) ON DELETE NO ACTION,
    snapshot_date  DATE NOT NULL,
    balance        INTEGER NOT NULL,
    change_reason  VARCHAR(20) NOT NULL CHECK (change_reason IN ('purchase', 'consume', 'expire', 'cancel')),
    created_at     TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE (user_id, snapshot_date)
);

CREATE INDEX idx_coin_balance_snapshots_user_date ON coin_balance_snapshots(user_id, snapshot_date DESC);

-- ----------------------------------------------------------
-- Coin expiry notifications (dedup table)
-- ----------------------------------------------------------
CREATE TABLE coin_expiry_notifications (
    id          BIGSERIAL PRIMARY KEY,
    user_id     UUID NOT NULL REFERENCES users(id) ON DELETE NO ACTION,
    valid_until TIMESTAMPTZ NOT NULL,
    type        VARCHAR(10) NOT NULL CHECK (type IN ('30day', '7day')),
    notified_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE (user_id, valid_until, type)
);

-- ----------------------------------------------------------
-- Settled revenue (confirmed platform reports)
-- ----------------------------------------------------------
CREATE TABLE settled_revenues (
    id                      BIGSERIAL PRIMARY KEY,
    period                  VARCHAR(7) NOT NULL,   -- YYYY-MM
    country                 VARCHAR(10) NOT NULL,
    settled_net_revenue_jpy NUMERIC(14,2) NOT NULL,
    refund_adjustment_jpy   NUMERIC(14,2) NOT NULL DEFAULT 0,
    registered_by           UUID REFERENCES admin_users(id) ON DELETE NO ACTION,
    created_at              TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE (period, country)
);

-- ----------------------------------------------------------
-- Revenue adjustment factors
-- ----------------------------------------------------------
CREATE TABLE revenue_adjustment_factors (
    id                BIGSERIAL PRIMARY KEY,
    period            VARCHAR(7) NOT NULL,
    country           VARCHAR(10) NOT NULL,
    adjustment_factor NUMERIC(10,6) NOT NULL,
    calculated_at     TIMESTAMPTZ NOT NULL DEFAULT now(),
    override_reason   TEXT,                        -- non-NULL only for manual super_admin overrides
    overridden_by     UUID REFERENCES admin_users(id) ON DELETE NO ACTION,
    UNIQUE (period, country)
);

-- ----------------------------------------------------------
-- Monthly revenue snapshots
-- ----------------------------------------------------------
CREATE TABLE monthly_revenue_snapshots (
    id                                  BIGSERIAL PRIMARY KEY,
    period                              VARCHAR(7) NOT NULL,
    country                             VARCHAR(10) NOT NULL,
    total_coins_purchased               BIGINT NOT NULL DEFAULT 0,
    total_coins_consumed                BIGINT NOT NULL DEFAULT 0,
    total_coins_expired                 BIGINT NOT NULL DEFAULT 0,
    total_estimated_purchase_value_jpy  NUMERIC(14,2) NOT NULL DEFAULT 0,
    total_estimated_consumption_value_jpy NUMERIC(14,2) NOT NULL DEFAULT 0,
    settled_net_revenue_jpy             NUMERIC(14,2) NOT NULL DEFAULT 0,
    adjustment_factor                   NUMERIC(10,6) NOT NULL DEFAULT 1,
    avg_coin_value_jpy_snapshot         NUMERIC(12,4),
    transaction_count_purchase          INTEGER NOT NULL DEFAULT 0,
    transaction_count_consumed          INTEGER NOT NULL DEFAULT 0,
    calculated_at                       TIMESTAMPTZ NOT NULL DEFAULT now(),
    UNIQUE (period, country)
);

-- ----------------------------------------------------------
-- Creator revenue share contracts
-- ----------------------------------------------------------
CREATE TABLE creator_revenue_contracts (
    id                 UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    creator_id         UUID NOT NULL REFERENCES creators(id) ON DELETE NO ACTION,
    revenue_share_rate NUMERIC(5,4) NOT NULL CHECK (revenue_share_rate BETWEEN 0 AND 1),
    effective_start    DATE NOT NULL,
    effective_end      DATE,
    created_at         TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX idx_creator_revenue_contracts_creator ON creator_revenue_contracts(creator_id, effective_start DESC);

-- ----------------------------------------------------------
-- Tax rates (for future direct sales / creator withholding)
-- ----------------------------------------------------------
CREATE TABLE tax_rates (
    id         BIGSERIAL PRIMARY KEY,
    country    VARCHAR(10) NOT NULL,
    tax_type   VARCHAR(30) NOT NULL,
    tax_rate   NUMERIC(5,4) NOT NULL,
    valid_from DATE NOT NULL,
    valid_to   DATE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
