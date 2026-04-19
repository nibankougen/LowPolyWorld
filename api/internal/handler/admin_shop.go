package handler

import (
	"bytes"
	"crypto/sha256"
	"encoding/hex"
	"encoding/json"
	"io"
	"net/http"
	"strconv"
	"strings"
	"time"

	"github.com/go-chi/chi/v5"
	"github.com/nibankougen/LowPolyWorld/api/internal/middleware"
	"github.com/nibankougen/LowPolyWorld/api/internal/response"
)

// ── Creator management ──────────────────────────────────────────────────────

type adminCreatorRow struct {
	ID          string    `json:"id"`
	UserID      string    `json:"userId"`
	DisplayName string    `json:"displayName"`
	Bio         string    `json:"bio"`
	CreatedAt   time.Time `json:"createdAt"`
}

// AdminListCreators handles GET /admin/creators.
func (h *Handler) AdminListCreators(w http.ResponseWriter, r *http.Request) {
	after := r.URL.Query().Get("after")
	limit := 50

	rows, err := h.DB.Query(r.Context(),
		`SELECT id, user_id, display_name, bio, created_at
		 FROM creators
		 WHERE ($1 = '' OR created_at < (SELECT created_at FROM creators WHERE id = $1::UUID))
		 ORDER BY created_at DESC
		 LIMIT $2`,
		after, limit+1,
	)
	if err != nil {
		h.Logger.Error("admin list creators", "error", err)
		response.InternalError(w, r, h.Cfg.IsProduction())
		return
	}
	defer rows.Close()

	var creators []adminCreatorRow
	for rows.Next() {
		var c adminCreatorRow
		if err := rows.Scan(&c.ID, &c.UserID, &c.DisplayName, &c.Bio, &c.CreatedAt); err != nil {
			continue
		}
		creators = append(creators, c)
	}

	var cur response.Cursor
	if len(creators) > limit {
		creators = creators[:limit]
		cur.Next = creators[len(creators)-1].ID
	}

	response.JSON(w, http.StatusOK, map[string]any{
		"creators": creators,
		"cursor":   cur,
	})
}

// AdminCreateCreator handles POST /admin/creators.
// Body: { "userId": "...", "displayName": "...", "bio": "..." }
func (h *Handler) AdminCreateCreator(w http.ResponseWriter, r *http.Request) {
	var req struct {
		UserID      string `json:"userId"`
		DisplayName string `json:"displayName"`
		Bio         string `json:"bio"`
	}
	if err := json.NewDecoder(r.Body).Decode(&req); err != nil {
		response.Error(w, r, http.StatusBadRequest, "validation_error", "invalid JSON body")
		return
	}
	if req.UserID == "" {
		response.Error(w, r, http.StatusBadRequest, "validation_error", "userId is required")
		return
	}
	if !isValidUUID(req.UserID) {
		response.Error(w, r, http.StatusBadRequest, "validation_error", "userId must be a valid UUID")
		return
	}

	var c adminCreatorRow
	err := h.DB.QueryRow(r.Context(),
		`INSERT INTO creators (user_id, display_name, bio)
		 VALUES ($1, $2, $3)
		 RETURNING id, user_id, display_name, bio, created_at`,
		req.UserID, req.DisplayName, req.Bio,
	).Scan(&c.ID, &c.UserID, &c.DisplayName, &c.Bio, &c.CreatedAt)
	if err != nil {
		if strings.Contains(err.Error(), "unique") || strings.Contains(err.Error(), "duplicate") {
			response.Error(w, r, http.StatusConflict, "already_exists", "creator already exists for this user")
			return
		}
		h.Logger.Error("admin create creator", "error", err)
		response.InternalError(w, r, h.Cfg.IsProduction())
		return
	}

	response.JSON(w, http.StatusCreated, c)
}

