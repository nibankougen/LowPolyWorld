using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// ユーザー認証状態・プロフィールを管理する DontDestroyOnLoad シングルトン。
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
    public ApiClient Api => _apiClient;

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
    }

    public async Task<bool> TryRefreshAccessTokenAsync(CancellationToken ct = default)
    {
        var refreshToken = GetRefreshToken();
        if (string.IsNullOrEmpty(refreshToken))
            return false;

        // Temporarily create a client without auth header to call /auth/refresh
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
        PlayerPrefs.DeleteKey(PrefKeyRefreshToken);
        PlayerPrefs.Save();
    }
}
