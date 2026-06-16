using System.Collections.Generic;
using NUnit.Framework;

public class GimmickTemplateLogicTests
{
    // ── 一覧 / 取得 ─────────────────────────────────────────────────────────────

    [Test]
    public void All_ContainsSixBuiltinTemplates()
    {
        var ids = new List<string>();
        foreach (var t in GimmickTemplateLogic.All)
            ids.Add(t.Id);

        Assert.AreEqual(6, ids.Count);
        CollectionAssert.AreEquivalent(
            new[] { "twoTeams", "tagBasic", "countdown", "periodic", "comboLock", "raceTiming" },
            ids
        );
    }

    [Test]
    public void Get_KnownAndUnknown()
    {
        Assert.IsNotNull(GimmickTemplateLogic.Get("countdown"));
        Assert.AreEqual("countdown", GimmickTemplateLogic.Get("countdown").Id);
        Assert.IsNull(GimmickTemplateLogic.Get("nope"));
        Assert.IsNull(GimmickTemplateLogic.Get(null));
    }

    [Test]
    public void Insert_UnknownTemplate_Fails()
    {
        var tab = new GimmickTabLogic();
        var result = GimmickTemplateLogic.Insert(tab, "nope");

        Assert.IsFalse(result.Success);
        Assert.IsNotNull(result.Error);
        Assert.AreEqual(0, result.Rules.Count);
        Assert.AreEqual(0, tab.TotalCount);
    }

    // ── 基本挿入 ────────────────────────────────────────────────────────────────

    [Test]
    public void Insert_TwoTeams_AddsTwoRulesAndNamesPlayerState()
    {
        var tab = new GimmickTabLogic();
        var result = GimmickTemplateLogic.Insert(tab, "twoTeams");

        Assert.IsTrue(result.Success);
        Assert.AreEqual(2, result.Rules.Count);
        Assert.AreEqual(2, tab.Rules.Count);
        // プレイヤーステートに「チーム」ラベルが自動設定される
        Assert.AreEqual("チーム", tab.GetPlayerStateLabel(0));
        // ルールは playerNumber の偶奇（mod 2）で振り分ける
        Assert.AreEqual("チームA振り分け", result.Rules[0].label);
        Assert.AreEqual("mod_eq", result.Rules[0].conditions[0].op);
        Assert.AreEqual("playerNumber", result.Rules[0].conditions[0].type);
        Assert.AreEqual(0, result.Rules[0].conditions[0].modResult);
        Assert.AreEqual(1, result.Rules[1].conditions[0].modResult);
    }

    [Test]
    public void Insert_TagBasic_AllocatesWorldStateAndUsesPlayerCountRandom()
    {
        var tab = new GimmickTabLogic();
        var result = GimmickTemplateLogic.Insert(tab, "tagBasic");

        Assert.IsTrue(result.Success);
        Assert.AreEqual(3, result.Rules.Count);
        Assert.AreEqual("鬼番号", tab.GetWorldStateLabel(0));

        var selectOni = result.Rules[0];
        Assert.AreEqual("setWorldState", selectOni.actions[0].type);
        Assert.AreEqual("random", selectOni.actions[0].value.kind);
        Assert.IsTrue(selectOni.actions[0].value.maxIsPlayerCount);
    }

    [Test]
    public void Insert_Countdown_UsesDefaultSecondsOnTimerTrigger()
    {
        var tab = new GimmickTabLogic();
        var result = GimmickTemplateLogic.Insert(tab, "countdown");

        Assert.IsTrue(result.Success);
        Assert.AreEqual("カウントダウン", tab.GetTimerLabel(0));
        // 終了処理ルールの timerReached トリガーに既定秒数（60）が入る
        var endRule = result.Rules[1];
        Assert.AreEqual("timerReached", endRule.triggers[0].type);
        Assert.AreEqual(60f, endRule.triggers[0].timerSeconds);
    }

    [Test]
    public void Insert_Countdown_ClampsSecondsParam()
    {
        var tab = new GimmickTabLogic();
        var result = GimmickTemplateLogic.Insert(
            tab,
            "countdown",
            new Dictionary<string, int> { { "seconds", 99999 } }
        );

        Assert.IsTrue(result.Success);
        Assert.AreEqual(3600f, result.Rules[1].triggers[0].timerSeconds); // 上限 3600 にクランプ
    }

    // ── 空きスロットの自動割り当て ──────────────────────────────────────────────

    [Test]
    public void Insert_AllocatesFreeSlot_SkippingUsedOnes()
    {
        var tab = new GimmickTabLogic();
        // ワールドステート 0・1 を使用済みにする
        tab.SetWorldStateLabel(0, "既存A");
        tab.SetWorldStateInitial(1, 5);

        var result = GimmickTemplateLogic.Insert(tab, "tagBasic");

        Assert.IsTrue(result.Success);
        // 空いている最初のスロット（2）が割り当てられる
        Assert.AreEqual("鬼番号", tab.GetWorldStateLabel(2));
        Assert.AreEqual("既存A", tab.GetWorldStateLabel(0));
        Assert.AreEqual(2, result.Rules[0].actions[0].stateIndex);
    }

