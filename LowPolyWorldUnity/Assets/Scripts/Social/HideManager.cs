using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 非表示ユーザー・非表示ワールドリストを管理する DontDestroyOnLoad シングルトン。
/// API 呼び出しのオーケストレーションを行い、ロジックは HideListLogic / HideWorldListLogic に委譲する。
/// </summary>
public class HideManager : MonoBehaviour
{
    public static HideManager Instance { get; private set; }

    public HideListLogic Logic { get; } = new HideListLogic();
    public HideWorldListLogic WorldLogic { get; } = new HideWorldListLogic();

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

    /// <summary>サーバーから非表示ユーザー・ワールドリストを取得して Logic を初期化する。</summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        var api = UserManager.Instance.Api;

        var (usersRes, usersErr) = await api.GetAsync<HiddenUsersResponse>("/api/v1/me/hidden-users", ct);
        if (usersErr != null)
            Debug.LogWarning($"[HideManager] failed to load hidden users: {usersErr}");
        else
        {
            var ids = usersRes?.users?.ConvertAll(u => u.id) ?? new System.Collections.Generic.List<string>();
            Logic.SetAll(ids);
        }

        var (worldsRes, worldsErr) = await api.GetAsync<HiddenWorldsResponse>("/api/v1/me/hidden-worlds", ct);
        if (worldsErr != null)
            Debug.LogWarning($"[HideManager] failed to load hidden worlds: {worldsErr}");
        else
        {
            var wids = worldsRes?.worlds?.ConvertAll(w => w.id) ?? new System.Collections.Generic.List<string>();
            WorldLogic.SetAll(wids);
        }
    }

    // ── User hide/unhide ─────────────────────────────────────────────────────

    /// <summary>ユーザーを非表示にする。API 呼び出し後にローカル状態を更新する。</summary>
    public async Task<bool> HideUserAsync(string targetId, CancellationToken ct = default)
    {
        var api = UserManager.Instance.Api;
        var err = await api.PostJsonNoBodyAsync($"/api/v1/me/hidden-users/{targetId}", null, ct);
        if (err != null)
        {
            Debug.LogWarning($"[HideManager] HideUser failed: {err}");
            return false;
        }
        Logic.Add(targetId);
        return true;
    }

    /// <summary>ユーザーの非表示を解除する。API 呼び出し後にローカル状態を更新する。</summary>
    public async Task<bool> UnhideUserAsync(string targetId, CancellationToken ct = default)
    {
        var api = UserManager.Instance.Api;
        var err = await api.DeleteAsync($"/api/v1/me/hidden-users/{targetId}", ct);
        if (err != null)
        {
            Debug.LogWarning($"[HideManager] UnhideUser failed: {err}");
            return false;
        }
        Logic.Remove(targetId);
        return true;
    }

    // ── World hide/unhide ────────────────────────────────────────────────────

    /// <summary>ワールドを非表示にする。API 呼び出し後にローカル状態を更新する。</summary>
    public async Task<bool> HideWorldAsync(string worldId, CancellationToken ct = default)
    {
        var api = UserManager.Instance.Api;
        var err = await api.PostJsonNoBodyAsync($"/api/v1/me/hidden-worlds/{worldId}", null, ct);
        if (err != null)
        {
            Debug.LogWarning($"[HideManager] HideWorld failed: {err}");
            return false;
        }
        WorldLogic.Add(worldId);
        return true;
    }

    /// <summary>ワールドの非表示を解除する。API 呼び出し後にローカル状態を更新する。</summary>
    public async Task<bool> UnhideWorldAsync(string worldId, CancellationToken ct = default)
    {
        var api = UserManager.Instance.Api;
        var err = await api.DeleteAsync($"/api/v1/me/hidden-worlds/{worldId}", ct);
        if (err != null)
        {
            Debug.LogWarning($"[HideManager] UnhideWorld failed: {err}");
            return false;
        }
        WorldLogic.Remove(worldId);
        return true;
    }
}
