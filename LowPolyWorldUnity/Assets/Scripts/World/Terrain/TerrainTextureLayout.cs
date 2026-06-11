using System;
using UnityEngine;

/// <summary>
/// 地形テクスチャ内の領域レイアウト（world-creation.md セクション 15.5 / 15.6）。
///
/// ランダム地形テクスチャ（256×256）: 8×8 分割（各 32×32）。行 0 = 最下行。
///   行 0: 下面 0〜3 / 将来拡張用、行 1: 坂側面下端 0〜3 / 坂側面 0〜3、
///   行 2: 側面上端下端、行 3: 側面下端、行 4: 側面、行 5: 側面上端、行 6: 上面中間、行 7: 上面
/// 固定地形テクスチャ（32×256）: 1×8 分割。バリアントは 1 種のみ。
///   坂側面下端は存在しないため坂側面の行を使用する。
/// </summary>
public static class TerrainTextureLayout
{
    private const float Cell = 1f / 8f;

    /// <summary>領域のバリアント数（必ず 2 の累乗）。</summary>
    public static int GetVariantCount(TerrainFaceRegion region, bool isFixedTexture)
    {
        if (isFixedTexture)
            return 1;
        switch (region)
        {
            case TerrainFaceRegion.RampSide:
            case TerrainFaceRegion.RampSideBottom:
            case TerrainFaceRegion.Bottom:
                return 4;
            default:
                return 8;
        }
    }

    /// <summary>テクスチャ全体を [0,1]² としたときの領域 UV 矩形。</summary>
    public static Rect GetRegionRect(TerrainFaceRegion region, int variantIndex, bool isFixedTexture)
    {
        int count = GetVariantCount(region, isFixedTexture);
        if ((uint)variantIndex >= (uint)count)
            throw new ArgumentOutOfRangeException(
                nameof(variantIndex), $"バリアントインデックスは 0〜{count - 1} の範囲で指定してください。");

        if (isFixedTexture)
            return new Rect(0f, FixedRow(region) * Cell, 1f, Cell);

        var (row, colOffset) = RandomCell(region);
        return new Rect((colOffset + variantIndex) * Cell, row * Cell, Cell, Cell);
    }

    private static (int row, int colOffset) RandomCell(TerrainFaceRegion region)
    {
        switch (region)
        {
            case TerrainFaceRegion.Top: return (7, 0);
            case TerrainFaceRegion.TopMiddle: return (6, 0);
            case TerrainFaceRegion.SideTop: return (5, 0);
            case TerrainFaceRegion.Side: return (4, 0);
            case TerrainFaceRegion.SideBottom: return (3, 0);
            case TerrainFaceRegion.SideTopBottom: return (2, 0);
            case TerrainFaceRegion.RampSide: return (1, 4);
            case TerrainFaceRegion.RampSideBottom: return (1, 0);
            case TerrainFaceRegion.Bottom: return (0, 0);
            default:
                throw new ArgumentOutOfRangeException(nameof(region));
        }
    }

    private static int FixedRow(TerrainFaceRegion region)
    {
        switch (region)
        {
            case TerrainFaceRegion.Top: return 7;
            case TerrainFaceRegion.TopMiddle: return 6;
            case TerrainFaceRegion.SideTop: return 5;
            case TerrainFaceRegion.Side: return 4;
            case TerrainFaceRegion.SideBottom: return 3;
            case TerrainFaceRegion.SideTopBottom: return 2;
            case TerrainFaceRegion.RampSide: return 1;
            case TerrainFaceRegion.RampSideBottom: return 1; // 固定テクスチャに坂側面下端はない（15.6）
            case TerrainFaceRegion.Bottom: return 0;
            default:
                throw new ArgumentOutOfRangeException(nameof(region));
        }
    }
}
