package storage

import (
	"context"
	"io"
)

// Storage is the content-addressed asset store interface.
type Storage interface {
	// Put stores the reader's content under {hash}.{ext}.
	Put(ctx context.Context, hash, ext string, r io.Reader) error
	// URL returns the public URL for the asset identified by hash+ext.
	URL(hash, ext string) string
	// Exists checks whether the asset already exists in the store.
	Exists(ctx context.Context, hash, ext string) (bool, error)
	// Delete removes the asset identified by hash+ext. No-ops if already absent.
	Delete(hash, ext string) error
}
