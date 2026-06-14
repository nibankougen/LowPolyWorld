using System;
using System.IO;

/// <summary>
/// ボクセルバイナリファイルのシリアライズ / デシリアライズ
/// （world-creation.md セクション 15.13）。
///
/// フォーマット（version 2 — ボクセル 2 byte 化に伴い v1 から変更。リリース前のため後方互換なし）:
///   [4 bytes] magic: ASCII "LWVT"（0x4C 0x57 0x56 0x54）
///   [4 bytes] version: 2（リトルエンディアン）
///   [4 bytes] chunk count（リトルエンディアン・非空チャンクのみ）
///   per chunk:
///     [1 byte]  cx
///     [1 byte]  cy
///     [1 byte]  cz
///     [2 bytes] RLE data length（リトルエンディアン）
///     [N bytes] RLE data: (ushort value LE, byte count) の 3 バイトペア
///
/// ファイルは UGC 由来のため、デシリアライズは TryDeserialize で防御的に検証し、
/// 不正データはエラー理由付きで拒否する（サーバー側はワールド保存時に同等の検証で拒否）。
/// </summary>
public static class TerrainBinarySerializer
{
    public const int Version = 2;

    private static readonly byte[] Magic = { 0x4C, 0x57, 0x56, 0x54 }; // "LWVT"

    // ── シリアライズ ──────────────────────────────────────────────────────────

    public static byte[] Serialize(TerrainVoxelStore store)
    {
        if (store == null)
            throw new ArgumentNullException(nameof(store));

        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        writer.Write(Magic);
        writer.Write(Version);                  // int32 リトルエンディアン
        writer.Write(store.NonEmptyChunkCount); // int32 リトルエンディアン

        foreach (var (cx, cy, cz, chunk) in store.EnumerateNonEmptyChunks())
        {
            byte[] rle = TerrainRle.Encode(chunk.ToVoxels());
            writer.Write((byte)cx);
            writer.Write((byte)cy);
            writer.Write((byte)cz);
            writer.Write((ushort)rle.Length);   // リトルエンディアン
            writer.Write(rle);
        }

        writer.Flush();
        return ms.ToArray();
    }

    // ── デシリアライズ ────────────────────────────────────────────────────────

    /// <summary>
    /// バイナリからボクセルデータを復元する。失敗時は false を返し error に理由を入れる。
    /// </summary>
    public static bool TryDeserialize(byte[] data, out TerrainVoxelStore store, out string error)
    {
        store = null;
        error = null;

        if (data == null || data.Length < 12)
        {
            error = "データが短すぎます";
            return false;
        }

        for (int i = 0; i < 4; i++)
        {
            if (data[i] != Magic[i])
            {
                error = "マジックナンバーが不正です";
                return false;
            }
        }

        using var ms = new MemoryStream(data, 4, data.Length - 4);
        using var reader = new BinaryReader(ms);

        int version = reader.ReadInt32();
        if (version != Version)
        {
            error = $"未対応のバージョンです: {version}";
            return false;
        }

        int chunkCount = reader.ReadInt32();
        if (chunkCount < 0 || chunkCount > TerrainVoxelStore.ChunkCountX
                * TerrainVoxelStore.ChunkCountY * TerrainVoxelStore.ChunkCountZ)
        {
            error = $"チャンク数が不正です: {chunkCount}";
            return false;
        }

        var result = new TerrainVoxelStore();

        for (int i = 0; i < chunkCount; i++)
        {
            if (ms.Length - ms.Position < 5)
            {
                error = "チャンクヘッダーが途中で切れています";
                return false;
            }

            int cx = reader.ReadByte();
            int cy = reader.ReadByte();
            int cz = reader.ReadByte();
            int rleLength = reader.ReadUInt16();

            if (!TerrainVoxelStore.ChunkInBounds(cx, cy, cz))
            {
                error = $"チャンク座標が範囲外です: ({cx}, {cy}, {cz})";
                return false;
            }
            if (ms.Length - ms.Position < rleLength)
            {
                error = "RLE データが途中で切れています";
                return false;
            }

            byte[] rle = reader.ReadBytes(rleLength);
            ushort[] voxels = TerrainRle.Decode(rle, TerrainChunk.VoxelCount);
            if (voxels == null)
            {
                error = $"チャンク ({cx}, {cy}, {cz}) の RLE データが不正です";
                return false;
            }

            var chunk = TerrainChunk.FromVoxels(voxels);
            if (chunk == null)
            {
                error = $"チャンク ({cx}, {cy}, {cz}) に不正なボクセル値が含まれています";
                return false;
            }
            if (!IsPaddingEmpty(cx, cy, cz, chunk))
            {
                error = $"チャンク ({cx}, {cy}, {cz}) のワールド範囲外（パディング領域）に地形があります";
                return false;
            }
            if (!result.TryAddChunk(cx, cy, cz, chunk))
            {
                error = $"チャンク座標が重複しています: ({cx}, {cy}, {cz})";
                return false;
            }
        }

        if (ms.Position != ms.Length)
        {
            error = "末尾に余分なデータがあります";
            return false;
        }

        store = result;
        return true;
    }

    // 端チャンクのパディング領域（ワールド 63×31×63 の外）に非空ボクセルがないか検証する
    private static bool IsPaddingEmpty(int cx, int cy, int cz, TerrainChunk chunk)
    {
        int maxLocalX = Math.Min(TerrainChunk.Size, TerrainVoxelStore.SizeX - cx * TerrainChunk.Size);
        int maxLocalY = Math.Min(TerrainChunk.Size, TerrainVoxelStore.SizeY - cy * TerrainChunk.Size);
        int maxLocalZ = Math.Min(TerrainChunk.Size, TerrainVoxelStore.SizeZ - cz * TerrainChunk.Size);

        // パディングがないチャンクはスキャン不要
        if (maxLocalX == TerrainChunk.Size && maxLocalY == TerrainChunk.Size && maxLocalZ == TerrainChunk.Size)
            return true;

        for (int y = 0; y < TerrainChunk.Size; y++)
        for (int z = 0; z < TerrainChunk.Size; z++)
        for (int x = 0; x < TerrainChunk.Size; x++)
        {
            if (x < maxLocalX && y < maxLocalY && z < maxLocalZ)
                continue;
            if (chunk.GetVoxel(x, y, z) != TerrainVoxel.Empty)
                return false;
        }
        return true;
    }
}
