package response

import (
	"encoding/json"
	"net/http"
)

type ctxKey string

const RequestIDKey ctxKey = "request_id"

type envelope struct {
	Data any `json:"data"`
}

type cursorEnvelope struct {
	Data   any    `json:"data"`
	Cursor Cursor `json:"cursor"`
}

type Cursor struct {
	Next    string `json:"next"`
	HasMore bool   `json:"has_more"`
}

type errEnvelope struct {
	Error errBody `json:"error"`
}

type errBody struct {
	Code      string      `json:"code"`
	Message   string      `json:"message,omitempty"`
	Details   []ErrDetail `json:"details,omitempty"`
	RequestID string      `json:"request_id,omitempty"`
}

type ErrDetail struct {
	Field   string `json:"field"`
	Code    string `json:"code"`
	Message string `json:"message"`
}

func JSON(w http.ResponseWriter, status int, data any) {
	w.Header().Set("Content-Type", "application/json")
	w.WriteHeader(status)
	_ = json.NewEncoder(w).Encode(envelope{Data: data})
}

func JSONCursor(w http.ResponseWriter, status int, data any, cursor Cursor) {
	w.Header().Set("Content-Type", "application/json")
	w.WriteHeader(status)
	_ = json.NewEncoder(w).Encode(cursorEnvelope{Data: data, Cursor: cursor})
}

func Error(w http.ResponseWriter, r *http.Request, status int, code, message string) {
	w.Header().Set("Content-Type", "application/json")
	w.WriteHeader(status)
	body := errBody{Code: code, Message: message}
	if id, ok := r.Context().Value(RequestIDKey).(string); ok {
		body.RequestID = id
	}
	_ = json.NewEncoder(w).Encode(errEnvelope{Error: body})
}

func ValidationError(w http.ResponseWriter, r *http.Request, details []ErrDetail) {
	w.Header().Set("Content-Type", "application/json")
	w.WriteHeader(http.StatusBadRequest)
	body := errBody{
		Code:    "validation_error",
		Message: "one or more fields are invalid",
		Details: details,
	}
	if id, ok := r.Context().Value(RequestIDKey).(string); ok {
		body.RequestID = id
	}
	_ = json.NewEncoder(w).Encode(errEnvelope{Error: body})
}

func InternalError(w http.ResponseWriter, r *http.Request, isProd bool) {
	w.Header().Set("Content-Type", "application/json")
	w.WriteHeader(http.StatusInternalServerError)
	body := errBody{Code: "internal_server_error"}
	if id, ok := r.Context().Value(RequestIDKey).(string); ok {
		body.RequestID = id
	}
	_ = json.NewEncoder(w).Encode(errEnvelope{Error: body})
}
