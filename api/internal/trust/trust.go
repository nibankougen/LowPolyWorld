package trust

import (
	"context"
	"log/slog"
	"math"
	"time"

	"github.com/jackc/pgx/v5/pgxpool"
)

const (
	LevelVisitor     = "visitor"
	LevelNewUser     = "new_user"
	LevelUser        = "user"
	LevelTrustedUser = "trusted_user"
)

// LevelRank returns an ordinal for level comparison (higher = more trusted).
func LevelRank(level string) int {
	switch level {
	case LevelTrustedUser:
		return 3
	case LevelUser:
		return 2
	case LevelNewUser:
		return 1
	default:
		return 0
	}
}

// CalculatePoints returns the trust points earned for leaving a public room.
// Formula (from unity-game-abstract.md §22.3):
//
//	floor( (joinCount + exitCount) / 2.0 * floor(durationSec / 60) )
//
// joinCount  = number of OTHER users present when this user joined.
// exitCount  = number of OTHER users present when this user leaves.
// durationSec = seconds the user was in the room.
func CalculatePoints(joinCount, exitCount int, durationSec int64) float64 {
	if durationSec < 60 {
		return 0
	}
	floorMinutes := durationSec / 60
	avg := float64(joinCount+exitCount) / 2.0
	return math.Floor(avg * float64(floorMinutes))
}

// UserSnapshot holds the data required to evaluate trust level.
// FriendCount and HasCoinPurchase are wired in Phase 9/8 respectively;
// until then they default to their zero values and their conditions are not met.
type UserSnapshot struct {
	TrustPoints      float64
	IsPremium        bool
	TrustLevelLocked bool
	PublicWorldCount int
	MaxWorldLikes    int64
	FriendCount      int  // Phase 9
	HasCoinPurchase  bool // Phase 8
}

// EvaluateLevel returns the trust level the snapshot qualifies for.
// Implements the algorithm from unity-game-abstract.md §22.4.
// The caller must check TrustLevelLocked and skip the DB write when true.
func EvaluateLevel(snap UserSnapshot) string {
	// trusted_user: public worlds >= 2 AND any world has >= 100 likes
	if snap.PublicWorldCount >= 2 && snap.MaxWorldLikes >= 100 {
		return LevelTrustedUser
	}
	// user: (trust_points >= 1000 AND friends >= 5) OR coin purchases > 0 OR premium
	if (snap.TrustPoints >= 1000 && snap.FriendCount >= 5) || snap.HasCoinPurchase || snap.IsPremium {
		return LevelUser
	}
	// new_user: trust_points >= 1000 OR (trust_points >= 300 AND friends >= 3)
	if snap.TrustPoints >= 1000 || (snap.TrustPoints >= 300 && snap.FriendCount >= 3) {
		return LevelNewUser
	}
	return LevelVisitor
}

