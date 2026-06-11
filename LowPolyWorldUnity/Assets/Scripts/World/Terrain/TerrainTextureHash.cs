using System;

/// <summary>
/// ランダム地形テクスチャのバリアント選択ハッシュ（world-creation.md セクション 15.9）。
/// グリッド座標・面方向から決定論的にバリアントインデックスを選択する。
/// </summary>
public static class TerrainTextureHash
{
    /// <summary>
    /// バリアントインデックスを選択する。variantCount は 2 の累乗（1 / 4 / 8 など）であること。
    /// </summary>
    public static int SelectIndex(int xg, int yg, int zg, int directionIndex, int variantCount)
    {
        if (variantCount <= 0 || (variantCount & (variantCount - 1)) != 0)
            throw new ArgumentException("バリアント数は 2 の累乗である必要があります。", nameof(variantCount));

        unchecked
        {
            uint h = (uint)(73856093 * xg ^ 19349663 * yg ^ 83492791 * zg ^ directionIndex) * 2654435761u;
            h ^= h >> 13;
            h *= 1274126177u;
            return (int)(h & (uint)(variantCount - 1));
        }
    }
}