// AdminUpdateCreator handles PATCH /admin/creators/{creatorID}.
func (h *Handler) AdminUpdateCreator(w http.ResponseWriter, r *http.Request) {
	creatorID := chi.URLParam(r, "creatorID")

	var req struct {
		DisplayName *string `json:"displayName"`
		Bio         *string `json:"bio"`
	}
	if err := json.NewDecoder(r.Body).Decode(&req); err != nil {
		response.Error(w, r, http.StatusBadRequest, "validation_error", "invalid JSON body")
		return
	}

	tag, err := h.DB.Exec(r.Context(),
		`UPDATE creators
		 SET display_name = COALESCE($2, display_name),
		     bio          = COALESCE($3, bio),
		     updated_at   = now()
		 WHERE id = $1`,
		creatorID, req.DisplayName, req.Bio,
	)
	if err != nil {
		h.Logger.Error("admin update creator", "error", err)
		response.InternalError(w, r, h.Cfg.IsProduction())
		return
	}
	if tag.RowsAffected() == 0 {
		response.Error(w, r, http.StatusNotFound, "not_found", "creator not found")
		return
	}

	w.WriteHeader(http.StatusNoContent)
}

// ── Product management ──────────────────────────────────────────────────────

type adminProductRow struct {
	ID                   string    `json:"id"`
	CreatorID            string    `json:"creatorId"`
	Name                 string    `json:"name"`
	Description          string    `json:"description"`
	Category             string    `json:"category"`
	PriceCoins           int       `json:"priceCoins"`
	AssetHash            string    `json:"assetHash"`
	AssetURL             string    `json:"assetUrl"`
	ThumbnailHash        *string   `json:"thumbnailHash,omitempty"`
	ThumbnailURL         *string   `json:"thumbnailUrl,omitempty"`
	TextureCost          *int      `json:"textureCost,omitempty"`
	ColliderSizeCategory *string   `json:"colliderSizeCategory,omitempty"`
	EditAllowed          bool      `json:"editAllowed"`
	IsPublished          bool      `json:"isPublished"`
	Tags                 []string  `json:"tags"`
	CreatedAt            time.Time `json:"createdAt"`
}

const maxProductAssetSize = 2 * 1024 * 1024 // 2MB for shop products (GLB/VRM)
const maxProductThumbSize = 512 * 1024       // 512KB thumbnail

// AdminListProducts handles GET /admin/products.
// Query: category, is_published, after (cursor by created_at)
func (h *Handler) AdminListProducts(w http.ResponseWriter, r *http.Request) {
	categoryFilter := r.URL.Query().Get("category")
	isPublishedFilter := r.URL.Query().Get("is_published")
	after := r.URL.Query().Get("after")
	limit := 50

	var publishedArg *bool
	if isPublishedFilter == "true" {
		v := true
		publishedArg = &v
	} else if isPublishedFilter == "false" {
		v := false
		publishedArg = &v
	}

	rows, err := h.DB.Query(r.Context(),
		`SELECT id, creator_id, name, description, category, price_coins,
		        asset_hash, thumbnail_hash, texture_cost, collider_size_category,
		        edit_allowed, is_published, tags, created_at
		 FROM products
		 WHERE ($1 = '' OR category = $1)
		   AND ($2::boolean IS NULL OR is_published = $2)
		   AND ($3 = '' OR created_at < (SELECT created_at FROM products WHERE id = $3::UUID))
		 ORDER BY created_at DESC
		 LIMIT $4`,
		categoryFilter, publishedArg, after, limit+1,
	)
	if err != nil {
		h.Logger.Error("admin list products", "error", err)
		response.InternalError(w, r, h.Cfg.IsProduction())
		return
	}
	defer rows.Close()

	var products []adminProductRow
	for rows.Next() {
		var p adminProductRow
		if err := rows.Scan(
			&p.ID, &p.CreatorID, &p.Name, &p.Description, &p.Category, &p.PriceCoins,
			&p.AssetHash, &p.ThumbnailHash, &p.TextureCost, &p.ColliderSizeCategory,
			&p.EditAllowed, &p.IsPublished, &p.Tags, &p.CreatedAt,
		); err != nil {
			continue
		}
		ext := assetExtForCategory(p.Category)
		p.AssetURL = h.Storage.URL(p.AssetHash, ext)
		if p.ThumbnailHash != nil {
			u := h.Storage.URL(*p.ThumbnailHash, "png")
			p.ThumbnailURL = &u
		}
		products = append(products, p)
	}

	var cur response.Cursor
	if len(products) > limit {
		products = products[:limit]
		cur.Next = products[len(products)-1].ID
	}

	response.JSON(w, http.StatusOK, map[string]any{
		"products": products,
		"cursor":   cur,
	})
}

