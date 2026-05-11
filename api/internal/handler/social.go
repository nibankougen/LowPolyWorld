package handler

import (
	"context"
	"net/http"

	"github.com/go-chi/chi/v5"
	"github.com/nibankougen/LowPolyWorld/api/internal/middleware"
	"github.com/nibankougen/LowPolyWorld/api/internal/plan"
	"github.com/nibankougen/LowPolyWorld/api/internal/response"
)

type userSummary struct {
	ID          string `json:"id"`
	DisplayName string `json:"displayName"`
	Name        string `json:"name,omitempty"`
}

type userListResponse struct {
	Users []userSummary `json:"users"`
}

func scanUserSummaryRows(rows interface {
	Next() bool
	Scan(...any) error
	Err() error
	Close()
}) []userSummary {
	defer rows.Close()
	users := []userSummary{}
	for rows.Next() {
		var u userSummary
		var dn, name *string
		if err := rows.Scan(&u.ID, &dn, &name); err != nil {
			continue
		}
		if dn != nil {
			u.DisplayName = *dn
		}
		if name != nil {
			u.Name = *name
		}
		users = append(users, u)
	}
	if rows.Err() != nil {
		return []userSummary{}
	}
	return users
}

// ── Hidden users ──────────────────────────────────────────────────────────────

// ListHiddenUsers handles GET /api/v1/me/hidden-users.
type hiddenUserEntry struct {
	ID          string `json:"id"`
	DisplayName string `json:"displayName"`
	Name        string `json:"name,omitempty"`
}

type hiddenUsersDetailResponse struct {
	Users []hiddenUserEntry `json:"users"`
}

func (h *Handler) ListHiddenUsers(w http.ResponseWriter, r *http.Request) {
	userID := middleware.UserIDFromContext(r.Context())

	rows, err := h.DB.Query(r.Context(),
		`SELECT hu.target_id::text, COALESCE(au.display_name, ''), COALESCE(au.name, '')
		 FROM hidden_users hu
		 LEFT JOIN active_users au ON au.user_id = hu.target_id AND au.deleted_at IS NULL
		 WHERE hu.user_id = $1
		 ORDER BY hu.created_at DESC`,
		userID,
	)
	if err != nil {
		h.Logger.Error("list hidden users", "error", err)
		response.InternalError(w, r, h.Cfg.IsProduction())
		return
	}
	defer rows.Close()

	users := []hiddenUserEntry{}
	for rows.Next() {
		var u hiddenUserEntry
		if err := rows.Scan(&u.ID, &u.DisplayName, &u.Name); err != nil {
			continue
		}
		users = append(users, u)
	}
	response.ClientJSON(w, http.StatusOK, hiddenUsersDetailResponse{Users: users})
}

// HideUser handles POST /api/v1/me/hidden-users/{targetID}.
// Hides a user and auto-dissolves any friendship or pending requests.
func (h *Handler) HideUser(w http.ResponseWriter, r *http.Request) {
	userID := middleware.UserIDFromContext(r.Context())
	targetID := chi.URLParam(r, "targetID")

	if userID == targetID {
		response.Error(w, r, http.StatusBadRequest, "validation_error", "cannot hide yourself")
		return
	}

	var exists bool
	_ = h.DB.QueryRow(r.Context(),
		`SELECT EXISTS(SELECT 1 FROM active_users WHERE user_id = $1 AND deleted_at IS NULL)`, targetID,
	).Scan(&exists)
	if !exists {
		response.Error(w, r, http.StatusNotFound, "not_found", "user not found")
		return
	}

	tx, err := h.DB.Begin(r.Context())
	if err != nil {
		response.InternalError(w, r, h.Cfg.IsProduction())
		return
	}
	defer tx.Rollback(r.Context()) //nolint:errcheck

	_, _ = tx.Exec(r.Context(),
		`INSERT INTO hidden_users (user_id, target_id) VALUES ($1, $2) ON CONFLICT DO NOTHING`,
		userID, targetID,
	)
	// Auto-dissolve friendship (both directions) and any pending requests
	_, _ = tx.Exec(r.Context(),
		`DELETE FROM friend_requests
		 WHERE (requester_id = $1 AND addressee_id = $2)
		    OR (requester_id = $2 AND addressee_id = $1)`,
		userID, targetID,
	)

	if err := tx.Commit(r.Context()); err != nil {
		response.InternalError(w, r, h.Cfg.IsProduction())
		return
	}
	w.WriteHeader(http.StatusNoContent)
}

