using NUnit.Framework;

public class GimmickTabLogicTests
{
    // ── ステート定義 ───────────────────────────────────────────────────────────

    [Test]
    public void SanitizeLabel_TrimsAndTruncates()
    {
        Assert.AreEqual("", GimmickTabLogic.SanitizeLabel(null));
        Assert.AreEqual("abc", GimmickTabLogic.SanitizeLabel("  abc  "));
        Assert.AreEqual(20, GimmickTabLogic.SanitizeLabel(new string('あ', 30)).Length);
    }

    [Test]
    public void ClampStateValue_0to255()
    {
        Assert.AreEqual(0, GimmickTabLogic.ClampStateValue(-5));
        Assert.AreEqual(255, GimmickTabLogic.ClampStateValue(999));
        Assert.AreEqual(128, GimmickTabLogic.ClampStateValue(128));
    }

    [Test]
    public void SetWorldState_SanitizesAndClamps()
    {
        var logic = new GimmickTabLogic();
        logic.SetWorldStateLabel(3, "  スコア  ");
        logic.SetWorldStateInitial(3, 300);
        Assert.AreEqual("スコア", logic.GetWorldStateLabel(3));
        Assert.AreEqual(255, logic.GetWorldStateInitial(3));
    }

    [Test]
    public void DefaultState_EmptyLabelZeroValue()
    {
        var logic = new GimmickTabLogic();
        Assert.AreEqual("", logic.GetWorldStateLabel(0));
        Assert.AreEqual(0, logic.GetPlayerStateInitial(0));
        Assert.AreEqual("", logic.GetTimerLabel(0));
    }

    // ── ルール一覧 ─────────────────────────────────────────────────────────────

    [Test]
    public void AddRule_DefaultSequentialName()
    {
        var logic = new GimmickTabLogic();
        var a = logic.AddRule();
        var b = logic.AddRule();
        Assert.AreEqual("ルール1", a.label);
        Assert.AreEqual("ルール2", b.label);
        Assert.AreEqual(2, logic.Rules.Count);
        Assert.AreNotEqual(a.ruleId, b.ruleId);
    }

    [Test]
    public void AddRule_CustomNameSanitized()
    {
        var logic = new GimmickTabLogic();
        var r = logic.AddRule("  得点ルール  ");
        Assert.AreEqual("得点ルール", r.label);
    }

    [Test]
    public void AddRule_NextDefaultNameSkipsExistingMax()
    {
        var logic = new GimmickTabLogic();
        logic.AddRule("ルール5");
        var r = logic.AddRule();
        Assert.AreEqual("ルール6", r.label);
    }

    [Test]
    public void RenameRule_RejectsEmpty()
    {
        var logic = new GimmickTabLogic();
        var r = logic.AddRule();
        Assert.IsFalse(logic.RenameRule(r.ruleId, "   "));
        Assert.AreEqual("ルール1", r.label, "空名は拒否され元のまま");
        Assert.IsTrue(logic.RenameRule(r.ruleId, "新名称"));
        Assert.AreEqual("新名称", r.label);
    }

    [Test]
    public void DeleteRule_RemovesById()
    {
        var logic = new GimmickTabLogic();
        var a = logic.AddRule();
        var b = logic.AddRule();
        Assert.IsTrue(logic.DeleteRule(a.ruleId));
        Assert.AreEqual(1, logic.Rules.Count);
        Assert.AreEqual(b.ruleId, logic.Rules[0].ruleId);
        Assert.IsFalse(logic.DeleteRule("missing"));
    }

    [Test]
    public void MoveRule_ReordersAndClamps()
    {
        var logic = new GimmickTabLogic();
        var a = logic.AddRule();
        var b = logic.AddRule();
        var c = logic.AddRule();
        Assert.IsTrue(logic.MoveRule(c.ruleId, 0));
        Assert.AreEqual(c.ruleId, logic.Rules[0].ruleId);
        Assert.AreEqual(a.ruleId, logic.Rules[1].ruleId);
        // 範囲外はクランプ（末尾へ）
        Assert.IsTrue(logic.MoveRule(c.ruleId, 99));
        Assert.AreEqual(c.ruleId, logic.Rules[2].ruleId);
    }

    [Test]
    public void TotalCount_IncludesGroups_AndLimitEnforced()
    {
        var logic = new GimmickTabLogic();
        var def = new WorldDefinitionJson
        {
            gimmickGroups = new[] { new GroupJson { groupId = "g1", name = "グループ1" } },
        };
        logic.LoadFrom(def);
        Assert.AreEqual(1, logic.TotalCount, "グループも合計に含む");

        // 残り 99 を埋める → 100 で打ち止め
        for (int i = 0; i < 99; i++)
            Assert.IsNotNull(logic.AddRule());
        Assert.AreEqual(100, logic.TotalCount);
        Assert.IsFalse(logic.CanAddRule);
        Assert.IsNull(logic.AddRule(), "100 超は追加不可");
        Assert.AreEqual(100, logic.TotalCount);
    }

    // ── ワールド定義との往復 ───────────────────────────────────────────────────

    [Test]
    public void LoadFrom_PopulatesStatesByIndex()
    {
        var logic = new GimmickTabLogic();
        var def = new WorldDefinitionJson
        {
            worldStates = new[] { new WorldStateData { index = 2, label = "HP", initialValue = 10 } },
            playerStates = new[] { new WorldStateData { index = 1, label = "弾", initialValue = 5 } },
            timers = new[] { new TimerData { index = 3, label = "T" } },
        };
        logic.LoadFrom(def);
        Assert.AreEqual("HP", logic.GetWorldStateLabel(2));
        Assert.AreEqual(10, logic.GetWorldStateInitial(2));
        Assert.AreEqual("弾", logic.GetPlayerStateLabel(1));
        Assert.AreEqual("T", logic.GetTimerLabel(3));
    }

    [Test]
    public void WriteTo_OnlyMeaningfulSlots_RoundTrips()
    {
        var logic = new GimmickTabLogic();
        logic.SetWorldStateLabel(0, "スコア");
        logic.SetWorldStateInitial(5, 7); // ラベル無し・値のみ
        logic.SetTimerLabel(0, "main");
        logic.AddRule("R1");

        var def = new WorldDefinitionJson();
        logic.WriteTo(def);

        // 既定（空ラベル・値0）のスロットは書き出さない
        Assert.AreEqual(2, def.worldStates.Length);
        Assert.AreEqual(0, def.playerStates.Length);
        Assert.AreEqual(1, def.timers.Length);
        Assert.AreEqual(1, def.gimmicks.Length);

        // 往復で同じ状態に戻る
        var logic2 = new GimmickTabLogic();
        logic2.LoadFrom(def);
        Assert.AreEqual("スコア", logic2.GetWorldStateLabel(0));
        Assert.AreEqual(7, logic2.GetWorldStateInitial(5));
        Assert.AreEqual("main", logic2.GetTimerLabel(0));
        Assert.AreEqual("R1", logic2.Rules[0].label);
    }

    [Test]
    public void LoadFrom_Null_ResetsToDefault()
    {
        var logic = new GimmickTabLogic();
        logic.SetWorldStateLabel(0, "x");
        logic.AddRule();
        logic.LoadFrom(null);
        Assert.AreEqual("", logic.GetWorldStateLabel(0));
        Assert.AreEqual(0, logic.Rules.Count);
        Assert.AreEqual(0, logic.TotalCount);
    }
}
