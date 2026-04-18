package handler

import (
	"encoding/json"
	"net/http"
	"time"

	"github.com/go-chi/chi/v5"
	"github.com/nibankougen/LowPolyWorld/api/internal/middleware"
	"github.com/nibankougen/LowPolyWorld/api/internal/plan"
	"github.com/nibankougen/LowPolyWorld/api/internal/response"
)

type roomResponse struct {
	ID             string `json:"id"`
	WorldID        string `json:"worldId"`
	RoomType       string `json:"roomType"`
	Language       string `json:"language"`
	MaxPlayers     int    `json:"maxPlayers"`
	CurrentPlayers int    `json:"currentPlayers"`
	CreatedAt      string `json:"createdAt"`
}

// ListRooms handles GET /api/v1/worlds/{worldID}/rooms.
func (h *Handler) ListRooms(w http.ResponseWriter, r *http.Request) {
	worldID := chi.URLParam(r, "worldID")
	userID := middleware.UserIDFromContext(r.Context())

	// Get user language preference for sorting
	var userLanguage string
	_ = h.DB.QueryRow(r.Context(),
		`SELECT language FROM active_users WHERE user_id = $1`, userID,
	).Scan(&userLanguage)
	if userLanguage == "" {
		userLanguage = "ja"
	}

	// Verify world exists and is public
	var exists bool
	_ = h.DB.QueryRow(r.Context(),
		`SELECT EXISTS(SELECT 1 FROM worlds WHERE id = $1 AND is_public = TRUE)`, worldID,
	).Scan(&exists)
	if !exists {
		response.Error(w, r, http.StatusNotFound, "not_found", "world not found")
		return
	}

	rows, err := h.DB.Query(r.Context(),
		`SELECT r.id, r.world_id, r.room_type, r.language, r.max_players,
		        (SELECT count(*) FROM room_members rm WHERE rm.room_id = r.id) AS current_players,
		        r.created_at
		 FROM rooms r
		 WHERE r.world_id = $1 AND r.room_type = 'public'
		 ORDER BY (r.language = $2) DESC, r.created_at ASC`,
		worldID, userLanguage,
	)
	if err != nil {
		h.Logger.Error("list rooms", "error", err)
		response.InternalError(w, r, h.Cfg.IsProduction())
		return
	}
	defer rows.Close()

	var rooms []roomResponse
	for rows.Next() {
		var rm roomResponse
		var createdAt time.Time
		if err := rows.Scan(&rm.ID, &rm.WorldID, &rm.RoomType, &rm.Language,
			&rm.MaxPlayers, &rm.CurrentPlayers, &createdAt); err != nil {
			continue
		}
		rm.CreatedAt = createdAt.UTC().Format(time.RFC3339)
		rooms = append(rooms, rm)
	}
	if rooms == nil {
		rooms = []roomResponse{}
	}

	response.JSON(w, http.StatusOK, rooms)
}

type createRoomRequest struct {
	RoomType string `json:"room_type"`
	Language string `json:"language"`
}

// CreateRoom handles POST /api/v1/worlds/{worldID}/rooms.
func (h *Handler) CreateRoom(w http.ResponseWriter, r *http.Request) {
	worldID := chi.URLParam(r, "worldID")
	userID := middleware.UserIDFromContext(r.Context())

	var req createRoomRequest
	if err := json.NewDecoder(r.Body).Decode(&req); err != nil {
		response.Error(w, r, http.StatusBadRequest, "validation_error", "invalid request body")
		return
	}

	// Default values
	if req.RoomType == "" {
		req.RoomType = "public"
	}
	if req.Language == "" {
		req.Language = "ja"
	}

	// Validate room_type
	validTypes := map[string]bool{"public": true, "friends_only": true, "followers_only": true, "invite_only": true}
	if !validTypes[req.RoomType] {
		response.Error(w, r, http.StatusBadRequest, "validation_error", "invalid room_type")
		return
	}

	// Verify world exists and is public
	var worldMaxPlayers int
	err := h.DB.QueryRow(r.Context(),
		`SELECT max_players FROM worlds WHERE id = $1 AND is_public = TRUE`, worldID,
	).Scan(&worldMaxPlayers)
	if err != nil {
		response.Error(w, r, http.StatusNotFound, "not_found", "world not found")
		return
	}

	// Get user plan capabilities for maxPlayers cap
	var subscriptionTier string
	_ = h.DB.QueryRow(r.Context(),
		`SELECT subscription_tier FROM active_users WHERE user_id = $1`, userID,
	).Scan(&subscriptionTier)
	caps := plan.GetCapabilities(plan.Tier(subscriptionTier))

	maxPlayers := worldMaxPlayers
	if maxPlayers > caps.MaxPlayersLimit {
		maxPlayers = caps.MaxPlayersLimit
	}

	// Check invite_only permission
	if req.RoomType == "invite_only" && !caps.InviteRoomCreate {
		response.Error(w, r, http.StatusForbidden, "forbidden", "invite_only rooms require premium subscription")
		return
	}

	var roomID string
	err = h.DB.QueryRow(r.Context(),
		`INSERT INTO rooms (world_id, creator_user_id, room_type, language, max_players)
		 VALUES ($1, $2, $3, $4, $5)
		 RETURNING id`,
		worldID, userID, req.RoomType, req.Language, maxPlayers,
	).Scan(&roomID)
	if err != nil {
		h.Logger.Error("create room", "error", err)
		response.InternalError(w, r, h.Cfg.IsProduction())
		return
	}

	// Auto-join creator
	_, _ = h.DB.Exec(r.Context(),
		`INSERT INTO room_members (room_id, user_id) VALUES ($1, $2) ON CONFLICT DO NOTHING`,
		roomID, userID,
	)

	response.JSON(w, http.StatusCreated, roomResponse{
		ID:             roomID,
		WorldID:        worldID,
		RoomType:       req.RoomType,
		Language:       req.Language,
		MaxPlayers:     maxPlayers,
		CurrentPlayers: 1,
		CreatedAt:      time.Now().UTC().Format(time.RFC3339),
	})
}

