package handler

import (
	"net/http"
	"strconv"
	"strings"
	"time"

	"github.com/go-chi/chi/v5"
	"github.com/nibankougen/LowPolyWorld/api/internal/middleware"
	"github.com/nibankougen/LowPolyWorld/api/internal/response"
	"golang.org/x/text/unicode/norm"
)

const defaultPageLimit = 20
const maxPageLimit = 50

type worldResponse struct {
	ID           string `json:"id"`
	Name         string `json:"name"`
	Description  string `json:"description"`
	ThumbnailURL string `json:"thumbnailUrl"`
	GlbURL       string `json:"glbUrl"`
	IsPublic     bool   `json:"isPublic"`
	MaxPlayers   int    `json:"maxPlayers"`
	LikesCount   int    `json:"likesCount"`
	CreatedAt    string `json:"createdAt"`
}

// buildWorldResponse converts raw DB fields into a worldResponse.
func (h *Handler) buildWorldResponse(
	id, name, description string,
	thumbnailHash, glbHash *string,
	isPublic bool,
	maxPlayers, likesCount int,
	createdAt time.Time,
) worldResponse {
	wr := worldResponse{
		ID:          id,
		Name:        name,
		Description: description,
		IsPublic:    isPublic,
		MaxPlayers:  maxPlayers,
		LikesCount:  likesCount,
		CreatedAt:   createdAt.UTC().Format(time.RFC3339),
	}
	if thumbnailHash != nil && *thumbnailHash != "" {
		wr.ThumbnailURL = h.Storage.URL(*thumbnailHash, "png")
	}
	if glbHash != nil && *glbHash != "" {
		wr.GlbURL = h.Storage.URL(*glbHash, "glb")
	}
	return wr
}

// ListNewWorlds handles GET /api/v1/worlds/new — public worlds, newest first.
func (h *Handler) ListNewWorlds(w http.ResponseWriter, r *http.Request) {
	limit := parseLimit(r)
	after := r.URL.Query().Get("after")

	var err error
	var pgrows worldRows

	if after == "" {
		pgrows, err = h.DB.Query(r.Context(),
			`SELECT id, name, description, thumbnail_hash, glb_hash, is_public, max_players, likes_count, created_at
			 FROM worlds WHERE is_public = TRUE
			 ORDER BY created_at DESC, id DESC
			 LIMIT $1`,
			limit+1,
		)
	} else {
		// Keyset pagination: fetch rows older than the cursor world (by created_at, then id as tiebreak)
		pgrows, err = h.DB.Query(r.Context(),
			`SELECT id, name, description, thumbnail_hash, glb_hash, is_public, max_players, likes_count, created_at
			 FROM worlds
			 WHERE is_public = TRUE
			   AND (created_at, id) < (
			       SELECT created_at, id FROM worlds WHERE id = $1
			   )
			 ORDER BY created_at DESC, id DESC
			 LIMIT $2`,
			after, limit+1,
		)
	}
	if err != nil {
		h.Logger.Error("list new worlds", "error", err)
		response.InternalError(w, r, h.Cfg.IsProduction())
		return
	}
	defer pgrows.Close()

	worlds, cursor := h.scanWorldRows(pgrows, limit)
	response.JSONCursor(w, http.StatusOK, worlds, cursor)
}

