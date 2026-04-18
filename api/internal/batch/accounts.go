package batch

import (
	"context"
	"log/slog"
	"time"

	"github.com/jackc/pgx/v5/pgxpool"
	"github.com/nibankougen/LowPolyWorld/api/internal/storage"
)

const expiredAccountsRetentionDays = 30

// DeleteExpiredAccounts hard-deletes active_users rows where deleted_at is older than 30 days,
// and removes uploaded assets from storage if no other user references the same content hash.
// The users row (and any financial/audit references to it) is preserved.
// Returns the number of accounts deleted.
func DeleteExpiredAccounts(ctx context.Context, db *pgxpool.Pool, store storage.Storage, logger *slog.Logger) (int, error) {
	cutoff := time.Now().UTC().Add(-expiredAccountsRetentionDays * 24 * time.Hour)

	// Collect user IDs to delete
	rows, err := db.Query(ctx,
		`SELECT user_id FROM active_users WHERE deleted_at IS NOT NULL AND deleted_at <= $1`,
		cutoff,
	)
	if err != nil {
		return 0, err
	}
	var userIDs []string
	for rows.Next() {
		var id string
		if err := rows.Scan(&id); err != nil {
			rows.Close()
			return 0, err
		}
		userIDs = append(userIDs, id)
	}
	rows.Close()
	if err := rows.Err(); err != nil {
		return 0, err
	}

	if len(userIDs) == 0 {
		return 0, nil
	}

	for _, userID := range userIDs {
		if err := deleteUserAssets(ctx, db, store, logger, userID); err != nil {
			logger.Warn("failed to delete assets for user, skipping", "user_id", userID, "error", err)
		}

		// Delete avatar and accessory rows (reference users.id ON DELETE NO ACTION, so not auto-cascaded)
		if _, err := db.Exec(ctx, `DELETE FROM accessories WHERE user_id = $1`, userID); err != nil {
			logger.Error("failed to delete accessories", "user_id", userID, "error", err)
			return 0, err
		}
		if _, err := db.Exec(ctx, `DELETE FROM avatars WHERE user_id = $1`, userID); err != nil {
			logger.Error("failed to delete avatars", "user_id", userID, "error", err)
			return 0, err
		}

		if _, err := db.Exec(ctx,
			`DELETE FROM active_users WHERE user_id = $1`, userID,
		); err != nil {
			logger.Error("failed to hard-delete active_users", "user_id", userID, "error", err)
			return 0, err
		}
	}

	return len(userIDs), nil
}

// deleteUserAssets removes avatar and accessory files from storage that are exclusively owned by userID.
func deleteUserAssets(ctx context.Context, db *pgxpool.Pool, store storage.Storage, logger *slog.Logger, userID string) error {
	// Collect avatar hashes
	rows, err := db.Query(ctx,
		`SELECT vrm_hash, texture_hash FROM avatars WHERE user_id = $1`, userID,
	)
	if err != nil {
		return err
	}
	type hashExt struct {
		hash string
		ext  string
	}
	var candidates []hashExt
	for rows.Next() {
		var vrmHash string
		var texHash *string
		if err := rows.Scan(&vrmHash, &texHash); err != nil {
			rows.Close()
			return err
		}
		if vrmHash != "" {
			candidates = append(candidates, hashExt{vrmHash, "vrm"})
		}
		if texHash != nil && *texHash != "" {
			candidates = append(candidates, hashExt{*texHash, "png"})
		}
	}
	rows.Close()

	// Collect accessory hashes
	rows, err = db.Query(ctx,
		`SELECT glb_hash, texture_hash FROM accessories WHERE user_id = $1`, userID,
	)
	if err != nil {
		return err
	}
	for rows.Next() {
		var glbHash string
		var texHash *string
		if err := rows.Scan(&glbHash, &texHash); err != nil {
			rows.Close()
			return err
		}
		if glbHash != "" {
			candidates = append(candidates, hashExt{glbHash, "glb"})
		}
		if texHash != nil && *texHash != "" {
			candidates = append(candidates, hashExt{*texHash, "png"})
		}
	}
	rows.Close()

	// For each candidate hash, delete only if no other user references it
	for _, c := range candidates {
		var refCount int
		_ = db.QueryRow(ctx,
			`SELECT count(*) FROM (
				SELECT 1 FROM avatars WHERE (vrm_hash = $1 OR texture_hash = $1) AND user_id != $2
				UNION ALL
				SELECT 1 FROM accessories WHERE (glb_hash = $1 OR texture_hash = $1) AND user_id != $2
			) refs`,
			c.hash, userID,
		).Scan(&refCount)
		if refCount > 0 {
			continue
		}
		if err := store.Delete(c.hash, c.ext); err != nil {
			logger.Warn("storage delete failed", "hash", c.hash, "ext", c.ext, "error", err)
		}
	}

	return nil
}
