/// <summary>
/// 地形ブロックの形状（world-creation.md セクション 15.12）。
/// 形状値は 0〜17（5 ビット）。ボクセルは 2 byte（ushort）で表現する。
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

    // 凹角（内角）。立方体から 1 つの上角を斜めに切り落とした形（下面 4 頂点・上面 3 頂点）。
    // 切り落とした角（低い側）へ斜面が下る。坂 2 枚が直角に出会う内側の隅を埋める。
    ConcaveNW = 14, // 切り欠き = NW 上角（West・North 側面が三角形、South・East 側面が full）
    ConcaveNE = 15, // 切り欠き = NE 上角
    ConcaveSE = 16, // 切り欠き = SE 上角
    ConcaveSW = 17, // 切り欠き = SW 上角
}

/// <summary>
/// ボクセル値のエンコード / デコード（world-creation.md セクション 15.13）。
///
/// 2 byte/voxel（ushort・リトルエンディアンで保存）:
///   bit 8-4: shape（TerrainShape。0〜17。0 = empty）
///   bit 3-0: palette index（0〜15。shape=0 のときは無効）
///   bit 15-9: 予約（0）
///
/// 形状が 18 種（empty/cube/ramp×4/diag×4/corner×4/concave×4）に増え 4 bit に収まらなくなったため
/// 2 byte に拡張した。ビット配置は従来の 1 byte 版（shape を上位・palette を下位 4 bit）を踏襲する。
/// </summary>
public static class TerrainVoxel
{
    public const int MaxPaletteIndex = 15;
    public const byte MaxShapeValue = (byte)TerrainShape.ConcaveSW;

    public const ushort Empty = 0;

    /// <summary>形状とパレットインデックスからボクセル値を作る。</summary>
    public static ushort Encode(TerrainShape shape, int paletteIndex)
    {
        if ((uint)paletteIndex > MaxPaletteIndex)
            throw new System.ArgumentOutOfRangeException(nameof(paletteIndex),
                $"パレットインデックスは 0〜{MaxPaletteIndex} の範囲で指定してください。");
        if ((byte)shape > MaxShapeValue)
            throw new System.ArgumentOutOfRangeException(nameof(shape));

        if (shape == TerrainShape.Empty)
            return Empty; // empty はパレットも 0 に正規化する

        return (ushort)(((int)shape << 4) | paletteIndex);
    }

    public static TerrainShape GetShape(ushort voxel) => (TerrainShape)(voxel >> 4);

    /// <summary>パレットインデックス（shape=Empty のときは無効値）。</summary>
    public static int GetPaletteIndex(ushort voxel) => voxel & 0x0F;

    public static bool IsEmpty(ushort voxel) => (voxel >> 4) == 0;

    /// <summary>
    /// ボクセル値として妥当か（shape が定義済み・empty のとき palette が 0・予約ビットが 0）。
    /// バイナリデシリアライズ時の検証に使う。
    /// </summary>
    public static bool IsValid(ushort voxel)
    {
        int shape = voxel >> 4;
        if (shape > MaxShapeValue)
            return false; // 予約ビット（bit 9 以上）が立っていてもここで弾かれる
        if (shape == 0 && (voxel & 0x0F) != 0)
            return false; // empty はパレット 0 に正規化されている前提
        return true;
    }
}
