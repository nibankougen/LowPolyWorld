package handler

import (
	"bytes"
	"crypto/sha256"
	"encoding/hex"
	"io"
	"net/http"
	"time"

	"github.com/go-chi/chi/v5"
	"github.com/nibankougen/LowPolyWorld/api/internal/middleware"
	"github.com/nibankougen/LowPolyWorld/api/internal/response"
)

const (
	maxVRMSize     = 500 * 1024  // 500KB
	maxTextureSize = 512 * 1024  // 512KB
	maxGLBSize     = 100 * 1024  // 100KB (accessory)
)

type avatarResponse struct {
	ID               string `json:"id"`
	Name             string `json:"name"`
	VrmURL           string `json:"vrmUrl"`
	VrmHash          string `json:"vrmHash"`
	TextureURL       string `json:"textureUrl,omitempty"`
	TextureHash      string `json:"textureHash,omitempty"`
	ModerationStatus string `json:"moderationStatus"`
	CreatedAt        string `json:"createdAt"`
}

// ListMyAvatars handles GET /api/v1/me/avatars — returns all avatars owned by the authenticated user.
func (h *Handler) ListMyAvatars(w http.ResponseWriter, r *http.Request) {
	userID := middleware.UserIDFromContext(r.Context())

	rows, err := h.DB.Query(r.Context(),
		`SELECT id, name, vrm_hash, texture_hash, moderation_status, created_at
		 FROM avatars WHERE user_id = $1 ORDER BY created_at DESC`,
		userID,
	)
	if err != nil {
		h.Logger.Error("list avatars", "error", err)
		response.InternalError(w, r, h.Cfg.IsProduction())
		return
	}
	defer rows.Close()

	avatars := []avatarResponse{}
	for rows.Next() {
		var av avatarResponse
		var vrmHash string
		var textureHash *string
		var createdAt time.Time
		if err := rows.Scan(&av.ID, &av.Name, &vrmHash, &textureHash, &av.ModerationStatus, &createdAt); err != nil {
			continue
		}
		av.VrmHash = vrmHash
		av.VrmURL = h.Storage.URL(vrmHash, "vrm")
		if textureHash != nil && *textureHash != "" {
			av.TextureHash = *textureHash
			av.TextureURL = h.Storage.URL(*textureHash, "png")
		}
		av.CreatedAt = createdAt.UTC().Format(time.RFC3339)
		avatars = append(avatars, av)
	}

	response.JSON(w, http.StatusOK, avatars)
}

// UploadAvatar handles POST /api/v1/me/avatars — stores a VRM file and creates an avatar record.
// Accepts multipart/form-data with fields: vrm (file), name (string, optional).
func (h *Handler) UploadAvatar(w http.ResponseWriter, r *http.Request) {
	userID := middleware.UserIDFromContext(r.Context())

	if err := r.ParseMultipartForm(10 << 20); err != nil {
		response.Error(w, r, http.StatusBadRequest, "validation_error", "invalid multipart form")
		return
	}

	file, _, err := r.FormFile("vrm")
	if err != nil {
		response.Error(w, r, http.StatusBadRequest, "validation_error", "vrm file required")
		return
	}
	defer file.Close()

	data, err := io.ReadAll(io.LimitReader(file, maxVRMSize+1))
	if err != nil {
		response.Error(w, r, http.StatusBadRequest, "validation_error", "failed to read vrm file")
		return
	}
	if len(data) > maxVRMSize {
		response.Error(w, r, http.StatusRequestEntityTooLarge, "file_too_large", "VRM file must be 500KB or smaller")
		return
	}

	sum := sha256.Sum256(data)
	hash := hex.EncodeToString(sum[:])

	exists, _ := h.Storage.Exists(r.Context(), hash, "vrm")
	if !exists {
		if err := h.Storage.Put(r.Context(), hash, "vrm", bytes.NewReader(data)); err != nil {
			h.Logger.Error("store vrm", "error", err)
			response.InternalError(w, r, h.Cfg.IsProduction())
			return
		}
	}

	name := r.FormValue("name")
	if name == "" {
		name = "Avatar"
	}

	var avatarID string
	var createdAt time.Time
	err = h.DB.QueryRow(r.Context(),
		`INSERT INTO avatars (user_id, name, vrm_hash, moderation_status)
		 VALUES ($1, $2, $3, 'pending')
		 RETURNING id, created_at`,
		userID, name, hash,
	).Scan(&avatarID, &createdAt)
	if err != nil {
		h.Logger.Error("insert avatar", "error", err)
		response.InternalError(w, r, h.Cfg.IsProduction())
		return
	}

	response.JSON(w, http.StatusCreated, avatarResponse{
		ID:               avatarID,
		Name:             name,
		VrmHash:          hash,
		VrmURL:           h.Storage.URL(hash, "vrm"),
		ModerationStatus: "pending",
		CreatedAt:        createdAt.UTC().Format(time.RFC3339),
	})
}

