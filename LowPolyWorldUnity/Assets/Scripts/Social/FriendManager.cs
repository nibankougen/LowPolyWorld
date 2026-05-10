using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// フレンド関係を管理する DontDestroyOnLoad シングルトン。
/// API 呼び出しのオーケストレーションを行い、ロジックは FriendListLogic に委譲する。
/// </summary>
public class FriendManager : MonoBehaviour
{
    public static FriendManager Instance { get; private set; }

    public FriendListLogic Logic { get; private set; }

    /// <summary>フレンド関係が変化したときに発火する。</summary>
    public event Action OnFriendListChanged;

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

    /// <summary>
    /// サーバーからフレンドリスト・申請一覧を取得して Logic を初期化する。
    /// Bootstrapper がログイン後に呼び出す。
    /// </summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        var caps = UserManager.Instance.Capabilities;
        Logic = new FriendListLogic(caps?.friendLimit ?? 100);

        var api = UserManager.Instance.Api;
        var entries = new System.Collections.Generic.List<(string, FriendRelationStatus)>();

        var (friends, fErr) = await api.GetAsync<UserSummaryListResponse>("/api/v1/me/friends", ct);
        if (fErr != null)
        {
            Debug.LogWarning($"[FriendManager] failed to load friends: {fErr}");
            return;
        }
        if (friends?.users != null)
            foreach (var u in friends.users)
                entries.Add((u.id, FriendRelationStatus.Friends));

        var (received, rErr) = await api.GetAsync<UserSummaryListResponse>(
            "/api/v1/me/friend-requests/received",
            ct
        );
        if (rErr == null && received?.users != null)
            foreach (var u in received.users)
                entries.Add((u.id, FriendRelationStatus.RequestReceived));

        var (sent, sErr) = await api.GetAsync<UserSummaryListResponse>(
            "/api/v1/me/friend-requests/sent",
            ct
        );
        if (sErr == null && sent?.users != null)
            foreach (var u in sent.users)
                entries.Add((u.id, FriendRelationStatus.RequestSent));

        Logic.SetAll(entries);
    }

    /// <summary>フレンド申請を送る。結果は "pending" または "friends"。</summary>
    public async Task<string> SendFriendRequestAsync(string targetId, CancellationToken ct = default)
    {
        var api = UserManager.Instance.Api;
        var (res, err) = await api.PostJsonAsync<FriendRequestStatusResponse>(
            $"/api/v1/users/{targetId}/friend-request",
            null,
            ct
        );
        if (err != null)
        {
            Debug.LogWarning($"[FriendManager] SendFriendRequest failed: {err}");
            return null;
        }

        if (res.status == "friends")
            Logic.NotifyRequestAccepted(targetId);
        else
            Logic.SendRequest(targetId);

        OnFriendListChanged?.Invoke();
        return res.status;
    }

    /// <summary>受信した申請を承認する。</summary>
    public async Task<bool> AcceptFriendRequestAsync(string requesterId, CancellationToken ct = default)
    {
        var api = UserManager.Instance.Api;
        var err = await api.PostJsonNoBodyAsync(
            $"/api/v1/me/friend-requests/{requesterId}/accept",
            null,
            ct
        );
        if (err != null)
        {
            Debug.LogWarning($"[FriendManager] AcceptFriendRequest failed: {err}");
            return false;
        }
        Logic.AcceptRequest(requesterId);
        OnFriendListChanged?.Invoke();
        return true;
    }

    /// <summary>受信した申請を拒否する。</summary>
    public async Task<bool> RejectFriendRequestAsync(string requesterId, CancellationToken ct = default)
    {
        var api = UserManager.Instance.Api;
        var err = await api.PostJsonNoBodyAsync(
            $"/api/v1/me/friend-requests/{requesterId}/reject",
            null,
            ct
        );
        if (err != null)
        {
            Debug.LogWarning($"[FriendManager] RejectFriendRequest failed: {err}");
            return false;
        }
        Logic.RejectRequest(requesterId);
        OnFriendListChanged?.Invoke();
        return true;
    }

    /// <summary>送った申請をキャンセルする。</summary>
    public async Task<bool> CancelFriendRequestAsync(string addresseeId, CancellationToken ct = default)
    {
        var api = UserManager.Instance.Api;
        var err = await api.DeleteAsync(
            $"/api/v1/me/friend-requests/sent/{addresseeId}",
            ct
        );
        if (err != null)
        {
            Debug.LogWarning($"[FriendManager] CancelFriendRequest failed: {err}");
            return false;
        }
        Logic.CancelRequest(addresseeId);
        OnFriendListChanged?.Invoke();
        return true;
    }

    /// <summary>フレンドを解除する。</summary>
    public async Task<bool> RemoveFriendAsync(string friendId, CancellationToken ct = default)
    {
        var api = UserManager.Instance.Api;
        var err = await api.DeleteAsync($"/api/v1/me/friends/{friendId}", ct);
        if (err != null)
        {
            Debug.LogWarning($"[FriendManager] RemoveFriend failed: {err}");
            return false;
        }
        Logic.RemoveFriend(friendId);
        OnFriendListChanged?.Invoke();
        return true;
    }
}
