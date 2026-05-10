package handler

import (
	"context"
	"net/http"
	"time"

	"github.com/go-chi/chi/v5"
	"github.com/nibankougen/LowPolyWorld/api/internal/middleware"
	"github.com/nibankougen/LowPolyWorld/api/internal/response"
)

type notificationResponse struct {
	ID        string `json:"id"`
	Type      string `json:"type"`
	Body      string `json:"body"`
	RefID     string `json:"refId,omitempty"`
	IsRead    bool   `json:"isRead"`
	CreatedAt string `json:"createdAt"`
}

type notificationListResponse struct {
	Notifications []notificationResponse `json:"notifications"`
}

// ListNotifications handles GET /api/v1/me/notifications.
func (h *Handler) ListNotifications(w http.ResponseWriter, r *http.Request) {
	userID := middleware.UserIDFromContext(r.Context())

	rows, err := h.DB.Query(r.Context(),
		`SELECT id, type, body, ref_id, is_read, created_at
		 FROM notifications
		 WHERE user_id = $1
		 ORDER BY created_at DESC
		 LIMIT 100`,
		userID,
	)
	if err != nil {
		h.Logger.Error("list notifications", "error", err)
		response.InternalError(w, r, h.Cfg.IsProduction())
		return
	}
	defer rows.Close()

	notifications := []notificationResponse{}
	for rows.Next() {
		var n notificationResponse
		var refID *string
		var createdAt time.Time
		if err := rows.Scan(&n.ID, &n.Type, &n.Body, &refID, &n.IsRead, &createdAt); err != nil {
			continue
		}
		if refID != nil {
			n.RefID = *refID
		}
		n.CreatedAt = createdAt.UTC().Format(time.RFC3339)
		notifications = append(notifications, n)
	}
	if rows.Err() != nil {
		response.InternalError(w, r, h.Cfg.IsProduction())
		return
	}
	response.ClientJSON(w, http.StatusOK, notificationListResponse{Notifications: notifications})
}

// MarkNotificationRead handles PATCH /api/v1/me/notifications/{notificationID}/read.
func (h *Handler) MarkNotificationRead(w http.ResponseWriter, r *http.Request) {
	userID := middleware.UserIDFromContext(r.Context())
	notifID := chi.URLParam(r, "notificationID")

	_, _ = h.DB.Exec(r.Context(),
		`UPDATE notifications SET is_read = TRUE WHERE id = $1 AND user_id = $2`,
		notifID, userID,
	)
	w.WriteHeader(http.StatusNoContent)
}

// MarkAllNotificationsRead handles PATCH /api/v1/me/notifications/read-all.
func (h *Handler) MarkAllNotificationsRead(w http.ResponseWriter, r *http.Request) {
	userID := middleware.UserIDFromContext(r.Context())

	_, _ = h.DB.Exec(r.Context(),
		`UPDATE notifications SET is_read = TRUE WHERE user_id = $1 AND is_read = FALSE`,
		userID,
	)
	w.WriteHeader(http.StatusNoContent)
}

// ── Internal helpers ──────────────────────────────────────────────────────────

// createNotification inserts a notification record. Runs in a goroutine — errors are only logged.
func createNotification(ctx context.Context, h *Handler, userID, notifType, body, refID string) {
	_, err := h.DB.Exec(ctx,
		`INSERT INTO notifications (user_id, type, body, ref_id) VALUES ($1, $2, $3, NULLIF($4, ''))`,
		userID, notifType, body, refID,
	)
	if err != nil {
		h.Logger.Error("create notification", "type", notifType, "user_id", userID, "error", err)
	}
}

// notifyFollowersWorldPublished creates a notification for each follower of the world owner
// when a world is published. Rate-limited to 1 per follower per day per source user.
func notifyFollowersWorldPublished(ctx context.Context, h *Handler, worldOwnerID, _, worldName string) {
	rows, err := h.DB.Query(ctx,
		`SELECT follower_id FROM follows WHERE followee_id = $1`, worldOwnerID)
	if err != nil {
		h.Logger.Error("notify followers: query followers", "error", err)
		return
	}
	defer rows.Close()

	for rows.Next() {
		var followerID string
		if err := rows.Scan(&followerID); err != nil {
			continue
		}
		// Rate limit: skip if this follower already received a WorldPublished notification
		// from this world owner today.
		var alreadySent bool
		_ = h.DB.QueryRow(ctx,
			`SELECT EXISTS(
			     SELECT 1 FROM notifications
			     WHERE user_id = $1 AND type = 'world_published' AND ref_id = $2
			       AND created_at >= now() - interval '1 day'
			 )`,
			followerID, worldOwnerID,
		).Scan(&alreadySent)
		if alreadySent {
			continue
		}
		createNotification(ctx, h, followerID, "world_published",
			worldName+" が公開されました", worldOwnerID)
	}
}

// NotifyFollowersProductReleased creates a notification for each follower of the creator
// when a product is published. Rate-limited to 1 per follower per day per source creator.
func NotifyFollowersProductReleased(ctx context.Context, h *Handler, creatorUserID, productID, productName string) {
	rows, err := h.DB.Query(ctx,
		`SELECT follower_id FROM follows WHERE followee_id = $1`, creatorUserID)
	if err != nil {
		h.Logger.Error("notify followers: query followers for product", "error", err)
		return
	}
	defer rows.Close()

	for rows.Next() {
		var followerID string
		if err := rows.Scan(&followerID); err != nil {
			continue
		}
		var alreadySent bool
		_ = h.DB.QueryRow(ctx,
			`SELECT EXISTS(
			     SELECT 1 FROM notifications
			     WHERE user_id = $1 AND type = 'product_released' AND ref_id = $2
			       AND created_at >= now() - interval '1 day'
			 )`,
			followerID, creatorUserID,
		).Scan(&alreadySent)
		if alreadySent {
			continue
		}
		createNotification(ctx, h, followerID, "product_released",
			productName+" が販売開始されました", creatorUserID)
	}
}
