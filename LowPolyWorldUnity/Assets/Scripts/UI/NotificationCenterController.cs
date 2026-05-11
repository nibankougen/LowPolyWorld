using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

/// <summary>
/// 通知センターパネルを管理するコントローラー。
/// NotificationManager.Store からデータを読み込み、一覧表示と既読処理を行う。
/// 仕様: screens-and-modes.md セクション 15
/// </summary>
public class NotificationCenterController : IDisposable
{
    private readonly VisualElement _panel;
    private readonly ScrollView _list;
    private readonly VisualElement _empty;
    private readonly System.Threading.CancellationTokenSource _cts = new();

    public event Action OnCloseRequested;

    private static readonly Dictionary<NotificationType, string> TypeIcons = new()
    {
        { NotificationType.FriendRequest, "👤" },
        { NotificationType.WorldPublished, "🌐" },
        { NotificationType.ProductReleased, "🛍" },
        { NotificationType.CoinExpiry30d, "💰" },
        { NotificationType.CoinExpiry7d, "💰" },
    };

    public NotificationCenterController(VisualElement panel)
    {
        _panel = panel;
        _list = panel.Q<ScrollView>("notif-list");
        _empty = panel.Q<VisualElement>("notif-empty");

        panel.Q<Button>("btn-close")?.RegisterCallback<ClickEvent>(_ => OnCloseRequested?.Invoke());
        panel.Q<Button>("btn-mark-all-read")?.RegisterCallback<ClickEvent>(_ => OnMarkAllReadClicked());
    }

    /// <summary>パネルを表示して通知一覧を描画する。</summary>
    public void Show()
    {
        _panel.RemoveFromClassList("overlay-hidden");
        Refresh();
    }

    public void Hide()
    {
        _panel.AddToClassList("overlay-hidden");
    }

    private void Refresh()
    {
        if (NotificationManager.Instance == null) return;

        _list?.Clear();
        var notifications = NotificationManager.Instance.Store.GetAll();

        bool hasItems = notifications.Count > 0;
        if (_empty != null)
        {
            if (hasItems)
                _empty.AddToClassList("overlay-hidden");
            else
                _empty.RemoveFromClassList("overlay-hidden");
        }

        foreach (var item in notifications)
            _list?.Add(BuildRow(item));
    }

    private VisualElement BuildRow(NotificationItem item)
    {
        var row = new VisualElement();
        row.AddToClassList("notif-row");
        if (!item.IsRead)
            row.AddToClassList("notif-row--unread");

        var icon = new Label(TypeIcons.TryGetValue(item.Type, out var ic) ? ic : "●");
        icon.AddToClassList("notif-icon");
        row.Add(icon);

        var body = new Label(item.Body);
        body.AddToClassList("notif-body");
        if (item.IsRead)
            body.AddToClassList("notif-body--read");
        row.Add(body);

        row.RegisterCallback<ClickEvent>(_ => OnRowClicked(item));
        return row;
    }

    private async void OnRowClicked(NotificationItem item)
    {
        if (item.IsRead) return;
        if (NotificationManager.Instance == null) return;
        await NotificationManager.Instance.MarkReadAsync(item.Id, _cts.Token);
        if (_cts.IsCancellationRequested) return;
        Refresh();
    }

    private async void OnMarkAllReadClicked()
    {
        if (NotificationManager.Instance == null) return;
        await NotificationManager.Instance.MarkAllReadAsync(_cts.Token);
        if (_cts.IsCancellationRequested) return;
        Refresh();
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
        _list?.Clear();
    }
}
