using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// ユーザー認証状態・プロフィール・アバター選択状態を管理する DontDestroyOnLoad シングルトン。
/// - JWT アクセストークンはメモリのみに保持
/// - リフレッシュトークンは PlayerPrefs に保持（暗号化なし・開発環境）
/// - Bootstrapper が初期化後に StartupData を格納する
/// </summary>
public class UserManager : MonoBehaviour
{
    private const string PrefKeyRefreshToken = "auth_refresh_token";

    public static UserManager Instance { get; private set; }

    [SerializeField] private AppConfig _config;

    private string _accessToken;
    private ApiClient _apiClient;

    // ── Public state ────────────────────────────────────────────────────────

    public bool IsLoggedIn => !string.IsNullOrEmpty(_accessToken);
    public StartupUserProfile Profile { get; private set; }
    public PlanCapabilities Capabilities { get; private set; }
    public List<StartupAvatar> Avatars { get; private set; } = new List<StartupAvatar>();
    public ApiClient Api => _apiClient;

    /// <summary>現在選択中のアバターのローカルキャッシュパス。null = 未ダウンロードまたはアバターなし。</summary>
    public string SelectedAvatarLocalPath { get; private set; }

    // ── Lifecycle ────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        _apiClient = new ApiClient(_config.ApiBaseUrl, GetAccessToken);
    }

    // ── Token management ─────────────────────────────────────────────────────

    public string GetAccessToken() => _accessToken;

    public bool HasRefreshToken() => !string.IsNullOrEmpty(PlayerPrefs.GetString(PrefKeyRefreshToken, ""));

    public string GetRefreshToken() => PlayerPrefs.GetString(PrefKeyRefreshToken, "");

    public void StoreTokens(string accessToken, string refreshToken)
    {
        _accessToken = accessToken;
        if (!string.IsNullOrEmpty(refreshToken))
        {
            PlayerPrefs.SetString(PrefKeyRefreshToken, refreshToken);
            PlayerPrefs.Save();
        }
    }

    public void StoreStartupData(StartupResponse data)
    {
        Profile = data.user;
        Capabilities = data.planCapabilities;
        Avatars = data.avatars ?? new List<StartupAvatar>();
    }

    public async Task<bool> TryRefreshAccessTokenAsync(CancellationToken ct = default)
    {
        var refreshToken = GetRefreshToken();
        if (string.IsNullOrEmpty(refreshToken))
            return false;

        var anonClient = new ApiClient(_config.ApiBaseUrl);
        var (result, error) = await anonClient.PostJsonAsync<TokenResponse>(
            "/auth/refresh",
            new RefreshRequest { refresh_token = refreshToken },
            ct
        );

        if (error != null || result == null)
        {
            ClearSession();
            return false;
        }

        StoreTokens(result.access_token, result.refresh_token);
        return true;
    }

    public void ClearSession()
    {
        _accessToken = null;
        Profile = null;
        Avatars = new List<StartupAvatar>();
        SelectedAvatarLocalPath = null;
        PlayerPrefs.DeleteKey(PrefKeyRefreshToken);
        PlayerPrefs.Save();
    }

    // ── Avatar management ────────────────────────────────────────────────────

    /// <summary>
    /// 選択中のアバターを手動で切り替える（アバター管理画面から呼ぶ）。
    /// </summary>
    public void SelectAvatar(string localPath)
    {
        SelectedAvatarLocalPath = localPath;
    }

    /// <summary>
    /// /startup で取得したアバター一覧の先頭を CacheManager 経由でダウンロードし、
    /// SelectedAvatarLocalPath にセットする。アバターがなければ何もしない。
    /// </summary>
    public async Task DownloadAndCacheAvatarsAsync(CancellationToken ct)
    {
        if (CacheManager.Instance == null || Avatars.Count == 0) return;

        // 先頭アバターを自動選択（Phase 2でユーザーが選択できるようになる）
        var first = Avatars[0];
        if (string.IsNullOrEmpty(first.vrmUrl) || string.IsNullOrEmpty(first.vrmHash)) return;

        var (path, error) = await CacheManager.Instance.GetOrDownloadAsync(
            first.vrmUrl, first.vrmHash, "vrm", isOwn: true, ct
        );

        if (ct.IsCancellationRequested) return;

        if (error != null)
        {
            Debug.LogWarning($"[UserManager] Avatar download failed: {error}");
            return;
        }

        SelectedAvatarLocalPath = path;
    }
}
