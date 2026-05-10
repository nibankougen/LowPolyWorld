DROP TABLE IF EXISTS notifications;
DROP TABLE IF EXISTS hidden_worlds;
DROP TABLE IF EXISTS invite_links;
DROP INDEX IF EXISTS idx_rooms_state;
ALTER TABLE rooms DROP COLUMN IF EXISTS state;
