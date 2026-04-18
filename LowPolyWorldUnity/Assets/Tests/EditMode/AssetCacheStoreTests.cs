using System;
using System.IO;
using System.Security.Cryptography;
using NUnit.Framework;

public class AssetCacheStoreTests
{
    private string _tempOwn;
    private string _tempOthers;
    private AssetCacheStore _store;

    [SetUp]
    public void SetUp()
    {
        _tempOwn = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        _tempOthers = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempOwn);
        Directory.CreateDirectory(_tempOthers);
        _store = new AssetCacheStore(_tempOwn, _tempOthers, TimeSpan.FromDays(7));
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempOwn)) Directory.Delete(_tempOwn, recursive: true);
        if (Directory.Exists(_tempOthers)) Directory.Delete(_tempOthers, recursive: true);
    }

    // ── パス計算 ─────────────────────────────────────────────────────────────

    [Test]
    public void GetLocalPath_OwnAsset_UsesOwnDirectory()
    {
        var path = _store.GetLocalPath("abc123", "vrm", isOwn: true);
        StringAssert.StartsWith(_tempOwn, path);
        StringAssert.EndsWith("abc123.vrm", path);
    }

    [Test]
    public void GetLocalPath_OthersAsset_UsesOthersDirectory()
    {
        var path = _store.GetLocalPath("def456", "glb", isOwn: false);
        StringAssert.StartsWith(_tempOthers, path);
        StringAssert.EndsWith("def456.glb", path);
    }

    // ── ハッシュ検証 ─────────────────────────────────────────────────────────

    [Test]
    public void HashMatchesFile_CorrectHash_ReturnsTrue()
    {
        var data = System.Text.Encoding.UTF8.GetBytes("hello world");
        var expectedHash = ComputeSha256(data);
        var filePath = Path.Combine(_tempOwn, "test.bin");
        File.WriteAllBytes(filePath, data);

        Assert.IsTrue(_store.HashMatchesFile(filePath, expectedHash));
    }

    [Test]
    public void HashMatchesFile_WrongHash_ReturnsFalse()
    {
        var data = System.Text.Encoding.UTF8.GetBytes("hello world");
        var filePath = Path.Combine(_tempOwn, "test.bin");
        File.WriteAllBytes(filePath, data);

        Assert.IsFalse(_store.HashMatchesFile(filePath, "0000000000000000000000000000000000000000000000000000000000000000"));
    }

    [Test]
    public void HashMatchesFile_MissingFile_ReturnsFalse()
    {
        Assert.IsFalse(_store.HashMatchesFile(Path.Combine(_tempOwn, "nonexistent.bin"), "anyhash"));
    }

    // ── TTL 判定 ──────────────────────────────────────────────────────────────

    [Test]
    public void IsExpired_WithinTtl_ReturnsFalse()
    {
        var lastAccess = DateTime.UtcNow.AddDays(-3);
        var now = DateTime.UtcNow;
        Assert.IsFalse(_store.IsExpired(lastAccess, now));
    }

    [Test]
    public void IsExpired_ExactlyAtTtl_ReturnsTrue()
    {
        var lastAccess = DateTime.UtcNow.AddDays(-7).AddSeconds(-1);
        var now = DateTime.UtcNow;
        Assert.IsTrue(_store.IsExpired(lastAccess, now));
    }

    [Test]
    public void IsExpired_BeyondTtl_ReturnsTrue()
    {
        var lastAccess = DateTime.UtcNow.AddDays(-30);
        var now = DateTime.UtcNow;
        Assert.IsTrue(_store.IsExpired(lastAccess, now));
    }

    // ── 期限切れファイル検出・削除 ────────────────────────────────────────────

    [Test]
    public void GetExpiredFiles_ReturnsOnlyExpiredFiles()
    {
        var expiredPath = Path.Combine(_tempOthers, "expired.vrm");
        var freshPath = Path.Combine(_tempOthers, "fresh.vrm");
        File.WriteAllText(expiredPath, "old");
        File.WriteAllText(freshPath, "new");

        // Set last access to > 7 days ago for expired file
        File.SetLastAccessTimeUtc(expiredPath, DateTime.UtcNow.AddDays(-8));
        File.SetLastAccessTimeUtc(freshPath, DateTime.UtcNow.AddDays(-1));

        var expired = new System.Collections.Generic.List<string>(_store.GetExpiredFiles(DateTime.UtcNow));

        Assert.AreEqual(1, expired.Count);
        StringAssert.EndsWith("expired.vrm", expired[0]);
    }

    [Test]
    public void PurgeExpired_DeletesExpiredFiles_LeavesFreashFiles()
    {
        var expiredPath = Path.Combine(_tempOthers, "old.glb");
        var freshPath = Path.Combine(_tempOthers, "new.glb");
        File.WriteAllText(expiredPath, "old");
        File.WriteAllText(freshPath, "new");

        File.SetLastAccessTimeUtc(expiredPath, DateTime.UtcNow.AddDays(-10));
        File.SetLastAccessTimeUtc(freshPath, DateTime.UtcNow.AddDays(-1));

        _store.PurgeExpired(DateTime.UtcNow);

        Assert.IsFalse(File.Exists(expiredPath));
        Assert.IsTrue(File.Exists(freshPath));
    }

    // ── ヘルパー ──────────────────────────────────────────────────────────────

    private static string ComputeSha256(byte[] data)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(data);
        return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
    }
}
