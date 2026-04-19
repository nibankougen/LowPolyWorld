package adminauth_test

import (
	"testing"

	"github.com/nibankougen/LowPolyWorld/api/internal/adminauth"
)

func TestRoleRank(t *testing.T) {
	cases := []struct {
		role string
		want int
	}{
		{adminauth.RoleSuperAdmin, 3},
		{adminauth.RoleAdmin, 2},
		{adminauth.RoleModerator, 1},
		{"unknown", 0},
		{"", 0},
	}
	for _, tc := range cases {
		if got := adminauth.RoleRank(tc.role); got != tc.want {
			t.Errorf("RoleRank(%q) = %d, want %d", tc.role, got, tc.want)
		}
	}
}

func TestAtLeast(t *testing.T) {
	cases := []struct {
		actual, required string
		want             bool
	}{
		{adminauth.RoleSuperAdmin, adminauth.RoleSuperAdmin, true},
		{adminauth.RoleSuperAdmin, adminauth.RoleAdmin, true},
		{adminauth.RoleSuperAdmin, adminauth.RoleModerator, true},
		{adminauth.RoleAdmin, adminauth.RoleSuperAdmin, false},
		{adminauth.RoleAdmin, adminauth.RoleAdmin, true},
		{adminauth.RoleAdmin, adminauth.RoleModerator, true},
		{adminauth.RoleModerator, adminauth.RoleSuperAdmin, false},
		{adminauth.RoleModerator, adminauth.RoleAdmin, false},
		{adminauth.RoleModerator, adminauth.RoleModerator, true},
		{"unknown", adminauth.RoleModerator, false},
	}
	for _, tc := range cases {
		if got := adminauth.AtLeast(tc.actual, tc.required); got != tc.want {
			t.Errorf("AtLeast(%q, %q) = %v, want %v", tc.actual, tc.required, got, tc.want)
		}
	}
}

func TestHashPassword(t *testing.T) {
	hash, err := adminauth.HashPassword("correct-horse-battery-staple")
	if err != nil {
		t.Fatal(err)
	}
	if len(hash) < 30 {
		t.Errorf("hash too short: %q", hash)
	}
	// Hashing the same password again should produce a different hash (salt).
	hash2, _ := adminauth.HashPassword("correct-horse-battery-staple")
	if hash == hash2 {
		t.Error("expected unique hashes but got identical values")
	}
}
