-- Phase 9 (続き): ルーム状態管理 / 招待リンク / ワールド非表示 / 通知

-- rooms: 状態カラム追加
ALTER TABLE rooms ADD COLUMN state VARCHAR(10) NOT NULL DEFAULT 'open'
    CHECK (state IN ('open', 'locked', 'closed'));
CREATE INDEX idx_rooms_state ON rooms(state);

-- 招待リンク: ルームごとに1本のみ有効（既存を削除して再発行）
CREATE TABLE invite_links (
    token      TEXT PRIMARY KEY DEFAULT encode(gen_random_bytes(16), 'hex'),
    room_id    UUID    NOT NULL REFERENCES rooms(id) ON DELETE CASCADE,
    created_by UUID    NOT NULL REFERENCES users(id) ON DELETE NO ACTION,
    max_uses   INTEGER NOT NULL,
    use_count  INTEGER NOT NULL DEFAULT 0,
    expires_at TIMESTAMPTZ NOT NULL DEFAULT now() + interval '7 days',
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX idx_invite_links_room_id ON invite_links(room_id);

-- ワールド非表示
CREATE TABLE hidden_worlds (
    user_id    UUID NOT NULL REFERENCES users(id) ON DELETE NO ACTION,
    world_id   UUID NOT NULL REFERENCES worlds(id) ON DELETE CASCADE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (user_id, world_id)
);
CREATE INDEX idx_hidden_worlds_user_id ON hidden_worlds(user_id);

-- 通知 (friend_request / world_published / product_released / coin_expiry_30d / coin_expiry_7d)
CREATE TABLE notifications (
    id         UUID    PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id    UUID    NOT NULL REFERENCES users(id) ON DELETE NO ACTION,
    type       VARCHAR(50) NOT NULL,
    body       TEXT    NOT NULL DEFAULT '',
    ref_id     TEXT,
    is_read    BOOLEAN NOT NULL DEFAULT FALSE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX idx_notifications_user_id ON notifications(user_id, created_at DESC);
