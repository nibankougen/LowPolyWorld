using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// タイトル画面の起動フロー制御。
/// 1. GET /api/version → 非互換ならアップデートモーダル
/// 2. リフレッシュトークンあり → /startup → ホームへ遷移
/// 3. トークンなし → ログインモーダル
/// 4. ログイン後 @name 未設定 → name-setup モーダル
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class TitleScreenController : MonoBehaviour
{
    private const int ClientVersion = 1;

    [SerializeField] private AppConfig _config;

    private UIDocument _document;
    private VisualElement _loadingArea;
    private VisualElement _loginModal;
    private VisualElement _nameSetupModal;
    private VisualElement _updateModal;
    private VisualElement _errorArea;

    // Login modal
    private Toggle _termsToggle;
    private Toggle _privacyToggle;
    private Button _btnGoogle;
    private Button _btnApple;
    private Button _btnTermsLink;
    private Button _btnPrivacyLink;

    // Name setup modal
    private TextField _nameInput;
    private Label _nameError;
    private Button _btnNameConfirm;

    // Error area
    private Label _errorLabel;
    private Button _btnRetry;

    private CancellationTokenSource _cts;
    private Action _retryAction;

    private void Awake()
    {
        _document = GetComponent<UIDocument>();
    }

    private void OnEnable()
    {
        var root = _document.rootVisualElement;

        _loadingArea = root.Q<VisualElement>("loading-area");
        _loginModal = root.Q<VisualElement>("login-modal");
        _nameSetupModal = root.Q<VisualElement>("name-setup-modal");
        _updateModal = root.Q<VisualElement>("update-modal");
        _errorArea = root.Q<VisualElement>("error-area");

        _termsToggle = root.Q<Toggle>("terms-toggle");
        _privacyToggle = root.Q<Toggle>("privacy-toggle");
        _btnGoogle = root.Q<Button>("btn-google");
        _btnApple = root.Q<Button>("btn-apple");
        _btnTermsLink = root.Q<Button>("terms-link");
        _btnPrivacyLink = root.Q<Button>("privacy-link");

        _nameInput = root.Q<TextField>("name-input");
        _nameError = root.Q<Label>("name-error");
        _btnNameConfirm = root.Q<Button>("btn-name-confirm");

        _errorLabel = root.Q<Label>("error-label");
        _btnRetry = root.Q<Button>("btn-retry");

        _termsToggle.RegisterValueChangedCallback(_ => UpdateSignInButtons());
        _privacyToggle.RegisterValueChangedCallback(_ => UpdateSignInButtons());
        _btnGoogle.clicked += () => OnSocialSignInClicked("google");
        _btnApple.clicked += () => OnSocialSignInClicked("apple");
        _btnTermsLink.clicked += () => UnityEngine.Application.OpenURL(_config.TermsOfServiceUrl);
        _btnPrivacyLink.clicked += () => UnityEngine.Application.OpenURL(_config.PrivacyPolicyUrl);
        _btnNameConfirm.clicked += () => _ = OnNameConfirmClickedAsync();
        _btnRetry.clicked += () => _retryAction?.Invoke();

        root.Q<Button>("btn-store").clicked += OpenStore;

        _cts = new CancellationTokenSource();
        _ = RunStartupFlowAsync(_cts.Token);
    }

    private void OnDisable()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    // ── Startup flow ─────────────────────────────────────────────────────────

    private async Task RunStartupFlowAsync(CancellationToken ct)
    {
        ShowLoading(true);

        // Step 1: API version check
        var anonClient = new ApiClient(_config.ApiBaseUrl);
        var (versionData, versionError) = await anonClient.GetAsync<ApiVersionResponse>("/api/version", ct);
        if (ct.IsCancellationRequested) return;

        if (versionError != null)
        {
            ShowError("サーバーに接続できませんでした。", () => _ = RunStartupFlowAsync(_cts.Token));
            return;
        }

        if (ClientVersion < versionData.min_compatible_version)
        {
            ShowUpdateModal();
            return;
        }

        // Step 2: Try auto-login with stored refresh token
        if (UserManager.Instance.HasRefreshToken())
        {
            var refreshed = await UserManager.Instance.TryRefreshAccessTokenAsync(ct);
            if (ct.IsCancellationRequested) return;

            if (refreshed)
            {
                await FetchStartupAndNavigateAsync(ct);
                return;
            }
        }

        // Step 3: Show login modal
        ShowLoading(false);
        ShowModal(_loginModal);
    }

    private async Task FetchStartupAndNavigateAsync(CancellationToken ct)
    {
        ShowLoading(true);

        var (startupData, error) = await UserManager.Instance.Api.GetAsync<StartupResponse>("/startup", ct);
        if (ct.IsCancellationRequested) return;

        if (error != null)
        {
            ShowError("起動データの取得に失敗しました。", () => _ = FetchStartupAndNavigateAsync(_cts.Token));
            return;
        }

        UserManager.Instance.StoreStartupData(startupData);

        if (startupData.user.nameSetupRequired)
        {
            ShowLoading(false);
            ShowModal(_nameSetupModal);
            return;
        }

        NavigateToHome();
    }

    // ── Social sign in ────────────────────────────────────────────────────────

    private void OnSocialSignInClicked(string provider)
    {
        // Native OAuth flow is platform-dependent; redirect back via deep link.
        // For development, open a simulated URL and await the result token via callback.
        // TODO: integrate with Google Sign-In SDK (iOS/Android) in Phase 5 polish.
        ShowError($"{provider} サインインは現在開発中です。", null);
    }

    // Called by native bridge after OAuth completes with a provider-issued token
    public void OnOAuthCallback(string provider, string idToken)
    {
        _ = HandleOAuthCallbackAsync(provider, idToken, _cts?.Token ?? CancellationToken.None);
    }

    private async Task HandleOAuthCallbackAsync(string provider, string idToken, CancellationToken ct)
    {
        ShowLoading(true);
        HideModal(_loginModal);

        var anonClient = new ApiClient(_config.ApiBaseUrl);
        var body = new { id_token = idToken };
        var (result, error) = await anonClient.PostJsonAsync<TokenResponse>(
            $"/auth/{provider}/callback",
            body,
            ct
        );

        if (ct.IsCancellationRequested) return;

        if (error != null)
        {
            ShowModal(_loginModal);
            ShowError("サインインに失敗しました: " + error, null);
            ShowLoading(false);
            return;
        }

        UserManager.Instance.StoreTokens(result.access_token, result.refresh_token);
        await FetchStartupAndNavigateAsync(ct);
    }

    // ── Name setup ────────────────────────────────────────────────────────────

    private async Task OnNameConfirmClickedAsync()
    {
        var name = _nameInput.value?.Trim() ?? "";
        _nameError.style.display = DisplayStyle.None;

        if (name.Length < 3 || name.Length > 15)
        {
            ShowNameError("3〜15文字で入力してください");
            return;
        }

        _btnNameConfirm.SetEnabled(false);

        var (_, error) = await UserManager.Instance.Api.PutJsonAsync<object>(
            "/api/v1/me/name",
            new SetNameRequest { name = name },
            _cts?.Token ?? CancellationToken.None
        );

        _btnNameConfirm.SetEnabled(true);

        if (error != null)
        {
            var msg = error switch
            {
                "name_taken" => "この @name は既に使用されています",
                "forbidden" => "この操作には権限が必要です",
                _ => "設定に失敗しました: " + error,
            };
            ShowNameError(msg);
            return;
        }

        HideModal(_nameSetupModal);
        NavigateToHome();
    }

    // ── UI helpers ────────────────────────────────────────────────────────────

    private void ShowLoading(bool visible)
    {
        _loadingArea.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        _errorArea.style.display = DisplayStyle.None;
    }

    private void ShowModal(VisualElement modal)
    {
        _loadingArea.style.display = DisplayStyle.None;
        _errorArea.style.display = DisplayStyle.None;
        modal.style.display = DisplayStyle.Flex;
        UpdateSignInButtons();
    }

    private void HideModal(VisualElement modal) => modal.style.display = DisplayStyle.None;

    private void ShowUpdateModal()
    {
        ShowLoading(false);
        _updateModal.style.display = DisplayStyle.Flex;
    }

    private void ShowError(string message, Action retry)
    {
        _loadingArea.style.display = DisplayStyle.None;
        _errorLabel.text = message;
        _errorArea.style.display = DisplayStyle.Flex;
        _retryAction = retry;
        _btnRetry.style.display = retry != null ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void ShowNameError(string msg)
    {
        _nameError.text = msg;
        _nameError.style.display = DisplayStyle.Flex;
    }

    private void UpdateSignInButtons()
    {
        var canSignIn = _termsToggle.value && _privacyToggle.value;
        _btnGoogle.SetEnabled(canSignIn);
        _btnApple.SetEnabled(canSignIn);
    }

    private static void OpenStore()
    {
#if UNITY_IOS
        UnityEngine.Application.OpenURL("https://apps.apple.com/app/id000000000");
#elif UNITY_ANDROID
        UnityEngine.Application.OpenURL("market://details?id=world.lo_res.app");
#endif
    }

    private static void NavigateToHome()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene("HomeScene");
    }
}
