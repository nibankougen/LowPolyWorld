using System.Linq;
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
        int i = logic.AddWorldState();
        logic.SetWorldStateLabel(i, "  スコア  ");
        logic.SetWorldStateInitial(i, 300);
        Assert.AreEqual("スコア", logic.GetWorldStateLabel(i));
        Assert.AreEqual(255, logic.GetWorldStateInitial(i));
    }

    [Test]
    public void NewLogic_HasNoStates()
    {
        var logic = new GimmickTabLogic();
        Assert.AreEqual(0, logic.WorldStateCount);
        Assert.AreEqual(0, logic.PlayerStateCount);
        Assert.AreEqual(0, logic.TimerCount);
        Assert.AreEqual(0, logic.WorldStateIndices.Count);
    }

    // ── ステートの追加 / 削除 ───────────────────────────────────────────────────

    [Test]
    public void AddState_AssignsLowestFreeIndex()
    {
        var logic = new GimmickTabLogic();
        Assert.AreEqual(0, logic.AddWorldState("A"));
        Assert.AreEqual(1, logic.AddWorldState("B"));
        Assert.AreEqual(2, logic.AddWorldState("C"));
        CollectionAssert.AreEqual(new[] { 0, 1, 2 }, logic.WorldStateIndices);
        Assert.AreEqual(3, logic.WorldStateCount);
    }

    [Test]
    public void RemoveState_FreesIndexForReuse_KeepsOthersStable()
    {
        var logic = new GimmickTabLogic();
        logic.AddWorldState("A"); // 0
        logic.AddWorldState("B"); // 1
        logic.AddWorldState("C"); // 2

        Assert.IsTrue(logic.RemoveWorldState(1));
        CollectionAssert.AreEqual(new[] { 0, 2 }, logic.WorldStateIndices, "削除しても他の番号は詰めない");
        Assert.AreEqual("C", logic.GetWorldStateLabel(2));

        // 追加は空いた最小番号（1）を再利用する
        Assert.AreEqual(1, logic.AddWorldState("D"));
        Assert.AreEqual("D", logic.GetWorldStateLabel(1));
    }

    [Test]
    public void RemoveState_UndefinedReturnsFalse()
    {
        var logic = new GimmickTabLogic();
        Assert.IsFalse(logic.RemoveWorldState(0));
        Assert.IsFalse(logic.RemoveTimer(2));
    }

    [Test]
    public void AddState_FailsWhenFull()
    {
        var logic = new GimmickTabLogic();
        for (int i = 0; i < GimmickTabLogic.MaxPlayerStates; i++)
            Assert.GreaterOrEqual(logic.AddPlayerState(), 0);
        Assert.IsFalse(logic.CanAddPlayerState);
        Assert.AreEqual(-1, logic.AddPlayerState(), "満杯時は -1");
        Assert.AreEqual(GimmickTabLogic.MaxPlayerStates, logic.PlayerStateCount);
    }

    [Test]
    public void SetOnUndefinedIndex_IsIgnored()
    {
        var logic = new GimmickTabLogic();
        logic.SetWorldStateLabel(0, "x"); // 未定義 → 無視
        Assert.AreEqual(0, logic.WorldStateCount);
        Assert.AreEqual("", logic.GetWorldStateLabel(0));
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

    // ── グループ操作 ──────────────────────────────────────────────────────────

    [Test]
    public void CreateGroup_DefaultSequentialName_AndCountsTowardTotal()
    {
        var logic = new GimmickTabLogic();
        var g1 = logic.CreateGroup();
        var g2 = logic.CreateGroup();
        Assert.IsNotNull(g1);
        Assert.IsNotNull(g2);
        Assert.AreEqual("グループ1", logic.Groups[0].name);
        Assert.AreEqual("グループ2", logic.Groups[1].name);
        Assert.AreEqual(2, logic.TotalCount, "グループは合計に含む");
        Assert.AreNotEqual(g1, g2);
    }

    [Test]
    public void CreateGroup_CustomNameSanitized()
    {
        var logic = new GimmickTabLogic();
        var g = logic.CreateGroup(name: "  チーム戦  ");
        Assert.AreEqual("チーム戦", logic.Groups[0].name);
    }

    [Test]
    public void CreateGroup_FailsWhenTotalLimitReached()
    {
        var logic = new GimmickTabLogic();
        for (int i = 0; i < GimmickTabLogic.MaxRulesAndGroups; i++)
            Assert.IsNotNull(logic.AddRule());
        Assert.IsFalse(logic.CanCreateGroup);
        Assert.IsNull(logic.CreateGroup(), "ルール + グループ合計 100 超は作成不可");
    }

    [Test]
    public void CreateGroup_RejectsUnknownParent()
    {
        var logic = new GimmickTabLogic();
        Assert.IsNull(logic.CreateGroup("missing"));
    }

    [Test]
    public void CreateGroup_EnforcesMaxNestDepth()
    {
        var logic = new GimmickTabLogic();
        var g1 = logic.CreateGroup();         // 深さ 1
        var g2 = logic.CreateGroup(g1);       // 深さ 2
        var g3 = logic.CreateGroup(g2);       // 深さ 3
        var g4 = logic.CreateGroup(g3);       // 深さ 4
        Assert.IsNotNull(g4);
        Assert.AreEqual(4, logic.GroupDepth(g4));
        Assert.IsNull(logic.CreateGroup(g4), "5 段目は不可");
    }

    [Test]
    public void RenameGroup_RejectsEmpty()
    {
        var logic = new GimmickTabLogic();
        var g = logic.CreateGroup();
        Assert.IsFalse(logic.RenameGroup(g, "   "));
        Assert.AreEqual("グループ1", logic.Groups[0].name, "空名は拒否され元のまま");
        Assert.IsTrue(logic.RenameGroup(g, "新グループ"));
        Assert.AreEqual("新グループ", logic.Groups[0].name);
        Assert.IsFalse(logic.RenameGroup("missing", "x"));
    }

    [Test]
    public void SetRuleGroup_MovesInAndOut()
    {
        var logic = new GimmickTabLogic();
        var g = logic.CreateGroup();
        var r = logic.AddRule();
        Assert.AreEqual("", r.groupId, "新規ルールは既定でルート直下");

        Assert.IsTrue(logic.SetRuleGroup(r.ruleId, g));
        Assert.AreEqual(g, r.groupId);

        Assert.IsTrue(logic.SetRuleGroup(r.ruleId, ""), "ルート直下へ戻す");
        Assert.AreEqual("", r.groupId);

        Assert.IsFalse(logic.SetRuleGroup(r.ruleId, "missing"), "存在しないグループへは移動不可");
        Assert.IsFalse(logic.SetRuleGroup("missing", g));
    }

    [Test]
    public void AddRule_IntoGroup()
    {
        var logic = new GimmickTabLogic();
        var g = logic.CreateGroup();
        var r = logic.AddRule("R", g);
        Assert.AreEqual(g, r.groupId);
        // 存在しないグループ ID はルート扱い
        var r2 = logic.AddRule("R2", "missing");
        Assert.AreEqual("", r2.groupId);
    }

    [Test]
    public void DeleteGroup_PromotesChildRulesAndGroupsToParent()
    {
        var logic = new GimmickTabLogic();
        var parent = logic.CreateGroup();        // 深さ 1
        var child = logic.CreateGroup(parent);   // 深さ 2
        var grandchild = logic.CreateGroup(child); // 深さ 3
        var rule = logic.AddRule("R", child);

        Assert.IsTrue(logic.DeleteGroup(child));
        // 子ルールは親（parent）へ繰り上げ
        Assert.AreEqual(parent, rule.groupId);
        // 子グループ（grandchild）も親（parent）へ繰り上げ
        Assert.AreEqual(parent, logic.Groups.FirstOrDefault(g => g.groupId ==grandchild)?.parentGroupId);
        Assert.IsFalse(logic.DeleteGroup("missing"));
    }

    [Test]
    public void DeleteGroup_AtRoot_PromotesChildrenToRoot()
    {
        var logic = new GimmickTabLogic();
        var top = logic.CreateGroup();
        var sub = logic.CreateGroup(top);
        var rule = logic.AddRule("R", top);

        Assert.IsTrue(logic.DeleteGroup(top));
        Assert.AreEqual("", rule.groupId);
        Assert.AreEqual("", logic.Groups.FirstOrDefault(g => g.groupId ==sub)?.parentGroupId);
    }

    [Test]
    public void SetGroupParent_RejectsCycleAndSelf()
    {
        var logic = new GimmickTabLogic();
        var a = logic.CreateGroup();
        var b = logic.CreateGroup(a);
        Assert.IsFalse(logic.SetGroupParent(a, a), "自己への移動は不可");
        Assert.IsFalse(logic.SetGroupParent(a, b), "自身の子孫への移動は不可（循環）");
        Assert.IsTrue(logic.SetGroupParent(b, ""), "ルートへ移動は可");
        Assert.AreEqual("", logic.Groups.FirstOrDefault(g => g.groupId ==b)?.parentGroupId);
    }

    [Test]
    public void SetGroupParent_RejectsWhenResultingDepthExceedsMax()
    {
        var logic = new GimmickTabLogic();
        // a(1) > b(2) > c(3) のチェーンと、別ルートの d(1) > e(2)
        var a = logic.CreateGroup();
        var b = logic.CreateGroup(a);
        var c = logic.CreateGroup(b);
        var d = logic.CreateGroup();
        var e = logic.CreateGroup(d);

        // d のサブツリー高さ 2 を c(深さ 3) の下へ → 3 + 2 = 5 > 4 で不可
        Assert.IsFalse(logic.SetGroupParent(d, c));
        // b のサブツリー（b,c 高さ 2）を e(深さ 2) の下へ → 2 + 2 = 4 で可
        Assert.IsTrue(logic.SetGroupParent(b, e));
        Assert.AreEqual(4, logic.GroupDepth(c));
    }

    // ── D&D 用の位置指定移動 ───────────────────────────────────────────────────

    [Test]
    public void MoveRuleBefore_MovesDown_AndReparents()
    {
        var logic = new GimmickTabLogic();
        var g = logic.CreateGroup();
        var a = logic.AddRule("A");
        var b = logic.AddRule("B");
        var c = logic.AddRule("C");

        // A を C の直前へ（下方向）→ 順序 B, A, C・A はルート維持
        Assert.IsTrue(logic.MoveRuleBefore(a.ruleId, c.ruleId));
        CollectionAssert.AreEqual(
            new[] { b.ruleId, a.ruleId, c.ruleId },
            new[] { logic.Rules[0].ruleId, logic.Rules[1].ruleId, logic.Rules[2].ruleId });

        // グループ内のルール（D を作って）→ A を D の直前に置くと A も同グループになる
        logic.SetRuleGroup(b.ruleId, g);
        Assert.IsTrue(logic.MoveRuleBefore(a.ruleId, b.ruleId));
        Assert.AreEqual(g, a.groupId, "anchor のコンテナへ吸い込まれる");
    }

    [Test]
    public void MoveRuleBefore_MovesUp()
    {
        var logic = new GimmickTabLogic();
        var a = logic.AddRule("A");
        var b = logic.AddRule("B");
        var c = logic.AddRule("C");
        // C を A の直前へ（上方向）→ C, A, B
        Assert.IsTrue(logic.MoveRuleBefore(c.ruleId, a.ruleId));
        CollectionAssert.AreEqual(
            new[] { c.ruleId, a.ruleId, b.ruleId },
            new[] { logic.Rules[0].ruleId, logic.Rules[1].ruleId, logic.Rules[2].ruleId });
    }

    [Test]
    public void MoveRuleToContainerEnd_IntoGroup_AndBackToRoot()
    {
        var logic = new GimmickTabLogic();
        var g = logic.CreateGroup();
        var a = logic.AddRule("A");
        logic.AddRule("B", g); // グループ内に既存兄弟

        Assert.IsTrue(logic.MoveRuleToContainerEnd(a.ruleId, g));
        Assert.AreEqual(g, a.groupId);
        // グループ末尾 → 配列上 B の直後（= 最後）
        Assert.AreEqual(a.ruleId, logic.Rules[logic.Rules.Count - 1].ruleId);

        Assert.IsTrue(logic.MoveRuleToContainerEnd(a.ruleId, ""), "ルートへ戻す");
        Assert.AreEqual("", a.groupId);
        Assert.IsFalse(logic.MoveRuleToContainerEnd(a.ruleId, "missing"));
    }

    [Test]
    public void MoveGroupBefore_ReordersSiblings()
    {
        var logic = new GimmickTabLogic();
        var g1 = logic.CreateGroup(name: "1");
        var g2 = logic.CreateGroup(name: "2");
        var g3 = logic.CreateGroup(name: "3");
        // g3 を g1 の直前へ → 表示順（_groups 順）3,1,2
        Assert.IsTrue(logic.MoveGroupBefore(g3, g1));
        CollectionAssert.AreEqual(
            new[] { g3, g1, g2 },
            new[] { logic.Groups[0].groupId, logic.Groups[1].groupId, logic.Groups[2].groupId });
        // sortOrder も振り直される
        Assert.AreEqual(0, logic.Groups[0].sortOrder);
        Assert.AreEqual(1, logic.Groups[1].sortOrder);
        Assert.AreEqual(2, logic.Groups[2].sortOrder);
    }

    [Test]
    public void MoveGroupToParentEnd_Reparents_RejectsCycle()
    {
        var logic = new GimmickTabLogic();
        var a = logic.CreateGroup();
        var b = logic.CreateGroup();
        // b を a の中へ
        Assert.IsTrue(logic.MoveGroupToParentEnd(b, a));
        Assert.AreEqual(a, logic.Groups.FirstOrDefault(g => g.groupId == b)?.parentGroupId);
        // a を自身の子孫 b の中へ → 循環で不可
        Assert.IsFalse(logic.MoveGroupToParentEnd(a, b));
    }

    [Test]
    public void Groups_RoundTripThroughWorldDefinition()
    {
        var logic = new GimmickTabLogic();
        var parent = logic.CreateGroup(name: "親");
        var child = logic.CreateGroup(parent, "子");
        var rule = logic.AddRule("R", child);

        var def = new WorldDefinitionJson();
        logic.WriteTo(def);
        Assert.AreEqual(2, def.gimmickGroups.Length);
        Assert.AreEqual(1, def.gimmicks.Length);

        var logic2 = new GimmickTabLogic();
        logic2.LoadFrom(def);
        Assert.AreEqual(2, logic2.GroupCount);
        Assert.AreEqual(child, logic2.Rules[0].groupId);
        Assert.AreEqual(2, logic2.GroupDepth(child));
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
    public void WriteTo_WritesAllDefinedStates_IncludingEmpty_RoundTrips()
    {
        var logic = new GimmickTabLogic();
        logic.AddWorldState("スコア"); // 0
        int w1 = logic.AddWorldState("", 7); // 1: ラベル無し・値あり
        logic.AddWorldState(); // 2: ラベル空・値0 でも定義済みなら保持する
        logic.AddTimer("main"); // timer 0
        logic.AddRule("R1");

        var def = new WorldDefinitionJson();
        logic.WriteTo(def);

        // 定義済みは空ラベル・値0 でもすべて書き出す
        Assert.AreEqual(3, def.worldStates.Length);
        Assert.AreEqual(0, def.playerStates.Length);
        Assert.AreEqual(1, def.timers.Length);
        Assert.AreEqual(1, def.gimmicks.Length);

        // 往復で同じ状態に戻る
        var logic2 = new GimmickTabLogic();
        logic2.LoadFrom(def);
        Assert.AreEqual(3, logic2.WorldStateCount);
        Assert.AreEqual("スコア", logic2.GetWorldStateLabel(0));
        Assert.AreEqual(7, logic2.GetWorldStateInitial(w1));
        Assert.IsTrue(logic2.IsWorldStateDefined(2));
        Assert.AreEqual("main", logic2.GetTimerLabel(0));
        Assert.AreEqual("R1", logic2.Rules[0].label);
    }

    [Test]
    public void LoadFrom_Null_ResetsToDefault()
    {
        var logic = new GimmickTabLogic();
        logic.AddWorldState("x");
        logic.AddRule();
        logic.LoadFrom(null);
        Assert.AreEqual(0, logic.WorldStateCount);
        Assert.AreEqual("", logic.GetWorldStateLabel(0));
        Assert.AreEqual(0, logic.Rules.Count);
        Assert.AreEqual(0, logic.TotalCount);
    }
}
