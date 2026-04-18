package handler

import (
	"net/http"
	"time"

	"github.com/nibankougen/LowPolyWorld/api/internal/middleware"
	"github.com/nibankougen/LowPolyWorld/api/internal/plan"
	"github.com/nibankougen/LowPolyWorld/api/internal/response"
)

type startupUserProfile struct {
	ID                string `json:"id"`
	DisplayName       string `json:"displayName"`
	Name              string `json:"name"`
	NameSetupRequired bool   `json:"nameSetupRequired"`
	Language          string `json:"language"`
	SubscriptionTier  string `json:"subscriptionTier"`
	VivoxID           string `json:"vivoxId"`
	CreatedAt         string `json:"createdAt"`
}

type startupAvatar struct {
	ID               string `json:"id"`
	Name             string `json:"name"`
	VrmURL           string `json:"vrmUrl"`
	TextureURL       string `json:"textureUrl,omitempty"`
	ModerationStatus string `json:"moderationStatus"`
	CreatedAt        string `json:"createdAt"`
}

type startupResponse struct {
	User             startupUserProfile `json:"user"`
	PlanCapabilities plan.Capabilities  `json:"planCapabilities"`
	SecurityNotice   *string            `json:"securityNotice"`
	Avatars          []startupAvatar    `json:"avatars"`
	Worlds           []worldResponse    `json:"worlds"`
}

// GetStartup handles GET /startup — returns everything the client needs to initialise.
func (h *Handler) GetStartup(w http.ResponseWriter, r *http.Request) {
	userID := middleware.UserIDFromContext(r.Context())

	// --- User profile ---
	var user startupUserProfile
	var displayName *string
	var name *string
	var userCreatedAt time.Time
	err := h.DB.QueryRow(r.Context(),
		`SELECT au.user_id, au.display_name, au.name, au.name_setup_required,
		        au.language, au.subscription_tier, au.vivox_id::text, u.created_at
		 FROM active_users au
		 JOIN users u ON u.id = au.user_id
		 WHERE au.user_id = $1 AND au.deleted_at IS NULL`,
		userID,
	).Scan(&user.ID, &displayName, &name, &user.NameSetupRequired,
		&user.Language, &user.SubscriptionTier, &user.VivoxID, &userCreatedAt)
	if err != nil {
		h.Logger.Error("startup: fetch user", "error", err)
		response.InternalError(w, r, h.Cfg.IsProduction())
		return
	}
	if displayName != nil {
		user.DisplayName = *displayName
	}
	if name != nil {
		user.Name = *name
	}
	user.CreatedAt = userCreatedAt.UTC().Format(time.RFC3339)

	caps := plan.GetCapabilities(plan.Tier(user.SubscriptionTier))

	// --- Avatars ---
	avatarRows, err := h.DB.Query(r.Context(),
		`SELECT id, name, vrm_hash, texture_hash, moderation_status, created_at
		 FROM avatars WHERE user_id = $1 ORDER BY created_at DESC`,
		userID,
	)
	if err != nil {
		h.Logger.Error("startup: fetch avatars", "error", err)
		response.InternalError(w, r, h.Cfg.IsProduction())
		return
	}
	defer avatarRows.Close()

	avatars := []startupAvatar{}
	for avatarRows.Next() {
		var av startupAvatar
		var vrmHash string
		var textureHash *string
		var createdAt time.Time
		if err := avatarRows.Scan(&av.ID, &av.Name, &vrmHash, &textureHash, &av.ModerationStatus, &createdAt); err != nil {
			continue
		}
		av.VrmURL = h.Storage.URL(vrmHash, "vrm")
		if textureHash != nil && *textureHash != "" {
			av.TextureURL = h.Storage.URL(*textureHash, "png")
		}
		av.CreatedAt = createdAt.UTC().Format(time.RFC3339)
		avatars = append(avatars, av)
	}

	// --- Worlds (first page of public worlds) ---
	worldRows, err := h.DB.Query(r.Context(),
		`SELECT id, name, description, thumbnail_hash, glb_hash, is_public, max_players, likes_count, created_at
		 FROM worlds WHERE is_public = TRUE
		 ORDER BY created_at DESC
		 LIMIT $1`,
		defaultPageLimit,
	)
	if err != nil {
		h.Logger.Error("startup: fetch worlds", "error", err)
		response.InternalError(w, r, h.Cfg.IsProduction())
		return
	}
	defer worldRows.Close()

	worlds := []worldResponse{}
	for worldRows.Next() {
		var (
			id, name, description string
			thumbnailHash         *string
			glbHash               *string
			isPublic              bool
			maxPlayers, likes     int
			createdAt             time.Time
		)
		if err := worldRows.Scan(&id, &name, &description, &thumbnailHash, &glbHash,
			&isPublic, &maxPlayers, &likes, &createdAt); err != nil {
			continue
		}
		worlds = append(worlds, h.buildWorldResponse(id, name, description, thumbnailHash, glbHash, isPublic, maxPlayers, likes, createdAt))
	}

	response.JSON(w, http.StatusOK, startupResponse{
		User:             user,
		PlanCapabilities: caps,
		SecurityNotice:   nil,
		Avatars:          avatars,
		Worlds:           worlds,
	})
}
