package main

import (
	"context"
	"fmt"
	"log/slog"
	"os"

	"github.com/nibankougen/LowPolyWorld/api/internal/batch"
	"github.com/nibankougen/LowPolyWorld/api/internal/config"
	"github.com/nibankougen/LowPolyWorld/api/internal/db"
	"github.com/nibankougen/LowPolyWorld/api/internal/storage"
)

func main() {
	if len(os.Args) < 2 {
		fmt.Fprintln(os.Stderr, "usage: batch <command>")
		fmt.Fprintln(os.Stderr, "commands:")
		fmt.Fprintln(os.Stderr, "  delete-expired-accounts")
		fmt.Fprintln(os.Stderr, "  cleanup-access-logs")
		os.Exit(1)
	}

	logger := slog.New(slog.NewJSONHandler(os.Stdout, nil))

	cfg, err := config.Load()
	if err != nil {
		logger.Error("load config", "error", err)
		os.Exit(1)
	}

	ctx := context.Background()

	pool, err := db.Connect(ctx, cfg.DatabaseURL)
	if err != nil {
		logger.Error("connect db", "error", err)
		os.Exit(1)
	}
	defer pool.Close()

	var store storage.Storage
	if cfg.StorageBackend == "local" {
		store = storage.NewLocalStorage(cfg.StorageLocalPath, cfg.StorageBaseURL)
	} else {
		logger.Error("unsupported storage backend", "backend", cfg.StorageBackend)
		os.Exit(1)
	}

	cmd := os.Args[1]
	switch cmd {
	case "delete-expired-accounts":
		count, err := batch.DeleteExpiredAccounts(ctx, pool, store, logger)
		if err != nil {
			logger.Error("batch failed", "batch", cmd, "error", err)
			os.Exit(1)
		}
		logger.Info("batch completed",
			"event", "batch_completed",
			"batch", "delete-expired-accounts",
			"affected_count", count,
		)
	case "cleanup-access-logs":
		// Retention is enforced by Cloud Logging bucket policy (1-year TTL).
		logger.Info("batch completed",
			"event", "batch_completed",
			"batch", "cleanup-access-logs",
			"affected_count", 0,
		)
	default:
		fmt.Fprintf(os.Stderr, "unknown command: %s\n", cmd)
		os.Exit(1)
	}
}
