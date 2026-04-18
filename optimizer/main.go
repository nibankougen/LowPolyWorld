package main

import (
	"encoding/binary"
	"encoding/json"
	"errors"
	"fmt"
	"io"
	"log/slog"
	"net/http"
	"os"
)

// Limits defined in unity-game-abstract.md Avatar System
const (
	maxFileSizeBytes = 500 * 1024 // 500 KB
	maxTriangles     = 512
	maxBones         = 50
)

func main() {
	logger := slog.New(slog.NewJSONHandler(os.Stdout, nil))
	port := getEnv("PORT", "9090")

	mux := http.NewServeMux()
	mux.HandleFunc("GET /health", func(w http.ResponseWriter, r *http.Request) {
		w.Header().Set("Content-Type", "application/json")
		_, _ = w.Write([]byte(`{"status":"ok"}`))
	})
	mux.HandleFunc("POST /optimize", func(w http.ResponseWriter, r *http.Request) {
		handleOptimize(w, r, logger)
	})

	addr := ":" + port
	logger.Info("optimizer starting", "addr", addr)
	if err := http.ListenAndServe(addr, mux); err != nil {
		logger.Error("server error", "error", err)
		os.Exit(1)
	}
}

// handleOptimize validates a VRM (GLB) file and returns it as-is when valid.
// Request: multipart/form-data with field "vrm_file"
// Response 200: validated VRM bytes (application/octet-stream)
// Response 400: validation error JSON
func handleOptimize(w http.ResponseWriter, r *http.Request, logger *slog.Logger) {
	if err := r.ParseMultipartForm(maxFileSizeBytes * 2); err != nil {
		writeError(w, http.StatusBadRequest, "invalid_request", "failed to parse multipart form")
		return
	}

	file, _, err := r.FormFile("vrm_file")
	if err != nil {
		writeError(w, http.StatusBadRequest, "invalid_request", "vrm_file field is required")
		return
	}
	defer file.Close()

	data, err := io.ReadAll(io.LimitReader(file, maxFileSizeBytes+1))
	if err != nil {
		writeError(w, http.StatusBadRequest, "read_error", "failed to read file")
		return
	}

	if len(data) > maxFileSizeBytes {
		writeError(w, http.StatusUnprocessableEntity, "file_too_large",
			fmt.Sprintf("file size %d bytes exceeds limit of %d bytes", len(data), maxFileSizeBytes))
		return
	}

	gltfDoc, err := parseGLB(data)
	if err != nil {
		writeError(w, http.StatusUnprocessableEntity, "invalid_vrm", err.Error())
		return
	}

	triCount := countTriangles(gltfDoc)
	if triCount > maxTriangles {
		writeError(w, http.StatusUnprocessableEntity, "too_many_triangles",
			fmt.Sprintf("triangle count %d exceeds limit of %d", triCount, maxTriangles))
		return
	}

	boneCount := countBones(gltfDoc)
	if boneCount > maxBones {
		writeError(w, http.StatusUnprocessableEntity, "too_many_bones",
			fmt.Sprintf("bone count %d exceeds limit of %d", boneCount, maxBones))
		return
	}

	logger.Info("vrm validated",
		"size_bytes", len(data),
		"triangles", triCount,
		"bones", boneCount,
	)

	// Strip extensions not needed at runtime (keeps VRM 1.0 required extensions).
	optimized := stripNonEssentialExtensions(data, gltfDoc)

	w.Header().Set("Content-Type", "application/octet-stream")
	w.Header().Set("X-Triangle-Count", fmt.Sprintf("%d", triCount))
	w.Header().Set("X-Bone-Count", fmt.Sprintf("%d", boneCount))
	w.WriteHeader(http.StatusOK)
	_, _ = w.Write(optimized)
}

// ── GLB / glTF helpers ───────────────────────────────────────────────────────

// glbMagic is the first 4 bytes of a valid GLB file.
const glbMagic = 0x46546C67 // "glTF"

// gltfDocument holds the parsed JSON chunk fields we need for validation.
type gltfDocument struct {
	Accessors []struct {
		Count int    `json:"count"`
		Type  string `json:"type"`
	} `json:"accessors"`
	Meshes []struct {
		Primitives []struct {
			Indices *int `json:"indices"`
			Mode    *int `json:"mode"`
		} `json:"primitives"`
	} `json:"meshes"`
	Skins []struct {
		Joints []int `json:"joints"`
	} `json:"skins"`
	ExtensionsUsed []string `json:"extensionsUsed"`
	// jsonOffset and jsonLen track the JSON chunk position in the original GLB
	jsonOffset int
	jsonLen    int
}

