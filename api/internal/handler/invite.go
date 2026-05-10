package handler

import (
	"net/http"
	"time"

	"github.com/go-chi/chi/v5"
	"github.com/nibankougen/LowPolyWorld/api/internal/middleware"
	"github.com/nibankougen/LowPolyWorld/api/internal/response"
)

type inviteLinkResponse struct {
	Token     string `json:"token"`
	RoomID    string `json:"roomId"`
	MaxUses   int    `json:"maxUses"`
	UseCount  int    `json:"useCount"`
	ExpiresAt string `json:"expiresAt"`
	CreatedAt string `json:"createdAt"`
}

// CreateOrRenewInviteLink handles POST /api/v1/rooms/{roomID}/invite-link.
// Creator-only. Deletes existing links for this room and issues a new one.
// max_uses = room.max_players.
func (h *Handler) CreateOrRenewInviteLink(w http.ResponseWriter, r *http.Request) {
	userID := middleware.UserIDFromContext(r.Context())
	roomID := chi.URLParam(r, "roomID")

	// Verify caller is the creator and fetch max_players
	var maxPlayers int
	err := h.DB.QueryRow(r.Context(),
		`SELECT max_players FROM rooms WHERE id = $1 AND creator_user_id = $2`,
		roomID, userID,
	).Scan(&maxPlayers)
	if err != nil {
		response.Error(w, r, http.StatusForbidden, "forbidden", "only the room creator can manage invite links")
		return
	}

	tx, err := h.DB.Begin(r.Context())
	if err != nil {
		response.InternalError(w, r, h.Cfg.IsProduction())
		return
	}
	defer tx.Rollback(r.Context()) //nolint:errcheck

	// Invalidate all existing links for this room
	_, _ = tx.Exec(r.Context(),
		`DELETE FROM invite_links WHERE room_id = $1`, roomID)

	// Issue new link
	var link inviteLinkResponse
	var expiresAt, createdAt time.Time
	err = tx.QueryRow(r.Context(),
		`INSERT INTO invite_links (room_id, created_by, max_uses)
		 VALUES ($1, $2, $3)
		 RETURNING token, room_id, max_uses, use_count, expires_at, created_at`,
		roomID, userID, maxPlayers,
	).Scan(&link.Token, &link.RoomID, &link.MaxUses, &link.UseCount, &expiresAt, &createdAt)
	if err != nil {
		h.Logger.Error("create invite link", "error", err)
		response.InternalError(w, r, h.Cfg.IsProduction())
		return
	}

	if err := tx.Commit(r.Context()); err != nil {
		response.InternalError(w, r, h.Cfg.IsProduction())
		return
	}

	link.ExpiresAt = expiresAt.UTC().Format(time.RFC3339)
	link.CreatedAt = createdAt.UTC().Format(time.RFC3339)
	response.ClientJSON(w, http.StatusCreated, link)
}

// GetInviteLink handles GET /api/v1/rooms/{roomID}/invite-link.
// Creator-only. Returns the current active invite link for the room.
func (h *Handler) GetInviteLink(w http.ResponseWriter, r *http.Request) {
	userID := middleware.UserIDFromContext(r.Context())
	roomID := chi.URLParam(r, "roomID")

	// Verify caller is the creator
	var creatorCheck bool
	_ = h.DB.QueryRow(r.Context(),
		`SELECT EXISTS(SELECT 1 FROM rooms WHERE id = $1 AND creator_user_id = $2)`,
		roomID, userID,
	).Scan(&creatorCheck)
	if !creatorCheck {
		response.Error(w, r, http.StatusForbidden, "forbidden", "only the room creator can view invite links")
		return
	}

	var link inviteLinkResponse
	var expiresAt, createdAt time.Time
	err := h.DB.QueryRow(r.Context(),
		`SELECT token, room_id, max_uses, use_count, expires_at, created_at
		 FROM invite_links
		 WHERE room_id = $1
		 ORDER BY created_at DESC
		 LIMIT 1`,
		roomID,
	).Scan(&link.Token, &link.RoomID, &link.MaxUses, &link.UseCount, &expiresAt, &createdAt)
	if err != nil {
		response.Error(w, r, http.StatusNotFound, "not_found", "no invite link found for this room")
		return
	}

	link.ExpiresAt = expiresAt.UTC().Format(time.RFC3339)
	link.CreatedAt = createdAt.UTC().Format(time.RFC3339)
	response.ClientJSON(w, http.StatusOK, link)
}

