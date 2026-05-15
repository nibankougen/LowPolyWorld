using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.UIElements;

/// <summary>
/// アカウント設定画面コントローラー。
/// @name 変更 / 言語選択 / ソーシャルプロバイダー管理 / アカウント削除 を担当する。
/// 仕様: screens-and-modes.md セクション 5.1, 10.4, 18
/// </summary>
public class AccountSettingsController : IDisposable
{
    private static readonly (string code, string label)[] Languages =
    {
        ("ja", "日本語"),
        ("en", "English"),
        ("zh-Hans", "中文（简体）"),
        ("zh-Hant", "中文（繁體）"),
        ("ko", "한국어"),
        ("fr", "Français"),
        ("es", "Español"),
        ("it", "Italiano"),
        ("de", "Deutsch"),
        ("pt-BR", "Português (Brasil)"),
    };

    private static readonly string[] ProviderCodes = { "google", "x", "apple" };
    private static readonly string[] ProviderLabels = { "Google", "X (Twitter)", "Apple" };

    private readonly CancellationTokenSource _cts = new();

    // @name
    private readonly Label _labelCurrentName;
    private readonly Label _labelNextChange;
    private readonly TextField _fieldNewName;
    private readonly Label _labelNameError;
    private readonly Button _btnChangeName;

    // Language
    private readonly DropdownField _dropdownLanguage;
    private bool _dropdownInitialized;

    // Providers
    private readonly VisualElement _providerList;
    private List<SocialProviderEntry> _providers = new();

    // Dialog
    private readonly VisualElement _dialogBackdrop;
    private readonly Label _dialogTitle;
    private readonly Label _dialogBody;
    private readonly Button _btnDialogConfirm;

    // Cached profile
    private MeProfileResponse _profile;

    public event Action OnBackRequested;
    public event Action OnLoggedOut;

    public AccountSettingsController(VisualElement root)
    {
        _labelCurrentName = root.Q<Label>("label-current-name");
        _labelNextChange = root.Q<Label>("label-next-change");
        _fieldNewName = root.Q<TextField>("field-new-name");
        _labelNameError = root.Q<Label>("label-name-error");
        _btnChangeName = root.Q<Button>("btn-change-name");
        _dropdownLanguage = root.Q<DropdownField>("dropdown-language");
        _providerList = root.Q<VisualElement>("provider-list");
        _dialogBackdrop = root.Q<VisualElement>("dialog-backdrop");
        _dialogTitle = root.Q<Label>("dialog-title");
        _dialogBody = root.Q<Label>("dialog-body");
        _btnDialogConfirm = root.Q<Button>("btn-dialog-confirm");

        root.Q<Button>("btn-back")?.RegisterCallback<ClickEvent>(_ => OnBackRequested?.Invoke());
        root.Q<Button>("btn-contact")?.RegisterCallback<ClickEvent>(
            _ => Application.OpenURL("mailto:nibankougen@gmail.com")
        );
        root.Q<Button>("btn-delete-account")?.RegisterCallback<ClickEvent>(_ => ShowDeleteDialog());
        root.Q<Button>("btn-dialog-cancel")?.RegisterCallback<ClickEvent>(_ => HideDialog());

        _dialogBackdrop?.RegisterCallback<ClickEvent>(OnBackdropClicked);
        _btnChangeName?.RegisterCallback<ClickEvent>(_ => OnChangeNameClicked());
        _fieldNewName?.RegisterValueChangedCallback(e => ValidateNameField(e.newValue));
        _btnChangeName?.SetEnabled(false);

        SetupLanguageDropdown();

        // キャッシュ済みデータで先行表示してから API で補完
        ShowCachedProfile();
        LoadFromApiAsync();
    }

    // ── 初期表示 ────────────────────────────────────────────────────────────────

    private void ShowCachedProfile()
    {
        var cached = UserManager.Instance?.Profile;
        if (cached == null) return;

        if (_labelCurrentName != null)
            _labelCurrentName.text = $"@{cached.name}";

        var canChange = UserManager.Instance?.Capabilities?.nameChange ?? false;
        if (_fieldNewName != null) _fieldNewName.SetEnabled(canChange);
        if (!canChange && _labelNextChange != null)
            _labelNextChange.text = "@name の変更にはプレミアム会員が必要です";

        SetDropdownToLanguage(cached.language);
    }

    private async void LoadFromApiAsync()
    {
        if (UserManager.Instance == null) return;
        var api = UserManager.Instance.Api;
        var ct = _cts.Token;

        try
        {
            var (profile, _) = await api.GetAsync<MeProfileResponse>("/api/v1/me/profile", ct);
            if (ct.IsCancellationRequested) return;
            if (profile != null)
            {
                _profile = profile;
                ApplyProfile();
            }

            var (providersRes, _) = await api.GetAsync<SocialProvidersResponse>(
                "/api/v1/me/providers",
                ct
            );
            if (ct.IsCancellationRequested) return;
            _providers = providersRes?.providers ?? new List<SocialProviderEntry>();
            RebuildProviderList();
        }
        catch (OperationCanceledException) { }
        catch (Exception e)
        {
            Debug.LogWarning($"[AccountSettings] load error: {e.Message}");
        }
    }

