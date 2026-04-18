using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// ハッシュベースのアセットキャッシュ。
/// - 自分のアセット: Application.persistentDataPath/cache/own/ に永続保存
/// - 他人のアセット: Application.temporaryCachePath/cache/others/ に一時保存（TTL 7日）
/// - ハッシュ一致 → キャッシュヒット（再ダウンロード不要）
/// </summary>
public class CacheManager : MonoBehaviour
{
    private const string OwnCacheDir = "cache/own";
    private const string OthersCacheDir = "cache/others";
    private static readonly TimeSpan OthersTtl = TimeSpan.FromDays(7);

    public static CacheManager Instance { get; private set; }

    [SerializeField] private AppConfig _config;

    private AssetCacheStore _store;
    private ApiClient _anonClient;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        var ownPath = Path.Combine(Application.persistentDataPath, OwnCacheDir);
        var othersPath = Path.Combine(Application.temporaryCachePath, OthersCacheDir);
        Directory.CreateDirectory(ownPath);
        Directory.CreateDirectory(othersPath);

        _store = new AssetCacheStore(ownPath, othersPath, OthersTtl);
        _anonClient = new ApiClient(_config.ApiBaseUrl);
    }

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// URL からアセットを取得し、ローカルファイルパスを返す。
    /// hash が一致すればキャッシュから返す。isOwn=true のアセットは永続保存。
    /// </summary>
    public async Task<(string localPath, string error)> GetOrDownloadAsync(
        string url,
        string hash,
        string ext,
        bool isOwn,
        CancellationToken ct = default
    )
    {
        var localPath = _store.GetLocalPath(hash, ext, isOwn);

        if (File.Exists(localPath))
        {
            // Touch the file so TTL starts from last access
            if (!isOwn)
                File.SetLastAccessTimeUtc(localPath, DateTime.UtcNow);

            if (_store.HashMatchesFile(localPath, hash))
                return (localPath, null);

            // Hash mismatch — delete stale file and re-download
            File.Delete(localPath);
        }

        return await DownloadAndCacheAsync(url, hash, ext, localPath, ct);
    }

    /// <summary>
    /// URL から Texture2D を取得する（ローカルキャッシュを経由）。
    /// </summary>
    public async Task<(Texture2D tex, string error)> GetOrDownloadTextureAsync(
        string url,
        string hash,
        string ext,
        bool isOwn,
        CancellationToken ct = default
    )
    {
        var (localPath, error) = await GetOrDownloadAsync(url, hash, ext, isOwn, ct);
        if (error != null)
            return (null, error);

        byte[] data;
        try
        {
            data = File.ReadAllBytes(localPath);
        }
        catch (Exception ex)
        {
            return (null, ex.Message);
        }

        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!tex.LoadImage(data))
        {
            Destroy(tex);
            return (null, "texture_load_failed");
        }
        return (tex, null);
    }

    /// <summary>
    /// 期限切れの他人アセットを削除する（起動時やアイドル時に呼ぶ）。
    /// </summary>
    public void PurgeExpiredOthersCache()
    {
        _store.PurgeExpired(DateTime.UtcNow);
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private async Task<(string localPath, string error)> DownloadAndCacheAsync(
        string url,
        string hash,
        string ext,
        string localPath,
        CancellationToken ct
    )
    {
        var (data, error) = await _anonClient.GetBytesAsync(url, ct);
        if (error != null)
            return (null, error);

        try
        {
            await Task.Run(() => File.WriteAllBytes(localPath, data), ct);
        }
        catch (Exception ex)
        {
            return (null, ex.Message);
        }

        return (localPath, null);
    }
}
