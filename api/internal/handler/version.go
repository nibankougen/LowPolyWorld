package handler

import (
	"net/http"

	"github.com/nibankougen/LowPolyWorld/api/internal/response"
)

type versionResponse struct {
	MinCompatibleVersion int `json:"min_compatible_version"`
	LatestVersion        int `json:"latest_version"`
}

// GetVersion returns the API version compatibility information.
func (h *Handler) GetVersion(w http.ResponseWriter, r *http.Request) {
	response.JSON(w, http.StatusOK, versionResponse{
		MinCompatibleVersion: h.Cfg.MinAppVersion,
		LatestVersion:        h.Cfg.AppVersion,
	})
}
