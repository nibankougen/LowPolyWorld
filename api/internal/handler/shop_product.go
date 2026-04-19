package handler

import (
	"encoding/json"
	"net/http"
	"strconv"
	"strings"
	"time"

	"github.com/go-chi/chi/v5"
	mw "github.com/nibankougen/LowPolyWorld/api/internal/middleware"
	"github.com/nibankougen/LowPolyWorld/api/internal/response"
)

// ── Types ────────────────────────────────────────────────────────────────────

type productResponse struct {
	ID                   string   `json:"id"`
	CreatorID            string   `json:"creator_id"`
	CreatorName          string   `json:"creator_name"`
	Name                 string   `json:"name"`
	Description          string   `json:"description"`
	Category             string   `json:"category"`
	PriceCoins           int      `json:"price_coins"`
	ThumbnailURL         *string  `json:"thumbnail_url"`
	TextureCost          *int     `json:"texture_cost,omitempty"`
	ColliderSizeCategory *string  `json:"collider_size_category,omitempty"`
	EditAllowed          bool     `json:"edit_allowed"`
	LikesCount           int      `json:"likes_count"`
	RecentPurchaseCount  int      `json:"recent_purchase_count"`
	Tags                 []string `json:"tags"`
	LikedByMe            bool     `json:"liked_by_me"`
	PurchasedByMe        bool     `json:"purchased_by_me"`
	CreatedAt            string   `json:"created_at"`
}

// ── List Products ─────────────────────────────────────────────────────────────

// GET /shop/products
// Query: category, sort (popularity|likes|newest|oldest), q, after, limit,
//
//	texture_cost_min, texture_cost_max, collider_size_category
func (h *Handler) ListProducts(w http.ResponseWriter, r *http.Request) {
	userID := mw.UserIDFromContext(r.Context())

	q := r.URL.Query()
	category := q.Get("category")
	sort := q.Get("sort")
	if sort == "" {
		sort = "popularity"
	}
	search := q.Get("q")
	after := q.Get("after")
	limit := 20
	if l, err := strconv.Atoi(q.Get("limit")); err == nil && l > 0 && l <= 50 {
		limit = l
	}
	colliderSize := q.Get("collider_size_category")
	textureCostMin := q.Get("texture_cost_min")
	textureCostMax := q.Get("texture_cost_max")

	validCategories := map[string]bool{"avatar": true, "accessory": true, "world_object": true, "stamp": true}
	if category != "" && !validCategories[category] {
		response.Error(w, r, http.StatusBadRequest, "INVALID_CATEGORY", "invalid category")
		return
	}
	validSorts := map[string]bool{"popularity": true, "likes": true, "newest": true, "oldest": true}
	if !validSorts[sort] {
		response.Error(w, r, http.StatusBadRequest, "INVALID_SORT", "invalid sort")
		return
	}

	var orderClause string
	switch sort {
	case "popularity":
		orderClause = "p.recent_purchase_count DESC, p.created_at DESC"
	case "likes":
		orderClause = "p.likes_count DESC, p.created_at DESC"
	case "newest":
		orderClause = "p.created_at DESC"
	case "oldest":
		orderClause = "p.created_at ASC"
	}

	args := []any{}
	argIdx := 1
	conds := []string{"p.is_published = TRUE"}

	if category != "" {
		conds = append(conds, "p.category = $"+strconv.Itoa(argIdx))
		args = append(args, category)
		argIdx++
	}
	if search != "" {
		conds = append(conds, "(p.name ILIKE $"+strconv.Itoa(argIdx)+" OR $"+strconv.Itoa(argIdx+1)+"::text = ANY(p.tags))")
		args = append(args, "%"+search+"%", strings.ToLower(search))
		argIdx += 2
	}
	if colliderSize != "" {
		conds = append(conds, "p.collider_size_category = $"+strconv.Itoa(argIdx))
		args = append(args, colliderSize)
		argIdx++
	}
	if textureCostMin != "" {
		if tcMin, err := strconv.Atoi(textureCostMin); err == nil {
			conds = append(conds, "p.texture_cost >= $"+strconv.Itoa(argIdx))
			args = append(args, tcMin)
			argIdx++
		}
	}
	if textureCostMax != "" {
		if tcMax, err := strconv.Atoi(textureCostMax); err == nil {
			conds = append(conds, "p.texture_cost <= $"+strconv.Itoa(argIdx))
			args = append(args, tcMax)
			argIdx++
		}
	}
	if after != "" {
		conds = append(conds, "p.id > $"+strconv.Itoa(argIdx))
		args = append(args, after)
		argIdx++
	}

	where := "WHERE " + strings.Join(conds, " AND ")
	sqlQ := `
		SELECT p.id, p.creator_id, c.display_name, p.name, p.description,
		       p.category, p.price_coins, p.thumbnail_hash,
		       p.texture_cost, p.collider_size_category,
		       p.edit_allowed, p.likes_count, p.recent_purchase_count, p.tags, p.created_at,
		       (SELECT COUNT(*) > 0 FROM product_likes pl WHERE pl.product_id = p.id AND pl.user_id = $` + strconv.Itoa(argIdx) + `),
		       (SELECT COUNT(*) > 0 FROM user_products up WHERE up.product_id = p.id AND up.user_id = $` + strconv.Itoa(argIdx) + `)
		FROM products p
		JOIN creators c ON c.id = p.creator_id
		` + where + `
		ORDER BY ` + orderClause + `
		LIMIT $` + strconv.Itoa(argIdx+1)
	args = append(args, userID, limit+1)

	rows, err := h.DB.Query(r.Context(), sqlQ, args...)
	if err != nil {
		h.Logger.Error("list products query", "error", err)
		response.Error(w, r, http.StatusInternalServerError, "DB_ERROR", "database error")
		return
	}
	defer rows.Close()

	products := []productResponse{}
	for rows.Next() {
		var p productResponse
		var thumbnailHash *string
		var createdAt time.Time
		if err := rows.Scan(
			&p.ID, &p.CreatorID, &p.CreatorName, &p.Name, &p.Description,
			&p.Category, &p.PriceCoins, &thumbnailHash,
			&p.TextureCost, &p.ColliderSizeCategory,
			&p.EditAllowed, &p.LikesCount, &p.RecentPurchaseCount, &p.Tags, &createdAt,
			&p.LikedByMe, &p.PurchasedByMe,
		); err != nil {
			h.Logger.Error("scan product row", "error", err)
			response.Error(w, r, http.StatusInternalServerError, "DB_ERROR", "database error")
			return
		}
		p.CreatedAt = createdAt.UTC().Format(time.RFC3339)
		if thumbnailHash != nil {
			u := h.Storage.URL(*thumbnailHash, "png")
			p.ThumbnailURL = &u
		}
		products = append(products, p)
	}

	hasNext := len(products) > limit
	if hasNext {
		products = products[:limit]
	}
	var nextCursor *string
	if hasNext && len(products) > 0 {
		c := products[len(products)-1].ID
		nextCursor = &c
	}
	response.JSON(w, http.StatusOK, map[string]any{"products": products, "next_cursor": nextCursor})
}