// AdminCreateProduct handles POST /admin/products.
// Accepts multipart/form-data:
//
//	asset      (file)   — GLB or VRM asset
//	thumbnail  (file)   — PNG thumbnail (optional)
//	creatorId  (string)
//	name       (string)
//	description (string, optional)
//	category   (string) — avatar | accessory | world_object | stamp
//	priceCoins (int)
//	textureCost (int, optional, world_object only)
//	colliderSizeCategory (string, optional)
//	editAllowed (bool, default true)
//	tags       (comma-separated string, optional)
func (h *Handler) AdminCreateProduct(w http.ResponseWriter, r *http.Request) {
	if err := r.ParseMultipartForm(20 << 20); err != nil {
		response.Error(w, r, http.StatusBadRequest, "validation_error", "invalid multipart form")
		return
	}

	// Required text fields
	creatorID := r.FormValue("creatorId")
	name := r.FormValue("name")
	category := r.FormValue("category")
	priceStr := r.FormValue("priceCoins")

	if creatorID == "" || name == "" || category == "" || priceStr == "" {
		response.Error(w, r, http.StatusBadRequest, "validation_error", "creatorId, name, category, priceCoins are required")
		return
	}
	validCategories := map[string]bool{"avatar": true, "accessory": true, "world_object": true, "stamp": true}
	if !validCategories[category] {
		response.Error(w, r, http.StatusBadRequest, "validation_error", "category must be avatar, accessory, world_object, or stamp")
		return
	}
	priceCoins, err := strconv.Atoi(priceStr)
	if err != nil || priceCoins < 0 {
		response.Error(w, r, http.StatusBadRequest, "validation_error", "priceCoins must be a non-negative integer")
		return
	}

	// Optional fields
	description := r.FormValue("description")
	editAllowed := r.FormValue("editAllowed") != "false"

	var textureCost *int
	if v := r.FormValue("textureCost"); v != "" {
		tc, err := strconv.Atoi(v)
		if err != nil || tc < 0 {
			response.Error(w, r, http.StatusBadRequest, "validation_error", "textureCost must be a non-negative integer")
			return
		}
		textureCost = &tc
	}
	var colliderSize *string
	if v := r.FormValue("colliderSizeCategory"); v != "" {
		validSizes := map[string]bool{"small": true, "medium": true, "large": true}
		if !validSizes[v] {
			response.Error(w, r, http.StatusBadRequest, "validation_error", "colliderSizeCategory must be small, medium, or large")
			return
		}
		colliderSize = &v
	}
	var tags []string
	if v := r.FormValue("tags"); v != "" {
		for _, t := range strings.Split(v, ",") {
			t = strings.TrimSpace(t)
			if t != "" {
				tags = append(tags, t)
			}
		}
	}
	if tags == nil {
		tags = []string{}
	}

	// Asset file (required)
	assetFile, _, err := r.FormFile("asset")
	if err != nil {
		response.Error(w, r, http.StatusBadRequest, "validation_error", "asset file required")
		return
	}
	defer assetFile.Close()

	assetData, err := io.ReadAll(io.LimitReader(assetFile, int64(maxProductAssetSize)+1))
	if err != nil {
		response.Error(w, r, http.StatusBadRequest, "validation_error", "failed to read asset file")
		return
	}
	if len(assetData) > maxProductAssetSize {
		response.Error(w, r, http.StatusRequestEntityTooLarge, "file_too_large", "asset file must be 2MB or smaller")
		return
	}
	assetSum := sha256.Sum256(assetData)
	assetHash := hex.EncodeToString(assetSum[:])
	assetExt := assetExtForCategory(category)

	if exists, _ := h.Storage.Exists(r.Context(), assetHash, assetExt); !exists {
		if err := h.Storage.Put(r.Context(), assetHash, assetExt, bytes.NewReader(assetData)); err != nil {
			h.Logger.Error("store product asset", "error", err)
			response.InternalError(w, r, h.Cfg.IsProduction())
			return
		}
	}

	// Thumbnail (optional) — only set thumbnailHash after confirming file is stored.
	var thumbnailHash *string
	if thumbFile, _, err := r.FormFile("thumbnail"); err == nil {
		defer thumbFile.Close()
		thumbData, err := io.ReadAll(io.LimitReader(thumbFile, int64(maxProductThumbSize)+1))
		if err == nil && len(thumbData) <= maxProductThumbSize {
			sum := sha256.Sum256(thumbData)
			thumbHash := hex.EncodeToString(sum[:])
			stored := true
			if exists, _ := h.Storage.Exists(r.Context(), thumbHash, "png"); !exists {
				if putErr := h.Storage.Put(r.Context(), thumbHash, "png", bytes.NewReader(thumbData)); putErr != nil {
					h.Logger.Warn("store product thumbnail failed, continuing without thumbnail", "error", putErr)
					stored = false
				}
			}
			if stored {
				thumbnailHash = &thumbHash
			}
		}
	}

	var p adminProductRow
	err = h.DB.QueryRow(r.Context(),
		`INSERT INTO products
		   (creator_id, name, description, category, price_coins, asset_hash,
		    thumbnail_hash, texture_cost, collider_size_category, edit_allowed, is_published, tags)
		 VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,false,$11)
		 RETURNING id, creator_id, name, description, category, price_coins,
		           asset_hash, thumbnail_hash, texture_cost, collider_size_category,
		           edit_allowed, is_published, tags, created_at`,
		creatorID, name, description, category, priceCoins, assetHash,
		thumbnailHash, textureCost, colliderSize, editAllowed, tags,
	).Scan(
		&p.ID, &p.CreatorID, &p.Name, &p.Description, &p.Category, &p.PriceCoins,
		&p.AssetHash, &p.ThumbnailHash, &p.TextureCost, &p.ColliderSizeCategory,
		&p.EditAllowed, &p.IsPublished, &p.Tags, &p.CreatedAt,
	)
	if err != nil {
		h.Logger.Error("admin create product", "error", err)
		response.InternalError(w, r, h.Cfg.IsProduction())
		return
	}

	p.AssetURL = h.Storage.URL(p.AssetHash, assetExt)
	if p.ThumbnailHash != nil {
		u := h.Storage.URL(*p.ThumbnailHash, "png")
		p.ThumbnailURL = &u
	}

	response.JSON(w, http.StatusCreated, p)
}