    private void ApplyProfile()
    {
        if (_profile == null) return;
        var canChange = UserManager.Instance?.Capabilities?.nameChange ?? false;

        if (_labelCurrentName != null)
            _labelCurrentName.text = $"@{_profile.name}";

        if (_fieldNewName != null)
            _fieldNewName.SetEnabled(canChange);

        if (_labelNextChange != null)
        {
            if (canChange)
            {
                var formatted = AccountSettingsLogic.FormatDate(_profile.nextNameChangeAt);
                _labelNextChange.text = formatted != null
                    ? $"次回変更可能日: {formatted}"
                    : "現在変更可能です";
            }
            else
            {
                _labelNextChange.text = "@name の変更にはプレミアム会員が必要です";
            }
        }

        SetDropdownToLanguage(_profile.language);
    }

    // ── 言語ドロップダウン ─────────────────────────────────────────────────────

    private void SetupLanguageDropdown()
    {
        if (_dropdownLanguage == null) return;

        var choices = new List<string>();
        foreach (var (_, lbl) in Languages)
            choices.Add(lbl);
        _dropdownLanguage.choices = choices;

        // choices 設定後に初期化完了フラグを立て、ユーザー操作のみ保存する
        _dropdownInitialized = true;

        _dropdownLanguage.RegisterValueChangedCallback(e =>
        {
            if (!_dropdownInitialized) return;
            var idx = Array.FindIndex(Languages, l => l.label == e.newValue);
            if (idx < 0) return;
            SaveLanguageAsync(Languages[idx].code);
        });
    }

    private void SetDropdownToLanguage(string code)
    {
        if (_dropdownLanguage == null || string.IsNullOrEmpty(code)) return;
        var idx = Array.FindIndex(Languages, l => l.code == code);
        if (idx >= 0)
            _dropdownLanguage.SetValueWithoutNotify(Languages[idx].label);
    }

    private async void SaveLanguageAsync(string code)
    {
        var locale = LocalizationSettings.AvailableLocales.GetLocale(code);
        if (locale != null)
            LocalizationSettings.SelectedLocale = locale;

        if (UserManager.Instance == null) return;
        var ct = _cts.Token;
        try
        {
            await UserManager.Instance.Api.PatchJsonAsync<object>(
                "/api/v1/me/language",
                new SetLanguageRequest { language = code },
                ct
            );
        }
        catch (OperationCanceledException) { }
        catch (Exception e)
        {
            Debug.LogWarning($"[AccountSettings] language save error: {e.Message}");
        }
    }

    // ── ソーシャルプロバイダー ─────────────────────────────────────────────────

    private void RebuildProviderList()
    {
        _providerList?.Clear();
        if (_providerList == null) return;

        var linked = new HashSet<string>();
        foreach (var p in _providers)
            linked.Add(p.provider);

        var canUnlink = _providers.Count > 1;

        for (var i = 0; i < ProviderCodes.Length; i++)
        {
            var row = BuildProviderRow(ProviderCodes[i], ProviderLabels[i], linked.Contains(ProviderCodes[i]), canUnlink);
            // :last-child は USS 非対応のため末尾行の境界線をコードで除去
            if (i == ProviderCodes.Length - 1)
                row.style.borderBottomWidth = 0;
            _providerList.Add(row);
        }
    }

    private VisualElement BuildProviderRow(string code, string label, bool isLinked, bool canUnlink)
    {
        var row = new VisualElement();
        row.AddToClassList("provider-row");

        var lbl = new Label(label);
        lbl.AddToClassList("provider-label");
        row.Add(lbl);

        var spacer = new VisualElement();
        spacer.style.flexGrow = 1;
        row.Add(spacer);

        if (isLinked)
        {
            var status = new Label("連携中");
            status.AddToClassList("provider-status-linked");
            row.Add(status);

            var btnUnlink = new Button(() => UnlinkProviderAsync(code)) { text = "解除" };
            btnUnlink.AddToClassList("provider-btn-unlink");
            btnUnlink.SetEnabled(canUnlink);
            if (!canUnlink)
                btnUnlink.tooltip = "最低1つのプロバイダーが必要です";
            row.Add(btnUnlink);
        }
        else
        {
            var btnLink = new Button(() => OnAddProviderClicked(code)) { text = "追加" };
            btnLink.AddToClassList("provider-btn-link");
            row.Add(btnLink);
        }

        return row;
    }

    private async void UnlinkProviderAsync(string provider)
    {
        if (UserManager.Instance == null) return;
        var ct = _cts.Token;
        try
        {
            var error = await UserManager.Instance.Api.DeleteAsync(
                $"/api/v1/me/providers/{provider}",
                ct
            );
            if (ct.IsCancellationRequested) return;
            if (error != null)
            {
                FlashMessageController.Current?.Show("解除に失敗しました", FlashMessageType.Error);
                return;
            }
            _providers.RemoveAll(p => p.provider == provider);
            RebuildProviderList();
            FlashMessageController.Current?.Show("連携を解除しました");
        }
        catch (OperationCanceledException) { }
        catch (Exception e)
        {
            Debug.LogWarning($"[AccountSettings] unlink error: {e.Message}");
        }
    }

