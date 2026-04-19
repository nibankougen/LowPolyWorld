DROP TABLE IF EXISTS admin_audit_logs;
DROP TABLE IF EXISTS user_violation_reports;
DROP TABLE IF EXISTS room_trust_events;
DROP TABLE IF EXISTS trust_level_logs;
ALTER TABLE room_members DROP COLUMN IF EXISTS join_member_count;
