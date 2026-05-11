using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// フレンド管理画面のコントローラー。
/// 3タブ（フレンド / 申請中 / 申請受信）を管理する。
/// 仕様: screens-and-modes.md セクション 9.3
/// </summary>
public class FriendScreenController : IDisposable
{
    private readonly VisualElement _root;
    private readonly ScrollView _list;
    private readonly VisualElement _empty;
    private readonly Label _emptyLabel;

    private readonly Button _tabFriends;
    private readonly Button _tabSent;
    private readonly Button _tabReceived;
    private readonly VisualElement _searchBar;
    private readonly TextField _fieldSearch;

    private enum Tab { Friends, Sent, Received }
    private Tab _activeTab = Tab.Friends;

    // キャッシュ: サーバーから取得したユーザー一覧
    private List<UserSummaryResponse> _friends = new();
    private List<UserSummaryResponse> _sentRequests = new();
    private List<UserSummaryResponse> _receivedRequests = new();

    private CancellationTokenSource _cts = new();

    /// <summary>戻るボタン押下時に発火する。</summary>
    public event Action OnBackRequested;

    /// <summary>ユーザー行タップ時に発火する（userId）。</summary>
    public event Action<string> OnUserTapped;

    /// <summary>「フレンドがいるルーム」ボタン押下時に発火する。</summary>
    public event Action OnFriendsRoomsRequested;

    public FriendScreenController(VisualElement root)
    {
        _root = root;
        _list = root.Q<ScrollView>("friend-list");
        _empty = root.Q<VisualElement>("empty-state");
        _emptyLabel = root.Q<Label>("empty-label");

        _tabFriends = root.Q<Button>("tab-friends");
        _tabSent = root.Q<Button>("tab-sent");
        _tabReceived = root.Q<Button>("tab-received");
        _searchBar = root.Q<VisualElement>("search-bar");
        _fieldSearch = root.Q<TextField>("field-search");

        root.Q<Button>("btn-back")?.RegisterCallback<ClickEvent>(_ => OnBackRequested?.Invoke());
        root.Q<Button>("btn-friends-rooms")?.RegisterCallback<ClickEvent>(_ => OnFriendsRoomsRequested?.Invoke());
        root.Q<Button>("btn-search")?.RegisterCallback<ClickEvent>(_ => OnSearchClicked());

        _tabFriends?.RegisterCallback<ClickEvent>(_ => SelectTab(Tab.Friends));
        _tabSent?.RegisterCallback<ClickEvent>(_ => SelectTab(Tab.Sent));
        _tabReceived?.RegisterCallback<ClickEvent>(_ => SelectTab(Tab.Received));

        LoadAllAsync();
    }

    private async void LoadAllAsync()
    {
        if (UserManager.Instance == null) return;
        var api = UserManager.Instance.Api;
        var ct = _cts.Token;

        try
        {
            var (friends, _) = await api.GetAsync<UserSummaryListResponse>("/api/v1/me/friends", ct);
            _friends = friends?.users ?? new List<UserSummaryResponse>();

            var (sent, _) = await api.GetAsync<UserSummaryListResponse>("/api/v1/me/friend-requests/sent", ct);
            _sentRequests = sent?.users ?? new List<UserSummaryResponse>();

            var (received, _) = await api.GetAsync<UserSummaryListResponse>("/api/v1/me/friend-requests/received", ct);
            _receivedRequests = received?.users ?? new List<UserSummaryResponse>();
        }
        catch (OperationCanceledException) { return; }
        catch (Exception e)
        {
            Debug.LogWarning($"[FriendScreen] load failed: {e.Message}");
        }

        Refresh();
    }

    private void SelectTab(Tab tab)
    {
        _activeTab = tab;

        SetTabActive(_tabFriends, tab == Tab.Friends);
        SetTabActive(_tabSent, tab == Tab.Sent);
        SetTabActive(_tabReceived, tab == Tab.Received);

        bool showSearch = tab == Tab.Friends;
        if (_searchBar != null)
        {
            if (showSearch)
                _searchBar.RemoveFromClassList("overlay-hidden");
            else
                _searchBar.AddToClassList("overlay-hidden");
        }

        Refresh();
    }

    private void Refresh()
    {
        _list?.Clear();

        List<UserSummaryResponse> users;
        string emptyText;

        switch (_activeTab)
        {
            case Tab.Sent:
                users = _sentRequests;
                emptyText = "申請中のフレンドはいません";
                break;
            case Tab.Received:
                users = _receivedRequests;
                emptyText = "受信した申請はありません";
                break;
            default:
                users = _friends;
                emptyText = "フレンドはいません";
                break;
        }

        if (_emptyLabel != null)
            _emptyLabel.text = emptyText;

        if (users.Count == 0)
        {
            _empty?.RemoveFromClassList("overlay-hidden");
            return;
        }

        _empty?.AddToClassList("overlay-hidden");

        foreach (var u in users)
            _list?.Add(BuildUserRow(u));
    }

