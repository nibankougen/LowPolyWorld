#if UNITY_EDITOR
using UnityEngine;

/// <summary>
/// エディタ確認用のプレースホルダー地形アトラスを生成する（サーバー生成アトラスの代替）。
/// パレット 0 = ランダム地形テクスチャ（256×256・8×8 領域）、
/// パレット 1 = 固定地形テクスチャ（32×256・1×8 領域）を 1024×1024 アトラスに配置する。
/// 領域ごとに色分けし、セル境界に枠線を描いて UV の向き・はみ出しを目視確認できるようにする。
/// </summary>
public static class TerrainDebugAtlas
{
    private const int AtlasSize = 1024;
    private const int Cell = 32;

    private static readonly Rect RandomTextureRect = new Rect(0f, 0f, 0.25f, 0.25f);     // 256×256
    private static readonly Rect FixedTextureRect = new Rect(0.25f, 0f, 0.03125f, 0.25f); // 32×256

    public static TerrainAtlasMap CreateAtlasMap() =>
        new TerrainAtlasMap(new[]
        {
            new TerrainAtlasMap.Entry(false, RandomTextureRect),
            new TerrainAtlasMap.Entry(true, FixedTextureRect),
        });

    public static Texture2D CreateTexture()
    {
        var texture = new Texture2D(AtlasSize, AtlasSize, TextureFormat.RGBA32, false)
        {
            name = "TerrainDebugAtlas",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
        };

        var pixels = new Color32[AtlasSize * AtlasSize];
        var unused = new Color32(60, 60, 70, 255);
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = unused;

        // パレット 0: ランダム地形テクスチャ（緑系・8×8 セル）
        for (int row = 0; row < 8; row++)
            for (int col = 0; col < 8; col++)
                FillCell(pixels, col * Cell, row * Cell, RandomCellColor(row, col));

        // パレット 1: 固定地形テクスチャ（青灰系・1×8 セル、x = 256 から）
        for (int row = 0; row < 8; row++)
            FillCell(pixels, 256, row * Cell, FixedCellColor(row));

        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        return texture;
    }

    private static Color RandomCellColor(int row, int col)
    {
        // 行 = 領域（15.5 のレイアウト）。バリアント（列）でわずかに明度を変える
        Color baseColor;
        switch (row)
        {
            case 7: baseColor = new Color(0.30f, 0.65f, 0.30f); break;                       // 上面
            case 6: baseColor = new Color(0.25f, 0.60f, 0.55f); break;                       // 上面中間
            case 5: baseColor = new Color(0.62f, 0.46f, 0.30f); break;                       // 側面上端
            case 4: baseColor = new Color(0.52f, 0.38f, 0.25f); break;                       // 側面
            case 3: baseColor = new Color(0.42f, 0.30f, 0.20f); break;                       // 側面下端
            case 2: baseColor = new Color(0.60f, 0.42f, 0.22f); break;                       // 側面上端下端
            case 1:
                baseColor = col < 4
                    ? new Color(0.50f, 0.35f, 0.50f)                                          // 坂側面下端
                    : new Color(0.35f, 0.45f, 0.65f);                                         // 坂側面
                break;
            default:
                baseColor = col < 4 ? new Color(0.30f, 0.30f, 0.32f) : Color.magenta; break;  // 下面 / 将来拡張
        }
        float variant = 0.82f + 0.06f * (col % 4);
        return baseColor * variant;
    }

    private static Color FixedCellColor(int row)
    {
        // ランダム側と同じ行配色を青灰寄りに（パレットの見分け用）
        Color c = RandomCellColor(row == 1 ? 1 : row, row == 1 ? 4 : 0);
        return new Color(c.r * 0.7f, c.g * 0.75f, c.b * 1.1f + 0.1f);
    }

    private static void FillCell(Color32[] pixels, int x0, int y0, Color color)
    {
        // Color * float はアルファも乗算してカットアウト閾値 (0.5) を下回るため、必ず不透明に固定する
        Color32 fill = Opaque(color);
        Color32 border = Opaque(color * 0.55f);
        for (int y = 0; y < Cell; y++)
        {
            for (int x = 0; x < Cell; x++)
            {
                bool isBorder = x < 2 || x >= Cell - 2 || y < 2 || y >= Cell - 2;
                pixels[(y0 + y) * AtlasSize + (x0 + x)] = isBorder ? border : fill;
            }
        }
    }

    private static Color32 Opaque(Color color) => new Color(color.r, color.g, color.b, 1f);
}
#endif
