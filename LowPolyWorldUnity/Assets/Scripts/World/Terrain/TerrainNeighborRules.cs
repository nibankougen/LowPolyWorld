/// <summary>
/// 面カリングの隣接判定ルール（world-creation.md セクション 15.12 の形状別テーブル準拠）。
///
/// | B の形状 | A の上面判定（B が真上） | A の側面判定（B が隣） |
/// |---|---|---|
/// | cube | 非表示 | 非表示 |
/// | ramp | 非表示（ramp の下面は full） | 表示 |
/// | diag | 表示 | B の solid 部分が面を完全に覆う場合のみ非表示 |
///
/// 透明テクスチャは隣接判定に影響しない（15.14 — ボクセルの有無のみで判定する）。
/// </summary>
public static class TerrainNeighborRules
{
    public static bool IsRamp(TerrainShape shape) =>
        shape >= TerrainShape.RampN && shape <= TerrainShape.RampW;

    public static bool IsDiag(TerrainShape shape) =>
        shape >= TerrainShape.DiagNW && shape <= TerrainShape.DiagSW;

    /// <summary>
    /// 角（外角・四面体）か。角は底面が半三角・上面が斜面・full 側面を持たないため、隣接ブロックの
    /// 面を一切隠さない（HidesTopFace/HidesBottomFace/HidesSideFace はすべて false に fall-through する）。
    /// </summary>
    public static bool IsCorner(TerrainShape shape) =>
        shape >= TerrainShape.CornerNW && shape <= TerrainShape.CornerSW;

    /// <summary>
    /// 真上の隣接ブロックが A の上面（上の平面に接する面 = cube の上面・diag の上面三角形）を隠すか。
    /// 坂の斜面は平面に接しないためカリング対象外（常に生成する）。
    /// </summary>
    public static bool HidesTopFace(byte neighborAbove)
    {
        var shape = TerrainVoxel.GetShape(neighborAbove);
        return shape == TerrainShape.Cube || IsRamp(shape);
    }

    /// <summary>真下の隣接ブロックが A の下面を隠すか（上面が full なのは cube のみ）。</summary>
    public static bool HidesBottomFace(byte neighborBelow) =>
        TerrainVoxel.GetShape(neighborBelow) == TerrainShape.Cube;

    /// <summary>faceDir 方向の隣接ブロックが A の側面を隠すか。</summary>
    public static bool HidesSideFace(byte neighbor, TerrainFaceDir faceDir)
    {
        var shape = TerrainVoxel.GetShape(neighbor);
        if (shape == TerrainShape.Cube)
            return true;
        if (IsDiag(shape))
            return DiagCoversFace(shape, TerrainFaceDirUtil.Opposite(faceDir));
        return false; // empty・ramp は側面を隠さない
    }

    /// <summary>diag 形状の solid 部分が、自ブロックの face 方向の面を完全に覆うか。</summary>
    public static bool DiagCoversFace(TerrainShape diag, TerrainFaceDir face)
    {
        switch (diag)
        {
            case TerrainShape.DiagNW:
                return face == TerrainFaceDir.North || face == TerrainFaceDir.West;
            case TerrainShape.DiagNE:
                return face == TerrainFaceDir.North || face == TerrainFaceDir.East;
            case TerrainShape.DiagSE:
                return face == TerrainFaceDir.South || face == TerrainFaceDir.East;
            case TerrainShape.DiagSW:
                return face == TerrainFaceDir.South || face == TerrainFaceDir.West;
            default:
                return false;
        }
    }

    /// <summary>
    /// 同じ種類の地形か（テクスチャ領域選択 15.8 用 — 同一パレットインデックスかつ両方非 empty）。
    /// </summary>
    public static bool IsSameKind(byte a, byte b) =>
        !TerrainVoxel.IsEmpty(a)
        && !TerrainVoxel.IsEmpty(b)
        && TerrainVoxel.GetPaletteIndex(a) == TerrainVoxel.GetPaletteIndex(b);
}
