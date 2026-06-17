using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// 公開前の内部テストプレイによる無限ループ検出（9.10 / 11.7.6）のテスト。
/// ルールは JSON → GimmickRuleConverter で構築し、実パイプラインを通す。
/// </summary>
public class GimmickLoopPrecheckTests
{
    private static IReadOnlyList<RuntimeGimmickRule> Convert(params GimmickRule[] rules)
    {
        var result = GimmickRuleConverter.Convert(rules);
        Assert.AreEqual(0, result.InvalidRules.Count, "テスト用ルールは妥当なはず");
        return result.Rules;
    }

    private static GimmickRule Rule(string id, GimmickTrigger[] triggers, params GimmickAction[] actions) =>
        new GimmickRule { ruleId = id, label = id, triggers = triggers, actions = actions };

    private static GimmickTrigger Trig(string type, string targetId = "") =>
        new GimmickTrigger { type = type, targetId = targetId };

    private static GimmickAction SetWorld(int index, int value) =>
        new GimmickAction
        {
            type = "setWorldState", stateIndex = index, stateOp = "set",
            value = new GimmickValueJson { kind = "fixed", value = value },
        };

    private static GimmickAction Call(string subId) =>
        new GimmickAction { type = "callSubroutine", targetId = subId };

    // ── ループ検出 ────────────────────────────────────────────────────────────

    [Test]
    public void RoomStartSubroutineRecursion_DetectedWithRuleId()
    {
        // roomStart で起動し、自分自身（called "loop"）を呼び続ける → 入室直後に無限ループ。
        var rules = Convert(
            Rule("loop", new[] { Trig("roomStart"), Trig("called", "loop") }, Call("loop")));

        var result = GimmickLoopPrecheck.RunRoomStart(rules);

        Assert.IsTrue(result.HasLoop);
        Assert.AreEqual("loop", result.LoopRuleId);
    }

    [Test]
    public void TwoRulesPingPongSubroutine_DetectedAsLoop()
    {
        // a を roomStart 起動 → b を呼ぶ → b は a を呼ぶ … の相互再帰。
        var rules = Convert(
            Rule("a", new[] { Trig("roomStart"), Trig("called", "a") }, Call("b")),
            Rule("b", new[] { Trig("called", "b") }, Call("a")));

        var result = GimmickLoopPrecheck.RunRoomStart(rules);

        Assert.IsTrue(result.HasLoop);
        Assert.IsNotEmpty(result.LoopRuleId);
    }

    // ── 正常（ループなし）────────────────────────────────────────────────────

    [Test]
    public void SimpleRoomStartRule_NoLoop()
    {
        var rules = Convert(Rule("r1", new[] { Trig("roomStart") }, SetWorld(0, 1)));

        var result = GimmickLoopPrecheck.RunRoomStart(rules);

        Assert.IsFalse(result.HasLoop);
        Assert.AreEqual("", result.LoopRuleId);
    }

    [Test]
    public void RuleNotTriggeredByRoomStart_NoLoop()
    {
        // タップ起点のサブルーチン再帰は roomStart シミュレーションでは発火しない（ライブ検出側の領分）。
        var rules = Convert(
            Rule("loop", new[] { Trig("objectTap"), Trig("called", "loop") }, Call("loop")));

        var result = GimmickLoopPrecheck.RunRoomStart(rules);

        Assert.IsFalse(result.HasLoop);
    }

    [Test]
    public void EmptyOrNullRules_NoLoop()
    {
        Assert.IsFalse(GimmickLoopPrecheck.RunRoomStart(null).HasLoop);
        Assert.IsFalse(GimmickLoopPrecheck.RunRoomStart(new List<RuntimeGimmickRule>()).HasLoop);
        Assert.AreSame(GimmickLoopPrecheck.Result.None, GimmickLoopPrecheck.RunRoomStart(null));
    }

    [Test]
    public void WorldInitials_PassedToSimulation()
    {
        // 初期値で条件が成立し loop が発火するケース: ws0 初期値 1 のとき roomStart で再帰。
        var loopRule = new GimmickRule
        {
            ruleId = "loop",
            label = "loop",
            triggers = new[] { Trig("roomStart"), Trig("called", "loop") },
            conditions = new[]
            {
                new GimmickCondition
                {
                    type = "worldState", stateIndex = 0, op = "eq",
                    threshold = new GimmickValueJson { kind = "fixed", value = 1 },
                },
            },
            actions = new[] { Call("loop") },
        };
        var rules = Convert(loopRule);

        // 初期値 0 → 条件不成立 → ループしない。
        Assert.IsFalse(GimmickLoopPrecheck.RunRoomStart(rules, worldInitials: new[] { 0 }).HasLoop);
        // 初期値 1 → 条件成立 → ループ検出。
        Assert.IsTrue(GimmickLoopPrecheck.RunRoomStart(rules, worldInitials: new[] { 1 }).HasLoop);
    }
}