// ── Get Product ───────────────────────────────────────────────────────────────

// GET /shop/products/{productID}
func (h *Handler) GetProduct(w http.ResponseWriter, r *http.Request) {
	userID := mw.UserIDFromContext(r.Context())
	productID := chi.URLParam(r, "productID")

	var p productResponse
	var thumbnailHash *string
	var createdAt time.Time

	err := h.DB.QueryRow(r.Context(), `
		SELECT p.id, p.creator_id, c.display_name, p.name, p.description,
		       p.category, p.price_coins, p.thumbnail_hash,
		       p.texture_cost, p.collider_size_category,
		       p.edit_allowed, p.likes_count, p.recent_purchase_count, p.tags, p.created_at,
		       (SELECT COUNT(*) > 0 FROM product_likes pl WHERE pl.product_id = p.id AND pl.user_id = $2),
		       (SELECT COUNT(*) > 0 FROM user_products up WHERE up.product_id = p.id AND up.user_id = $2)
		FROM products p
		JOIN creators c ON c.id = p.creator_id
		WHERE p.id = $1 AND p.is_published = TRUE`,
		productID, userID,
	).Scan(
		&p.ID, &p.CreatorID, &p.CreatorName, &p.Name, &p.Description,
		&p.Category, &p.PriceCoins, &thumbnailHash,
		&p.TextureCost, &p.ColliderSizeCategory,
		&p.EditAllowed, &p.LikesCount, &p.RecentPurchaseCount, &p.Tags, &createdAt,
		&p.LikedByMe, &p.PurchasedByMe,
	)
	if err != nil {
		response.Error(w, r, http.StatusNotFound, "NOT_FOUND", "product not found")
		return
	}
	p.CreatedAt = createdAt.UTC().Format(time.RFC3339)
	if thumbnailHash != nil {
		u := h.Storage.URL(*thumbnailHash, "png")
		p.ThumbnailURL = &u
	}
	response.JSON(w, http.StatusOK, p)
}

