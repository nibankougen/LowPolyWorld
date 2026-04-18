CREATE EXTENSION IF NOT EXISTS "pgcrypto";
CREATE EXTENSION IF NOT EXISTS "pg_trgm";

CREATE TABLE users (
    id         UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE active_users (
    user_id                      UUID PRIMARY KEY REFERENCES users(id) ON DELETE NO ACTION,
    display_name                 TEXT,
    name                         TEXT,
    email                        TEXT,
    google_sub                   TEXT UNIQUE,
    apple_sub                    TEXT UNIQUE,
    locale                       VARCHAR(10) NOT NULL DEFAULT 'ja-JP',
    age_group                    VARCHAR(20) NOT NULL DEFAULT 'adult' CHECK (age_group IN ('young_teen', 'teen', 'adult')),
    age_verified_at              TIMESTAMPTZ NOT NULL DEFAULT now(),
    parental_consent_verified_at TIMESTAMPTZ,
    parental_email               TEXT,
    token_revision               INTEGER NOT NULL DEFAULT 0,
    name_setup_required          BOOLEAN NOT NULL DEFAULT TRUE,
    trust_level                  VARCHAR(20) NOT NULL DEFAULT 'visitor',
    trust_points                 FLOAT NOT NULL DEFAULT 0,
    trust_level_locked           BOOLEAN NOT NULL DEFAULT FALSE,
    is_restricted                BOOLEAN NOT NULL DEFAULT FALSE,
    deleted_at                   TIMESTAMPTZ,
    subscription_tier            VARCHAR(20) NOT NULL DEFAULT 'free',
    subscription_expires_at      TIMESTAMPTZ,
    last_name_change_at          TIMESTAMPTZ,
    vivox_id                     UUID NOT NULL DEFAULT gen_random_uuid(),
    last_seen_security_notice_id TEXT,
    language                     VARCHAR(10) NOT NULL DEFAULT 'ja',
    created_at                   TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at                   TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE refresh_tokens (
    id          BIGSERIAL PRIMARY KEY,
    user_id     UUID NOT NULL REFERENCES users(id) ON DELETE NO ACTION,
    token_hash  TEXT NOT NULL UNIQUE,
    expires_at  TIMESTAMPTZ NOT NULL,
    revoked_at  TIMESTAMPTZ,
    device_name TEXT,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX idx_refresh_tokens_user_id ON refresh_tokens(user_id);

CREATE TABLE parental_consents (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id         UUID NOT NULL REFERENCES users(id) ON DELETE NO ACTION,
    verified_at     TIMESTAMPTZ NOT NULL,
    consent_version VARCHAR(20) NOT NULL
);

CREATE TABLE worlds (
    id                   UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    owner_user_id        UUID NOT NULL REFERENCES users(id) ON DELETE NO ACTION,
    name                 TEXT NOT NULL,
    description          TEXT NOT NULL DEFAULT '',
    thumbnail_hash       TEXT,
    glb_hash             TEXT NOT NULL DEFAULT '',
    is_public            BOOLEAN NOT NULL DEFAULT TRUE,
    max_players          INTEGER NOT NULL DEFAULT 6,
    likes_count          INTEGER NOT NULL DEFAULT 0,
    ambient_sound_id     VARCHAR(50) NOT NULL DEFAULT 'none',
    ambient_sound_volume FLOAT NOT NULL DEFAULT 1.0,
    created_at           TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at           TIMESTAMPTZ NOT NULL DEFAULT now()
);

-- Partial unique index: @name uniqueness only for non-deleted users (deleted rows have name = NULL)
CREATE UNIQUE INDEX active_users_name_unique ON active_users(name) WHERE name IS NOT NULL;

CREATE INDEX idx_worlds_likes_count ON worlds(likes_count DESC);
CREATE INDEX idx_worlds_created_at ON worlds(created_at DESC);
CREATE INDEX idx_worlds_owner_user_id ON worlds(owner_user_id);
CREATE INDEX idx_worlds_is_public ON worlds(is_public);
CREATE INDEX idx_worlds_name_trgm ON worlds USING GIN (name gin_trgm_ops);

CREATE TABLE world_likes (
    world_id   UUID NOT NULL REFERENCES worlds(id) ON DELETE CASCADE,
    user_id    UUID NOT NULL REFERENCES users(id) ON DELETE NO ACTION,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (world_id, user_id)
);

CREATE TABLE world_tags (
    id             UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    world_id       UUID NOT NULL REFERENCES worlds(id) ON DELETE CASCADE,
    tag_text       TEXT NOT NULL,
    tag_normalized TEXT NOT NULL
);

CREATE INDEX idx_world_tags_world_id ON world_tags(world_id);
CREATE INDEX idx_world_tags_normalized ON world_tags(tag_normalized);

CREATE TABLE rooms (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    world_id        UUID NOT NULL REFERENCES worlds(id) ON DELETE CASCADE,
    creator_user_id UUID NOT NULL REFERENCES users(id) ON DELETE NO ACTION,
    room_type       VARCHAR(20) NOT NULL DEFAULT 'public' CHECK (room_type IN ('public', 'friends_only', 'followers_only', 'invite_only')),
    language        VARCHAR(10) NOT NULL DEFAULT 'ja',
    max_players     INTEGER NOT NULL DEFAULT 6,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX idx_rooms_world_id ON rooms(world_id);

CREATE TABLE room_members (
    room_id   UUID NOT NULL REFERENCES rooms(id) ON DELETE CASCADE,
    user_id   UUID NOT NULL REFERENCES users(id) ON DELETE NO ACTION,
    joined_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (room_id, user_id)
);

CREATE INDEX idx_room_members_user_id ON room_members(user_id);

CREATE TABLE avatars (
    id                UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id           UUID NOT NULL REFERENCES users(id) ON DELETE NO ACTION,
    name              TEXT NOT NULL DEFAULT '',
    vrm_hash          TEXT NOT NULL,
    texture_hash      TEXT,
    moderation_status VARCHAR(20) NOT NULL DEFAULT 'pending' CHECK (moderation_status IN ('pending', 'approved', 'rejected')),
    created_at        TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX idx_avatars_user_id ON avatars(user_id);

CREATE TABLE accessories (
    id           UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id      UUID NOT NULL REFERENCES users(id) ON DELETE NO ACTION,
    name         TEXT NOT NULL DEFAULT '',
    glb_hash     TEXT NOT NULL,
    texture_hash TEXT,
    created_at   TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX idx_accessories_user_id ON accessories(user_id);