    private void OnAddProviderClicked(string provider)
    {
        // TODO: OAuth 追加フローは Phase 5 の OAuth 実装を再利用して実装予定
        FlashMessageController.Current?.Show("この機能は近日公開予定です", FlashMessageType.Info);
    }

    // ── @name 変更 ──────────────────────────────────────────────────────────────

    private void ValidateNameField(string value)
    {
        if (_labelNameError == null || _btnChangeName == null) return;
        if (string.IsNullOrEmpty(value))
        {
            _labelNameError.text = string.Empty;
            _btnChangeName.SetEnabled(false);
            return;
        }
        var result = AccountSettingsLogic.ValidateName(value);
        _labelNameError.text = AccountSettingsLogic.ValidationMessage(result);
        _btnChangeName.SetEnabled(result == AccountSettingsLogic.NameValidationResult.Ok);
    }

    private async void OnChangeNameClicked()
    {
        if (_fieldNewName == null || UserManager.Instance == null) return;

        var newName = AccountSettingsLogic.NormalizeName(_fieldNewName.value);
        if (AccountSettingsLogic.ValidateName(newName) != AccountSettingsLogic.NameValidationResult.Ok)
            return;

        _btnChangeName?.SetEnabled(false);
        var ct = _cts.Token;
        try
        {
            var (res, error) = await UserManager.Instance.Api.PatchJsonAsync<ChangeNameResponse>(
                "/api/v1/me/name",
                new SetNameRequest { name = newName },
                ct
            );
            if (ct.IsCancellationRequested) return;

            if (error != null)
            {
                var msg = error switch
                {
                    "name_taken" => "この @name はすでに使用されています",
                    "name_change_too_soon" => "まだ @name を変更できません",
                    "premium_required" => "@name の変更にはプレミアム会員が必要です",
                    _ => "@name の変更に失敗しました",
                };
                if (_labelNameError != null) _labelNameError.text = msg;
                _btnChangeName?.SetEnabled(true);
                return;
            }

            if (_profile != null && res != null)
            {
                _profile.name = res.name;
                _profile.nextNameChangeAt = res.nextNameChangeAt;
            }
            _fieldNewName.SetValueWithoutNotify(string.Empty);
            if (_labelNameError != null) _labelNameError.text = string.Empty;
            _btnChangeName?.SetEnabled(false);
            ApplyProfile();
            FlashMessageController.Current?.Show("@name を変更しました");
        }
        catch (OperationCanceledException) { }
        catch (Exception e)
        {
            Debug.LogWarning($"[AccountSettings] name change error: {e.Message}");
            _btnChangeName?.SetEnabled(true);
        }
    }

    // ── アカウント削除ダイアログ ───────────────────────────────────────────────

    private void ShowDeleteDialog()
    {
        if (_dialogBackdrop == null) return;
        if (_dialogTitle != null)
            _dialogTitle.text = "アカウントを削除しますか？";
        if (_dialogBody != null)
            _dialogBody.text =
                "・アカウントは即時ログアウトされ、アクセスできなくなります\n"
                + "・表示名・@name・連携サービス情報は即時削除されます\n"
                + "・アップロードしたアバター・ワールド・オブジェクトなどのデータは30日後に完全に削除されます\n"
                + "・購入履歴・コイン取引履歴は削除されません\n"
                + "・削除したアカウントは復旧できません";
        if (_btnDialogConfirm != null)
        {
            _btnDialogConfirm.text = "削除する";
            // 二重登録防止: 先に解除してから登録
            _btnDialogConfirm.clicked -= OnDeleteConfirmed;
            _btnDialogConfirm.clicked += OnDeleteConfirmed;
        }
        _dialogBackdrop.RemoveFromClassList("overlay-hidden");
    }

    private void HideDialog()
    {
        _dialogBackdrop?.AddToClassList("overlay-hidden");
        if (_btnDialogConfirm != null)
            _btnDialogConfirm.clicked -= OnDeleteConfirmed;
    }

    private void OnBackdropClicked(ClickEvent e)
    {
        // ダイアログボックス外タップで閉じる
        if (e.target == _dialogBackdrop)
            HideDialog();
    }

    private async void OnDeleteConfirmed()
    {
        HideDialog();
        if (UserManager.Instance == null) return;
        try
        {
            await UserManager.Instance.Api.DeleteAsync("/api/v1/me", _cts.Token);
        }
        catch (OperationCanceledException) { return; }
        catch (Exception e)
        {
            Debug.LogWarning($"[AccountSettings] delete account error: {e.Message}");
        }
        UserManager.Instance.ClearSession();
        OnLoggedOut?.Invoke();
    }

    // ── Dispose ─────────────────────────────────────────────────────────────────

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
        HideDialog();
    }
}