// UnhideUser handles DELETE /api/v1/me/hidden-users/{targetID}.
func (h *Handler) UnhideUser(w http.ResponseWriter, r *http.Request) {
	userID := middleware.UserIDFromContext(r.Context())
	targetID := chi.URLParam(r, "targetID")

	_, _ = h.DB.Exec(r.Context(),
		`DELETE FROM hidden_users WHERE user_id = $1 AND target_id = $2`,
		userID, targetID,
	)
	w.WriteHeader(http.StatusNoContent)
}

// ── Follows ───────────────────────────────────────────────────────────────────

// FollowUser handles POST /api/v1/users/{userID}/follow.
func (h *Handler) FollowUser(w http.ResponseWriter, r *http.Request) {
	followerID := middleware.UserIDFromContext(r.Context())
	followeeID := chi.URLParam(r, "userID")

	if followerID == followeeID {
		response.Error(w, r, http.StatusBadRequest, "validation_error", "cannot follow yourself")
		return
	}

	var exists bool
	_ = h.DB.QueryRow(r.Context(),
		`SELECT EXISTS(SELECT 1 FROM active_users WHERE user_id = $1 AND deleted_at IS NULL)`, followeeID,
	).Scan(&exists)
	if !exists {
		response.Error(w, r, http.StatusNotFound, "not_found", "user not found")
		return
	}

	if _, err := h.DB.Exec(r.Context(),
		`INSERT INTO follows (follower_id, followee_id) VALUES ($1, $2) ON CONFLICT DO NOTHING`,
		followerID, followeeID,
	); err != nil {
		h.Logger.Error("follow user", "error", err)
		response.InternalError(w, r, h.Cfg.IsProduction())
		return
	}
	w.WriteHeader(http.StatusNoContent)
}

// UnfollowUser handles DELETE /api/v1/users/{userID}/follow.
func (h *Handler) UnfollowUser(w http.ResponseWriter, r *http.Request) {
	followerID := middleware.UserIDFromContext(r.Context())
	followeeID := chi.URLParam(r, "userID")

	_, _ = h.DB.Exec(r.Context(),
		`DELETE FROM follows WHERE follower_id = $1 AND followee_id = $2`,
		followerID, followeeID,
	)
	w.WriteHeader(http.StatusNoContent)
}

// ListFollowers handles GET /api/v1/users/{userID}/followers.
func (h *Handler) ListFollowers(w http.ResponseWriter, r *http.Request) {
	targetID := chi.URLParam(r, "userID")

	rows, err := h.DB.Query(r.Context(),
		`SELECT au.user_id, au.display_name, au.name
		 FROM follows f
		 JOIN active_users au ON au.user_id = f.follower_id AND au.deleted_at IS NULL
		 WHERE f.followee_id = $1
		 ORDER BY f.created_at DESC`,
		targetID,
	)
	if err != nil {
		h.Logger.Error("list followers", "error", err)
		response.InternalError(w, r, h.Cfg.IsProduction())
		return
	}
	response.ClientJSON(w, http.StatusOK, userListResponse{Users: scanUserSummaryRows(rows)})
}

// ListFollowing handles GET /api/v1/users/{userID}/following.
func (h *Handler) ListFollowing(w http.ResponseWriter, r *http.Request) {
	targetID := chi.URLParam(r, "userID")

	rows, err := h.DB.Query(r.Context(),
		`SELECT au.user_id, au.display_name, au.name
		 FROM follows f
		 JOIN active_users au ON au.user_id = f.followee_id AND au.deleted_at IS NULL
		 WHERE f.follower_id = $1
		 ORDER BY f.created_at DESC`,
		targetID,
	)
	if err != nil {
		h.Logger.Error("list following", "error", err)
		response.InternalError(w, r, h.Cfg.IsProduction())
		return
	}
	response.ClientJSON(w, http.StatusOK, userListResponse{Users: scanUserSummaryRows(rows)})
}

