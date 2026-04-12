using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GLTFast;
using UnityEngine;

/// <summary>
/// GLTF/GLB をローカルパスから読み込む暫定版 WorldLoader。
/// Phase 5 以降で API サーバー連携版に差し替える。
/// </summary>
public class WorldLoader : MonoBehaviour
{
    public static WorldLoader Instance { get; private set; }

    /// <summary>ロード完了イベント。引数: ロードされたワールドのルート GameObject。</summary>
    public event Action<GameObject> OnWorldLoaded;

    /// <summary>ロード失敗イベント。</summary>
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

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
        _cts?.Cancel();
        _cts?.Dispose();
    }

    /// <summary>
    /// StreamingAssets/{relativePath} から GLB/GLTF をロードする。
    /// </summary>
    public async void LoadFromStreamingAssets(string relativePath)
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();

        if (_currentWorld != null)
        {
            Destroy(_currentWorld);
            _currentWorld = null;
        }

        string fullPath = Path.Combine(Application.streamingAssetsPath, relativePath);
        if (!File.Exists(fullPath))
        {
            OnWorldLoadFailed?.Invoke($"World file not found: {fullPath}");
            return;
        }

        try
        {
            var go = new GameObject("World");
            var gltf = go.AddComponent<GltfAsset>();
            var uri = new Uri(fullPath);
            bool success = await gltf.Load(uri.AbsoluteUri);

            if (_cts.Token.IsCancellationRequested)
            {
                Destroy(go);
                return;
            }

            if (!success)
            {
                Destroy(go);
                OnWorldLoadFailed?.Invoke($"Failed to load world: {relativePath}");
                return;
            }

            // World レイヤーを設定
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

    private static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform)
            SetLayerRecursive(child.gameObject, layer);
    }
}