// AdminUpdateProduct handles PATCH /admin/products/{productID}.
// Body JSON: { name, description, priceCoins, editAllowed, isPublished, tags,
//
//	textureCost, colliderSizeCategory }
func (h *Handler) AdminUpdateProduct(w http.ResponseWriter, r *http.Request) {
	productID := chi.URLParam(r, "productID")

	var req struct {
		Name                 *string  `json:"name"`
		Description          *string  `json:"description"`
		PriceCoins           *int     `json:"priceCoins"`
		EditAllowed          *bool    `json:"editAllowed"`
		IsPublished          *bool    `json:"isPublished"`
		TextureCost          *int     `json:"textureCost"`
		ColliderSizeCategory *string  `json:"colliderSizeCategory"`
		Tags                 []string `json:"tags"`
	}
	if err := json.NewDecoder(r.Body).Decode(&req); err != nil {
		response.Error(w, r, http.StatusBadRequest, "validation_error", "invalid JSON body")
		return
	}
	if req.PriceCoins != nil && *req.PriceCoins < 0 {
		response.Error(w, r, http.StatusBadRequest, "validation_error", "priceCoins must be non-negative")
		return
	}
	if req.ColliderSizeCategory != nil {
		validSizes := map[string]bool{"small": true, "medium": true, "large": true}
		if !validSizes[*req.ColliderSizeCategory] {
			response.Error(w, r, http.StatusBadRequest, "validation_error", "colliderSizeCategory must be small, medium, or large")
			return
		}
	}

	// Build tags arg: nil = keep existing, non-nil slice = replace
	var tagsArg any
	if req.Tags != nil {
		tagsArg = req.Tags
	}

	tag, err := h.DB.Exec(r.Context(),
		`UPDATE products SET
		   name                  = COALESCE($2, name),
		   description           = COALESCE($3, description),
		   price_coins           = COALESCE($4, price_coins),
		   edit_allowed          = COALESCE($5, edit_allowed),
		   is_published          = COALESCE($6, is_published),
		   texture_cost          = COALESCE($7, texture_cost),
		   collider_size_category = COALESCE($8, collider_size_category),
		   tags                  = COALESCE($9, tags),
		   updated_at            = now()
		 WHERE id = $1`,
		productID,
		req.Name, req.Description, req.PriceCoins,
		req.EditAllowed, req.IsPublished,
		req.TextureCost, req.ColliderSizeCategory,
		tagsArg,
	)
	if err != nil {
		h.Logger.Error("admin update product", "error", err)
		response.InternalError(w, r, h.Cfg.IsProduction())
		return
	}
	if tag.RowsAffected() == 0 {
		response.Error(w, r, http.StatusNotFound, "not_found", "product not found")
		return
	}

	w.WriteHeader(http.StatusNoContent)
}