// ── Friends ───────────────────────────────────────────────────────────────────

type sendFriendRequestResponse struct {
	Status string `json:"status"` // "pending" | "friends"
}

// SendFriendRequest handles POST /api/v1/users/{userID}/friend-request.
// If the target already sent a request to the caller, mutual approval triggers immediately.
func (h *Handler) SendFriendRequest(w http.ResponseWriter, r *http.Request) {
	requesterID := middleware.UserIDFromContext(r.Context())
	addresseeID := chi.URLParam(r, "userID")

	if requesterID == addresseeID {
		response.Error(w, r, http.StatusBadRequest, "validation_error", "cannot send friend request to yourself")
		return
	}

	var exists bool
	_ = h.DB.QueryRow(r.Context(),
		`SELECT EXISTS(SELECT 1 FROM active_users WHERE user_id = $1 AND deleted_at IS NULL)`, addresseeID,
	).Scan(&exists)
	if !exists {
		response.Error(w, r, http.StatusNotFound, "not_found", "user not found")
		return
	}

	// Silently discard if addressee has hidden the requester (spec §6.5)
	var hiddenByAddressee bool
	_ = h.DB.QueryRow(r.Context(),
		`SELECT EXISTS(SELECT 1 FROM hidden_users WHERE user_id = $1 AND target_id = $2)`,
		addresseeID, requesterID,
	).Scan(&hiddenByAddressee)
	if hiddenByAddressee {
		response.ClientJSON(w, http.StatusOK, sendFriendRequestResponse{Status: "pending"})
		return
	}

	// Check caller's friend limit
	var subscriptionTier string
	_ = h.DB.QueryRow(r.Context(),
		`SELECT subscription_tier FROM active_users WHERE user_id = $1`, requesterID,
	).Scan(&subscriptionTier)
	caps := plan.GetCapabilities(plan.Tier(subscriptionTier))

	var friendCount int
	_ = h.DB.QueryRow(r.Context(),
		`SELECT count(*) FROM friend_requests WHERE requester_id = $1 AND status = 'accepted'`, requesterID,
	).Scan(&friendCount)
	if friendCount >= caps.FriendLimit {
		response.Error(w, r, http.StatusConflict, "friend_limit_reached", "friend limit reached")
		return
	}

	// Return early if already sent or already friends; reset if previously rejected
	var existingStatus string
	_ = h.DB.QueryRow(r.Context(),
		`SELECT status FROM friend_requests WHERE requester_id = $1 AND addressee_id = $2`,
		requesterID, addresseeID,
	).Scan(&existingStatus)
	if existingStatus == "accepted" {
		response.ClientJSON(w, http.StatusOK, sendFriendRequestResponse{Status: "friends"})
		return
	}
	if existingStatus == "pending" {
		response.ClientJSON(w, http.StatusOK, sendFriendRequestResponse{Status: "pending"})
		return
	}
	// existingStatus == "rejected": reset to pending so re-send works

	// Check for reverse pending request (mutual approval)
	var reversePending bool
	_ = h.DB.QueryRow(r.Context(),
		`SELECT EXISTS(
		     SELECT 1 FROM friend_requests
		     WHERE requester_id = $1 AND addressee_id = $2 AND status = 'pending'
		 )`,
		addresseeID, requesterID,
	).Scan(&reversePending)

	tx, err := h.DB.Begin(r.Context())
	if err != nil {
		response.InternalError(w, r, h.Cfg.IsProduction())
		return
	}
	defer tx.Rollback(r.Context()) //nolint:errcheck

	resultStatus := "pending"
	if reversePending {
		// Check addressee's friend limit for mutual approval
		var addresseeTier string
		_ = h.DB.QueryRow(r.Context(),
			`SELECT subscription_tier FROM active_users WHERE user_id = $1`, addresseeID,
		).Scan(&addresseeTier)
		addresseeCaps := plan.GetCapabilities(plan.Tier(addresseeTier))
		var addresseeFriendCount int
		_ = h.DB.QueryRow(r.Context(),
			`SELECT count(*) FROM friend_requests WHERE requester_id = $1 AND status = 'accepted'`, addresseeID,
		).Scan(&addresseeFriendCount)

		if addresseeFriendCount < addresseeCaps.FriendLimit {
			// Mutual approval: accept both directions
			_, _ = tx.Exec(r.Context(),
				`UPDATE friend_requests SET status = 'accepted', updated_at = now()
				 WHERE requester_id = $1 AND addressee_id = $2`,
				addresseeID, requesterID,
			)
			_, _ = tx.Exec(r.Context(),
				`INSERT INTO friend_requests (requester_id, addressee_id, status)
				 VALUES ($1, $2, 'accepted')
				 ON CONFLICT (requester_id, addressee_id) DO UPDATE SET status = 'accepted', updated_at = now()`,
				requesterID, addresseeID,
			)
			resultStatus = "friends"
		} else {
			// Addressee at capacity — fall back to pending
			_, _ = tx.Exec(r.Context(),
				`INSERT INTO friend_requests (requester_id, addressee_id, status)
				 VALUES ($1, $2, 'pending')
				 ON CONFLICT (requester_id, addressee_id) DO UPDATE SET status = 'pending', updated_at = now()`,
				requesterID, addresseeID,
			)
		}
	} else {
		_, _ = tx.Exec(r.Context(),
			`INSERT INTO friend_requests (requester_id, addressee_id, status)
			 VALUES ($1, $2, 'pending')
			 ON CONFLICT (requester_id, addressee_id) DO UPDATE SET status = 'pending', updated_at = now()`,
			requesterID, addresseeID,
		)
	}

	if err := tx.Commit(r.Context()); err != nil {
		response.InternalError(w, r, h.Cfg.IsProduction())
		return
	}

	// Notify addressee about the new friend request (async, pending only)
	if resultStatus == "pending" {
		var displayName string
		_ = h.DB.QueryRow(r.Context(),
			`SELECT COALESCE(display_name, '') FROM active_users WHERE user_id = $1`, requesterID,
		).Scan(&displayName)
		if displayName == "" {
			displayName = "ユーザー"
		}
		go createNotification(context.Background(), h, addresseeID,
			"friend_request", displayName+"さんからフレンド申請が届きました", requesterID)
	}

	response.ClientJSON(w, http.StatusOK, sendFriendRequestResponse{Status: resultStatus})
}

