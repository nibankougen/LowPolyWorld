using System;
using System.Threading;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// マイプロフィール画面コントローラー。
/// 表示名・@name・フォロワー/フォロー中/フレンド数・公認バッジを表示し、表示名の編集を担当する。
/// 仕様: screens-and-modes.md セクション 22.1
/// </summary>
public class MyProfileController : IDisposable
{
    private readonly CancellationTokenSource _cts = new();

    private readonly Label _displayName;
    private readonly Label _verifiedBadge;
    private readonly Label _atName;
    private readonly Label _followerCount;
    private readonly Label _followingCount;
    private readonly Label _friendCount;
    private readonly TextField _fieldDisplayName;
    private readonly Label _labelNameError;
    private readonly Button _btnSaveName;

    public event Action OnBackRequested;

    /// <summary>フォロワー一覧への遷移要求。引数は自分のユーザーID。</summary>
    public event Action<string> OnFollowersRequested;

    /// <summary>フォロー中一覧への遷移要求。引数は自分のユーザーID。</summary>
    public event Action<string> OnFollowingRequested;

    /// <summary>フレンド管理画面への遷移要求。</summary>
    public event Action OnFriendsRequested;

    public MyProfileController(VisualElement root)
    {
        _displayName = root.Q<Label>("myp-display-name");
        _verifiedBadge = root.Q<Label>("myp-verified-badge");
        _atName = root.Q<Label>("myp-at-name");
        _followerCount = root.Q<Label>("myp-follower-count");
        _followingCount = root.Q<Label>("myp-following-count");
        _friendCount = root.Q<Label>("myp-friend-count");
        _fieldDisplayName = root.Q<TextField>("field-display-name");
        _labelNameError = root.Q<Label>("label-name-error");
        _btnSaveName = root.Q<Button>("btn-save-name");

        root.Q<Button>("btn-back")?.RegisterCallback<ClickEvent>(_ => OnBackRequested?.Invoke());

        root.Q<Button>("btn-followers")?.RegisterCallback<ClickEvent>(_ =>
            OnFollowersRequested?.Invoke(UserManager.Instance?.Profile?.id));
        root.Q<Button>("btn-following")?.RegisterCallback<ClickEvent>(_ =>
            OnFollowingRequested?.Invoke(UserManager.Instance?.Profile?.id));
        root.Q<Button>("btn-friends")?.RegisterCallback<ClickEvent>(_ =>
            OnFriendsRequested?.Invoke());

        _fieldDisplayName?.RegisterValueChangedCallback(e => ValidateDisplayNameField(e.newValue));
        _btnSaveName?.RegisterCallback<ClickEvent>(_ => OnSaveNameClicked());
        _btnSaveName?.SetEnabled(false);

        ApplyCachedProfile();
        LoadFromApiAsync();
    }

    // ── 初期表示 ────────────────────────────────────────────────────────────────

    private void ApplyCachedProfile()
    {
        var profile = UserManager.Instance?.Profile;
        if (profile == null) return;

        if (_displayName != null)
            _displayName.text = profile.displayName ?? "";
        if (_atName != null)
            _atName.text = string.IsNullOrEmpty(profile.name) ? "" : $"@{profile.name}";
        if (_fieldDisplayName != null)
            _fieldDisplayName.SetValueWithoutNotify(profile.displayName ?? "");

        SetVerifiedBadge(profile.isVerified);

        // フレンド数はローカルキャッシュから即時反映
        var count = FriendManager.Instance?.Logic?.FriendCount ?? 0;
        if (_friendCount != null)
            _friendCount.text = MyProfileLogic.FormatSocialCount(count);
    }

    private void SetVerifiedBadge(bool isVerified)
    {
        if (_verifiedBadge == null) return;
        if (isVerified)
            _verifiedBadge.RemoveFromClassList("overlay-hidden");
        else
            _verifiedBadge.AddToClassList("overlay-hidden");
    }

    private async void LoadFromApiAsync()
    {
        if (UserManager.Instance == null) return;
        var profileId = UserManager.Instance.Profile?.id;
        if (string.IsNullOrEmpty(profileId)) return;

        var api = UserManager.Instance.Api;
        var ct = _cts.Token;

        try
        {
            var (pub, _) = await api.GetAsync<PublicUserResponse>(
                $"/api/v1/users/{profileId}", ct);
            if (ct.IsCancellationRequested) return;
            if (pub != null)
            {
                if (_followerCount != null)
                    _followerCount.text = MyProfileLogic.FormatSocialCount(pub.followerCount);
                if (_followingCount != null)
                    _followingCount.text = MyProfileLogic.FormatSocialCount(pub.followingCount);
                // isVerified は startup キャッシュより公開プロフィールを優先
                SetVerifiedBadge(pub.isVerified);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception e)
        {
            Debug.LogWarning($"[MyProfile] load error: {e.Message}");
        }
    }

    // ── 表示名編集 ──────────────────────────────────────────────────────────────

    private void ValidateDisplayNameField(string value)
    {
        if (_labelNameError == null || _btnSaveName == null) return;

        var current = UserManager.Instance?.Profile?.displayName ?? "";
        if (string.IsNullOrWhiteSpace(value) || value.Trim() == current)
        {
            _labelNameError.text = string.Empty;
            _btnSaveName.SetEnabled(false);
            return;
        }

        var result = MyProfileLogic.ValidateDisplayName(value);
        _labelNameError.text = MyProfileLogic.ValidationMessage(result);
        _btnSaveName.SetEnabled(result == MyProfileLogic.DisplayNameValidationResult.Ok);
    }

    private async void OnSaveNameClicked()
    {
        if (_fieldDisplayName == null || UserManager.Instance == null) return;

        var newName = _fieldDisplayName.value?.Trim() ?? "";
        var result = MyProfileLogic.ValidateDisplayName(newName);
        if (result != MyProfileLogic.DisplayNameValidationResult.Ok) return;

        _btnSaveName?.SetEnabled(false);
        var ct = _cts.Token;

        try
        {
            var (res, error) = await UserManager.Instance.Api.PatchJsonAsync<SetDisplayNameResponse>(
                "/api/v1/me/display-name",
                new SetDisplayNameRequest { displayName = newName },
                ct
            );
            if (ct.IsCancellationRequested) return;

            if (error != null)
            {
                if (_labelNameError != null) _labelNameError.text = "表示名の保存に失敗しました";
                _btnSaveName?.SetEnabled(true);
                return;
            }

            var saved = res?.displayName ?? newName;
            if (_displayName != null) _displayName.text = saved;
            if (UserManager.Instance.Profile != null) UserManager.Instance.Profile.displayName = saved;
            _fieldDisplayName.SetValueWithoutNotify(saved);
            if (_labelNameError != null) _labelNameError.text = string.Empty;
            _btnSaveName?.SetEnabled(false);
            FlashMessageController.Current?.Show("表示名を変更しました");
        }
        catch (OperationCanceledException) { }
        catch (Exception e)
        {
            Debug.LogWarning($"[MyProfile] save name error: {e.Message}");
            _btnSaveName?.SetEnabled(true);
        }
    }

    // ── Dispose ─────────────────────────────────────────────────────────────────

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }
}
