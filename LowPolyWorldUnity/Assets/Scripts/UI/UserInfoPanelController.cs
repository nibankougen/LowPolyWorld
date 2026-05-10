using System;
using System.Threading;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// ユーザー情報パネルを管理するコントローラー。
/// アバタータップ時・フレンドリストタップ時などに ShowAsync で開く。
/// 仕様: screens-and-modes.md セクション 2.4
/// </summary>
public class UserInfoPanelController : IDisposable
{
    private readonly VisualElement _backdrop;
    private readonly VisualElement _actions;
    private readonly VisualElement _moreMenu;

    private readonly Label _displayName;
    private readonly Label _atName;
    private readonly Label _followerCount;
    private readonly Label _followingCount;

    private readonly Button _btnFollow;
    private readonly Button _btnUnfollow;
    private readonly Button _btnFriendRequest;
    private readonly Button _btnFriendPending;
    private readonly Button _btnFriendReceived;
    private readonly Button _btnRemoveFriend;
    private readonly Button _btnMore;
    private readonly Button _btnHideUser;
    private readonly Button _btnUnhideUser;
    private readonly Button _btnReportUser;

    private string _targetUserId;
    private CancellationTokenSource _cts;

    /// <summary>通報ボタン押下時に発火する（targetUserId, isAlreadyHidden）。</summary>
    public event Action<string, bool> OnReportRequested;

    /// <summary>フォロワー数タップ時に発火する（targetUserId）。</summary>
    public event Action<string> OnFollowersRequested;

    /// <summary>フォロー中数タップ時に発火する（targetUserId）。</summary>
    public event Action<string> OnFollowingRequested;

    public UserInfoPanelController(VisualElement backdrop)
    {
        _backdrop = backdrop;
        _actions = backdrop.Q<VisualElement>("uip-actions");
        _moreMenu = backdrop.Q<VisualElement>("uip-more-menu");

        _displayName = backdrop.Q<Label>("uip-display-name");
        _atName = backdrop.Q<Label>("uip-at-name");
        _followerCount = backdrop.Q<Label>("uip-follower-count");
        _followingCount = backdrop.Q<Label>("uip-following-count");

        _btnFollow = backdrop.Q<Button>("btn-follow");
        _btnUnfollow = backdrop.Q<Button>("btn-unfollow");
        _btnFriendRequest = backdrop.Q<Button>("btn-friend-request");
        _btnFriendPending = backdrop.Q<Button>("btn-friend-pending");
        _btnFriendReceived = backdrop.Q<Button>("btn-friend-received");
        _btnRemoveFriend = backdrop.Q<Button>("btn-remove-friend");
        _btnMore = backdrop.Q<Button>("btn-more");
        _btnHideUser = backdrop.Q<Button>("btn-hide-user");
        _btnUnhideUser = backdrop.Q<Button>("btn-unhide-user");
        _btnReportUser = backdrop.Q<Button>("btn-report-user");

        backdrop.Q<Button>("btn-close")?.RegisterCallback<ClickEvent>(_ => Hide());
        backdrop.RegisterCallback<ClickEvent>(OnBackdropClicked);

        _btnFollow?.RegisterCallback<ClickEvent>(_ => OnFollowClicked());
        _btnUnfollow?.RegisterCallback<ClickEvent>(_ => OnUnfollowClicked());
        _btnFriendRequest?.RegisterCallback<ClickEvent>(_ => OnFriendRequestClicked());
        _btnFriendPending?.RegisterCallback<ClickEvent>(_ => OnCancelFriendRequestClicked());
        _btnFriendReceived?.RegisterCallback<ClickEvent>(_ => OnAcceptFriendRequestClicked());
        _btnRemoveFriend?.RegisterCallback<ClickEvent>(_ => OnRemoveFriendClicked());
        _btnMore?.RegisterCallback<ClickEvent>(_ => ToggleMoreMenu());
        _btnHideUser?.RegisterCallback<ClickEvent>(_ => OnHideClicked());
        _btnUnhideUser?.RegisterCallback<ClickEvent>(_ => OnUnhideClicked());
        _btnReportUser?.RegisterCallback<ClickEvent>(_ => OnReportClicked());
        backdrop.Q<Button>("btn-followers")?.RegisterCallback<ClickEvent>(_ => OnFollowersRequested?.Invoke(_targetUserId));
        backdrop.Q<Button>("btn-following")?.RegisterCallback<ClickEvent>(_ => OnFollowingRequested?.Invoke(_targetUserId));

        _backdrop.AddToClassList("overlay-hidden");
    }

    /// <summary>指定ユーザー ID のパネルを非同期で開く。</summary>
    public async void ShowAsync(string userId)
    {
        _targetUserId = userId;
        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        _backdrop.RemoveFromClassList("overlay-hidden");
        _displayName.text = "";
        _atName.text = "";
        _followerCount.text = "–";
        _followingCount.text = "–";
        _moreMenu.AddToClassList("overlay-hidden");

        bool isSelf = UserManager.Instance?.Profile?.id == userId;
        if (_actions != null)
        {
            if (isSelf)
                _actions.AddToClassList("overlay-hidden");
            else
                _actions.RemoveFromClassList("overlay-hidden");
        }

        try
        {
            var api = UserManager.Instance?.Api;
            if (api == null) return;

            var (res, err) = await api.GetAsync<PublicUserResponse>($"/api/v1/users/{userId}", _cts.Token);
            if (err != null || res == null) return;

            _displayName.text = res.displayName;
            _atName.text = string.IsNullOrEmpty(res.name) ? "" : $"@{res.name}";
            _followerCount.text = res.followerCount.ToString();
            _followingCount.text = res.followingCount.ToString();

            if (!isSelf)
                RefreshActionButtons();
        }
        catch (OperationCanceledException) { }
        catch (Exception e)
        {
            Debug.LogWarning($"[UserInfoPanel] failed: {e.Message}");
        }
    }

