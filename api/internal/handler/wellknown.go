package handler

import "net/http"

// ServeAppleAppSiteAssociation handles GET /.well-known/apple-app-site-association.
// Required for iOS Universal Links domain verification.
// Before shipping: replace "TEAMID" with the actual Apple Developer Team ID.
func (h *Handler) ServeAppleAppSiteAssociation(w http.ResponseWriter, r *http.Request) {
	w.Header().Set("Content-Type", "application/json")
	_, _ = w.Write([]byte(`{
  "applinks": {
    "details": [{
      "appIDs": ["TEAMID.com.nibankougen.lowpolyworld"],
      "components": [{"/" : "/invite/*", "comment": "招待リンク"}]
    }]
  }
}`))
}

// ServeAssetLinks handles GET /.well-known/assetlinks.json.
// Required for Android App Links domain verification.
// Before shipping: replace the placeholder sha256_cert_fingerprints value with
// the actual SHA-256 fingerprint of the production signing certificate.
func (h *Handler) ServeAssetLinks(w http.ResponseWriter, r *http.Request) {
	w.Header().Set("Content-Type", "application/json")
	_, _ = w.Write([]byte(`[{
  "relation": ["delegate_permission/common.handle_all_urls"],
  "target": {
    "namespace": "android_app",
    "package_name": "com.nibankougen.lowpolyworld",
    "sha256_cert_fingerprints": ["REPLACE_WITH_SHA256_CERT_FINGERPRINT"]
  }
}]`))
}
