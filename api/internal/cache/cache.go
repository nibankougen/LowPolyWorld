package cache

import (
	"context"
	"time"

	"github.com/redis/go-redis/v9"
)

// Client wraps a Redis client with helpers for the Write-Around caching strategy.
type Client struct {
	rdb *redis.Client
}

// New connects to Redis at the given URL and returns a Client.
func New(redisURL string) (*Client, error) {
	opt, err := redis.ParseURL(redisURL)
	if err != nil {
		return nil, err
	}
	rdb := redis.NewClient(opt)
	return &Client{rdb: rdb}, nil
}

// Get returns the cached value for key, or ("", false) on miss or error.
func (c *Client) Get(ctx context.Context, key string) (string, bool) {
	val, err := c.rdb.Get(ctx, key).Result()
	if err != nil {
		return "", false
	}
	return val, true
}

// Set stores value under key with the given TTL.
func (c *Client) Set(ctx context.Context, key, value string, ttl time.Duration) {
	c.rdb.Set(ctx, key, value, ttl)
}

// Del deletes one or more keys.
func (c *Client) Del(ctx context.Context, keys ...string) {
	if len(keys) > 0 {
		c.rdb.Del(ctx, keys...)
	}
}

// ScanDel deletes all keys matching a glob pattern using SCAN to avoid blocking.
func (c *Client) ScanDel(ctx context.Context, pattern string) {
	var cursor uint64
	for {
		keys, next, err := c.rdb.Scan(ctx, cursor, pattern, 100).Result()
		if err != nil {
			return
		}
		if len(keys) > 0 {
			c.rdb.Del(ctx, keys...)
		}
		cursor = next
		if cursor == 0 {
			return
		}
	}
}

// Close closes the underlying Redis connection.
func (c *Client) Close() error {
	return c.rdb.Close()
}