// AcceptFriendRequest handles POST /api/v1/me/friend-requests/{requesterID}/accept.
func (h *Handler) AcceptFriendRequest(w http.ResponseWriter, r *http.Request) {
	addresseeID := middleware.UserIDFromContext(r.Context())
	requesterID := chi.URLParam(r, "requesterID")

	// Check addressee's friend limit
	var subscriptionTier string
	_ = h.DB.QueryRow(r.Context(),
		`SELECT subscription_tier FROM active_users WHERE user_id = $1`, addresseeID,
	).Scan(&subscriptionTier)
	caps := plan.GetCapabilities(plan.Tier(subscriptionTier))
	var friendCount int
	_ = h.DB.QueryRow(r.Context(),
		`SELECT count(*) FROM friend_requests WHERE requester_id = $1 AND status = 'accepted'`, addresseeID,
	).Scan(&friendCount)
	if friendCount >= caps.FriendLimit {
		response.Error(w, r, http.StatusConflict, "friend_limit_reached", "friend limit reached")
		return
	}

	tx, err := h.DB.Begin(r.Context())
	if err != nil {
		response.InternalError(w, r, h.Cfg.IsProduction())
		return
	}
	defer tx.Rollback(r.Context()) //nolint:errcheck

	tag, err := tx.Exec(r.Context(),
		`UPDATE friend_requests SET status = 'accepted', updated_at = now()
		 WHERE requester_id = $1 AND addressee_id = $2 AND status = 'pending'`,
		requesterID, addresseeID,
	)
	if err != nil || tag.RowsAffected() == 0 {
		response.Error(w, r, http.StatusNotFound, "not_found", "friend request not found")
		return
	}
	// Create the reverse accepted row
	_, _ = tx.Exec(r.Context(),
		`INSERT INTO friend_requests (requester_id, addressee_id, status)
		 VALUES ($1, $2, 'accepted')
		 ON CONFLICT (requester_id, addressee_id) DO UPDATE SET status = 'accepted', updated_at = now()`,
		addresseeID, requesterID,
	)

	if err := tx.Commit(r.Context()); err != nil {
		response.InternalError(w, r, h.Cfg.IsProduction())
		return
	}
	w.WriteHeader(http.StatusNoContent)
}

