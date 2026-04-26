package config

import (
	"fmt"
	"os"
	"strconv"
)

type Config struct {
	AppEnv       string
	Port         string
	DatabaseURL  string
	RedisURL     string
	OptimizerURL string

	StorageBackend   string
	StorageLocalPath string
	StorageBaseURL   string

	JWTPrivateKeyPath string
	JWTPublicKeyPath  string

	GoogleClientID string
	AppleClientID  string

	ResendAPIKey string
	BatchSecret  string

	AppleBundleID     string
	GooglePackageName string
	PubSubAudience    string

	AppVersion    int
	MinAppVersion int
}

func Load() (*Config, error) {
	appVersion, err := parseInt("APP_VERSION", "1")
	if err != nil {
		return nil, err
	}
	minAppVersion, err := parseInt("MIN_APP_VERSION", "1")
	if err != nil {
		return nil, err
	}

	return &Config{
		AppEnv:            getEnv("APP_ENV", "development"),
		Port:              getEnv("PORT", "8080"),
		DatabaseURL:       mustEnv("DATABASE_URL"),
		RedisURL:          getEnv("REDIS_URL", "redis://localhost:6379"),
		OptimizerURL:      getEnv("OPTIMIZER_URL", "http://localhost:9090"),
		StorageBackend:    getEnv("STORAGE_BACKEND", "local"),
		StorageLocalPath:  getEnv("STORAGE_LOCAL_PATH", "/data/assets"),
		StorageBaseURL:    getEnv("STORAGE_BASE_URL", "http://localhost:8080"),
		JWTPrivateKeyPath: getEnv("JWT_PRIVATE_KEY_PATH", "secrets/jwt_private_key"),
		JWTPublicKeyPath:  getEnv("JWT_PUBLIC_KEY_PATH", "secrets/jwt_public_key"),
		GoogleClientID:    getEnv("GOOGLE_CLIENT_ID", ""),
		AppleClientID:     getEnv("APPLE_CLIENT_ID", ""),
		ResendAPIKey:      getEnv("RESEND_API_KEY", ""),
		BatchSecret:       getEnv("BATCH_SECRET", ""),
		AppleBundleID:     getEnv("APPLE_BUNDLE_ID", ""),
		GooglePackageName: getEnv("GOOGLE_PACKAGE_NAME", ""),
		PubSubAudience:    getEnv("PUBSUB_AUDIENCE", ""),
		AppVersion:        appVersion,
		MinAppVersion:     minAppVersion,
	}, nil
}

func (c *Config) IsProduction() bool { return c.AppEnv == "production" }

func getEnv(key, fallback string) string {
	if v := os.Getenv(key); v != "" {
		return v
	}
	return fallback
}

func mustEnv(key string) string {
	v := os.Getenv(key)
	if v == "" {
		panic(fmt.Sprintf("required environment variable %s is not set", key))
	}
	return v
}

func parseInt(key, fallback string) (int, error) {
	v := getEnv(key, fallback)
	n, err := strconv.Atoi(v)
	if err != nil {
		return 0, fmt.Errorf("invalid value for %s: %w", key, err)
	}
	return n, nil
}
