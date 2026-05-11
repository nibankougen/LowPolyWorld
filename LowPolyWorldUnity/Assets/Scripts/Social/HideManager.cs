using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 非表示ユーザーリストを管理する DontDestroyOnLoad シングルトン。
/// API 呼び出しのオーケストレーションを行い、ロジックは HideListLogic に委譲する。
/// </summary>
public class HideManager : MonoBehaviour
{
    public static HideManager Instance { get; private set; }

    public HideListLogic Logic { get; } = new HideListLogic();

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

    /// <summary>サーバーから非表示ユーザーリストを取得して Logic を初期化する。</summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        var api = UserManager.Instance.Api;
        var (res, err) = await api.GetAsync<HiddenUsersResponse>("/api/v1/me/hidden-users", ct);
        if (err != null)
        {
            Debug.LogWarning($"[HideManager] failed to load hidden users: {err}");
            return;
        }
        var ids = res?.users?.ConvertAll(u => u.id) ?? new System.Collections.Generic.List<string>();
        Logic.SetAll(ids);
    }

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

    /// <summary>非表示を解除する。API 呼び出し後にローカル状態を更新する。</summary>
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
}