func parseGLB(data []byte) (*gltfDocument, error) {
	if len(data) < 12 {
		return nil, errors.New("file too small to be a valid GLB")
	}
	magic := binary.LittleEndian.Uint32(data[0:4])
	if magic != glbMagic {
		return nil, errors.New("not a valid GLB file (magic mismatch)")
	}
	version := binary.LittleEndian.Uint32(data[4:8])
	if version != 2 {
		return nil, fmt.Errorf("unsupported GLB version %d (expected 2)", version)
	}

	// Find JSON chunk (type 0x4E4F534A = "JSON")
	const chunkTypeJSON = 0x4E4F534A
	offset := 12
	for offset+8 <= len(data) {
		chunkLen := int(binary.LittleEndian.Uint32(data[offset:]))
		chunkType := binary.LittleEndian.Uint32(data[offset+4:])
		chunkData := offset + 8
		if chunkType == chunkTypeJSON {
			if chunkData+chunkLen > len(data) {
				return nil, errors.New("GLB JSON chunk truncated")
			}
			var doc gltfDocument
			if err := json.Unmarshal(data[chunkData:chunkData+chunkLen], &doc); err != nil {
				return nil, fmt.Errorf("invalid glTF JSON: %w", err)
			}
			doc.jsonOffset = chunkData
			doc.jsonLen = chunkLen
			return &doc, nil
		}
		offset = chunkData + chunkLen
	}
	return nil, errors.New("GLB JSON chunk not found")
}

// countTriangles counts total triangle faces across all mesh primitives.
// Uses the indices accessor count / 3 when indices are present; otherwise position count / 3.
func countTriangles(doc *gltfDocument) int {
	const modeTriangles = 4 // glTF default
	total := 0
	for _, mesh := range doc.Meshes {
		for _, prim := range mesh.Primitives {
			mode := modeTriangles
			if prim.Mode != nil {
				mode = *prim.Mode
			}
			if mode != modeTriangles {
				continue
			}
			if prim.Indices != nil && *prim.Indices < len(doc.Accessors) {
				total += doc.Accessors[*prim.Indices].Count / 3
			}
		}
	}
	return total
}

// countBones counts the total unique joint nodes across all skins.
func countBones(doc *gltfDocument) int {
	seen := map[int]struct{}{}
	for _, skin := range doc.Skins {
		for _, joint := range skin.Joints {
			seen[joint] = struct{}{}
		}
	}
	return len(seen)
}

// vrmRequiredExtensions lists VRM 1.0 and supporting extensions that must be preserved.
var vrmRequiredExtensions = map[string]struct{}{
	"VRMC_vrm":                    {},
	"VRMC_springBone":             {},
	"VRMC_node_constraint":        {},
	"VRMC_materials_mtoon":        {},
	"VRMC_vrm_animation":          {},
	"KHR_materials_unlit":         {},
	"KHR_texture_transform":       {},
	"KHR_mesh_quantization":       {},
	"EXT_mesh_gpu_instancing":     {},
}

// stripNonEssentialExtensions rewrites the GLB JSON chunk to remove extensionsUsed entries
// for extensions not in vrmRequiredExtensions. The binary chunk is untouched.
func stripNonEssentialExtensions(data []byte, doc *gltfDocument) []byte {
	if doc.jsonOffset == 0 || doc.jsonLen == 0 {
		return data
	}

	// Parse JSON as a generic map to surgically edit extensionsUsed only
	var raw map[string]json.RawMessage
	if err := json.Unmarshal(data[doc.jsonOffset:doc.jsonOffset+doc.jsonLen], &raw); err != nil {
		return data // leave unchanged if re-parse fails
	}

	var used []string
	if err := json.Unmarshal(raw["extensionsUsed"], &used); err != nil || len(used) == 0 {
		return data
	}

	filtered := used[:0]
	for _, ext := range used {
		if _, keep := vrmRequiredExtensions[ext]; keep {
			filtered = append(filtered, ext)
		}
	}
	if len(filtered) == len(used) {
		return data // nothing to strip
	}

	newUsed, err := json.Marshal(filtered)
	if err != nil {
		return data
	}
	raw["extensionsUsed"] = json.RawMessage(newUsed)

	newJSON, err := json.Marshal(raw)
	if err != nil {
		return data
	}

	// Pad newJSON to 4-byte alignment (GLB requirement)
	for len(newJSON)%4 != 0 {
		newJSON = append(newJSON, 0x20) // space
	}

	// Rebuild GLB: header + JSON chunk + remaining chunks
	const chunkTypeJSON = 0x4E4F534A
	headerAndBefore := data[:12]
	remainingChunks := data[doc.jsonOffset+doc.jsonLen:]

	out := make([]byte, 0, len(data)+len(newJSON))
	out = append(out, headerAndBefore...)

	// Write new JSON chunk
	chunkLenBuf := make([]byte, 4)
	binary.LittleEndian.PutUint32(chunkLenBuf, uint32(len(newJSON)))
	out = append(out, chunkLenBuf...)
	chunkTypeBuf := make([]byte, 4)
	binary.LittleEndian.PutUint32(chunkTypeBuf, chunkTypeJSON)
	out = append(out, chunkTypeBuf...)
	out = append(out, newJSON...)
	out = append(out, remainingChunks...)

	// Patch total length in GLB header
	binary.LittleEndian.PutUint32(out[8:], uint32(len(out)))

	return out
}

// ── Error helpers ────────────────────────────────────────────────────────────

func writeError(w http.ResponseWriter, status int, code, message string) {
	w.Header().Set("Content-Type", "application/json")
	w.WriteHeader(status)
	body, _ := json.Marshal(map[string]any{
		"error": map[string]string{"code": code, "message": message},
	})
	_, _ = w.Write(body)
}

func getEnv(key, fallback string) string {
	if v := os.Getenv(key); v != "" {
		return v
	}
	return fallback
}