// ── Avatar moderation ────────────────────────────────────────────────────────

type adminAvatarRow struct {
	ID               string    `json:"id"`
	UserID           string    `json:"userId"`
	Name             string    `json:"name"`
	VrmHash          string    `json:"vrmHash"`
	VrmURL           string    `json:"vrmUrl"`
	TextureHash      *string   `json:"textureHash,omitempty"`
	TextureURL       *string   `json:"textureUrl,omitempty"`
	ModerationStatus string    `json:"moderationStatus"`
	CreatedAt        time.Time `json:"createdAt"`
}

// AdminListPendingAvatars handles GET /admin/avatars?status=pending (default).
func (h *Handler) AdminListPendingAvatars(w http.ResponseWriter, r *http.Request) {
	status := r.URL.Query().Get("status")
	if status == "" {
		status = "pending"
	}
	validStatuses := map[string]bool{"pending": true, "approved": true, "rejected": true}
	if !validStatuses[status] {
		response.Error(w, r, http.StatusBadRequest, "validation_error", "status must be pending, approved, or rejected")
		return
	}
	after := r.URL.Query().Get("after")
	limit := 50

	rows, err := h.DB.Query(r.Context(),
		`SELECT id, user_id, name, vrm_hash, texture_hash, moderation_status, created_at
		 FROM avatars
		 WHERE moderation_status = $1
		   AND ($2 = '' OR created_at < (SELECT created_at FROM avatars WHERE id = $2::UUID))
		 ORDER BY created_at ASC
		 LIMIT $3`,
		status, after, limit+1,
	)
	if err != nil {
		h.Logger.Error("admin list avatars", "error", err)
		response.InternalError(w, r, h.Cfg.IsProduction())
		return
	}
	defer rows.Close()

	var avatars []adminAvatarRow
	for rows.Next() {
		var av adminAvatarRow
		if err := rows.Scan(&av.ID, &av.UserID, &av.Name, &av.VrmHash, &av.TextureHash, &av.ModerationStatus, &av.CreatedAt); err != nil {
			continue
		}
		av.VrmURL = h.Storage.URL(av.VrmHash, "vrm")
		if av.TextureHash != nil {
			u := h.Storage.URL(*av.TextureHash, "png")
			av.TextureURL = &u
		}
		avatars = append(avatars, av)
	}

	var cur response.Cursor
	if len(avatars) > limit {
		avatars = avatars[:limit]
		cur.Next = avatars[len(avatars)-1].ID
	}

	response.JSON(w, http.StatusOK, map[string]any{
		"avatars": avatars,
		"cursor":  cur,
	})
}

// AdminModerateAvatar handles PATCH /admin/avatars/{avatarID}/moderation.
// Body: { "status": "approved" | "rejected", "reason": "..." }
func (h *Handler) AdminModerateAvatar(w http.ResponseWriter, r *http.Request) {
	avatarID := chi.URLParam(r, "avatarID")

	var req struct {
		Status string `json:"status"`
		Reason string `json:"reason"`
	}
	if err := json.NewDecoder(r.Body).Decode(&req); err != nil {
		response.Error(w, r, http.StatusBadRequest, "validation_error", "invalid JSON body")
		return
	}
	if req.Status != "approved" && req.Status != "rejected" {
		response.Error(w, r, http.StatusBadRequest, "validation_error", "status must be approved or rejected")
		return
	}

	h.Logger.Info("admin moderate avatar",
		"avatar_id", avatarID,
		"status", req.Status,
		"reason", req.Reason,
	)

	tag, err := h.DB.Exec(r.Context(),
		`UPDATE avatars SET moderation_status = $2 WHERE id = $1`,
		avatarID, req.Status,
	)
	if err != nil {
		h.Logger.Error("admin moderate avatar", "error", err)
		response.InternalError(w, r, h.Cfg.IsProduction())
		return
	}
	if tag.RowsAffected() == 0 {
		response.Error(w, r, http.StatusNotFound, "not_found", "avatar not found")
		return
	}

	w.WriteHeader(http.StatusNoContent)
}

