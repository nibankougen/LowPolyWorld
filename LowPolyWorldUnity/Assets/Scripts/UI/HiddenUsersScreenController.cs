using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 非表示ユーザー管理画面コントローラー。
/// 仕様: screens-and-modes.md セクション 14.2
/// </summary>
public class HiddenUsersScreenController : IDisposable
{
    private readonly VisualElement _root;
    private readonly ScrollView _list;
    private readonly VisualElement _empty;
    private readonly VisualElement _loading;

    private List<HiddenUserEntry> _users = new();
    private CancellationTokenSource _cts = new();

    public event Action OnBackRequested;

    public HiddenUsersScreenController(VisualElement root)
    {
        _root = root;
        _list = root.Q<ScrollView>("user-list");
        _empty = root.Q<VisualElement>("empty-state");
        _loading = root.Q<VisualElement>("loading-state");

        root.Q<Button>("btn-back")?.RegisterCallback<ClickEvent>(_ => OnBackRequested?.Invoke());

        LoadAsync();
    }

    private async void LoadAsync()
    {
        if (UserManager.Instance == null) return;

        _loading?.RemoveFromClassList("overlay-hidden");
        _empty?.AddToClassList("overlay-hidden");
        _list?.Clear();

        var api = UserManager.Instance.Api;
        var ct = _cts.Token;

        try
        {
            var (res, _) = await api.GetAsync<HiddenUsersResponse>("/api/v1/me/hidden-users", ct);
            _users = res?.users ?? new List<HiddenUserEntry>();
        }
        catch (OperationCanceledException) { return; }
        catch (Exception e)
        {
            Debug.LogWarning($"[HiddenUsersScreen] load failed: {e.Message}");
        }

        _loading?.AddToClassList("overlay-hidden");
        Refresh();
    }

    private void Refresh()
    {
        _list?.Clear();

        if (_users.Count == 0)
        {
            _empty?.RemoveFromClassList("overlay-hidden");
            return;
        }

        _empty?.AddToClassList("overlay-hidden");
        foreach (var u in _users)
            _list?.Add(BuildUserRow(u));
    }

    private VisualElement BuildUserRow(HiddenUserEntry user)
    {
        var row = new VisualElement();
        row.AddToClassList("user-row");

        var avatar = new Label("♟");
        avatar.AddToClassList("user-avatar-placeholder");
        row.Add(avatar);

        var info = new VisualElement();
        info.AddToClassList("user-info");

        var displayName = string.IsNullOrEmpty(user.displayName) ? "（削除済みユーザー）" : user.displayName;
        var nameLabel = new Label(displayName);
        nameLabel.AddToClassList("user-display-name");
        info.Add(nameLabel);

        if (!string.IsNullOrEmpty(user.name))
        {
            var atLabel = new Label($"@{user.name}");
            atLabel.AddToClassList("user-at-name");
            info.Add(atLabel);
        }

        row.Add(info);

        var btnUnhide = new Button(() => OnUnhideClicked(user.id, row));
        btnUnhide.text = "非表示解除";
        btnUnhide.AddToClassList("btn-unhide");
        row.Add(btnUnhide);

        return row;
    }

    private async void OnUnhideClicked(string userId, VisualElement row)
    {
        if (HideManager.Instance == null) return;
        bool ok = await HideManager.Instance.UnhideUserAsync(userId);
        if (ok)
        {
            _users.RemoveAll(u => u.id == userId);
            Refresh();
            FlashMessageController.Current?.Show("非表示を解除しました");
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
        _list?.Clear();
    }
}