    private void RefreshActionButtons()
    {
        bool isFollowing = FollowManager.Instance?.Logic.IsFollowing(_targetUserId) ?? false;
        bool isHidden = HideManager.Instance?.Logic.IsHidden(_targetUserId) ?? false;
        var friendStatus = FriendManager.Instance?.Logic.GetStatus(_targetUserId) ?? FriendRelationStatus.None;

        SetVisible(_btnFollow, !isFollowing);
        SetVisible(_btnUnfollow, isFollowing);

        SetVisible(_btnFriendRequest, friendStatus == FriendRelationStatus.None && !isHidden);
        SetVisible(_btnFriendPending, friendStatus == FriendRelationStatus.RequestSent);
        SetVisible(_btnFriendReceived, friendStatus == FriendRelationStatus.RequestReceived);
        SetVisible(_btnRemoveFriend, friendStatus == FriendRelationStatus.Friends);

        SetVisible(_btnHideUser, !isHidden);
        SetVisible(_btnUnhideUser, isHidden);
    }

    public void Hide()
    {
        _cts?.Cancel();
        _backdrop.AddToClassList("overlay-hidden");
        _moreMenu.AddToClassList("overlay-hidden");
    }

    private void OnBackdropClicked(ClickEvent e)
    {
        if (e.target == _backdrop)
            Hide();
    }

    private void ToggleMoreMenu()
    {
        if (_moreMenu.ClassListContains("overlay-hidden"))
            _moreMenu.RemoveFromClassList("overlay-hidden");
        else
            _moreMenu.AddToClassList("overlay-hidden");
    }

    private async void OnFollowClicked()
    {
        if (FollowManager.Instance == null) return;
        bool ok = await FollowManager.Instance.FollowAsync(_targetUserId);
        if (ok)
        {
            RefreshActionButtons();
            FlashMessageController.Current?.Show("フォローしました", FlashMessageType.Success);
        }
    }

    private async void OnUnfollowClicked()
    {
        if (FollowManager.Instance == null) return;
        bool ok = await FollowManager.Instance.UnfollowAsync(_targetUserId);
        if (ok)
        {
            RefreshActionButtons();
            FlashMessageController.Current?.Show("フォローを解除しました");
        }
    }

    private async void OnFriendRequestClicked()
    {
        if (FriendManager.Instance == null) return;
        string status = await FriendManager.Instance.SendFriendRequestAsync(_targetUserId);
        if (status != null)
        {
            RefreshActionButtons();
            FlashMessageController.Current?.Show("フレンド申請を送りました", FlashMessageType.Success);
        }
    }

    private async void OnCancelFriendRequestClicked()
    {
        if (FriendManager.Instance == null) return;
        bool ok = await FriendManager.Instance.CancelFriendRequestAsync(_targetUserId);
        if (ok) RefreshActionButtons();
    }

    private async void OnAcceptFriendRequestClicked()
    {
        if (FriendManager.Instance == null) return;
        bool ok = await FriendManager.Instance.AcceptFriendRequestAsync(_targetUserId);
        if (ok)
        {
            RefreshActionButtons();
            FlashMessageController.Current?.Show("フレンドになりました！", FlashMessageType.Success);
        }
    }

    private async void OnRemoveFriendClicked()
    {
        if (FriendManager.Instance == null) return;
        await FriendManager.Instance.RemoveFriendAsync(_targetUserId);
        RefreshActionButtons();
    }

    private async void OnHideClicked()
    {
        if (HideManager.Instance == null) return;
        bool ok = await HideManager.Instance.HideUserAsync(_targetUserId);
        if (ok)
        {
            _moreMenu.AddToClassList("overlay-hidden");
            RefreshActionButtons();
            FlashMessageController.Current?.Show("非表示にしました");
        }
    }

    private async void OnUnhideClicked()
    {
        if (HideManager.Instance == null) return;
        bool ok = await HideManager.Instance.UnhideUserAsync(_targetUserId);
        if (ok)
        {
            _moreMenu.AddToClassList("overlay-hidden");
            RefreshActionButtons();
        }
    }

    private void OnReportClicked()
    {
        bool isHidden = HideManager.Instance?.Logic.IsHidden(_targetUserId) ?? false;
        _moreMenu.AddToClassList("overlay-hidden");
        OnReportRequested?.Invoke(_targetUserId, isHidden);
    }

    private static void SetVisible(VisualElement el, bool visible)
    {
        if (el == null) return;
        if (visible)
            el.RemoveFromClassList("overlay-hidden");
        else
            el.AddToClassList("overlay-hidden");
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }
}