// ListLikedWorlds handles GET /api/v1/worlds/liked — worlds liked by the current user.
func (h *Handler) ListLikedWorlds(w http.ResponseWriter, r *http.Request) {
	userID := middleware.UserIDFromContext(r.Context())
	limit := parseLimit(r)
	after := r.URL.Query().Get("after")

	var err error
	var pgrows worldRows

	if after == "" {
		pgrows, err = h.DB.Query(r.Context(),
			`SELECT w.id, w.name, w.description, w.thumbnail_hash, w.glb_hash,
			        w.is_public, w.max_players, w.likes_count, w.created_at
			 FROM worlds w
			 JOIN world_likes wl ON wl.world_id = w.id
			 WHERE wl.user_id = $1
			 ORDER BY wl.created_at DESC, w.id DESC
			 LIMIT $2`,
			userID, limit+1,
		)
	} else {
		// Keyset pagination on (wl.created_at, w.id)
		pgrows, err = h.DB.Query(r.Context(),
			`SELECT w.id, w.name, w.description, w.thumbnail_hash, w.glb_hash,
			        w.is_public, w.max_players, w.likes_count, w.created_at
			 FROM worlds w
			 JOIN world_likes wl ON wl.world_id = w.id
			 WHERE wl.user_id = $1
			   AND (wl.created_at, w.id) < (
			       SELECT wl2.created_at, wl2.world_id FROM world_likes wl2 WHERE wl2.world_id = $2 AND wl2.user_id = $1
			   )
			 ORDER BY wl.created_at DESC, w.id DESC
			 LIMIT $3`,
			userID, after, limit+1,
		)
	}
	if err != nil {
		h.Logger.Error("list liked worlds", "error", err)
		response.InternalError(w, r, h.Cfg.IsProduction())
		return
	}
	defer pgrows.Close()

	worlds, cursor := h.scanWorldRows(pgrows, limit)
	response.JSONCursor(w, http.StatusOK, worlds, cursor)
}

// ListFollowingWorlds handles GET /api/v1/worlds/following — stub (follow system in later phase).
func (h *Handler) ListFollowingWorlds(w http.ResponseWriter, r *http.Request) {
	response.JSONCursor(w, http.StatusOK, []worldResponse{}, response.Cursor{Next: "", HasMore: false})
}

