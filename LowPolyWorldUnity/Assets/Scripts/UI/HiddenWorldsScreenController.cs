using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 非表示ワールド管理画面コントローラー。
/// 仕様: screens-and-modes.md セクション 14.4・19
/// </summary>
public class HiddenWorldsScreenController : IDisposable
{
    private readonly VisualElement _root;
    private readonly ScrollView _list;
    private readonly VisualElement _empty;
    private readonly VisualElement _loading;

    private List<HiddenWorldEntry> _worlds = new();
    private CancellationTokenSource _cts = new();

    public event Action OnBackRequested;

    public HiddenWorldsScreenController(VisualElement root)
    {
        _root = root;
        _list = root.Q<ScrollView>("world-list");
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
            var (res, _) = await api.GetAsync<HiddenWorldsResponse>("/api/v1/me/hidden-worlds", ct);
            _worlds = res?.worlds ?? new List<HiddenWorldEntry>();
        }
        catch (OperationCanceledException) { return; }
        catch (Exception e)
        {
            Debug.LogWarning($"[HiddenWorldsScreen] load failed: {e.Message}");
        }

        _loading?.AddToClassList("overlay-hidden");
        Refresh();
    }

    private void Refresh()
    {
        _list?.Clear();

        if (_worlds.Count == 0)
        {
            _empty?.RemoveFromClassList("overlay-hidden");
            return;
        }

        _empty?.AddToClassList("overlay-hidden");
        foreach (var w in _worlds)
            _list?.Add(BuildWorldRow(w));
    }

    private VisualElement BuildWorldRow(HiddenWorldEntry world)
    {
        var row = new VisualElement();
        row.AddToClassList("world-row");

        var icon = new VisualElement();
        icon.AddToClassList("world-icon");
        row.Add(icon);

        var nameLabel = new Label(string.IsNullOrEmpty(world.name) ? "（削除済みワールド）" : world.name);
        nameLabel.AddToClassList("world-name");
        row.Add(nameLabel);

        var btnUnhide = new Button(() => OnUnhideClicked(world.id, world.name));
        btnUnhide.text = "非表示解除";
        btnUnhide.AddToClassList("btn-unhide");
        row.Add(btnUnhide);

        return row;
    }

    private async void OnUnhideClicked(string worldId, string worldName)
    {
        if (UserManager.Instance == null) return;
        var api = UserManager.Instance.Api;
        var err = await api.DeleteAsync($"/api/v1/me/hidden-worlds/{worldId}", _cts.Token);
        if (err != null)
        {
            Debug.LogWarning($"[HiddenWorldsScreen] unhide failed: {err}");
            return;
        }
        _worlds.RemoveAll(w => w.id == worldId);
        Refresh();
        FlashMessageController.Current?.Show("非表示を解除しました");
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
        _list?.Clear();
    }
}