// DeleteAvatar handles DELETE /api/v1/me/avatars/{avatarID}.
func (h *Handler) DeleteAvatar(w http.ResponseWriter, r *http.Request) {
	userID := middleware.UserIDFromContext(r.Context())
	avatarID := chi.URLParam(r, "avatarID")

	tag, err := h.DB.Exec(r.Context(),
		`DELETE FROM avatars WHERE id = $1 AND user_id = $2`,
		avatarID, userID,
	)
	if err != nil {
		h.Logger.Error("delete avatar", "error", err)
		response.InternalError(w, r, h.Cfg.IsProduction())
		return
	}
	if tag.RowsAffected() == 0 {
		response.Error(w, r, http.StatusNotFound, "not_found", "avatar not found")
		return
	}

	w.WriteHeader(http.StatusNoContent)
}

// UpdateAvatarTexture handles PUT /api/v1/me/avatars/{avatarID}/texture — uploads a composite PNG texture.
// Accepts multipart/form-data with field: texture (file).
func (h *Handler) UpdateAvatarTexture(w http.ResponseWriter, r *http.Request) {
	userID := middleware.UserIDFromContext(r.Context())
	avatarID := chi.URLParam(r, "avatarID")

	var avatarExists bool
	_ = h.DB.QueryRow(r.Context(),
		`SELECT EXISTS(SELECT 1 FROM avatars WHERE id = $1 AND user_id = $2)`,
		avatarID, userID,
	).Scan(&avatarExists)
	if !avatarExists {
		response.Error(w, r, http.StatusNotFound, "not_found", "avatar not found")
		return
	}

	if err := r.ParseMultipartForm(2 << 20); err != nil {
		response.Error(w, r, http.StatusBadRequest, "validation_error", "invalid multipart form")
		return
	}

	file, _, err := r.FormFile("texture")
	if err != nil {
		response.Error(w, r, http.StatusBadRequest, "validation_error", "texture file required")
		return
	}
	defer file.Close()

	data, err := io.ReadAll(io.LimitReader(file, maxTextureSize+1))
	if err != nil || len(data) > maxTextureSize {
		response.Error(w, r, http.StatusRequestEntityTooLarge, "file_too_large", "texture file must be 512KB or smaller")
		return
	}

	sum := sha256.Sum256(data)
	hash := hex.EncodeToString(sum[:])

	exists, _ := h.Storage.Exists(r.Context(), hash, "png")
	if !exists {
		if err := h.Storage.Put(r.Context(), hash, "png", bytes.NewReader(data)); err != nil {
			h.Logger.Error("store texture", "error", err)
			response.InternalError(w, r, h.Cfg.IsProduction())
			return
		}
	}

	_, err = h.DB.Exec(r.Context(),
		`UPDATE avatars SET texture_hash = $1 WHERE id = $2`,
		hash, avatarID,
	)
	if err != nil {
		h.Logger.Error("update avatar texture_hash", "error", err)
		response.InternalError(w, r, h.Cfg.IsProduction())
		return
	}

	response.JSON(w, http.StatusOK, map[string]string{
		"textureHash": hash,
		"textureUrl":  h.Storage.URL(hash, "png"),
	})
}
