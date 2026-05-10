using System.Collections.Generic;

public enum NotificationType
{
    FriendRequest,
    WorldPublished,
    ProductReleased,
    CoinExpiry30d,
    CoinExpiry7d,
}

/// <summary>アプリ内通知1件のデータ。</summary>
public class NotificationItem
{
    public string Id { get; }
    public NotificationType Type { get; }
    public string Body { get; }
    public long CreatedAtUnix { get; }
    public bool IsRead { get; set; }

    public NotificationItem(string id, NotificationType type, string body, long createdAtUnix)
    {
        Id = id;
        Type = type;
        Body = body;
        CreatedAtUnix = createdAtUnix;
    }
}

/// <summary>
/// アプリ内通知のローカルストア（純粋 C#）。
/// 未読件数カウント・既読マーク・種別フィルタリングを提供する。
/// 仕様: screens-and-modes.md セクション 15
/// </summary>
public class NotificationStore
{
    private readonly List<NotificationItem> _notifications = new();

    public int Count => _notifications.Count;

    public int UnreadCount
    {
        get
        {
            int count = 0;
            foreach (var n in _notifications)
                if (!n.IsRead)
                    count++;
            return count;
        }
    }

    public IReadOnlyList<NotificationItem> GetAll() => _notifications;

    /// <summary>指定種別の通知だけを返す。</summary>
    public List<NotificationItem> GetByType(NotificationType type)
    {
        var result = new List<NotificationItem>();
        foreach (var n in _notifications)
            if (n.Type == type)
                result.Add(n);
        return result;
    }

    /// <summary>通知を追加する（新着は先頭に挿入して最新順を維持する）。</summary>
    public void Add(NotificationItem notification) => _notifications.Insert(0, notification);

    /// <summary>指定 ID の通知を既読にする。見つからない場合は false。</summary>
    public bool MarkRead(string notificationId)
    {
        foreach (var n in _notifications)
        {
            if (n.Id == notificationId)
            {
                n.IsRead = true;
                return true;
            }
        }
        return false;
    }

    /// <summary>全通知を既読にする。</summary>
    public void MarkAllRead()
    {
        foreach (var n in _notifications)
            n.IsRead = true;
    }

    /// <summary>サーバーから取得したリストで全件置換する。</summary>
    public void SetAll(IEnumerable<NotificationItem> notifications)
    {
        _notifications.Clear();
        foreach (var n in notifications)
            _notifications.Add(n);
    }
}
