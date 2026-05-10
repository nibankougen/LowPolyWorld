-- Phase 9: social graph — hidden users, follows, friend requests

CREATE TABLE hidden_users (
    user_id    UUID NOT NULL REFERENCES users(id) ON DELETE NO ACTION,
    target_id  UUID NOT NULL REFERENCES users(id) ON DELETE NO ACTION,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (user_id, target_id)
);
CREATE INDEX idx_hidden_users_user_id ON hidden_users(user_id);

CREATE TABLE follows (
    follower_id UUID NOT NULL REFERENCES users(id) ON DELETE NO ACTION,
    followee_id UUID NOT NULL REFERENCES users(id) ON DELETE NO ACTION,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (follower_id, followee_id)
);
CREATE INDEX idx_follows_follower_id ON follows(follower_id);
CREATE INDEX idx_follows_followee_id ON follows(followee_id);

-- Each friendship is represented as two directional rows both with status='accepted'.
-- Pending/rejected requests are represented as a single row from requester → addressee.
CREATE TABLE friend_requests (
    requester_id UUID        NOT NULL REFERENCES users(id) ON DELETE NO ACTION,
    addressee_id UUID        NOT NULL REFERENCES users(id) ON DELETE NO ACTION,
    status       VARCHAR(20) NOT NULL DEFAULT 'pending'
                             CHECK (status IN ('pending', 'accepted', 'rejected')),
    created_at   TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at   TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (requester_id, addressee_id)
);
CREATE INDEX idx_friend_requests_requester ON friend_requests(requester_id);
CREATE INDEX idx_friend_requests_addressee ON friend_requests(addressee_id);
