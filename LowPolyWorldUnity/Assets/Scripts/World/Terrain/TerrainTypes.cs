/// <summary>
/// 地形ブロックの形状（world-creation.md セクション 15.12）。4 ビットで表現する。
/// </summary>
public enum TerrainShape : byte
{
    Empty = 0,  // 空（地形なし）
    Cube = 1,   // 通常の立方体
    RampN = 2,  // 坂（North 側が高い）
    RampE = 3,  // 坂（East 側が高い）
    RampS = 4,  // 坂（South 側が高い）
    RampW = 5,  // 坂（West 側が高い）
    DiagNW = 6, // 斜め（\ 方向・NW 半分が solid）
    DiagSE = 7, // 斜め（\ 方向・SE 半分が solid）
    DiagNE = 8, // 斜め（/ 方向・NE 半分が solid）
    DiagSW = 9, // 斜め（/ 方向・SW 半分が solid）

    // 角（外角・四面体。坂と斜めの組み合わせ。高い頂点が 1 つの上角にあり、対角の低い縁へ下る）
    CornerNW = 10, // 高い頂点 = NW 上角（West・North 側面が壁、SE 側へ下る）
    CornerNE = 11, // 高い頂点 = NE 上角
    CornerSE = 12, // 高い頂点 = SE 上角
    CornerSW = 13, // 高い頂点 = SW 上角
}

/// <summary>
/// ボクセルバイトのエンコード / デコード（world-creation.md セクション 15.13）。
///
/// 1 byte/voxel:
///   bit 7-4: shape nibble（TerrainShape。0 = empty）
///   bit 3-0: palette index nibble（0〜15。shape=0 のときは無効）
/// </summary>
public static class TerrainVoxel
{
    public const int MaxPaletteIndex = 15;
    public const byte MaxShapeValue = (byte)TerrainShape.CornerSW;

    public const byte Empty = 0;

    /// <summary>形状とパレットインデックスからボクセルバイトを作る。</summary>
    public static byte Encode(TerrainShape shape, int paletteIndex)
    {
        if ((uint)paletteIndex > MaxPaletteIndex)
            throw new System.ArgumentOutOfRangeException(nameof(paletteIndex),
                $"パレットインデックスは 0〜{MaxPaletteIndex} の範囲で指定してください。");
        if ((byte)shape > MaxShapeValue)
            throw new System.ArgumentOutOfRangeException(nameof(shape));

        if (shape == TerrainShape.Empty)
            return Empty; // empty はパレットも 0 に正規化する

        return (byte)(((byte)shape << 4) | paletteIndex);
    }

    public static TerrainShape GetShape(byte voxel) => (TerrainShape)(voxel >> 4);

    /// <summary>パレットインデックス（shape=Empty のときは無効値）。</summary>
    public static int GetPaletteIndex(byte voxel) => voxel & 0x0F;

    public static bool IsEmpty(byte voxel) => (voxel >> 4) == 0;

    /// <summary>
    /// ボクセルバイトとして妥当か（shape nibble が定義済み・empty のとき palette が 0）。
    /// バイナリデシリアライズ時の検証に使う。
    /// </summary>
    public static bool IsValid(byte voxel)
    {
        int shape = voxel >> 4;
        if (shape > MaxShapeValue)
            return false;
        if (shape == 0 && (voxel & 0x0F) != 0)
            return false; // empty はパレット 0 に正規化されている前提
        return true;
    }
}