// ── Settled revenue management ───────────────────────────────────────────────

type settledRevenueRow struct {
	ID                    int64     `json:"id"`
	Period                string    `json:"period"`
	Country               string    `json:"country"`
	SettledNetRevenueJpy  float64   `json:"settledNetRevenueJpy"`
	RefundAdjustmentJpy   float64   `json:"refundAdjustmentJpy"`
	AdjustmentFactor      *float64  `json:"adjustmentFactor,omitempty"`
	RegisteredBy          *string   `json:"registeredBy,omitempty"`
	CreatedAt             time.Time `json:"createdAt"`
}

// AdminListSettledRevenues handles GET /admin/settled-revenues.
// Query: period (YYYY-MM), country
func (h *Handler) AdminListSettledRevenues(w http.ResponseWriter, r *http.Request) {
	period := r.URL.Query().Get("period")
	country := r.URL.Query().Get("country")

	rows, err := h.DB.Query(r.Context(),
		`SELECT sr.id, sr.period, sr.country,
		        sr.settled_net_revenue_jpy, sr.refund_adjustment_jpy,
		        raf.adjustment_factor, sr.registered_by, sr.created_at
		 FROM settled_revenues sr
		 LEFT JOIN revenue_adjustment_factors raf
		   ON raf.period = sr.period AND raf.country = sr.country
		 WHERE ($1 = '' OR sr.period = $1)
		   AND ($2 = '' OR sr.country = $2)
		 ORDER BY sr.period DESC, sr.country ASC`,
		period, country,
	)
	if err != nil {
		h.Logger.Error("admin list settled revenues", "error", err)
		response.InternalError(w, r, h.Cfg.IsProduction())
		return
	}
	defer rows.Close()

	var revenues []settledRevenueRow
	for rows.Next() {
		var sr settledRevenueRow
		if err := rows.Scan(
			&sr.ID, &sr.Period, &sr.Country,
			&sr.SettledNetRevenueJpy, &sr.RefundAdjustmentJpy,
			&sr.AdjustmentFactor, &sr.RegisteredBy, &sr.CreatedAt,
		); err != nil {
			continue
		}
		revenues = append(revenues, sr)
	}

	response.JSON(w, http.StatusOK, map[string]any{"revenues": revenues})
}

