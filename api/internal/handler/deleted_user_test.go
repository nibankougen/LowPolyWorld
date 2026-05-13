package handler_test

import (
	"context"
	"encoding/json"
	"net/http"
	"net/http/httptest"
	"strings"
	"testing"

	"github.com/go-chi/chi/v5"
	"github.com/jackc/pgx/v5"
	"github.com/jackc/pgx/v5/pgconn"
	"github.com/nibankougen/LowPolyWorld/api/internal/config"
	"github.com/nibankougen/LowPolyWorld/api/internal/handler"
	"github.com/nibankougen/LowPolyWorld/api/internal/middleware"
)

// ── stubs ─────────────────────────────────────────────────────────────────────

// noRowsRow returns pgx.ErrNoRows from Scan, simulating a deleted (or absent) user.
type noRowsRow struct{}

func (noRowsRow) Scan(...any) error { return pgx.ErrNoRows }

// existsFalseRow fills the first *bool dest with false, simulating EXISTS = false.
type existsFalseRow struct{}

func (existsFalseRow) Scan(dest ...any) error {
	if len(dest) > 0 {
		if b, ok := dest[0].(*bool); ok {
			*b = false
		}
	}
	return nil
}

// mockDB is a minimal DBQuerier stub.
// queryRowFn is called for every QueryRow; defaults to noRowsRow if nil.
type mockDB struct {
	queryRowFn func(ctx context.Context, sql string, args ...any) pgx.Row
	execFn     func(ctx context.Context, sql string, args ...any) (pgconn.CommandTag, error)
}

func (m *mockDB) QueryRow(ctx context.Context, sql string, args ...any) pgx.Row {
	if m.queryRowFn != nil {
		return m.queryRowFn(ctx, sql, args...)
	}
	return noRowsRow{}
}

func (m *mockDB) Query(_ context.Context, _ string, _ ...any) (pgx.Rows, error) {
	return nil, nil
}

func (m *mockDB) Exec(ctx context.Context, sql string, args ...any) (pgconn.CommandTag, error) {
	if m.execFn != nil {
		return m.execFn(ctx, sql, args...)
	}
	return pgconn.CommandTag{}, nil
}

func (m *mockDB) Begin(_ context.Context) (pgx.Tx, error) {
	return nil, nil
}

// ── helpers ───────────────────────────────────────────────────────────────────

func newHandler(db handler.DBQuerier) *handler.Handler {
	return &handler.Handler{
		DB:   db,
		Pool: nil, // Pool is only used for fire-and-forget goroutines; not reached in 404 paths
		Cfg:  &config.Config{},
	}
}

// withChiParam injects a chi URL parameter into the request context.
func withChiParam(r *http.Request, key, value string) *http.Request {
	rctx := chi.NewRouteContext()
	rctx.URLParams.Add(key, value)
	return r.WithContext(context.WithValue(r.Context(), chi.RouteCtxKey, rctx))
}

// withUserID injects an authenticated user ID into the request context.
func withUserID(r *http.Request, userID string) *http.Request {
	return r.WithContext(context.WithValue(r.Context(), middleware.UserIDKey, userID))
}

func assertStatus(t *testing.T, rec *httptest.ResponseRecorder, want int) {
	t.Helper()
	if rec.Code != want {
		t.Errorf("status = %d; want %d — body: %s", rec.Code, want, rec.Body.String())
	}
}

func assertErrorCode(t *testing.T, rec *httptest.ResponseRecorder, wantCode string) {
	t.Helper()
	var body struct {
		Error struct {
			Code string `json:"code"`
		} `json:"error"`
	}
	if err := json.Unmarshal(rec.Body.Bytes(), &body); err != nil {
		t.Fatalf("failed to parse response body: %v — raw: %s", err, rec.Body.String())
	}
	if body.Error.Code != wantCode {
		t.Errorf("error.code = %q; want %q", body.Error.Code, wantCode)
	}
}

// ── tests: 削除ユーザー ID で公開 API が 404 を返すこと ─────────────────────

