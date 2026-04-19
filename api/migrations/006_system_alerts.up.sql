-- ============================================================
-- Phase 5B: System alerts table for security monitoring
-- ============================================================

CREATE TABLE system_alerts (
    id          BIGSERIAL PRIMARY KEY,
    alert_type  VARCHAR(60) NOT NULL,
    severity    VARCHAR(10) NOT NULL DEFAULT 'warning'
                    CHECK (severity IN ('info', 'warning', 'critical')),
    message     TEXT NOT NULL,
    details     JSONB,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT now(),
    resolved_at TIMESTAMPTZ
);

CREATE INDEX idx_system_alerts_created_at ON system_alerts(created_at DESC);
CREATE INDEX idx_system_alerts_type_created ON system_alerts(alert_type, created_at DESC);
CREATE INDEX idx_system_alerts_unresolved ON system_alerts(created_at DESC) WHERE resolved_at IS NULL;