// ProcessPublicRoomLeave records trust points and evaluates level promotion for a
// user leaving a public room. Runs as a fire-and-forget goroutine — errors are
// logged but do not affect the HTTP response.
//
// Parameters:
//   - userID: the leaving user's UUID string
//   - roomID: the room being left
//   - joinCount: other-user count stored in room_members.join_member_count
//   - exitCount: current other-user count (total members - 1) at leave time
//   - joinedAt: the timestamp from room_members.joined_at
func ProcessPublicRoomLeave(
	ctx context.Context,
	db *pgxpool.Pool,
	logger *slog.Logger,
	userID, roomID string,
	joinCount, exitCount int,
	joinedAt time.Time,
) {
	durationSec := int64(time.Since(joinedAt).Seconds())
	points := CalculatePoints(joinCount, exitCount, durationSec)
	floorMinutes := int(durationSec / 60)

	tx, err := db.Begin(ctx)
	if err != nil {
		logger.Error("trust: begin tx", "error", err)
		return
	}
	defer tx.Rollback(ctx) //nolint:errcheck

	// Insert room_trust_event (even if points == 0, for audit purposes).
	if _, err := tx.Exec(ctx,
		`INSERT INTO room_trust_events (user_id, room_id, join_count, exit_count, duration_minutes, points_awarded)
		 VALUES ($1, $2, $3, $4, $5, $6)`,
		userID, roomID, joinCount, exitCount, floorMinutes, points,
	); err != nil {
		logger.Error("trust: insert room_trust_event", "error", err)
		return
	}

	if points <= 0 {
		if err := tx.Commit(ctx); err != nil {
			logger.Error("trust: commit (no points)", "error", err)
		}
		return
	}

	// Add points to active_users.
	if _, err := tx.Exec(ctx,
		`UPDATE active_users SET trust_points = trust_points + $1, updated_at = now() WHERE user_id = $2`,
		points, userID,
	); err != nil {
		logger.Error("trust: update trust_points", "error", err)
		return
	}

	if err := tx.Commit(ctx); err != nil {
		logger.Error("trust: commit points", "error", err)
		return
	}

	// Evaluate level promotion in a separate query (non-critical, best-effort).
	evaluateAndPromote(ctx, db, logger, userID)
}

// evaluateAndPromote fetches the user snapshot, computes the target level,
// and updates active_users + inserts a trust_level_log if the level rises.
func evaluateAndPromote(ctx context.Context, db *pgxpool.Pool, logger *slog.Logger, userID string) {
	var snap UserSnapshot
	var currentLevel string
	var subTier string

	err := db.QueryRow(ctx,
		`SELECT au.trust_points, au.trust_level, au.trust_level_locked, au.subscription_tier,
		        COALESCE((SELECT COUNT(*) FROM worlds w
		                  WHERE w.owner_user_id = au.user_id AND w.is_public = TRUE), 0),
		        COALESCE((SELECT MAX(w.likes_count) FROM worlds w
		                  WHERE w.owner_user_id = au.user_id AND w.is_public = TRUE), 0)
		 FROM active_users au
		 WHERE au.user_id = $1 AND au.deleted_at IS NULL`,
		userID,
	).Scan(&snap.TrustPoints, &currentLevel, &snap.TrustLevelLocked, &subTier,
		&snap.PublicWorldCount, &snap.MaxWorldLikes)
	if err != nil {
		logger.Error("trust: fetch snapshot", "error", err)
		return
	}
	snap.IsPremium = subTier != "free"

	target := EvaluateLevel(snap)

	if snap.TrustLevelLocked {
		// Log the evaluation result but skip level change.
		if _, err := db.Exec(ctx,
			`INSERT INTO trust_level_logs (user_id, before_level, after_level, reason)
			 VALUES ($1, $2, $3, 'locked_skipped')`,
			userID, currentLevel, target,
		); err != nil {
			logger.Error("trust: log locked_skipped", "error", err)
		}
		return
	}

	// Only apply the change if the new level is strictly higher (no auto-demotion).
	if LevelRank(target) <= LevelRank(currentLevel) {
		return
	}

	tx, err := db.Begin(ctx)
	if err != nil {
		logger.Error("trust: begin promotion tx", "error", err)
		return
	}
	defer tx.Rollback(ctx) //nolint:errcheck

	if _, err := tx.Exec(ctx,
		`UPDATE active_users SET trust_level = $1, updated_at = now() WHERE user_id = $2`,
		target, userID,
	); err != nil {
		logger.Error("trust: update trust_level", "error", err)
		return
	}

	if _, err := tx.Exec(ctx,
		`INSERT INTO trust_level_logs (user_id, before_level, after_level, reason)
		 VALUES ($1, $2, $3, 'auto_promotion')`,
		userID, currentLevel, target,
	); err != nil {
		logger.Error("trust: insert trust_level_log", "error", err)
		return
	}

	if err := tx.Commit(ctx); err != nil {
		logger.Error("trust: commit promotion", "error", err)
	}
}
