-- Phase 5B: admin_users table for management panel authentication.
CREATE TABLE admin_users (
    id           UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    email        VARCHAR(254) NOT NULL UNIQUE,
    password_hash TEXT NOT NULL,           -- bcrypt hash
    role         VARCHAR(20)  NOT NULL DEFAULT 'moderator'
                     CHECK (role IN ('moderator', 'admin', 'super_admin')),
    is_active    BOOLEAN NOT NULL DEFAULT TRUE,
    created_at   TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at   TIMESTAMPTZ NOT NULL DEFAULT now()
);

-- Sessions for admin JWT (separate from user tokens).
CREATE TABLE admin_sessions (
    id           UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    admin_id     UUID NOT NULL REFERENCES admin_users(id) ON DELETE CASCADE,
    token_hash   TEXT NOT NULL UNIQUE,     -- SHA-256 hex of the bearer token
    expires_at   TIMESTAMPTZ NOT NULL,
    created_at   TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX idx_admin_sessions_admin_id ON admin_sessions(admin_id);
CREATE INDEX idx_admin_sessions_expires_at ON admin_sessions(expires_at);
