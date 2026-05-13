package handler

import (
	"log/slog"

	"github.com/jackc/pgx/v5/pgxpool"
	"github.com/nibankougen/LowPolyWorld/api/internal/auth"
	"github.com/nibankougen/LowPolyWorld/api/internal/cache"
	"github.com/nibankougen/LowPolyWorld/api/internal/config"
	"github.com/nibankougen/LowPolyWorld/api/internal/email"
	"github.com/nibankougen/LowPolyWorld/api/internal/storage"
)

// Handler holds shared dependencies for all HTTP handlers.
type Handler struct {
	DB          DBQuerier     // injectable for tests; set to Pool in production
	Pool        *pgxpool.Pool // raw pool for internal packages that require *pgxpool.Pool
	Cfg         *config.Config
	Storage     storage.Storage
	AuthSvc     *auth.Service
	Logger      *slog.Logger
	Cache       *cache.Client  // nil when Redis is unavailable
	EmailSender email.Sender   // nil-safe; no-op used in dev when RESEND_API_KEY unset
}