// ── Like / Unlike ─────────────────────────────────────────────────────────────

// POST /shop/products/{productID}/like
func (h *Handler) LikeProduct(w http.ResponseWriter, r *http.Request) {
	userID := mw.UserIDFromContext(r.Context())
	productID := chi.URLParam(r, "productID")

	var creatorUserID string
	err := h.DB.QueryRow(r.Context(),
		`SELECT c.user_id FROM products p JOIN creators c ON c.id = p.creator_id WHERE p.id = $1 AND p.is_published = TRUE`,
		productID).Scan(&creatorUserID)
	if err != nil {
		response.Error(w, r, http.StatusNotFound, "NOT_FOUND", "product not found")
		return
	}
	if creatorUserID == userID {
		response.Error(w, r, http.StatusForbidden, "SELF_LIKE_FORBIDDEN", "cannot like your own product")
		return
	}

	tx, err := h.DB.Begin(r.Context())
	if err != nil {
		response.Error(w, r, http.StatusInternalServerError, "DB_ERROR", "database error")
		return
	}
	defer tx.Rollback(r.Context())

	tag, err := tx.Exec(r.Context(),
		`INSERT INTO product_likes (product_id, user_id) VALUES ($1, $2) ON CONFLICT DO NOTHING`,
		productID, userID)
	if err != nil {
		response.Error(w, r, http.StatusInternalServerError, "DB_ERROR", "database error")
		return
	}
	if tag.RowsAffected() == 0 {
		response.Error(w, r, http.StatusConflict, "ALREADY_LIKED", "already liked")
		return
	}
	if _, err = tx.Exec(r.Context(),
		`UPDATE products SET likes_count = likes_count + 1 WHERE id = $1`, productID); err != nil {
		response.Error(w, r, http.StatusInternalServerError, "DB_ERROR", "database error")
		return
	}
	if err := tx.Commit(r.Context()); err != nil {
		response.Error(w, r, http.StatusInternalServerError, "DB_ERROR", "database error")
		return
	}
	w.WriteHeader(http.StatusNoContent)
}

// DELETE /shop/products/{productID}/like
func (h *Handler) UnlikeProduct(w http.ResponseWriter, r *http.Request) {
	userID := mw.UserIDFromContext(r.Context())
	productID := chi.URLParam(r, "productID")

	tx, err := h.DB.Begin(r.Context())
	if err != nil {
		response.Error(w, r, http.StatusInternalServerError, "DB_ERROR", "database error")
		return
	}
	defer tx.Rollback(r.Context())

	tag, err := tx.Exec(r.Context(),
		`DELETE FROM product_likes WHERE product_id = $1 AND user_id = $2`, productID, userID)
	if err != nil {
		response.Error(w, r, http.StatusInternalServerError, "DB_ERROR", "database error")
		return
	}
	if tag.RowsAffected() == 0 {
		response.Error(w, r, http.StatusNotFound, "NOT_LIKED", "not liked")
		return
	}
	if _, err = tx.Exec(r.Context(),
		`UPDATE products SET likes_count = GREATEST(likes_count - 1, 0) WHERE id = $1`, productID); err != nil {
		response.Error(w, r, http.StatusInternalServerError, "DB_ERROR", "database error")
		return
	}
	if err := tx.Commit(r.Context()); err != nil {
		response.Error(w, r, http.StatusInternalServerError, "DB_ERROR", "database error")
		return
	}
	w.WriteHeader(http.StatusNoContent)
}

// ── Purchase Product ──────────────────────────────────────────────────────────

