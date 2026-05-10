using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// フォロー中リストを管理する DontDestroyOnLoad シングルトン。
/// API 呼び出しのオーケストレーションを行い、ロジックは FollowListLogic に委譲する。
/// </summary>
public class FollowManager : MonoBehaviour
{
    public static FollowManager Instance { get; private set; }

    public FollowListLogic Logic { get; } = new FollowListLogic();

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

    /// <summary>サーバーからフォロー中リストを取得して Logic を初期化する。</summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        var userId = UserManager.Instance.Profile?.id;
        if (string.IsNullOrEmpty(userId))
            return;

        var api = UserManager.Instance.Api;
        var (res, err) = await api.GetAsync<UserSummaryListResponse>(
            $"/api/v1/users/{userId}/following",
            ct
        );
        if (err != null)
        {
            Debug.LogWarning($"[FollowManager] failed to load following: {err}");
            return;
        }

        var ids = new System.Collections.Generic.List<string>();
        if (res?.users != null)
            foreach (var u in res.users)
                ids.Add(u.id);
        Logic.SetAll(ids);
    }

    /// <summary>ユーザーをフォローする。API 呼び出し後にローカル状態を更新する。</summary>
    public async Task<bool> FollowAsync(string targetId, CancellationToken ct = default)
    {
        var api = UserManager.Instance.Api;
        var err = await api.PostJsonNoBodyAsync($"/api/v1/users/{targetId}/follow", null, ct);
        if (err != null)
        {
            Debug.LogWarning($"[FollowManager] Follow failed: {err}");
            return false;
        }
        Logic.Follow(targetId);
        return true;
    }

    /// <summary>フォローを解除する。API 呼び出し後にローカル状態を更新する。</summary>
    public async Task<bool> UnfollowAsync(string targetId, CancellationToken ct = default)
    {
        var api = UserManager.Instance.Api;
        var err = await api.DeleteAsync($"/api/v1/users/{targetId}/follow", ct);
        if (err != null)
        {
            Debug.LogWarning($"[FollowManager] Unfollow failed: {err}");
            return false;
        }
        Logic.Unfollow(targetId);
        return true;
    }
}
