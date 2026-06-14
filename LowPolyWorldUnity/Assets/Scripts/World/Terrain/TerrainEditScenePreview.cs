#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 地形タブの統合プレビュー（エディタ専用・Play モードで動作）。
/// ワールドエディタ UI + 地形レンダラー + 編集シーンコントローラー + カメラを配線し、
/// 実際にタッチ / クリックで地形を編集できる状態にする。
/// メニュー「Tools/LowPolyWorld/地形タブ統合プレビューを生成」から生成する。
/// </summary>
[RequireComponent(typeof(TerrainEditSceneController))]
public class TerrainEditScenePreview : MonoBehaviour
{
    [Tooltip("ワールドエディタ UI の GameObject（WorldEditorController 付き）")]
    public WorldEditorController editorController;

    [Tooltip("編集ビュー用カメラ")]
    public Camera editCamera;

    private void Start()
    {
        if (editorController == null || editorController.TerrainTab == null)
        {
            Debug.LogError("TerrainEditScenePreview: WorldEditorController が見つかりません。");
            return;
        }

        // デバッグアトラスは 2 パレット（ランダム / 固定）なので一覧も 2 件にする
        editorController.TerrainTab.SetTerrainList(new List<TerrainTabController.TerrainListItem>
        {
            new TerrainTabController.TerrainListItem("草地", false),
            new TerrainTabController.TerrainListItem("石（固定）", false),
        });

        // サンプル地形: 全面の地面（高さ 0）
        var store = new TerrainVoxelStore();
        ushort ground = TerrainVoxel.Encode(TerrainShape.Cube, 0);
        for (int x = 0; x < TerrainVoxelStore.SizeX; x++)
            for (int z = 0; z < TerrainVoxelStore.SizeZ; z++)
                store.SetVoxel(x, 0, z, ground);

        var doc = editorController.GetComponent<UIDocument>();
        GetComponent<TerrainEditSceneController>().Initialize(
            editorController.TerrainTab,
            doc.rootVisualElement,
            editCamera,
            store,
            TerrainDebugAtlas.CreateAtlasMap(),
            TerrainDebugAtlas.CreateTexture());

        // 地形タブを開いた状態で開始
        var switchTab = typeof(WorldEditorController).GetMethod(
            "SwitchTab",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        switchTab?.Invoke(editorController, new object[] { 0 });

        Debug.Log("地形タブ統合プレビュー: ブラシで地面をなぞって編集できます（高さバー ▲ で上の層へ）。");
    }
}
#endif
