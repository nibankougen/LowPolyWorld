#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ワールドエディタ UI の見た目確認用コンポーネント（エディタ専用）。
/// メニュー「Tools/LowPolyWorld/ワールドエディタ UI プレビューを生成」から生成し、
/// Play モードに入ると地形一覧へダミーデータを流し込む。
/// </summary>
public class WorldEditorUiPreview : MonoBehaviour
{
    private void Start()
    {
        var controller = GetComponent<WorldEditorController>();
        if (controller == null || controller.TerrainTab == null)
            return;

        controller.TerrainTab.SetTerrainList(new List<TerrainTabController.TerrainListItem>
        {
            new TerrainTabController.TerrainListItem("草地", false),
            new TerrainTabController.TerrainListItem("岩", false),
            new TerrainTabController.TerrainListItem("ガラス", true),
            new TerrainTabController.TerrainListItem("砂", false),
            new TerrainTabController.TerrainListItem("レンガ", false),
            new TerrainTabController.TerrainListItem("木板", false),
        });
        // 選択ありの状態のコピー / ペーストボタン表示も確認できるようにする
        controller.TerrainTab.SetSelectionState(true, true);
    }
}
#endif