// SearchWorlds handles GET /api/v1/worlds/search?q=<name>&tags=<t1,t2>&limit=n&after=<cursor>.
// Searches public worlds by name (pg_trgm ILIKE) and/or tags (AND match across world_tags).
// Both q and tags are optional; omitting both returns all public worlds newest-first.
func (h *Handler) SearchWorlds(w http.ResponseWriter, r *http.Request) {
	q := strings.TrimSpace(r.URL.Query().Get("q"))
	tagsParam := r.URL.Query().Get("tags")
	limit := parseLimit(r)
	after := r.URL.Query().Get("after")

	// Normalize and deduplicate tags
	var tags []string
	if tagsParam != "" {
		seen := map[string]bool{}
		for _, raw := range strings.Split(tagsParam, ",") {
			t := normalizeTag(raw)
			if t != "" && !seen[t] {
				tags = append(tags, t)
				seen[t] = true
			}
		}
	}
	tagCount := len(tags)

	// Filter banned tags from query (don't error — silently ignore)
	if tagCount > 0 {
		bannedRows, err := h.DB.Query(r.Context(),
			`SELECT tag_normalized FROM tag_ban_list WHERE tag_normalized = ANY($1)`,
			tags,
		)
		if err == nil {
			banned := map[string]bool{}
			for bannedRows.Next() {
				var b string
				if bannedRows.Scan(&b) == nil {
					banned[b] = true
				}
			}
			bannedRows.Close()
			filtered := tags[:0]
			for _, t := range tags {
				if !banned[t] {
					filtered = append(filtered, t)
				}
			}
			tags = filtered
			tagCount = len(tags)
		}
	}

	// Build WHERE conditions
	// Cursor: (created_at, id) < cursor row
	var (
		pgrows worldRows
		err    error
	)

	if tagCount == 0 {
		// Name-only search (or no filter)
		if after == "" {
			pgrows, err = h.DB.Query(r.Context(),
				`SELECT id, name, description, thumbnail_hash, glb_hash, is_public, max_players, likes_count, created_at
				 FROM worlds
				 WHERE is_public = TRUE
				   AND ($1 = '' OR name ILIKE '%' || $1 || '%')
				 ORDER BY created_at DESC, id DESC
				 LIMIT $2`,
				q, limit+1,
			)
		} else {
			pgrows, err = h.DB.Query(r.Context(),
				`SELECT id, name, description, thumbnail_hash, glb_hash, is_public, max_players, likes_count, created_at
				 FROM worlds
				 WHERE is_public = TRUE
				   AND ($1 = '' OR name ILIKE '%' || $1 || '%')
				   AND (created_at, id) < (SELECT created_at, id FROM worlds WHERE id = $2)
				 ORDER BY created_at DESC, id DESC
				 LIMIT $3`,
				q, after, limit+1,
			)
		}
	} else {
		// Name + tag AND search
		if after == "" {
			pgrows, err = h.DB.Query(r.Context(),
				`SELECT w.id, w.name, w.description, w.thumbnail_hash, w.glb_hash,
				        w.is_public, w.max_players, w.likes_count, w.created_at
				 FROM worlds w
				 JOIN world_tags wt ON wt.world_id = w.id AND wt.tag_normalized = ANY($1)
				 WHERE w.is_public = TRUE
				   AND ($2 = '' OR w.name ILIKE '%' || $2 || '%')
				 GROUP BY w.id, w.name, w.description, w.thumbnail_hash, w.glb_hash,
				          w.is_public, w.max_players, w.likes_count, w.created_at
				 HAVING COUNT(DISTINCT wt.tag_normalized) = $3
				 ORDER BY w.created_at DESC, w.id DESC
				 LIMIT $4`,
				tags, q, tagCount, limit+1,
			)
		} else {
			pgrows, err = h.DB.Query(r.Context(),
				`SELECT w.id, w.name, w.description, w.thumbnail_hash, w.glb_hash,
				        w.is_public, w.max_players, w.likes_count, w.created_at
				 FROM worlds w
				 JOIN world_tags wt ON wt.world_id = w.id AND wt.tag_normalized = ANY($1)
				 WHERE w.is_public = TRUE
				   AND ($2 = '' OR w.name ILIKE '%' || $2 || '%')
				   AND (w.created_at, w.id) < (SELECT created_at, id FROM worlds WHERE id = $3)
				 GROUP BY w.id, w.name, w.description, w.thumbnail_hash, w.glb_hash,
				          w.is_public, w.max_players, w.likes_count, w.created_at
				 HAVING COUNT(DISTINCT wt.tag_normalized) = $4
				 ORDER BY w.created_at DESC, w.id DESC
				 LIMIT $5`,
				tags, q, after, tagCount, limit+1,
			)
		}
	}
	if err != nil {
		h.Logger.Error("search worlds", "error", err)
		response.InternalError(w, r, h.Cfg.IsProduction())
		return
	}
	defer pgrows.Close()

	worlds, cursor := h.scanWorldRows(pgrows, limit)
	response.JSONCursor(w, http.StatusOK, worlds, cursor)
}

// normalizeTag applies NFKC normalization, lowercasing, and trimming to a tag string.
// Returns empty string if the result is blank.
func normalizeTag(raw string) string {
	n := norm.NFKC.String(strings.TrimSpace(raw))
	return strings.ToLower(n)
}

// GetWorld handles GET /api/v1/worlds/{worldID} — returns a single public world's details.
func (h *Handler) GetWorld(w http.ResponseWriter, r *http.Request) {
	worldID := chi.URLParam(r, "worldID")

	var (
		id, name, description string
		thumbnailHash         *string
		glbHash               *string
		isPublic              bool
		maxPlayers, likes     int
		createdAt             time.Time
	)
	err := h.DB.QueryRow(r.Context(),
		`SELECT id, name, description, thumbnail_hash, glb_hash, is_public, max_players, likes_count, created_at
		 FROM worlds WHERE id = $1 AND is_public = TRUE`,
		worldID,
	).Scan(&id, &name, &description, &thumbnailHash, &glbHash, &isPublic, &maxPlayers, &likes, &createdAt)
	if err != nil {
		response.Error(w, r, http.StatusNotFound, "not_found", "world not found")
		return
	}

	response.JSON(w, http.StatusOK, h.buildWorldResponse(id, name, description, thumbnailHash, glbHash, isPublic, maxPlayers, likes, createdAt))
}

