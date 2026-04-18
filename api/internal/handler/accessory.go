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

type accessoryResponse struct {
	ID          string `json:"id"`
	Name        string `json:"name"`
	GlbURL      string `json:"glbUrl"`
	GlbHash     string `json:"glbHash"`
	TextureURL  string `json:"textureUrl,omitempty"`
	TextureHash string `json:"textureHash,omitempty"`
	CreatedAt   string `json:"createdAt"`
}

// ListMyAccessories handles GET /api/v1/me/accessories.
func (h *Handler) ListMyAccessories(w http.ResponseWriter, r *http.Request) {
	userID := middleware.UserIDFromContext(r.Context())

	rows, err := h.DB.Query(r.Context(),
		`SELECT id, name, glb_hash, texture_hash, created_at
		 FROM accessories WHERE user_id = $1 ORDER BY created_at DESC`,
		userID,
	)
	if err != nil {
		h.Logger.Error("list accessories", "error", err)
		response.InternalError(w, r, h.Cfg.IsProduction())
		return
	}
	defer rows.Close()

	accessories := []accessoryResponse{}
	for rows.Next() {
		var ac accessoryResponse
		var glbHash string
		var textureHash *string
		var createdAt time.Time
		if err := rows.Scan(&ac.ID, &ac.Name, &glbHash, &textureHash, &createdAt); err != nil {
			continue
		}
		ac.GlbHash = glbHash
		ac.GlbURL = h.Storage.URL(glbHash, "glb")
		if textureHash != nil && *textureHash != "" {
			ac.TextureHash = *textureHash
			ac.TextureURL = h.Storage.URL(*textureHash, "png")
		}
		ac.CreatedAt = createdAt.UTC().Format(time.RFC3339)
		accessories = append(accessories, ac)
	}

	response.JSON(w, http.StatusOK, accessories)
}

// UploadAccessory handles POST /api/v1/me/accessories — stores a GLB file and creates an accessory record.
// Accepts multipart/form-data with fields: glb (file), name (string, optional).
func (h *Handler) UploadAccessory(w http.ResponseWriter, r *http.Request) {
	userID := middleware.UserIDFromContext(r.Context())

	if err := r.ParseMultipartForm(5 << 20); err != nil {
		response.Error(w, r, http.StatusBadRequest, "validation_error", "invalid multipart form")
		return
	}

	file, _, err := r.FormFile("glb")
	if err != nil {
		response.Error(w, r, http.StatusBadRequest, "validation_error", "glb file required")
		return
	}
	defer file.Close()

	data, err := io.ReadAll(io.LimitReader(file, maxGLBSize+1))
	if err != nil {
		response.Error(w, r, http.StatusBadRequest, "validation_error", "failed to read glb file")
		return
	}
	if len(data) > maxGLBSize {
		response.Error(w, r, http.StatusRequestEntityTooLarge, "file_too_large", "accessory GLB must be 100KB or smaller")
		return
	}

	sum := sha256.Sum256(data)
	hash := hex.EncodeToString(sum[:])

	exists, _ := h.Storage.Exists(r.Context(), hash, "glb")
	if !exists {
		if err := h.Storage.Put(r.Context(), hash, "glb", bytes.NewReader(data)); err != nil {
			h.Logger.Error("store glb", "error", err)
			response.InternalError(w, r, h.Cfg.IsProduction())
			return
		}
	}

	name := r.FormValue("name")
	if name == "" {
		name = "Accessory"
	}

	var accessoryID string
	var createdAt time.Time
	err = h.DB.QueryRow(r.Context(),
		`INSERT INTO accessories (user_id, name, glb_hash)
		 VALUES ($1, $2, $3)
		 RETURNING id, created_at`,
		userID, name, hash,
	).Scan(&accessoryID, &createdAt)
	if err != nil {
		h.Logger.Error("insert accessory", "error", err)
		response.InternalError(w, r, h.Cfg.IsProduction())
		return
	}

	response.JSON(w, http.StatusCreated, accessoryResponse{
		ID:        accessoryID,
		Name:      name,
		GlbHash:   hash,
		GlbURL:    h.Storage.URL(hash, "glb"),
		CreatedAt: createdAt.UTC().Format(time.RFC3339),
	})
}

// DeleteAccessory handles DELETE /api/v1/me/accessories/{accessoryID}.
func (h *Handler) DeleteAccessory(w http.ResponseWriter, r *http.Request) {
	userID := middleware.UserIDFromContext(r.Context())
	accessoryID := chi.URLParam(r, "accessoryID")

	tag, err := h.DB.Exec(r.Context(),
		`DELETE FROM accessories WHERE id = $1 AND user_id = $2`,
		accessoryID, userID,
	)
	if err != nil {
		h.Logger.Error("delete accessory", "error", err)
		response.InternalError(w, r, h.Cfg.IsProduction())
		return
	}
	if tag.RowsAffected() == 0 {
		response.Error(w, r, http.StatusNotFound, "not_found", "accessory not found")
		return
	}

	w.WriteHeader(http.StatusNoContent)
}

// UpdateAccessoryTexture handles PUT /api/v1/me/accessories/{accessoryID}/texture.
func (h *Handler) UpdateAccessoryTexture(w http.ResponseWriter, r *http.Request) {
	userID := middleware.UserIDFromContext(r.Context())
	accessoryID := chi.URLParam(r, "accessoryID")

	var exists bool
	_ = h.DB.QueryRow(r.Context(),
		`SELECT EXISTS(SELECT 1 FROM accessories WHERE id = $1 AND user_id = $2)`,
		accessoryID, userID,
	).Scan(&exists)
	if !exists {
		response.Error(w, r, http.StatusNotFound, "not_found", "accessory not found")
		return
	}

	if err := r.ParseMultipartForm(1 << 20); err != nil {
		response.Error(w, r, http.StatusBadRequest, "validation_error", "invalid multipart form")
		return
	}

	file, _, err := r.FormFile("texture")
	if err != nil {
		response.Error(w, r, http.StatusBadRequest, "validation_error", "texture file required")
		return
	}
	defer file.Close()

	const maxAccTextureSize = 64 * 1024 // 64KB for 64×64 PNG
	data, err := io.ReadAll(io.LimitReader(file, maxAccTextureSize+1))
	if err != nil || len(data) > maxAccTextureSize {
		response.Error(w, r, http.StatusRequestEntityTooLarge, "file_too_large", "accessory texture must be 64KB or smaller")
		return
	}

	sum := sha256.Sum256(data)
	hash := hex.EncodeToString(sum[:])

	exists2, _ := h.Storage.Exists(r.Context(), hash, "png")
	if !exists2 {
		if err := h.Storage.Put(r.Context(), hash, "png", bytes.NewReader(data)); err != nil {
			h.Logger.Error("store accessory texture", "error", err)
			response.InternalError(w, r, h.Cfg.IsProduction())
			return
		}
	}

	_, err = h.DB.Exec(r.Context(),
		`UPDATE accessories SET texture_hash = $1 WHERE id = $2`,
		hash, accessoryID,
	)
	if err != nil {
		h.Logger.Error("update accessory texture_hash", "error", err)
		response.InternalError(w, r, h.Cfg.IsProduction())
		return
	}

	response.JSON(w, http.StatusOK, map[string]string{
		"textureHash": hash,
		"textureUrl":  h.Storage.URL(hash, "png"),
	})
}
