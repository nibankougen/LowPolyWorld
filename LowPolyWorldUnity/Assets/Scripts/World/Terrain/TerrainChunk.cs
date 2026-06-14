using System;

/// <summary>
/// 地形チャンク（world-creation.md セクション 15.13）。
///
/// - サイズ: 16 × 16 × 16 ブロック
/// - 格納順: X → Z → Y（X が最内ループ。XZ 方向の繰り返しを RLE で圧縮しやすくするため）
/// - ボクセルは 2 byte（ushort）
/// </summary>
public class TerrainChunk
{
    public const int Size = 16;
    public const int VoxelCount = Size * Size * Size; // 4096

    private readonly ushort[] _voxels = new ushort[VoxelCount];
    private int _nonEmptyCount;

    /// <summary>全ボクセルが empty か（空チャンクはバイナリ保存時に省略される）。</summary>
    public bool IsEmpty => _nonEmptyCount == 0;

    /// <summary>チャンクローカル座標 → 配列インデックス（X → Z → Y 順）。</summary>
    public static int Index(int x, int y, int z)
    {
        ValidateLocal(x, y, z);
        return x + z * Size + y * Size * Size;
    }

    public ushort GetVoxel(int x, int y, int z) => _voxels[Index(x, y, z)];

    public void SetVoxel(int x, int y, int z, ushort voxel)
    {
        int index = Index(x, y, z);
        ushort prev = _voxels[index];
        if (prev == voxel)
            return;

        if (prev == TerrainVoxel.Empty)
            _nonEmptyCount++;
        else if (voxel == TerrainVoxel.Empty)
            _nonEmptyCount--;

        _voxels[index] = voxel;
    }

    /// <summary>格納順どおりの生ボクセル列をコピーして返す（シリアライズ用）。</summary>
    public ushort[] ToVoxels()
    {
        var copy = new ushort[VoxelCount];
        Array.Copy(_voxels, copy, VoxelCount);
        return copy;
    }

    /// <summary>
    /// 生ボクセル列（格納順 X → Z → Y・4096 要素）からチャンクを復元する。
    /// 不正な長さ・不正なボクセル値を含む場合は null を返す（UGC データの防御）。
    /// </summary>
    public static TerrainChunk FromVoxels(ushort[] voxels)
    {
        if (voxels == null || voxels.Length != VoxelCount)
            return null;

        var chunk = new TerrainChunk();
        for (int i = 0; i < VoxelCount; i++)
        {
            ushort v = voxels[i];
            if (!TerrainVoxel.IsValid(v))
                return null;
            if (v != TerrainVoxel.Empty)
                chunk._nonEmptyCount++;
            chunk._voxels[i] = v;
        }
        return chunk;
    }

    private static void ValidateLocal(int x, int y, int z)
    {
        if ((uint)x >= Size || (uint)y >= Size || (uint)z >= Size)
            throw new ArgumentOutOfRangeException(
                $"チャンクローカル座標は 0〜{Size - 1} の範囲で指定してください: ({x}, {y}, {z})");
    }
}