// LikeWorld handles POST /api/v1/worlds/{worldID}/like.
func (h *Handler) LikeWorld(w http.ResponseWriter, r *http.Request) {
	userID := middleware.UserIDFromContext(r.Context())
	worldID := chi.URLParam(r, "worldID")

	// Verify world exists and is public; retrieve owner for self-like check
	var ownerUserID string
	err := h.DB.QueryRow(r.Context(),
		`SELECT owner_user_id FROM worlds WHERE id = $1 AND is_public = TRUE`, worldID,
	).Scan(&ownerUserID)
	if err != nil {
		response.Error(w, r, http.StatusNotFound, "not_found", "world not found")
		return
	}

	if ownerUserID == userID {
		response.Error(w, r, http.StatusForbidden, "forbidden", "cannot like your own world")
		return
	}

	tag, err := h.DB.Exec(r.Context(),
		`INSERT INTO world_likes (world_id, user_id) VALUES ($1, $2) ON CONFLICT DO NOTHING`,
		worldID, userID,
	)
	if err != nil {
		h.Logger.Error("like world", "error", err)
		response.InternalError(w, r, h.Cfg.IsProduction())
		return
	}
	if tag.RowsAffected() == 0 {
		response.Error(w, r, http.StatusConflict, "already_liked", "already liked this world")
		return
	}

	_, _ = h.DB.Exec(r.Context(),
		`UPDATE worlds SET likes_count = likes_count + 1 WHERE id = $1`, worldID,
	)

	w.WriteHeader(http.StatusNoContent)
}

// UnlikeWorld handles DELETE /api/v1/worlds/{worldID}/like.
func (h *Handler) UnlikeWorld(w http.ResponseWriter, r *http.Request) {
	userID := middleware.UserIDFromContext(r.Context())
	worldID := chi.URLParam(r, "worldID")

	tag, err := h.DB.Exec(r.Context(),
		`DELETE FROM world_likes WHERE world_id = $1 AND user_id = $2`,
		worldID, userID,
	)
	if err != nil {
		h.Logger.Error("unlike world", "error", err)
		response.InternalError(w, r, h.Cfg.IsProduction())
		return
	}
	if tag.RowsAffected() == 0 {
		response.Error(w, r, http.StatusNotFound, "not_found", "like not found")
		return
	}

	_, _ = h.DB.Exec(r.Context(),
		`UPDATE worlds SET likes_count = GREATEST(0, likes_count - 1) WHERE id = $1`, worldID,
	)

	w.WriteHeader(http.StatusNoContent)
}

type worldRows interface {
	Next() bool
	Scan(...any) error
	Err() error
	Close()
}

// scanWorldRows reads up to limit world rows, returning the list and cursor.
func (h *Handler) scanWorldRows(rows worldRows, limit int) ([]worldResponse, response.Cursor) {
	var worlds []worldResponse
	for rows.Next() {
		var (
			id, name, description string
			thumbnailHash         *string
			glbHash               *string
			isPublic              bool
			maxPlayers, likes     int
			createdAt             time.Time
		)
		if err := rows.Scan(&id, &name, &description, &thumbnailHash, &glbHash,
			&isPublic, &maxPlayers, &likes, &createdAt); err != nil {
			continue
		}
		worlds = append(worlds, h.buildWorldResponse(id, name, description, thumbnailHash, glbHash, isPublic, maxPlayers, likes, createdAt))
	}

	cursor := response.Cursor{}
	if len(worlds) > limit {
		worlds = worlds[:limit]
		cursor.HasMore = true
		cursor.Next = worlds[len(worlds)-1].ID
	}
	return worlds, cursor
}

// parseLimit reads the "limit" query parameter, clamping to [1, maxPageLimit].
func parseLimit(r *http.Request) int {
	s := r.URL.Query().Get("limit")
	if s == "" {
		return defaultPageLimit
	}
	n, err := strconv.Atoi(s)
	if err != nil || n < 1 {
		return defaultPageLimit
	}
	if n > maxPageLimit {
		return maxPageLimit
	}
	return n
}
