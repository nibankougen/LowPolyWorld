-- Phase 5B: trust level system, violation reports, admin audit logs

-- Record how many other members were in the room when this user joined.
-- Used for trust point calculation on leave.
ALTER TABLE room_members ADD COLUMN join_member_count INTEGER NOT NULL DEFAULT 0;

-- Audit trail for every trust level change (automatic promotions and manual admin overrides).
CREATE TABLE trust_level_logs (
    id           BIGSERIAL PRIMARY KEY,
    user_id      UUID NOT NULL REFERENCES users(id) ON DELETE NO ACTION,
    before_level VARCHAR(20) NOT NULL,
    after_level  VARCHAR(20) NOT NULL,
    reason       TEXT NOT NULL,
    admin_id     UUID REFERENCES users(id) ON DELETE NO ACTION,
    created_at   TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX idx_trust_level_logs_user_id ON trust_level_logs(user_id);
CREATE INDEX idx_trust_level_logs_created_at ON trust_level_logs(created_at DESC);

-- One row per public-room exit that contributed trust points.
CREATE TABLE room_trust_events (
    id               BIGSERIAL PRIMARY KEY,
    user_id          UUID NOT NULL REFERENCES users(id) ON DELETE NO ACTION,
    room_id          UUID NOT NULL,
    join_count       INTEGER NOT NULL,
    exit_count       INTEGER NOT NULL,
    duration_minutes INTEGER NOT NULL,
    points_awarded   FLOAT NOT NULL,
    created_at       TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX idx_room_trust_events_user_id ON room_trust_events(user_id);

-- User-submitted violation reports.
CREATE TABLE user_violation_reports (
    id                UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    reporter_id       UUID NOT NULL REFERENCES users(id) ON DELETE NO ACTION,
    target_id         UUID NOT NULL REFERENCES users(id) ON DELETE NO ACTION,
    reason            VARCHAR(50) NOT NULL,
    detail            TEXT NOT NULL DEFAULT '',
    is_auto_generated BOOLEAN NOT NULL DEFAULT FALSE,
    created_at        TIMESTAMPTZ NOT NULL DEFAULT now()
);
-- Index for restriction-threshold queries (count unique reporters per target).
CREATE INDEX idx_violation_reports_target_id ON user_violation_reports(target_id);
-- Index to deduplicate per (reporter, target) pair for threshold counting.
CREATE INDEX idx_violation_reports_reporter_target ON user_violation_reports(reporter_id, target_id);

-- Append-only log of every admin operation (insert-only; no UPDATE/DELETE on this table).
CREATE TABLE admin_audit_logs (
    id              BIGSERIAL PRIMARY KEY,
    admin_id        TEXT NOT NULL,
    action          VARCHAR NOT NULL,
    target_type     VARCHAR NOT NULL,
    target_id       TEXT NOT NULL,
    before_value    JSONB,
    after_value     JSONB,
    notes           TEXT,
    response_status SMALLINT NOT NULL DEFAULT 200,
    error_code      VARCHAR,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX idx_admin_audit_logs_admin_id ON admin_audit_logs(admin_id);
CREATE INDEX idx_admin_audit_logs_created_at ON admin_audit_logs(created_at DESC);
