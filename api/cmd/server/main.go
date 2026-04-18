package main

import (
	"context"
	"fmt"
	"log/slog"
	"net/http"
	"os"
	"path/filepath"
	"strings"
	"time"

	"github.com/go-chi/chi/v5"
	chimiddleware "github.com/go-chi/chi/v5/middleware"
	"github.com/go-chi/cors"
	"github.com/go-chi/httprate"
	"github.com/nibankougen/LowPolyWorld/api/internal/auth"
	"github.com/nibankougen/LowPolyWorld/api/internal/cache"
	"github.com/nibankougen/LowPolyWorld/api/internal/config"
	"github.com/nibankougen/LowPolyWorld/api/internal/db"
	"github.com/nibankougen/LowPolyWorld/api/internal/handler"
	mw "github.com/nibankougen/LowPolyWorld/api/internal/middleware"
	"github.com/nibankougen/LowPolyWorld/api/internal/storage"
)

func main() {
	logger := slog.New(slog.NewJSONHandler(os.Stdout, nil))

	cfg, err := config.Load()
	if err != nil {
		logger.Error("load config", "error", err)
		os.Exit(1)
	}

	ctx := context.Background()

	// Connect to database
	pool, err := db.Connect(ctx, cfg.DatabaseURL)
	if err != nil {
		logger.Error("connect db", "error", err)
		os.Exit(1)
	}
	defer pool.Close()

	// Run migrations
	migrationsPath := resolveMigrationsPath()
	if err := db.RunMigrations(cfg.DatabaseURL, migrationsPath); err != nil {
		logger.Error("run migrations", "error", err)
		os.Exit(1)
	}
	logger.Info("migrations applied")

	// Init auth service
	authSvc, err := auth.NewService(ctx, cfg.JWTPrivateKeyPath, cfg.JWTPublicKeyPath,
		cfg.GoogleClientID, cfg.AppleClientID)
	if err != nil {
		logger.Error("init auth service", "error", err)
		os.Exit(1)
	}

	// Init storage
	var store storage.Storage
	var localStorage *storage.LocalStorage
	if cfg.StorageBackend == "local" {
		ls := storage.NewLocalStorage(cfg.StorageLocalPath, cfg.StorageBaseURL)
		localStorage = ls
		store = ls
	} else {
		// Production storage (R2/S3) would be initialised here
		logger.Error("unsupported storage backend", "backend", cfg.StorageBackend)
		os.Exit(1)
	}

	redisCache, err := cache.New(cfg.RedisURL)
	if err != nil {
		logger.Warn("redis unavailable, caching disabled", "error", err)
		redisCache = nil
	} else {
		defer redisCache.Close()
		logger.Info("redis connected", "url", cfg.RedisURL)
	}

	h := &handler.Handler{
		DB:      pool,
		Cfg:     cfg,
		Storage: store,
		AuthSvc: authSvc,
		Logger:  logger,
		Cache:   redisCache,
	}

	authMW := mw.NewAuthMiddleware(authSvc, pool)

	r := chi.NewRouter()

	// Global middleware
	r.Use(chimiddleware.RealIP)
	r.Use(mw.RequestID)
	r.Use(mw.AccessLog(logger))
	r.Use(chimiddleware.Recoverer)
	r.Use(chimiddleware.Compress(5))
	r.Use(cors.Handler(cors.Options{
		AllowedOrigins:   []string{"*"},
		AllowedMethods:   []string{"GET", "POST", "PUT", "DELETE", "OPTIONS"},
		AllowedHeaders:   []string{"Accept", "Authorization", "Content-Type", "X-Request-ID"},
		ExposedHeaders:   []string{"X-Request-ID"},
		AllowCredentials: false,
		MaxAge:           300,
	}))

	// Health check
	r.Get("/health", func(w http.ResponseWriter, r *http.Request) {
		w.Header().Set("Content-Type", "application/json")
		_, _ = w.Write([]byte(`{"status":"ok"}`))
	})

	// Version endpoint
	r.Get("/api/version", h.GetVersion)

	// Auth endpoints (rate limited: 5 req/min per IP)
	authRateLimiter := httprate.LimitByIP(5, time.Minute)
	r.Route("/auth", func(r chi.Router) {
		r.Use(authRateLimiter)
		r.Post("/google/callback", h.GoogleCallback)
		r.Post("/apple/callback", h.AppleCallback)
		r.Post("/refresh", h.RefreshToken)
		r.With(authMW.Authenticate).Post("/logout", h.Logout)
	})

	// Authenticated startup
	r.With(authMW.Authenticate).Get("/startup", h.GetStartup)

	// Authenticated API v1 routes
	r.Route("/api/v1", func(r chi.Router) {
		r.Use(authMW.Authenticate)

		// Me (user profile)
		r.Get("/me", h.GetMe)
		r.Put("/me/name", h.SetupName)
		r.Delete("/me", h.DeleteAccount)

		// Me — extended
		r.Patch("/me/language", h.SetLanguage)
		r.Patch("/me/display-name", h.UpdateDisplayName)

		// Me — auth providers
		r.Get("/me/auth-providers", h.ListAuthProviders)
		r.Post("/me/auth-providers/{provider}", h.LinkAuthProvider)
		r.Delete("/me/auth-providers/{provider}", h.UnlinkAuthProvider)

		// Public user profiles
		r.Get("/users/{userID}", h.GetPublicUser)

		// Me — friends
		r.Get("/me/friends/rooms", h.ListFriendsRooms)

		// Me — avatars
		r.Get("/me/avatars", h.ListMyAvatars)
		r.Post("/me/avatars", h.UploadAvatar)
		r.Delete("/me/avatars/{avatarID}", h.DeleteAvatar)
		r.Put("/me/avatars/{avatarID}/texture", h.UpdateAvatarTexture)

		// Me — accessories
		r.Get("/me/accessories", h.ListMyAccessories)
		r.Post("/me/accessories", h.UploadAccessory)
		r.Delete("/me/accessories/{accessoryID}", h.DeleteAccessory)
		r.Put("/me/accessories/{accessoryID}/texture", h.UpdateAccessoryTexture)

		// Worlds
		r.Get("/worlds/search", h.SearchWorlds)
		r.Get("/worlds/new", h.ListNewWorlds)
		r.Get("/worlds/liked", h.ListLikedWorlds)
		r.Get("/worlds/following", h.ListFollowingWorlds)
		r.Get("/worlds/{worldID}", h.GetWorld)
		r.Post("/worlds/{worldID}/like", h.LikeWorld)
		r.Delete("/worlds/{worldID}/like", h.UnlikeWorld)

		// Rooms
		r.Get("/worlds/{worldID}/rooms", h.ListRooms)
		r.Post("/worlds/{worldID}/rooms", h.CreateRoom)
		r.Post("/worlds/{worldID}/rooms/recommended-join", h.RecommendedJoin)
		r.Post("/rooms/{roomID}/join", h.JoinRoom)
		r.Delete("/rooms/{roomID}/leave", h.LeaveRoom)
		r.Patch("/rooms/{roomID}/language", h.PatchRoomLanguage)
	})

	// Admin internal routes (triggered by Cloud Scheduler in production)
	r.Post("/admin/internal/run-batch/{batchName}", h.RunBatch)
	r.Patch("/admin/users/{userID}/restore", h.RestoreAccount)

	// Dev-only: serve local asset files
	if !cfg.IsProduction() && localStorage != nil {
		r.Get("/assets/{filename}", func(w http.ResponseWriter, r *http.Request) {
			filename := chi.URLParam(r, "filename")
			// filename is expected to be "{hash}.{ext}"
			dot := strings.LastIndex(filename, ".")
			if dot < 0 {
				http.NotFound(w, r)
				return
			}
			hash := filename[:dot]
			ext := filename[dot+1:]
			localStorage.ServeFile(w, r, hash, ext)
		})
	}

	addr := fmt.Sprintf(":%s", cfg.Port)
	logger.Info("server starting", "addr", addr, "env", cfg.AppEnv)
	if err := http.ListenAndServe(addr, r); err != nil {
		logger.Error("server error", "error", err)
		os.Exit(1)
	}
}

// resolveMigrationsPath finds the migrations directory relative to the binary or the working directory.
func resolveMigrationsPath() string {
	// Try next to binary first
	exe, err := os.Executable()
	if err == nil {
		p := filepath.Join(filepath.Dir(exe), "migrations")
		if _, err := os.Stat(p); err == nil {
			return p
		}
	}
	// Fall back to CWD
	return "migrations"
}