// AdminRegisterSettledRevenue handles POST /admin/settled-revenues.
// Body: { "period": "YYYY-MM", "country": "JP", "settledNetRevenueJpy": 123456.78, "refundAdjustmentJpy": 0 }
// After insert, calculates and upserts the revenue_adjustment_factor for this period/country.
func (h *Handler) AdminRegisterSettledRevenue(w http.ResponseWriter, r *http.Request) {
	var req struct {
		Period               string  `json:"period"`
		Country              string  `json:"country"`
		SettledNetRevenueJpy float64 `json:"settledNetRevenueJpy"`
		RefundAdjustmentJpy  float64 `json:"refundAdjustmentJpy"`
	}
	if err := json.NewDecoder(r.Body).Decode(&req); err != nil {
		response.Error(w, r, http.StatusBadRequest, "validation_error", "invalid JSON body")
		return
	}
	if req.Period == "" || req.Country == "" {
		response.Error(w, r, http.StatusBadRequest, "validation_error", "period and country are required")
		return
	}
	if len(req.Period) != 7 || req.Period[4] != '-' {
		response.Error(w, r, http.StatusBadRequest, "validation_error", "period must be YYYY-MM format")
		return
	}

	// Fetch admin ID from context (set by AdminAuth middleware)
	var adminID *string
	if admin := middleware.AdminUserFromContext(r.Context()); admin != nil {
		adminID = &admin.ID
	}

	tx, err := h.DB.Begin(r.Context())
	if err != nil {
		h.Logger.Error("begin tx settled revenue", "error", err)
		response.InternalError(w, r, h.Cfg.IsProduction())
		return
	}
	defer tx.Rollback(r.Context())

	var sr settledRevenueRow
	err = tx.QueryRow(r.Context(),
		`INSERT INTO settled_revenues (period, country, settled_net_revenue_jpy, refund_adjustment_jpy, registered_by)
		 VALUES ($1, $2, $3, $4, $5)
		 ON CONFLICT (period, country) DO UPDATE
		   SET settled_net_revenue_jpy = EXCLUDED.settled_net_revenue_jpy,
		       refund_adjustment_jpy   = EXCLUDED.refund_adjustment_jpy,
		       registered_by           = EXCLUDED.registered_by
		 RETURNING id, period, country, settled_net_revenue_jpy, refund_adjustment_jpy, registered_by, created_at`,
		req.Period, req.Country, req.SettledNetRevenueJpy, req.RefundAdjustmentJpy, adminID,
	).Scan(&sr.ID, &sr.Period, &sr.Country, &sr.SettledNetRevenueJpy, &sr.RefundAdjustmentJpy, &sr.RegisteredBy, &sr.CreatedAt)
	if err != nil {
		h.Logger.Error("insert settled revenue", "error", err)
		response.InternalError(w, r, h.Cfg.IsProduction())
		return
	}

	// Calculate adjustment factor:
	// factor = (settled_net + refund_adjustment) / estimated_consumption_value
	// Use EXISTS to avoid Cartesian product when one buyer has multiple coin_purchases.
	// A buyer is included when ANY of their purchases is from the given storefront_country.
	var estimatedConsumption float64
	err = tx.QueryRow(r.Context(),
		`SELECT COALESCE(SUM(ct.estimated_consumption_value_jpy), 0)
		 FROM coin_transactions ct
		 WHERE to_char(ct.created_at AT TIME ZONE 'Asia/Tokyo', 'YYYY-MM') = $1
		   AND EXISTS (
		     SELECT 1 FROM coin_purchases cp
		     WHERE cp.user_id = ct.buyer_id
		       AND cp.storefront_country = $2
		   )`,
		req.Period, req.Country,
	).Scan(&estimatedConsumption)
	if err != nil {
		h.Logger.Error("calc adjustment factor", "error", err)
		response.InternalError(w, r, h.Cfg.IsProduction())
		return
	}

	var adjustmentFactor float64 = 1.0
	netRevenue := req.SettledNetRevenueJpy + req.RefundAdjustmentJpy
	if estimatedConsumption > 0 {
		adjustmentFactor = netRevenue / estimatedConsumption
	}
	sr.AdjustmentFactor = &adjustmentFactor

	_, err = tx.Exec(r.Context(),
		`INSERT INTO revenue_adjustment_factors (period, country, adjustment_factor)
		 VALUES ($1, $2, $3)
		 ON CONFLICT (period, country) DO UPDATE
		   SET adjustment_factor = EXCLUDED.adjustment_factor,
		       calculated_at     = now(),
		       override_reason   = NULL,
		       overridden_by     = NULL`,
		req.Period, req.Country, adjustmentFactor,
	)
	if err != nil {
		h.Logger.Error("upsert adjustment factor", "error", err)
		response.InternalError(w, r, h.Cfg.IsProduction())
		return
	}

	if err := tx.Commit(r.Context()); err != nil {
		h.Logger.Error("commit settled revenue tx", "error", err)
		response.InternalError(w, r, h.Cfg.IsProduction())
		return
	}

	response.JSON(w, http.StatusCreated, sr)
}

// ── helpers ──────────────────────────────────────────────────────────────────

// isValidUUID reports whether s is a valid UUID (8-4-4-4-12 hex format).
func isValidUUID(s string) bool {
	if len(s) != 36 {
		return false
	}
	for i, c := range s {
		switch i {
		case 8, 13, 18, 23:
			if c != '-' {
				return false
			}
		default:
			if !((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')) {
				return false
			}
		}
	}
	return true
}

func assetExtForCategory(category string) string {
	switch category {
	case "avatar":
		return "vrm"
	default: // accessory, world_object, stamp
		return "glb"
	}
}