    private VisualElement BuildUserRow(UserSummaryResponse user)
    {
        var row = new VisualElement();
        row.AddToClassList("user-row");

        var avatar = new Label("♟");
        avatar.AddToClassList("user-avatar-placeholder");
        row.Add(avatar);

        var info = new VisualElement();
        info.AddToClassList("user-info");

        var nameLabel = new Label(user.displayName);
        nameLabel.AddToClassList("user-display-name");
        info.Add(nameLabel);

        if (!string.IsNullOrEmpty(user.name))
        {
            var atLabel = new Label($"@{user.name}");
            atLabel.AddToClassList("user-at-name");
            info.Add(atLabel);
        }

        row.Add(info);

        // タブ別アクションボタン
        switch (_activeTab)
        {
            case Tab.Friends:
                var btnRemove = new Button(() => OnRemoveFriendClicked(user.id));
                btnRemove.text = "解除";
                btnRemove.AddToClassList("btn-action-small");
                btnRemove.AddToClassList("btn-action-small--danger");
                row.Add(btnRemove);
                break;

            case Tab.Sent:
                var btnCancel = new Button(() => OnCancelRequestClicked(user.id));
                btnCancel.text = "キャンセル";
                btnCancel.AddToClassList("btn-action-small");
                btnCancel.AddToClassList("btn-action-small--secondary");
                row.Add(btnCancel);
                break;

            case Tab.Received:
                var btnAccept = new Button(() => OnAcceptClicked(user.id));
                btnAccept.text = "承認";
                btnAccept.AddToClassList("btn-action-small");
                btnAccept.AddToClassList("btn-action-small--accent");
                row.Add(btnAccept);

                var btnReject = new Button(() => OnRejectClicked(user.id));
                btnReject.text = "拒否";
                btnReject.AddToClassList("btn-action-small");
                btnReject.AddToClassList("btn-action-small--secondary");
                row.Add(btnReject);
                break;
        }

        row.RegisterCallback<ClickEvent>(e =>
        {
            if (e.target == row || e.target is Label)
                OnUserTapped?.Invoke(user.id);
        });

        return row;
    }

    private async void OnRemoveFriendClicked(string userId)
    {
        if (FriendManager.Instance == null) return;
        await FriendManager.Instance.RemoveFriendAsync(userId);
        _friends.RemoveAll(u => u.id == userId);
        Refresh();
        FlashMessageController.Current?.Show("フレンドを解除しました");
    }

    private async void OnCancelRequestClicked(string userId)
    {
        if (FriendManager.Instance == null) return;
        await FriendManager.Instance.CancelFriendRequestAsync(userId);
        _sentRequests.RemoveAll(u => u.id == userId);
        Refresh();
    }

    private async void OnAcceptClicked(string userId)
    {
        if (FriendManager.Instance == null) return;
        bool ok = await FriendManager.Instance.AcceptFriendRequestAsync(userId);
        if (ok)
        {
            var user = _receivedRequests.Find(u => u.id == userId);
            _receivedRequests.RemoveAll(u => u.id == userId);
            if (user != null) _friends.Add(user);
            Refresh();
            FlashMessageController.Current?.Show("フレンドになりました！", FlashMessageType.Success);
        }
    }

    private async void OnRejectClicked(string userId)
    {
        if (FriendManager.Instance == null) return;
        await FriendManager.Instance.RejectFriendRequestAsync(userId);
        _receivedRequests.RemoveAll(u => u.id == userId);
        Refresh();
    }

    private void OnSearchClicked()
    {
        string query = _fieldSearch?.value?.Trim();
        if (string.IsNullOrEmpty(query)) return;

        // @name 検索: GET /api/v1/users?name=xxx は未実装のため簡易フィルター
        // TODO: Phase 10 でユーザー検索 API 追加後にサーバー検索に切り替える
        string normalized = query.TrimStart('@').ToLower();
        _friends = _friends.FindAll(u =>
            (u.name ?? "").ToLower() == normalized ||
            (u.displayName ?? "").ToLower().Contains(normalized));
        Refresh();
    }

    private static void SetTabActive(Button btn, bool active)
    {
        if (btn == null) return;
        if (active)
            btn.AddToClassList("tab-btn--active");
        else
            btn.RemoveFromClassList("tab-btn--active");
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
        _list?.Clear();
    }
}