// POST /shop/products/{productID}/purchase
func (h *Handler) PurchaseProduct(w http.ResponseWriter, r *http.Request) {
	userID := mw.UserIDFromContext(r.Context())
	productID := chi.URLParam(r, "productID")

	var req struct {
		IdempotencyKey *string `json:"idempotency_key"`
	}
	if err := json.NewDecoder(r.Body).Decode(&req); err != nil && err.Error() != "EOF" {
		response.Error(w, r, http.StatusBadRequest, "INVALID_BODY", "invalid request body")
		return
	}

	// Return early if idempotency key already processed
	if req.IdempotencyKey != nil {
		var txID string
		err := h.DB.QueryRow(r.Context(),
			`SELECT id FROM coin_transactions WHERE idempotency_key = $1`, *req.IdempotencyKey).Scan(&txID)
		if err == nil {
			response.JSON(w, http.StatusOK, map[string]string{"transaction_id": txID})
			return
		}
	}

	tx, err := h.DB.Begin(r.Context())
	if err != nil {
		response.Error(w, r, http.StatusInternalServerError, "DB_ERROR", "database error")
		return
	}
	defer tx.Rollback(r.Context())

	var priceCoins int
	var creatorID string
	err = tx.QueryRow(r.Context(),
		`SELECT price_coins, creator_id FROM products WHERE id = $1 AND is_published = TRUE`,
		productID).Scan(&priceCoins, &creatorID)
	if err != nil {
		response.Error(w, r, http.StatusNotFound, "NOT_FOUND", "product not found")
		return
	}

	var alreadyPurchased bool
	_ = tx.QueryRow(r.Context(),
		`SELECT EXISTS(SELECT 1 FROM user_products WHERE user_id = $1 AND product_id = $2)`,
		userID, productID).Scan(&alreadyPurchased)
	if alreadyPurchased {
		response.Error(w, r, http.StatusConflict, "ALREADY_PURCHASED", "product already owned")
		return
	}

	// Compute available balance
	var rawTotal, deducted, spent int
	_ = tx.QueryRow(r.Context(),
		`SELECT COALESCE(SUM(coins_amount), 0) FROM coin_purchases WHERE user_id = $1 AND valid_until > now()`,
		userID).Scan(&rawTotal)
	_ = tx.QueryRow(r.Context(), `
		SELECT COALESCE(SUM(cc.coins_deducted), 0)
		FROM coin_purchase_cancellations cc
		JOIN coin_purchases cp ON cp.id = cc.coin_purchase_id
		WHERE cp.user_id = $1`, userID).Scan(&deducted)
	_ = tx.QueryRow(r.Context(),
		`SELECT COALESCE(SUM(coins_spent), 0) FROM coin_transactions WHERE buyer_id = $1`, userID).Scan(&spent)
	balance := rawTotal - deducted - spent

	if balance < priceCoins {
		response.Error(w, r, http.StatusPaymentRequired, "INSUFFICIENT_COINS", "insufficient coin balance")
		return
	}

	var avgCoinValueJPY float64
	_ = tx.QueryRow(r.Context(),
		`SELECT COALESCE(avg_coin_value_jpy, 0) FROM user_coin_values WHERE user_id = $1`, userID).Scan(&avgCoinValueJPY)

	estimatedValue := float64(priceCoins) * avgCoinValueJPY

	var coinTxID string
	if req.IdempotencyKey != nil {
		err = tx.QueryRow(r.Context(), `
			INSERT INTO coin_transactions
			  (idempotency_key, buyer_id, product_id, creator_id, coins_spent,
			   avg_coin_value_jpy_at_time, estimated_consumption_value_jpy)
			VALUES ($1, $2, $3, $4, $5, $6, $7)
			RETURNING id`,
			*req.IdempotencyKey, userID, productID, creatorID, priceCoins, avgCoinValueJPY, estimatedValue,
		).Scan(&coinTxID)
	} else {
		err = tx.QueryRow(r.Context(), `
			INSERT INTO coin_transactions
			  (buyer_id, product_id, creator_id, coins_spent,
			   avg_coin_value_jpy_at_time, estimated_consumption_value_jpy)
			VALUES ($1, $2, $3, $4, $5, $6)
			RETURNING id`,
			userID, productID, creatorID, priceCoins, avgCoinValueJPY, estimatedValue,
		).Scan(&coinTxID)
	}
	if err != nil {
		h.Logger.Error("insert coin_transaction", "error", err)
		response.Error(w, r, http.StatusInternalServerError, "DB_ERROR", "database error")
		return
	}

	if _, err = tx.Exec(r.Context(),
		`INSERT INTO user_products (user_id, product_id, transaction_id) VALUES ($1, $2, $3)`,
		userID, productID, coinTxID); err != nil {
		h.Logger.Error("insert user_product", "error", err)
		response.Error(w, r, http.StatusInternalServerError, "DB_ERROR", "database error")
		return
	}

	_, _ = tx.Exec(r.Context(),
		`UPDATE products SET recent_purchase_count = recent_purchase_count + 1 WHERE id = $1`, productID)

	if err := tx.Commit(r.Context()); err != nil {
		h.Logger.Error("commit purchase", "error", err)
		response.Error(w, r, http.StatusInternalServerError, "DB_ERROR", "database error")
		return
	}
	response.JSON(w, http.StatusOK, map[string]string{"transaction_id": coinTxID})
}