// RejectFriendRequest handles POST /api/v1/me/friend-requests/{requesterID}/reject.
func (h *Handler) RejectFriendRequest(w http.ResponseWriter, r *http.Request) {
	addresseeID := middleware.UserIDFromContext(r.Context())
	requesterID := chi.URLParam(r, "requesterID")

	_, _ = h.DB.Exec(r.Context(),
		`UPDATE friend_requests SET status = 'rejected', updated_at = now()
		 WHERE requester_id = $1 AND addressee_id = $2 AND status = 'pending'`,
		requesterID, addresseeID,
	)
	w.WriteHeader(http.StatusNoContent)
}

// CancelFriendRequest handles DELETE /api/v1/me/friend-requests/sent/{addresseeID}.
func (h *Handler) CancelFriendRequest(w http.ResponseWriter, r *http.Request) {
	requesterID := middleware.UserIDFromContext(r.Context())
	addresseeID := chi.URLParam(r, "addresseeID")

	_, _ = h.DB.Exec(r.Context(),
		`DELETE FROM friend_requests WHERE requester_id = $1 AND addressee_id = $2 AND status = 'pending'`,
		requesterID, addresseeID,
	)
	w.WriteHeader(http.StatusNoContent)
}

// ListFriends handles GET /api/v1/me/friends.
// A friendship is represented as two accepted rows; returns users via the requester_id=me direction.
func (h *Handler) ListFriends(w http.ResponseWriter, r *http.Request) {
	userID := middleware.UserIDFromContext(r.Context())

	rows, err := h.DB.Query(r.Context(),
		`SELECT au.user_id, au.display_name, au.name
		 FROM friend_requests fr
		 JOIN active_users au ON au.user_id = fr.addressee_id AND au.deleted_at IS NULL
		 WHERE fr.requester_id = $1 AND fr.status = 'accepted'
		 ORDER BY fr.updated_at DESC`,
		userID,
	)
	if err != nil {
		h.Logger.Error("list friends", "error", err)
		response.InternalError(w, r, h.Cfg.IsProduction())
		return
	}
	response.ClientJSON(w, http.StatusOK, userListResponse{Users: scanUserSummaryRows(rows)})
}

// ListFriendRequestsReceived handles GET /api/v1/me/friend-requests/received.
func (h *Handler) ListFriendRequestsReceived(w http.ResponseWriter, r *http.Request) {
	userID := middleware.UserIDFromContext(r.Context())

	rows, err := h.DB.Query(r.Context(),
		`SELECT au.user_id, au.display_name, au.name
		 FROM friend_requests fr
		 JOIN active_users au ON au.user_id = fr.requester_id AND au.deleted_at IS NULL
		 WHERE fr.addressee_id = $1 AND fr.status = 'pending'
		 ORDER BY fr.created_at DESC`,
		userID,
	)
	if err != nil {
		h.Logger.Error("list received requests", "error", err)
		response.InternalError(w, r, h.Cfg.IsProduction())
		return
	}
	response.ClientJSON(w, http.StatusOK, userListResponse{Users: scanUserSummaryRows(rows)})
}

