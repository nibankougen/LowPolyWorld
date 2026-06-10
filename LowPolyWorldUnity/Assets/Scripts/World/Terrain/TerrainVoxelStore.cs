using System;
using System.Collections.Generic;

/// <summary>
/// ワールド全体のボクセルデータを管理するロジッククラス（world-creation.md セクション 15.13）。
///
/// - ワールドサイズ: 63 × 31 × 63 ブロック（X・Z ±15.75m / Y ±7.75m・1 ブロック = 0.5m）
/// - チャンク分割: 4 × 2 × 4 = 最大 32 チャンク（端のチャンクはパディング扱い）
/// - 空チャンクはメモリ上も保持しない（遅延生成）
/// </summary>
public class TerrainVoxelStore
{
    public const int SizeX = 63;
    public const int SizeY = 31;
    public const int SizeZ = 63;

    public const int ChunkCountX = 4; // ceil(63 / 16)
    public const int ChunkCountY = 2; // ceil(31 / 16)
    public const int ChunkCountZ = 4; // ceil(63 / 16)

    private readonly Dictionary<int, TerrainChunk> _chunks = new(); // チャンクキー → チャンク

    /// <summary>非空チャンク数（バイナリ保存対象の数）。</summary>
    public int NonEmptyChunkCount
    {
        get
        {
            int count = 0;
            foreach (var chunk in _chunks.Values)
                if (!chunk.IsEmpty)
                    count++;
            return count;
        }
    }

    // ── ボクセルアクセス（ワールド座標・0 起点） ──────────────────────────────

    public byte GetVoxel(int x, int y, int z)
    {
        ValidateWorld(x, y, z);
        int key = ChunkKey(x >> 4, y >> 4, z >> 4);
        if (!_chunks.TryGetValue(key, out var chunk))
            return TerrainVoxel.Empty;
        return chunk.GetVoxel(x & 15, y & 15, z & 15);
    }

    public void SetVoxel(int x, int y, int z, byte voxel)
    {
        ValidateWorld(x, y, z);
        int key = ChunkKey(x >> 4, y >> 4, z >> 4);
        if (!_chunks.TryGetValue(key, out var chunk))
        {
            if (voxel == TerrainVoxel.Empty)
                return; // 空チャンクに empty を書く必要はない
            chunk = new TerrainChunk();
            _chunks[key] = chunk;
        }
        chunk.SetVoxel(x & 15, y & 15, z & 15, voxel);
    }

    /// <summary>ワールド座標が範囲内か（メッシュ生成の隣接判定などで使用）。</summary>
    public static bool InBounds(int x, int y, int z) =>
        (uint)x < SizeX && (uint)y < SizeY && (uint)z < SizeZ;

    // ── チャンクアクセス（シリアライズ用） ────────────────────────────────────

    /// <summary>チャンク座標が範囲内か。</summary>
    public static bool ChunkInBounds(int cx, int cy, int cz) =>
        (uint)cx < ChunkCountX && (uint)cy < ChunkCountY && (uint)cz < ChunkCountZ;

    /// <summary>チャンクを取得する（存在しない場合は null）。</summary>
    public TerrainChunk GetChunk(int cx, int cy, int cz)
    {
        ValidateChunk(cx, cy, cz);
        return _chunks.TryGetValue(ChunkKey(cx, cy, cz), out var chunk) ? chunk : null;
    }

    /// <summary>デシリアライズ時にチャンクを直接配置する。既存チャンクがある場合は false。</summary>
    public bool TryAddChunk(int cx, int cy, int cz, TerrainChunk chunk)
    {
        ValidateChunk(cx, cy, cz);
        if (chunk == null)
            return false;
        int key = ChunkKey(cx, cy, cz);
        if (_chunks.ContainsKey(key))
            return false;
        _chunks[key] = chunk;
        return true;
    }

    /// <summary>非空チャンクを (cx, cy, cz, chunk) で列挙する（チャンクキー昇順・決定的）。</summary>
    public IEnumerable<(int cx, int cy, int cz, TerrainChunk chunk)> EnumerateNonEmptyChunks()
    {
        var keys = new List<int>(_chunks.Keys);
        keys.Sort();
        foreach (int key in keys)
        {
            var chunk = _chunks[key];
            if (chunk.IsEmpty)
                continue;
            int cx = key & 0xFF;
            int cz = (key >> 8) & 0xFF;
            int cy = (key >> 16) & 0xFF;
            yield return (cx, cy, cz, chunk);
        }
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private static int ChunkKey(int cx, int cy, int cz) => cx | (cz << 8) | (cy << 16);

    private static void ValidateWorld(int x, int y, int z)
    {
        if (!InBounds(x, y, z))
            throw new ArgumentOutOfRangeException(
                $"ワールド座標は (0〜{SizeX - 1}, 0〜{SizeY - 1}, 0〜{SizeZ - 1}) の範囲で指定してください: ({x}, {y}, {z})");
    }

    private static void ValidateChunk(int cx, int cy, int cz)
    {
        if (!ChunkInBounds(cx, cy, cz))
            throw new ArgumentOutOfRangeException(
                $"チャンク座標は (0〜{ChunkCountX - 1}, 0〜{ChunkCountY - 1}, 0〜{ChunkCountZ - 1}) の範囲で指定してください: ({cx}, {cy}, {cz})");
    }
}
