using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

/// <summary>
/// ハッシュベースアセットキャッシュのストア管理ロジック（純粋 C#・MonoBehaviour 非依存）。
/// パス計算・ハッシュ検証・TTL 判定を担当する。実際の HTTP ダウンロードは呼び出し元が行う。
/// </summary>
public class AssetCacheStore
{
    private readonly string _ownPath;
    private readonly string _othersPath;
    private readonly TimeSpan _othersTtl;

    public AssetCacheStore(string ownPath, string othersPath, TimeSpan othersTtl)
    {
        _ownPath = ownPath;
        _othersPath = othersPath;
        _othersTtl = othersTtl;
    }

    /// <summary>
    /// hash + ext + isOwn からローカルファイルパスを返す。
    /// isOwn=true → 永続パス(_ownPath)、isOwn=false → 一時パス(_othersPath)。
    /// </summary>
    public string GetLocalPath(string hash, string ext, bool isOwn)
    {
        var dir = isOwn ? _ownPath : _othersPath;
        return Path.Combine(dir, $"{hash}.{ext}");
    }

    /// <summary>
    /// ファイルの SHA-256 ハッシュが expectedHash と一致するか検証する。
    /// ファイルが存在しない・読み取れない場合は false。
    /// </summary>
    public bool HashMatchesFile(string filePath, string expectedHash)
    {
        if (!File.Exists(filePath))
            return false;
        try
        {
            using var sha = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            var bytes = sha.ComputeHash(stream);
            var actual = BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
            return string.Equals(actual, expectedHash, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// lastAccessUtc が nowUtc 基準で TTL を超えているか判定する（他人アセット用）。
    /// </summary>
    public bool IsExpired(DateTime lastAccessUtc, DateTime nowUtc)
    {
        return nowUtc - lastAccessUtc > _othersTtl;
    }

    /// <summary>
    /// _othersPath 内の期限切れファイルパスを列挙する。
    /// </summary>
    public IEnumerable<string> GetExpiredFiles(DateTime nowUtc)
    {
        if (!Directory.Exists(_othersPath))
            yield break;

        foreach (var file in Directory.GetFiles(_othersPath))
        {
            var lastAccess = File.GetLastAccessTimeUtc(file);
            if (IsExpired(lastAccess, nowUtc))
                yield return file;
        }
    }

    /// <summary>
    /// _othersPath 内の期限切れファイルを削除する。削除失敗はサイレントに無視する。
    /// </summary>
    public void PurgeExpired(DateTime nowUtc)
    {
        foreach (var file in GetExpiredFiles(nowUtc))
        {
            try { File.Delete(file); }
            catch { }
        }
    }
}
