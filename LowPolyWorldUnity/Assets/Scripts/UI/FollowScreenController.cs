using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// フォロー管理画面のコントローラー。
/// フォロー中 / フォロワー の2タブを管理する。
/// 仕様: screens-and-modes.md セクション 13.2
/// </summary>
public class FollowScreenController : IDisposable
{
    private readonly VisualElement _root;
    private readonly ScrollView _list;
    private readonly VisualElement _empty;
    private readonly Label _emptyLabel;
    private readonly Button _tabFollowing;
    private readonly Button _tabFollowers;

    // 表示対象ユーザー ID（自分のフォロー/フォロワーか他ユーザーのものかを切り替え可能）
    private string _targetUserId;

    private enum Tab { Following, Followers }
    private Tab _activeTab = Tab.Following;

    private List<UserSummaryResponse> _followingUsers = new();
    private List<UserSummaryResponse> _followerUsers = new();

    private CancellationTokenSource _cts = new();

    public event Action OnBackRequested;
    public event Action<string> OnUserTapped;

    /// <param name="root">FollowScreen のルート要素。</param>
    /// <param name="targetUserId">表示するユーザーID（null の場合は自分）。</param>
    public FollowScreenController(VisualElement root, string targetUserId = null)
    {
        _root = root;
        _list = root.Q<ScrollView>("follow-list");
        _empty = root.Q<VisualElement>("empty-state");
        _emptyLabel = root.Q<Label>("empty-label");
        _tabFollowing = root.Q<Button>("tab-following");
        _tabFollowers = root.Q<Button>("tab-followers");

        _targetUserId = targetUserId ?? UserManager.Instance?.Profile?.id;

        root.Q<Button>("btn-back")?.RegisterCallback<ClickEvent>(_ => OnBackRequested?.Invoke());
        _tabFollowing?.RegisterCallback<ClickEvent>(_ => SelectTab(Tab.Following));
        _tabFollowers?.RegisterCallback<ClickEvent>(_ => SelectTab(Tab.Followers));

        if (!string.IsNullOrEmpty(targetUserId))
            root.Q<Label>("screen-title").text = "フォロー";

        LoadAsync();
    }

    private async void LoadAsync()
    {
        if (UserManager.Instance == null || string.IsNullOrEmpty(_targetUserId)) return;
        var api = UserManager.Instance.Api;
        var ct = _cts.Token;

        try
        {
            var (following, _) = await api.GetAsync<UserSummaryListResponse>(
                $"/api/v1/users/{_targetUserId}/following", ct);
            _followingUsers = following?.users ?? new List<UserSummaryResponse>();

            var (followers, _) = await api.GetAsync<UserSummaryListResponse>(
                $"/api/v1/users/{_targetUserId}/followers", ct);
            _followerUsers = followers?.users ?? new List<UserSummaryResponse>();
        }
        catch (OperationCanceledException) { return; }
        catch (Exception e)
        {
            Debug.LogWarning($"[FollowScreen] load failed: {e.Message}");
        }

        Refresh();
    }

    private void SelectTab(Tab tab)
    {
        _activeTab = tab;
        SetTabActive(_tabFollowing, tab == Tab.Following);
        SetTabActive(_tabFollowers, tab == Tab.Followers);
        Refresh();
    }

    private void Refresh()
    {
        _list?.Clear();

        List<UserSummaryResponse> users;
        string emptyText;

        if (_activeTab == Tab.Following)
        {
            users = _followingUsers;
            emptyText = "フォロー中のユーザーはいません";
        }
        else
        {
            users = _followerUsers;
            emptyText = "フォロワーはいません";
        }

        if (_emptyLabel != null) _emptyLabel.text = emptyText;

        if (users.Count == 0)
        {
            _empty?.RemoveFromClassList("overlay-hidden");
            return;
        }

        _empty?.AddToClassList("overlay-hidden");

        bool isSelf = _targetUserId == UserManager.Instance?.Profile?.id;

        foreach (var u in users)
            _list?.Add(BuildUserRow(u, isSelf));
    }

    private VisualElement BuildUserRow(UserSummaryResponse user, bool showActions)
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

        // 自分のフォロー画面ならフォロー解除ボタンを表示
        if (showActions && _activeTab == Tab.Following)
        {
            bool isFollowing = FollowManager.Instance?.Logic.IsFollowing(user.id) ?? false;
            if (isFollowing)
            {
                var btnUnfollow = new Button(() => OnUnfollowClicked(user.id, row));
                btnUnfollow.text = "フォロー中";
                btnUnfollow.AddToClassList("btn-action-small");
                btnUnfollow.AddToClassList("btn-action-small--secondary");
                row.Add(btnUnfollow);
            }
        }

        row.RegisterCallback<ClickEvent>(e =>
        {
            if (e.target == row || e.target is Label)
                OnUserTapped?.Invoke(user.id);
        });

        return row;
    }

    private async void OnUnfollowClicked(string userId, VisualElement row)
    {
        if (FollowManager.Instance == null) return;
        bool ok = await FollowManager.Instance.UnfollowAsync(userId);
        if (ok)
        {
            _followingUsers.RemoveAll(u => u.id == userId);
            Refresh();
            FlashMessageController.Current?.Show("フォローを解除しました");
        }
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
