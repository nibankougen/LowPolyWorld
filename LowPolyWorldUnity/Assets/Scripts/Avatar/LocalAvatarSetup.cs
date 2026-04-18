using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// WorldScene 起動時にローカルプレイヤーの VRM を読み込んで AvatarManager に登録する。
/// PlayerController と同じ GameObject にアタッチする。
/// アバターが未選択（SelectedAvatarLocalPath == null）の場合はスキップする。
/// デフォルトアバターのフォールバックは Phase 2 で実装予定。
/// </summary>
public class LocalAvatarSetup : MonoBehaviour
{
    [SerializeField] private RuntimeAnimatorController _animatorController;

    private CancellationTokenSource _cts;

    private void Start()
    {
        _cts = new CancellationTokenSource();
        _ = SetupLocalAvatarAsync(_cts.Token);
    }

    private void OnDestroy()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    private async Task SetupLocalAvatarAsync(CancellationToken ct)
    {
        var path = UserManager.Instance?.SelectedAvatarLocalPath;
        if (string.IsNullOrEmpty(path))
        {
            Debug.Log("[LocalAvatarSetup] No avatar selected — skipping VRM load.");
            return;
        }

        var vrmRoot = await VrmLoader.LoadFromLocalPathAsync(path, ct);
        if (ct.IsCancellationRequested)
        {
            if (vrmRoot != null) Destroy(vrmRoot);
            return;
        }

        if (vrmRoot == null)
        {
            Debug.LogWarning("[LocalAvatarSetup] VRM load failed.");
            return;
        }

        vrmRoot.transform.SetParent(transform, false);
        vrmRoot.transform.localPosition = Vector3.zero;
        vrmRoot.transform.localRotation = Quaternion.identity;

        VrmLoader.ApplyAnimator(vrmRoot, _animatorController);

        var userId = UserManager.Instance?.Profile?.id ?? "local";
        AvatarManager.Instance?.RegisterAvatar(userId, vrmRoot, isLocal: true);
    }
}
