package trust_test

import (
	"testing"

	"github.com/nibankougen/LowPolyWorld/api/internal/trust"
)

// ── CalculatePoints ──────────────────────────────────────────────────────────

func TestCalculatePoints(t *testing.T) {
	tests := []struct {
		name      string
		join      int
		exit      int
		durationS int64
		want      float64
	}{
		// Duration below 1 minute → always 0
		{"zero_everything", 0, 0, 0, 0},
		{"zero_users_60s", 0, 0, 60, 0},
		{"short_duration_59s", 5, 5, 59, 0},
		{"short_duration_0s", 10, 10, 0, 0},

		// Exact 1-minute session
		{"1_user_each_1min", 1, 1, 60, 1},
		{"5_users_each_1min", 5, 5, 60, 5},
		{"10_users_each_1min", 10, 10, 60, 10},

		// Odd sum → float average, outer floor applied
		{"odd_sum_1min", 5, 6, 60, 5},   // avg=5.5 * 1 → floor(5.5)=5
		{"odd_sum_2min", 5, 6, 120, 11}, // avg=5.5 * 2 → floor(11.0)=11
		{"odd_sum_3min", 3, 4, 180, 10}, // avg=3.5 * 3 → floor(10.5)=10

		// Longer sessions
		{"10_users_60min", 10, 10, 3600, 600},
		{"5_users_30min", 5, 5, 1800, 150},

		// Duration not a round minute (leftover seconds are truncated)
		{"90_sec_2users", 2, 2, 90, 2},   // floor(90/60)=1, avg=2, result=2
		{"119_sec_2users", 2, 2, 119, 2}, // floor(119/60)=1
		{"120_sec_2users", 2, 2, 120, 4}, // floor(120/60)=2

		// Zero other users (nobody else in room)
		{"solo_long_session", 0, 0, 3600, 0},

		// avg = (1+0)/2 = 0.5, floor_minutes = 10 → floor(0.5 * 10) = 5
		{"one_join_zero_exit_10min", 1, 0, 600, 5},

		// Max players scenario (23 others each way, 30 min)
		{"23_users_30min", 23, 23, 1800, 690},
	}

	for _, tc := range tests {
		t.Run(tc.name, func(t *testing.T) {
			got := trust.CalculatePoints(tc.join, tc.exit, tc.durationS)
			if got != tc.want {
				t.Errorf("CalculatePoints(%d, %d, %d) = %v; want %v",
					tc.join, tc.exit, tc.durationS, got, tc.want)
			}
		})
	}
}

// ── LevelRank ────────────────────────────────────────────────────────────────

func TestLevelRank(t *testing.T) {
	tests := []struct {
		level string
		want  int
	}{
		{trust.LevelVisitor, 0},
		{trust.LevelNewUser, 1},
		{trust.LevelUser, 2},
		{trust.LevelTrustedUser, 3},
		{"unknown_level", 0},
		{"", 0},
	}
	for _, tc := range tests {
		got := trust.LevelRank(tc.level)
		if got != tc.want {
			t.Errorf("LevelRank(%q) = %d; want %d", tc.level, got, tc.want)
		}
	}
}

// ── EvaluateLevel ────────────────────────────────────────────────────────────

func snap(opts ...func(*trust.UserSnapshot)) trust.UserSnapshot {
	s := trust.UserSnapshot{}
	for _, o := range opts {
		o(&s)
	}
	return s
}

func withPoints(p float64) func(*trust.UserSnapshot) {
	return func(s *trust.UserSnapshot) { s.TrustPoints = p }
}
func withPremium(v bool) func(*trust.UserSnapshot) {
	return func(s *trust.UserSnapshot) { s.IsPremium = v }
}
func withLocked(v bool) func(*trust.UserSnapshot) {
	return func(s *trust.UserSnapshot) { s.TrustLevelLocked = v }
}
func withWorlds(n int) func(*trust.UserSnapshot) {
	return func(s *trust.UserSnapshot) { s.PublicWorldCount = n }
}
func withMaxLikes(n int64) func(*trust.UserSnapshot) {
	return func(s *trust.UserSnapshot) { s.MaxWorldLikes = n }
}
func withFriends(n int) func(*trust.UserSnapshot) {
	return func(s *trust.UserSnapshot) { s.FriendCount = n }
}
func withCoin(v bool) func(*trust.UserSnapshot) {
	return func(s *trust.UserSnapshot) { s.HasCoinPurchase = v }
}

