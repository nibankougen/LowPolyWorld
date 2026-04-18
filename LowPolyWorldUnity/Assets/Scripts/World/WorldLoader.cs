using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GLTFast;
using UnityEngine;

/// <summary>
/// GLTF/GLB をローカルパスまたはURL（CacheManager経由）から読み込む WorldLoader。
/// WorldScene 起動時に WorldSessionData.WorldGlbUrl が設定されていれば自動ロードする。
/// </summary>
public class WorldLoader : MonoBehaviour
{
    public static WorldLoader Instance { get; private set; }

    public event Action<GameObject> OnWorldLoaded;
    public event Action<string> OnWorldLoadFailed;

    private GameObject _currentWorld;
    private CancellationTokenSource _cts;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        var glbUrl = WorldSessionData.WorldGlbUrl;
        if (!string.IsNullOrEmpty(glbUrl))
            LoadFromUrl(glbUrl);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
        _cts?.Cancel();
        _cts?.Dispose();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// CDN URL から GLB をダウンロード（CacheManager でキャッシュ）してロードする。
    /// URL はコンテンツアドレス型（{sha256}.glb）で、ファイル名からハッシュを抽出する。
    /// </summary>
    public async void LoadFromUrl(string url)
    {
        ResetCts();
        ClearCurrentWorld();

        var hash = ExtractHashFromUrl(url);
        if (string.IsNullOrEmpty(hash))
        {
            OnWorldLoadFailed?.Invoke($"Invalid GLB URL: {url}");
            return;
        }

        if (CacheManager.Instance == null)
        {
            OnWorldLoadFailed?.Invoke("CacheManager not ready");
            return;
        }

        var ct = _cts.Token;
        var (localPath, error) = await CacheManager.Instance.GetOrDownloadAsync(url, hash, "glb", isOwn: false, ct);

        if (ct.IsCancellationRequested) return;

        if (error != null)
        {
            OnWorldLoadFailed?.Invoke($"Failed to download world: {error}");
            return;
        }

        await LoadFromLocalPathAsync(localPath, ct);
    }

    /// <summary>
    /// StreamingAssets/{relativePath} から GLB/GLTF をロードする（開発用）。
    /// </summary>
    public async void LoadFromStreamingAssets(string relativePath)
    {
        ResetCts();
        ClearCurrentWorld();

        string fullPath = Path.Combine(Application.streamingAssetsPath, relativePath);
        if (!File.Exists(fullPath))
        {
            OnWorldLoadFailed?.Invoke($"World file not found: {fullPath}");
            return;
        }

        await LoadFromLocalPathAsync(fullPath, _cts.Token);
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private async Task LoadFromLocalPathAsync(string localPath, CancellationToken ct)
    {
        try
        {
            var go = new GameObject("World");
            var gltf = go.AddComponent<GltfAsset>();
            var uri = new Uri(localPath);
            bool success = await gltf.Load(uri.AbsoluteUri);

            if (ct.IsCancellationRequested)
            {
                Destroy(go);
                return;
            }

            if (!success)
            {
                Destroy(go);
                OnWorldLoadFailed?.Invoke($"Failed to load world from: {localPath}");
                return;
            }

            int worldLayer = LayerMask.NameToLayer("World");
            if (worldLayer >= 0)
                SetLayerRecursive(go, worldLayer);

            _currentWorld = go;
            OnWorldLoaded?.Invoke(go);
        }
        catch (OperationCanceledException) { }
        catch (Exception e)
        {
            Debug.LogError($"[WorldLoader] Exception: {e}");
            OnWorldLoadFailed?.Invoke(e.Message);
        }
    }

    private void ResetCts()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
    }

    private void ClearCurrentWorld()
    {
        if (_currentWorld != null)
        {
            Destroy(_currentWorld);
            _currentWorld = null;
        }
    }

    // Extracts SHA-256 hash from a content-addressed URL like https://cdn.example.com/abc123.glb
    private static string ExtractHashFromUrl(string url)
    {
        try
        {
            var fileName = Path.GetFileNameWithoutExtension(new Uri(url).LocalPath);
            return string.IsNullOrEmpty(fileName) ? null : fileName;
        }
        catch
        {
            return null;
        }
    }

    private static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform)
            SetLayerRecursive(child.gameObject, layer);
    }
}
