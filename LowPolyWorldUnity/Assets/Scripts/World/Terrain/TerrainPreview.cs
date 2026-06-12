#if UNITY_EDITOR
using UnityEngine;

/// <summary>
/// 地形システムの見た目確認用プレビュー（エディタ専用）。
/// メニュー「Tools/LowPolyWorld/地形プレビューを生成」から生成し、Scene ビューで確認する。
/// Inspector の Height Cull Threshold スライダーで Height Culling を即時切替できる（-1 = 無効）。
///
/// サンプル内容: 地面（2 チャンクにまたがる）/ 4 方向 ramp / 4 方向 diag / 坂の階段 /
/// 2 階建ての建物（Height Culling・上面中間確認用）/ 浮遊スラブ（下面・AO 確認用）/
/// 固定テクスチャ（パレット 1）の床と坂
/// </summary>
[ExecuteAlways]
public class TerrainPreview : MonoBehaviour
{
    [Range(-1, 31)]
    [Tooltip("Height Culling 閾値（-1 = 無効）。建物の天井が y=5 にあるので 5 で 1 階内部が見える")]
    public int heightCullThreshold = -1;

    [Range(-1, 31)]
    [Tooltip("地形タブの上方半透明（ディザ）閾値（-1 = 無効）。この高さ以上の地形が市松模様になる")]
    public int ditherThreshold = -1;

    [Tooltip("ON で白一色テクスチャにして AO（頂点カラー）だけを確認する。変更後は右クリック → 再構築")]
    public bool plainWhiteTexture;

    [ContextMenu("再構築")]
    public void BuildSample()
    {
        var terrainRenderer = GetComponent<TerrainRenderer>();
        if (terrainRenderer == null)
            terrainRenderer = gameObject.AddComponent<TerrainRenderer>();

        terrainRenderer.Build(
            CreateSampleStore(), TerrainDebugAtlas.CreateAtlasMap(), TerrainDebugAtlas.CreateTexture(plainWhiteTexture));
        terrainRenderer.ApplyHeightCullingThreshold(heightCullThreshold);
    }

    private void OnValidate()
    {
        // OnValidate 中の SetActive はレンダラーへの SendMessage（OnBecameInvisible）警告になるため、
        // 1 フレーム遅延して適用する
        UnityEditor.EditorApplication.delayCall -= ApplyThresholdDeferred;
        UnityEditor.EditorApplication.delayCall += ApplyThresholdDeferred;
    }

    private void ApplyThresholdDeferred()
    {
        if (this == null)
            return; // 破棄済み
        var terrainRenderer = GetComponent<TerrainRenderer>();
        if (terrainRenderer == null)
            return;
        terrainRenderer.ApplyHeightCullingThreshold(heightCullThreshold);
        terrainRenderer.ApplyDitherThreshold(ditherThreshold);
    }

    private static TerrainVoxelStore CreateSampleStore()
    {
        var store = new TerrainVoxelStore();

        void Set(int x, int y, int z, TerrainShape shape, int palette = 0) =>
            store.SetVoxel(x, y, z, TerrainVoxel.Encode(shape, palette));

        // 地面（チャンク境界 x=15/16 をまたぐ）
        for (int x = 4; x <= 28; x++)
            for (int z = 4; z <= 28; z++)
                Set(x, 0, z, TerrainShape.Cube);

        // 一段高い床パッチ（側面領域の確認）
        for (int x = 6; x <= 11; x++)
            for (int z = 6; z <= 11; z++)
                Set(x, 1, z, TerrainShape.Cube);

        // 4 方向の ramp（単独）
        Set(15, 1, 6, TerrainShape.RampN);
        Set(17, 1, 6, TerrainShape.RampE);
        Set(19, 1, 6, TerrainShape.RampS);
        Set(21, 1, 6, TerrainShape.RampW);

        // 4 方向の diag（単独）
        Set(15, 1, 10, TerrainShape.DiagNW);
        Set(17, 1, 10, TerrainShape.DiagNE);
        Set(19, 1, 10, TerrainShape.DiagSE);
        Set(21, 1, 10, TerrainShape.DiagSW);

        // 坂の階段（北向きに昇る・下は cube で充填）
        for (int i = 0; i < 4; i++)
        {
            int z = 14 + i;
            Set(6, 1 + i, z, TerrainShape.RampN);
            for (int y = 1; y <= i; y++)
                Set(6, y, z, TerrainShape.Cube);
        }

        // 2 階建ての建物（x 22..27, z 14..19）
        // 1 階壁 y=1..2 / 天井 y=3 / 2 階壁 y=4..5 / 屋根 y=6。南面 y=1..2 に出入口
        for (int x = 22; x <= 27; x++)
        {
            for (int z = 14; z <= 19; z++)
            {
                bool perimeter = x == 22 || x == 27 || z == 14 || z == 19;
                bool door = z == 14 && (x == 24 || x == 25);
                for (int y = 1; y <= 2; y++)
                    if (perimeter && !door)
                        Set(x, y, z, TerrainShape.Cube);
                Set(x, 3, z, TerrainShape.Cube); // 1 階天井
                for (int y = 4; y <= 5; y++)
                    if (perimeter)
                        Set(x, y, z, TerrainShape.Cube);
                Set(x, 6, z, TerrainShape.Cube); // 屋根
            }
        }
        // 1 階の家具（AO・上面中間の確認用）
        Set(24, 1, 17, TerrainShape.Cube);

        // 浮遊スラブ（下面と AO の確認）
        for (int x = 5; x <= 9; x++)
            for (int z = 22; z <= 26; z++)
                Set(x, 5, z, TerrainShape.Cube);

        // パレット 1（固定テクスチャ）の床と坂
        for (int x = 24; x <= 28; x++)
            for (int z = 24; z <= 28; z++)
                Set(x, 1, z, TerrainShape.Cube, 1);
        Set(26, 2, 26, TerrainShape.RampN, 1);

        return store;
    }
}
#endif