// JoinViaInviteLink handles POST /api/v1/invite/{token}/join.
// Validates the invite link and joins the room atomically.
func (h *Handler) JoinViaInviteLink(w http.ResponseWriter, r *http.Request) {
	userID := middleware.UserIDFromContext(r.Context())
	token := chi.URLParam(r, "token")

	tx, err := h.DB.Begin(r.Context())
	if err != nil {
		response.InternalError(w, r, h.Cfg.IsProduction())
		return
	}
	defer tx.Rollback(r.Context()) //nolint:errcheck

	// Fetch and lock the invite link row
	var roomID string
	var maxUses, useCount int
	var expiresAt time.Time
	err = tx.QueryRow(r.Context(),
		`SELECT room_id, max_uses, use_count, expires_at
		 FROM invite_links
		 WHERE token = $1
		 FOR UPDATE`,
		token,
	).Scan(&roomID, &maxUses, &useCount, &expiresAt)
	if err != nil {
		response.Error(w, r, http.StatusNotFound, "not_found", "invite link not found")
		return
	}

	// Validate link
	if time.Now().After(expiresAt) {
		response.Error(w, r, http.StatusGone, "invite_expired", "invite link has expired")
		return
	}
	if useCount >= maxUses {
		response.Error(w, r, http.StatusGone, "invite_limit_reached", "invite link has reached its use limit")
		return
	}

	// Fetch room details and validate state
	var rm roomResponse
	var createdAt time.Time
	var roomState string
	err = tx.QueryRow(r.Context(),
		`SELECT id, world_id, room_type, language, max_players, created_at, state
		 FROM rooms WHERE id = $1`,
		roomID,
	).Scan(&rm.ID, &rm.WorldID, &rm.RoomType, &rm.Language, &rm.MaxPlayers, &createdAt, &roomState)
	if err != nil {
		response.Error(w, r, http.StatusNotFound, "not_found", "room not found")
		return
	}
	rm.CreatedAt = createdAt.UTC().Format(time.RFC3339)

	if roomState != "open" {
		response.Error(w, r, http.StatusConflict, "room_not_open", "room is no longer accepting new members")
		return
	}

	// Atomic capacity check + join
	var joined bool
	_ = tx.QueryRow(r.Context(),
		`WITH capacity AS (
		    SELECT max_players, (SELECT count(*) FROM room_members WHERE room_id = $1) AS current
		    FROM rooms WHERE id = $1
		)
		INSERT INTO room_members (room_id, user_id, join_member_count)
		SELECT $1, $2, (SELECT count(*) FROM room_members WHERE room_id = $1)
		FROM capacity WHERE current < max_players
		ON CONFLICT DO NOTHING
		RETURNING TRUE`,
		roomID, userID,
	).Scan(&joined)

	if !joined {
		var current, max int
		_ = tx.QueryRow(r.Context(),
			`SELECT (SELECT count(*) FROM room_members WHERE room_id = $1), max_players FROM rooms WHERE id = $1`,
			roomID,
		).Scan(&current, &max)
		if current >= max {
			response.Error(w, r, http.StatusConflict, "room_full", "room is at maximum capacity")
			return
		}
		// Already a member — treat as success, don't increment use_count again
	} else {
		// Increment use_count only for first-time joins
		_, _ = tx.Exec(r.Context(),
			`UPDATE invite_links SET use_count = use_count + 1 WHERE token = $1`, token)
	}

	if err := tx.Commit(r.Context()); err != nil {
		response.InternalError(w, r, h.Cfg.IsProduction())
		return
	}

	_ = h.DB.QueryRow(r.Context(),
		`SELECT count(*) FROM room_members WHERE room_id = $1`, roomID,
	).Scan(&rm.CurrentPlayers)

	response.ClientJSON(w, http.StatusOK, rm)
}
