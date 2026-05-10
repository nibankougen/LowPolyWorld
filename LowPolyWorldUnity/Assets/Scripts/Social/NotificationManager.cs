using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// アプリ内通知を管理する DontDestroyOnLoad シングルトン。
/// API 呼び出しのオーケストレーションを行い、ロジックは NotificationStore に委譲する。
/// </summary>
public class NotificationManager : MonoBehaviour
{
    public static NotificationManager Instance { get; private set; }

    public NotificationStore Store { get; } = new NotificationStore();

    /// <summary>未読数が変化したときに発火する。</summary>
    public event Action OnUnreadCountChanged;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>サーバーから通知一覧を取得して Store を初期化する。</summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        var api = UserManager.Instance.Api;
        var (res, err) = await api.GetAsync<NotificationListResponse>("/api/v1/me/notifications", ct);
        if (err != null)
        {
            Debug.LogWarning($"[NotificationManager] failed to load notifications: {err}");
            return;
        }

        var items = new System.Collections.Generic.List<NotificationItem>();
        if (res?.notifications != null)
        {
            foreach (var n in res.notifications)
            {
                if (!System.Enum.TryParse<NotificationType>(ToPascalCase(n.type), out var type))
                    continue;
                long unixMs = 0;
                if (System.DateTime.TryParse(n.createdAt, out var dt))
                    unixMs = new System.DateTimeOffset(dt).ToUnixTimeMilliseconds();
                var item = new NotificationItem(n.id, type, n.body, unixMs) { IsRead = n.isRead };
                items.Add(item);
            }
        }
        Store.SetAll(items);
        OnUnreadCountChanged?.Invoke();
    }

    /// <summary>指定通知を既読にする。</summary>
    public async Task MarkReadAsync(string notificationId, CancellationToken ct = default)
    {
        var api = UserManager.Instance.Api;
        await api.PatchJsonAsync<object>($"/api/v1/me/notifications/{notificationId}/read", null, ct);
        Store.MarkRead(notificationId);
        OnUnreadCountChanged?.Invoke();
    }

    /// <summary>全通知を既読にする。</summary>
    public async Task MarkAllReadAsync(CancellationToken ct = default)
    {
        var api = UserManager.Instance.Api;
        await api.PatchJsonAsync<object>("/api/v1/me/notifications/read-all", null, ct);
        Store.MarkAllRead();
        OnUnreadCountChanged?.Invoke();
    }

    private static string ToPascalCase(string snakeCase)
    {
        if (string.IsNullOrEmpty(snakeCase))
            return snakeCase;
        var sb = new System.Text.StringBuilder();
        bool upper = true;
        foreach (char c in snakeCase)
        {
            if (c == '_')
            {
                upper = true;
            }
            else
            {
                sb.Append(upper ? char.ToUpper(c) : c);
                upper = false;
            }
        }
        return sb.ToString();
    }
}
