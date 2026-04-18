.PHONY: dev build test migrate-up migrate-down gen-keys tidy

gen-keys:
	mkdir -p secrets
	openssl genrsa -out secrets/jwt_private_key 2048
	openssl rsa -in secrets/jwt_private_key -pubout -out secrets/jwt_public_key

tidy:
	go mod tidy

build:
	go build -o bin/server ./cmd/server

dev:
	go run ./cmd/server

test:
	go test ./...

migrate-up:
	DATABASE_URL=$(DATABASE_URL) go run ./cmd/migrate up

migrate-down:
	DATABASE_URL=$(DATABASE_URL) go run ./cmd/migrate down