// ListFriendRequestsSent handles GET /api/v1/me/friend-requests/sent.
func (h *Handler) ListFriendRequestsSent(w http.ResponseWriter, r *http.Request) {
	userID := middleware.UserIDFromContext(r.Context())

	rows, err := h.DB.Query(r.Context(),
		`SELECT au.user_id, au.display_name, au.name
		 FROM friend_requests fr
		 JOIN active_users au ON au.user_id = fr.addressee_id AND au.deleted_at IS NULL
		 WHERE fr.requester_id = $1 AND fr.status = 'pending'
		 ORDER BY fr.created_at DESC`,
		userID,
	)
	if err != nil {
		h.Logger.Error("list sent requests", "error", err)
		response.InternalError(w, r, h.Cfg.IsProduction())
		return
	}
	response.ClientJSON(w, http.StatusOK, userListResponse{Users: scanUserSummaryRows(rows)})
}

// RemoveFriend handles DELETE /api/v1/me/friends/{friendID}.
func (h *Handler) RemoveFriend(w http.ResponseWriter, r *http.Request) {
	userID := middleware.UserIDFromContext(r.Context())
	friendID := chi.URLParam(r, "friendID")

	_, _ = h.DB.Exec(r.Context(),
		`DELETE FROM friend_requests
		 WHERE (requester_id = $1 AND addressee_id = $2 AND status = 'accepted')
		    OR (requester_id = $2 AND addressee_id = $1 AND status = 'accepted')`,
		userID, friendID,
	)
	w.WriteHeader(http.StatusNoContent)
}

// ── Hidden worlds ─────────────────────────────────────────────────────────────

type hiddenWorldEntry struct {
	ID   string `json:"id"`
	Name string `json:"name"`
}

type hiddenWorldsDetailResponse struct {
	Worlds []hiddenWorldEntry `json:"worlds"`
}

// ListHiddenWorlds handles GET /api/v1/me/hidden-worlds.
func (h *Handler) ListHiddenWorlds(w http.ResponseWriter, r *http.Request) {
	userID := middleware.UserIDFromContext(r.Context())

	rows, err := h.DB.Query(r.Context(),
		`SELECT hw.world_id::text, COALESCE(w.name, '')
		 FROM hidden_worlds hw
		 LEFT JOIN worlds w ON w.id = hw.world_id
		 WHERE hw.user_id = $1
		 ORDER BY hw.created_at DESC`,
		userID,
	)
	if err != nil {
		h.Logger.Error("list hidden worlds", "error", err)
		response.InternalError(w, r, h.Cfg.IsProduction())
		return
	}
	defer rows.Close()

	worlds := []hiddenWorldEntry{}
	for rows.Next() {
		var w hiddenWorldEntry
		if err := rows.Scan(&w.ID, &w.Name); err != nil {
			continue
		}
		worlds = append(worlds, w)
	}
	response.ClientJSON(w, http.StatusOK, hiddenWorldsDetailResponse{Worlds: worlds})
}

// HideWorld handles POST /api/v1/me/hidden-worlds/{worldID}.
func (h *Handler) HideWorld(w http.ResponseWriter, r *http.Request) {
	userID := middleware.UserIDFromContext(r.Context())
	worldID := chi.URLParam(r, "worldID")

	var exists bool
	_ = h.DB.QueryRow(r.Context(),
		`SELECT EXISTS(SELECT 1 FROM worlds WHERE id = $1 AND is_public = TRUE)`, worldID,
	).Scan(&exists)
	if !exists {
		response.Error(w, r, http.StatusNotFound, "not_found", "world not found")
		return
	}

	_, _ = h.DB.Exec(r.Context(),
		`INSERT INTO hidden_worlds (user_id, world_id) VALUES ($1, $2) ON CONFLICT DO NOTHING`,
		userID, worldID,
	)
	w.WriteHeader(http.StatusNoContent)
}

// UnhideWorld handles DELETE /api/v1/me/hidden-worlds/{worldID}.
func (h *Handler) UnhideWorld(w http.ResponseWriter, r *http.Request) {
	userID := middleware.UserIDFromContext(r.Context())
	worldID := chi.URLParam(r, "worldID")

	_, _ = h.DB.Exec(r.Context(),
		`DELETE FROM hidden_worlds WHERE user_id = $1 AND world_id = $2`,
		userID, worldID,
	)
	w.WriteHeader(http.StatusNoContent)
}