    [Test]
    public void Insert_TwoInsertions_AllocateDistinctSlots()
    {
        var tab = new GimmickTabLogic();
        GimmickTemplateLogic.Insert(tab, "tagBasic"); // ワールドステート 0
        GimmickTemplateLogic.Insert(tab, "comboLock"); // ワールドステート 1

        Assert.AreEqual("鬼番号", tab.GetWorldStateLabel(0));
        Assert.AreEqual("入力進捗", tab.GetWorldStateLabel(1));
    }

    // ── 容量不足時の原子的な失敗 ────────────────────────────────────────────────

    [Test]
    public void Insert_FailsWhenNoFreeWorldState_NoMutation()
    {
        var tab = new GimmickTabLogic();
        for (int i = 0; i < GimmickTabLogic.MaxWorldStates; i++)
            tab.SetWorldStateLabel(i, "使用中");

        var result = GimmickTemplateLogic.Insert(tab, "tagBasic");

        Assert.IsFalse(result.Success);
        Assert.AreEqual("ステートの空きが足りません", result.Error);
        Assert.AreEqual(0, tab.Rules.Count); // ルールは追加されない
    }

    [Test]
    public void Insert_FailsWhenNoFreeTimer_NoMutation()
    {
        var tab = new GimmickTabLogic();
        for (int i = 0; i < GimmickTabLogic.MaxTimers; i++)
            tab.SetTimerLabel(i, "使用中");

        var result = GimmickTemplateLogic.Insert(tab, "countdown");

        Assert.IsFalse(result.Success);
        Assert.AreEqual("タイマーの空きが足りません", result.Error);
        Assert.AreEqual(0, tab.Rules.Count);
    }

    [Test]
    public void Insert_FailsWhenRuleCapacityExceeded_NoMutation()
    {
        var tab = new GimmickTabLogic();
        for (int i = 0; i < 99; i++)
            tab.AddRule();
        Assert.AreEqual(99, tab.TotalCount);

        // tagBasic は 3 ルール → 99 + 3 = 102 > 100
        var result = GimmickTemplateLogic.Insert(tab, "tagBasic");

        Assert.IsFalse(result.Success);
        Assert.AreEqual("ルール数が上限（100）を超えます", result.Error);
        Assert.AreEqual(99, tab.TotalCount); // 追加されていない
        Assert.AreEqual("", tab.GetWorldStateLabel(0)); // ステートも触られていない
    }

    [Test]
    public void Insert_AtTheLimit_Succeeds()
    {
        var tab = new GimmickTabLogic();
        for (int i = 0; i < 97; i++)
            tab.AddRule();

        // 97 + 3 = 100（ちょうど上限）
        var result = GimmickTemplateLogic.Insert(tab, "tagBasic");

        Assert.IsTrue(result.Success);
        Assert.AreEqual(100, tab.TotalCount);
    }

    // ── 生成ルールの妥当性（対象 ID 不要なテンプレートはそのまま有効）─────────────

    [Test]
    public void Insert_Periodic_ProducesConverterValidRules()
    {
        var tab = new GimmickTabLogic();
        GimmickTemplateLogic.Insert(tab, "periodic");

        var def = new WorldDefinitionJson();
        tab.WriteTo(def);
        var result = GimmickRuleConverter.Convert(def.gimmicks);

        Assert.AreEqual(0, result.InvalidRules.Count, "周期処理テンプレートは対象 ID 不要で完全に有効なはず");
        Assert.AreEqual(2, result.Rules.Count);
    }

    [Test]
    public void Insert_RaceTiming_ProducesConverterValidRules()
    {
        var tab = new GimmickTabLogic();
        GimmickTemplateLogic.Insert(tab, "raceTiming");

        var def = new WorldDefinitionJson();
        tab.WriteTo(def);
        var result = GimmickRuleConverter.Convert(def.gimmicks);

        Assert.AreEqual(0, result.InvalidRules.Count);
        Assert.AreEqual(2, result.Rules.Count);
        Assert.AreEqual("着順", tab.GetWorldStateLabel(0));
        Assert.AreEqual("タイム", tab.GetTimerLabel(0));
    }

    [Test]
    public void Insert_ComboLock_RecordsProgressAcrossSteps()
    {
        var tab = new GimmickTabLogic();
        var result = GimmickTemplateLogic.Insert(tab, "comboLock");

        Assert.IsTrue(result.Success);
        Assert.AreEqual(4, result.Rules.Count);
        Assert.AreEqual("入力進捗", tab.GetWorldStateLabel(0));
        // 各ステップの条件は進捗ステートの値を比較する
        Assert.AreEqual("worldState", result.Rules[0].conditions[0].type);
        Assert.AreEqual(0, result.Rules[0].conditions[0].threshold.value);
        Assert.AreEqual(1, result.Rules[1].conditions[0].threshold.value);
        Assert.AreEqual(2, result.Rules[2].conditions[0].threshold.value);
        // 解錠ルールは扉を非表示にする
        Assert.AreEqual("showHideObject", result.Rules[2].actions[0].type);
        Assert.IsFalse(result.Rules[2].actions[0].visible);
    }
}