func TestEvaluateLevel(t *testing.T) {
	tests := []struct {
		name string
		snap trust.UserSnapshot
		want string
	}{
		// ── visitor (default) ──────────────────────────────────────────────────
		{"visitor_all_zero", snap(), trust.LevelVisitor},
		{"visitor_points_below_300", snap(withPoints(299)), trust.LevelVisitor},
		{"visitor_points_below_1000_no_friends", snap(withPoints(500)), trust.LevelVisitor},
		{"visitor_1_world_100_likes", snap(withWorlds(1), withMaxLikes(100)), trust.LevelVisitor},
		{"visitor_2_worlds_99_likes", snap(withWorlds(2), withMaxLikes(99)), trust.LevelVisitor},

		// ── new_user ──────────────────────────────────────────────────────────
		{"new_user_1000pts", snap(withPoints(1000)), trust.LevelNewUser},
		{"new_user_10000pts", snap(withPoints(10000)), trust.LevelNewUser},
		{"new_user_300pts_3friends", snap(withPoints(300), withFriends(3)), trust.LevelNewUser},
		{"new_user_500pts_5friends", snap(withPoints(500), withFriends(5)), trust.LevelNewUser},
		{"new_user_300pts_exact_boundary", snap(withPoints(300), withFriends(3)), trust.LevelNewUser},

		// new_user conditions NOT met
		{"not_new_user_299pts_3friends", snap(withPoints(299), withFriends(3)), trust.LevelVisitor},
		{"not_new_user_300pts_2friends", snap(withPoints(300), withFriends(2)), trust.LevelVisitor},
		{"not_new_user_999pts", snap(withPoints(999)), trust.LevelVisitor},

		// ── user ─────────────────────────────────────────────────────────────
		{"user_premium", snap(withPremium(true)), trust.LevelUser},
		{"user_premium_no_points", snap(withPremium(true), withPoints(0)), trust.LevelUser},
		{"user_1000pts_5friends", snap(withPoints(1000), withFriends(5)), trust.LevelUser},
		{"user_5000pts_10friends", snap(withPoints(5000), withFriends(10)), trust.LevelUser},
		{"user_coin_purchase", snap(withCoin(true)), trust.LevelUser},
		{"user_coin_no_points", snap(withCoin(true), withPoints(0)), trust.LevelUser},

		// user conditions NOT met (missing friends or points)
		{"not_user_1000pts_4friends", snap(withPoints(1000), withFriends(4)), trust.LevelNewUser},
		{"not_user_999pts_5friends", snap(withPoints(999), withFriends(5)), trust.LevelNewUser},
		{"not_user_1000pts_0friends", snap(withPoints(1000)), trust.LevelNewUser},

		// premium takes precedence over low points
		{"user_premium_beats_new_user_pts", snap(withPremium(true), withPoints(100)), trust.LevelUser},

		// ── trusted_user ─────────────────────────────────────────────────────
		{"trusted_2worlds_100likes", snap(withWorlds(2), withMaxLikes(100)), trust.LevelTrustedUser},
		{"trusted_3worlds_200likes", snap(withWorlds(3), withMaxLikes(200)), trust.LevelTrustedUser},
		{"trusted_beats_user_premium", snap(withPremium(true), withWorlds(2), withMaxLikes(100)), trust.LevelTrustedUser},
		{"trusted_beats_user_1000pts_5friends",
			snap(withPoints(1000), withFriends(5), withWorlds(2), withMaxLikes(100)),
			trust.LevelTrustedUser},

		// trusted_user conditions NOT met
		{"not_trusted_1world_1000likes", snap(withWorlds(1), withMaxLikes(1000)), trust.LevelVisitor},
		{"not_trusted_2worlds_99likes", snap(withWorlds(2), withMaxLikes(99)), trust.LevelVisitor},
		{"not_trusted_0worlds_100likes", snap(withWorlds(0), withMaxLikes(100)), trust.LevelVisitor},

		// ── locked flag does NOT change the returned level ─────────────────
		// EvaluateLevel returns the target level regardless of locking;
		// the caller (evaluateAndPromote) is responsible for skipping the DB write.
		{"locked_still_evaluates", snap(withLocked(true), withPoints(1000)), trust.LevelNewUser},
		{"locked_trusted_still_evaluates", snap(withLocked(true), withWorlds(2), withMaxLikes(100)), trust.LevelTrustedUser},
	}

	for _, tc := range tests {
		t.Run(tc.name, func(t *testing.T) {
			got := trust.EvaluateLevel(tc.snap)
			if got != tc.want {
				t.Errorf("EvaluateLevel(...) = %q; want %q", got, tc.want)
			}
		})
	}
}
