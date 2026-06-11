using System;

/// <summary>
/// 地形メッシュの面の向き。Up〜West は軸方向の平面、Slope は坂の斜め上面、
/// Hypotenuse は diag の斜辺垂直面（XZ 平面で 45° 傾いた面）。
/// </summary>
public enum TerrainFaceDir
{
    Up = 0,
    Down = 1,
    North = 2,
    South = 3,
    East = 4,
    West = 5,
    Slope = 6,
    Hypotenuse = 7,
}

/// <summary>
/// 地形テクスチャの領域名（world-creation.md セクション 15.5 / 15.6 / 15.8）。
/// </summary>
public enum TerrainFaceRegion
{
    Top = 0,            // 上面
    TopMiddle = 1,      // 上面中間（Height Culling 表示用）
    SideTop = 2,        // 側面上端
    Side = 3,           // 側面
    SideBottom = 4,     // 側面下端
    SideTopBottom = 5,  // 側面上端下端
    RampSide = 6,       // 坂側面（三角形面）
    RampSideBottom = 7, // 坂側面下端（三角形面）
    Bottom = 8,         // 下面
}

public static class TerrainFaceDirUtil
{
    /// <summary>バリアント選択ハッシュ用の方向インデックス（15.9。正の整数）。</summary>
    public static int DirectionIndex(TerrainFaceDir dir) => (int)dir + 1;

    /// <summary>軸方向の面の隣接オフセット。Slope / Hypotenuse は軸方向を持たないため不可。</summary>
    public static (int dx, int dy, int dz) Offset(TerrainFaceDir dir)
    {
        switch (dir)
        {
            case TerrainFaceDir.Up: return (0, 1, 0);
            case TerrainFaceDir.Down: return (0, -1, 0);
            case TerrainFaceDir.North: return (0, 0, 1);
            case TerrainFaceDir.South: return (0, 0, -1);
            case TerrainFaceDir.East: return (1, 0, 0);
            case TerrainFaceDir.West: return (-1, 0, 0);
            default:
                throw new ArgumentOutOfRangeException(nameof(dir), "軸方向の面のみオフセットを持ちます。");
        }
    }

    /// <summary>反対方向（軸方向の面のみ）。</summary>
    public static TerrainFaceDir Opposite(TerrainFaceDir dir)
    {
        switch (dir)
        {
            case TerrainFaceDir.Up: return TerrainFaceDir.Down;
            case TerrainFaceDir.Down: return TerrainFaceDir.Up;
            case TerrainFaceDir.North: return TerrainFaceDir.South;
            case TerrainFaceDir.South: return TerrainFaceDir.North;
            case TerrainFaceDir.East: return TerrainFaceDir.West;
            case TerrainFaceDir.West: return TerrainFaceDir.East;
            default:
                throw new ArgumentOutOfRangeException(nameof(dir), "軸方向の面のみ反対方向を持ちます。");
        }
    }
}