// TestGetPublicUser_DeletedUser checks that GET /api/v1/users/{userID} returns 404
// when the user row is absent (simulating deleted_at IS NOT NULL → no row returned).
func TestGetPublicUser_DeletedUser_Returns404(t *testing.T) {
	h := newHandler(&mockDB{}) // QueryRow always returns pgx.ErrNoRows

	r := httptest.NewRequest(http.MethodGet, "/api/v1/users/deleted-user", nil)
	r = withChiParam(r, "userID", "deleted-user-id")
	rec := httptest.NewRecorder()

	h.GetPublicUser(rec, r)

	assertStatus(t, rec, http.StatusNotFound)
	assertErrorCode(t, rec, "not_found")
}

// TestFollowUser_DeletedTarget checks that POST /api/v1/users/{userID}/follow returns 404
// when the followee does not exist (deleted_at IS NOT NULL → EXISTS returns false).
func TestFollowUser_DeletedTarget_Returns404(t *testing.T) {
	h := newHandler(&mockDB{
		queryRowFn: func(_ context.Context, _ string, _ ...any) pgx.Row {
			return existsFalseRow{} // EXISTS(... deleted_at IS NULL) = false
		},
	})

	r := httptest.NewRequest(http.MethodPost, "/api/v1/users/deleted-user/follow", nil)
	r = withChiParam(r, "userID", "deleted-user-id")
	r = withUserID(r, "requester-id")
	rec := httptest.NewRecorder()

	h.FollowUser(rec, r)

	assertStatus(t, rec, http.StatusNotFound)
	assertErrorCode(t, rec, "not_found")
}

// TestSendFriendRequest_DeletedAddressee checks that POST /api/v1/users/{userID}/friend-request
// returns 404 when the addressee is deleted.
func TestSendFriendRequest_DeletedAddressee_Returns404(t *testing.T) {
	h := newHandler(&mockDB{
		queryRowFn: func(_ context.Context, _ string, _ ...any) pgx.Row {
			return existsFalseRow{} // EXISTS(... deleted_at IS NULL) = false
		},
	})

	r := httptest.NewRequest(http.MethodPost, "/api/v1/users/deleted-user/friend-request", nil)
	r = withChiParam(r, "userID", "deleted-user-id")
	r = withUserID(r, "requester-id")
	rec := httptest.NewRecorder()

	h.SendFriendRequest(rec, r)

	assertStatus(t, rec, http.StatusNotFound)
	assertErrorCode(t, rec, "not_found")
}

// TestReportUser_DeletedTarget checks that POST /api/v1/users/{userID}/report returns 404
// when the target user is deleted.
func TestReportUser_DeletedTarget_Returns404(t *testing.T) {
	h := newHandler(&mockDB{
		queryRowFn: func(_ context.Context, _ string, _ ...any) pgx.Row {
			return existsFalseRow{} // EXISTS(... deleted_at IS NULL) = false
		},
	})

	body := `{"reason":"spam","detail":"test"}`
	r := httptest.NewRequest(http.MethodPost, "/api/v1/users/deleted-user/report",
		strings.NewReader(body))
	r.Header.Set("Content-Type", "application/json")
	r = withChiParam(r, "userID", "deleted-user-id")
	r = withUserID(r, "reporter-id")
	rec := httptest.NewRecorder()

	h.ReportUser(rec, r)

	assertStatus(t, rec, http.StatusNotFound)
	assertErrorCode(t, rec, "not_found")
}

// TestHideUser_DeletedTarget checks that POST /api/v1/me/hidden-users/{targetID} returns 404
// when the target user is deleted.
func TestHideUser_DeletedTarget_Returns404(t *testing.T) {
	h := newHandler(&mockDB{
		queryRowFn: func(_ context.Context, _ string, _ ...any) pgx.Row {
			return existsFalseRow{} // EXISTS(... deleted_at IS NULL) = false
		},
	})

	r := httptest.NewRequest(http.MethodPost, "/api/v1/me/hidden-users/deleted-user", nil)
	r = withChiParam(r, "targetID", "deleted-user-id")
	r = withUserID(r, "hider-id")
	rec := httptest.NewRecorder()

	h.HideUser(rec, r)

	assertStatus(t, rec, http.StatusNotFound)
	assertErrorCode(t, rec, "not_found")
}
