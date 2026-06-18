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

        SeedGimmickTab(controller);
    }

    // ギミックタブの見た目確認用ダミーデータ（ステート名・初期値・ルール数件）
    private static void SeedGimmickTab(WorldEditorController controller)
    {
        if (controller.GimmickTab == null)
            return;

        var def = new WorldDefinitionJson
        {
            worldStates = new[]
            {
                new WorldStateData { index = 0, label = "スコア", initialValue = 0 },
                new WorldStateData { index = 1, label = "残り時間", initialValue = 60 },
            },
            playerStates = new[]
            {
                new WorldStateData { index = 0, label = "ライフ", initialValue = 3 },
            },
            timers = new[]
            {
                new TimerData { index = 0, label = "メインタイマー" },
            },
            // グループツリーの表示確認用: g1（チーム戦）の下に g2（攻撃側）をネストし、
            // ルールをそれぞれのグループ / ルートに配置する。
            gimmickGroups = new[]
            {
                new GroupJson { groupId = "g1", name = "チーム戦", parentGroupId = "" },
                new GroupJson { groupId = "g2", name = "攻撃側", parentGroupId = "g1" },
            },
            gimmicks = new[]
            {
                new GimmickRule { ruleId = "r1", label = "開始時に得点リセット" },
                // r2 は入力イベント / 条件 / アクションを事前投入し、ルール編集画面の
                // 表示確認をしやすくする（「編集」ボタンで開ける）
                new GimmickRule
                {
                    ruleId = "r2",
                    label = "ゴール接触で加点",
                    groupId = "g1",
                    triggers = new[]
                    {
                        new GimmickTrigger { type = "playerTouchObject" },
                    },
                    conditions = new[]
                    {
                        new GimmickCondition { type = "worldState" },
                    },
                    actions = new[]
                    {
                        new GimmickAction { type = "setWorldState" },
                        new GimmickAction
                        {
                            type = "showMessage",
                            texts = new[] { new GimmickTextJson { lang = "", text = "ゴール！" } },
                        },
                    },
                },
                new GimmickRule { ruleId = "r3", label = "突撃する", groupId = "g2" },
                new GimmickRule { ruleId = "r4", label = "ルート直下のルール" },
            },
        };
        controller.GimmickTab.Logic.LoadFrom(def);
        controller.GimmickTab.Refresh();
    }
}
#endif