// ── List Purchased Products ───────────────────────────────────────────────────

// GET /me/products
func (h *Handler) ListMyProducts(w http.ResponseWriter, r *http.Request) {
	userID := mw.UserIDFromContext(r.Context())

	q := r.URL.Query()
	category := q.Get("category")
	after := q.Get("after")
	limit := 20
	if l, err := strconv.Atoi(q.Get("limit")); err == nil && l > 0 && l <= 50 {
		limit = l
	}

	args := []any{userID}
	argIdx := 2
	conds := []string{"up.user_id = $1"}

	if category != "" {
		conds = append(conds, "p.category = $"+strconv.Itoa(argIdx))
		args = append(args, category)
		argIdx++
	}
	if after != "" {
		conds = append(conds, "up.id > $"+strconv.Itoa(argIdx))
		args = append(args, after)
		argIdx++
	}

	where := "WHERE " + strings.Join(conds, " AND ")
	args = append(args, limit+1)

	rows, err := h.DB.Query(r.Context(), `
		SELECT up.id, p.id, p.creator_id, c.display_name, p.name, p.category,
		       p.price_coins, p.thumbnail_hash, p.edit_allowed, up.purchased_at
		FROM user_products up
		JOIN products p ON p.id = up.product_id
		JOIN creators c ON c.id = p.creator_id
		`+where+`
		ORDER BY up.purchased_at DESC
		LIMIT $`+strconv.Itoa(argIdx), args...)
	if err != nil {
		h.Logger.Error("list my products query", "error", err)
		response.Error(w, r, http.StatusInternalServerError, "DB_ERROR", "database error")
		return
	}
	defer rows.Close()

	type item struct {
		RowID       string  `json:"row_id"`
		ID          string  `json:"id"`
		CreatorID   string  `json:"creator_id"`
		CreatorName string  `json:"creator_name"`
		Name        string  `json:"name"`
		Category    string  `json:"category"`
		PriceCoins  int     `json:"price_coins"`
		ThumbnailURL *string `json:"thumbnail_url"`
		EditAllowed bool    `json:"edit_allowed"`
		PurchasedAt string  `json:"purchased_at"`
	}

	items := []item{}
	for rows.Next() {
		var it item
		var thumbnailHash *string
		var purchasedAt time.Time
		if err := rows.Scan(&it.RowID, &it.ID, &it.CreatorID, &it.CreatorName, &it.Name, &it.Category,
			&it.PriceCoins, &thumbnailHash, &it.EditAllowed, &purchasedAt); err != nil {
			h.Logger.Error("scan my product", "error", err)
			response.Error(w, r, http.StatusInternalServerError, "DB_ERROR", "database error")
			return
		}
		it.PurchasedAt = purchasedAt.UTC().Format(time.RFC3339)
		if thumbnailHash != nil {
			u := h.Storage.URL(*thumbnailHash, "png")
			it.ThumbnailURL = &u
		}
		items = append(items, it)
	}

	hasNext := len(items) > limit
	if hasNext {
		items = items[:limit]
	}
	var nextCursor *string
	if hasNext && len(items) > 0 {
		c := items[len(items)-1].RowID
		nextCursor = &c
	}
	response.JSON(w, http.StatusOK, map[string]any{"products": items, "next_cursor": nextCursor})
}

// ── Creator Info ──────────────────────────────────────────────────────────────

// GET /shop/creators/{creatorID}
func (h *Handler) GetCreator(w http.ResponseWriter, r *http.Request) {
	creatorID := chi.URLParam(r, "creatorID")

	var resp struct {
		ID          string `json:"id"`
		UserID      string `json:"user_id"`
		DisplayName string `json:"display_name"`
		Bio         string `json:"bio"`
		CreatedAt   string `json:"created_at"`
	}
	var createdAt time.Time
	err := h.DB.QueryRow(r.Context(),
		`SELECT id, user_id, display_name, bio, created_at FROM creators WHERE id = $1`, creatorID,
	).Scan(&resp.ID, &resp.UserID, &resp.DisplayName, &resp.Bio, &createdAt)
	if err != nil {
		response.Error(w, r, http.StatusNotFound, "NOT_FOUND", "creator not found")
		return
	}
	resp.CreatedAt = createdAt.UTC().Format(time.RFC3339)
	response.JSON(w, http.StatusOK, resp)
}