type recommendedJoinResponse struct {
	Action   string `json:"action"`             // "join" | "create" | "confirm_english"
	RoomID   string `json:"roomId,omitempty"`   // set when action == "join" or "confirm_english"
	Language string `json:"language,omitempty"` // language of the matched room
}

// RecommendedJoin handles POST /api/v1/worlds/{worldID}/rooms/recommended-join.
// Implements the 4-step recommendation logic:
//  1. Same language, space available → join most populated
//  2. Same language all full → create new room in user language
//  3. English room available → confirm_english (client shows confirmation modal)
//  4. None → create new room in user language
func (h *Handler) RecommendedJoin(w http.ResponseWriter, r *http.Request) {
	worldID := chi.URLParam(r, "worldID")
	userID := middleware.UserIDFromContext(r.Context())

	// Get user language
	var userLanguage string
	_ = h.DB.QueryRow(r.Context(),
		`SELECT language FROM active_users WHERE user_id = $1`, userID,
	).Scan(&userLanguage)
	if userLanguage == "" {
		userLanguage = "ja"
	}

	// Verify world exists and is public
	var worldExists bool
	_ = h.DB.QueryRow(r.Context(),
		`SELECT EXISTS(SELECT 1 FROM worlds WHERE id = $1 AND is_public = TRUE)`, worldID,
	).Scan(&worldExists)
	if !worldExists {
		response.Error(w, r, http.StatusNotFound, "not_found", "world not found")
		return
	}

	// Step 1: same language, space available → join most populated
	var sameLanguageRoomID string
	_ = h.DB.QueryRow(r.Context(),
		`SELECT r.id
		 FROM rooms r
		 WHERE r.world_id = $1 AND r.room_type = 'public' AND r.language = $2
		   AND (SELECT count(*) FROM room_members rm WHERE rm.room_id = r.id) < r.max_players
		 ORDER BY (SELECT count(*) FROM room_members rm WHERE rm.room_id = r.id) DESC
		 LIMIT 1`,
		worldID, userLanguage,
	).Scan(&sameLanguageRoomID)

	if sameLanguageRoomID != "" {
		_, _ = h.DB.Exec(r.Context(),
			`INSERT INTO room_members (room_id, user_id) VALUES ($1, $2) ON CONFLICT DO NOTHING`,
			sameLanguageRoomID, userID,
		)
		response.JSON(w, http.StatusOK, recommendedJoinResponse{
			Action:   "join",
			RoomID:   sameLanguageRoomID,
			Language: userLanguage,
		})
		return
	}

	// Step 2: check if same-language rooms exist at all (all full)
	var sameLanguageExists bool
	_ = h.DB.QueryRow(r.Context(),
		`SELECT EXISTS(
			SELECT 1 FROM rooms r
			WHERE r.world_id = $1 AND r.room_type = 'public' AND r.language = $2
		)`,
		worldID, userLanguage,
	).Scan(&sameLanguageExists)

	if sameLanguageExists {
		// All same-language rooms are full → create new one in user language
		response.JSON(w, http.StatusOK, recommendedJoinResponse{
			Action:   "create",
			Language: userLanguage,
		})
		return
	}

	// Step 3: English room available (only if user language is not English)
	if userLanguage != "en" {
		var englishRoomID string
		_ = h.DB.QueryRow(r.Context(),
			`SELECT r.id
			 FROM rooms r
			 WHERE r.world_id = $1 AND r.room_type = 'public' AND r.language = 'en'
			   AND (SELECT count(*) FROM room_members rm WHERE rm.room_id = r.id) < r.max_players
			 ORDER BY (SELECT count(*) FROM room_members rm WHERE rm.room_id = r.id) DESC
			 LIMIT 1`,
			worldID,
		).Scan(&englishRoomID)

		if englishRoomID != "" {
			response.JSON(w, http.StatusOK, recommendedJoinResponse{
				Action:   "confirm_english",
				RoomID:   englishRoomID,
				Language: "en",
			})
			return
		}
	}

	// Step 4: no suitable room → create new in user language
	response.JSON(w, http.StatusOK, recommendedJoinResponse{
		Action:   "create",
		Language: userLanguage,
	})
}
