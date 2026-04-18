CREATE TABLE tag_ban_list (
    tag_normalized TEXT PRIMARY KEY,
    created_at     TIMESTAMPTZ NOT NULL DEFAULT now()
);
