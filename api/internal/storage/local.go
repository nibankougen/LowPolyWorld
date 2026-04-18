package storage

import (
	"context"
	"fmt"
	"io"
	"net/http"
	"os"
	"path/filepath"
)

// LocalStorage stores assets on the local filesystem, used in development.
type LocalStorage struct {
	basePath string
	baseURL  string
}

// NewLocalStorage creates a LocalStorage rooted at basePath, with public URLs
// prefixed by baseURL (e.g. "http://localhost:8080").
func NewLocalStorage(basePath, baseURL string) *LocalStorage {
	return &LocalStorage{basePath: basePath, baseURL: baseURL}
}

// Put writes the reader's content to {basePath}/{hash}.{ext}.
func (s *LocalStorage) Put(_ context.Context, hash, ext string, r io.Reader) error {
	if err := os.MkdirAll(s.basePath, 0o755); err != nil {
		return fmt.Errorf("mkdirall: %w", err)
	}
	path := filepath.Join(s.basePath, hash+"."+ext)
	f, err := os.Create(path)
	if err != nil {
		return fmt.Errorf("create file: %w", err)
	}
	defer f.Close()
	if _, err = io.Copy(f, r); err != nil {
		return fmt.Errorf("write file: %w", err)
	}
	return nil
}

// URL returns the HTTP URL for the asset.
func (s *LocalStorage) URL(hash, ext string) string {
	return fmt.Sprintf("%s/assets/%s.%s", s.baseURL, hash, ext)
}

// Exists checks whether the file exists on disk.
func (s *LocalStorage) Exists(_ context.Context, hash, ext string) (bool, error) {
	path := filepath.Join(s.basePath, hash+"."+ext)
	_, err := os.Stat(path)
	if err == nil {
		return true, nil
	}
	if os.IsNotExist(err) {
		return false, nil
	}
	return false, err
}

// Delete removes the file for hash+ext from disk. No-ops if the file is absent.
func (s *LocalStorage) Delete(hash, ext string) error {
	path := filepath.Join(s.basePath, hash+"."+ext)
	err := os.Remove(path)
	if err != nil && !os.IsNotExist(err) {
		return fmt.Errorf("delete file: %w", err)
	}
	return nil
}

// ServeFile serves a local asset file by hash+ext with immutable caching headers.
func (s *LocalStorage) ServeFile(w http.ResponseWriter, r *http.Request, hash, ext string) {
	path := filepath.Join(s.basePath, hash+"."+ext)
	w.Header().Set("Cache-Control", "public, max-age=31536000, immutable")
	http.ServeFile(w, r, path)
}
