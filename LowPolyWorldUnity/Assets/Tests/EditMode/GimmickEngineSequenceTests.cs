using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// 表現力拡張（9.7b 待機 / 9.8 サブルーチン / 9.13 会話）のエンジン実行テスト。
/// ルールは JSON → GimmickRuleConverter で構築し、実パイプラインを通す。
/// </summary>
public class GimmickEngineSequenceTests
{
    private GimmickStateManager _state;
    private GimmickTimerLogic _timers;
    private List<string> _players;

    [SetUp]
    public void SetUp()
    {
        _state = new GimmickStateManager();
        _timers = new GimmickTimerLogic();
        _players = new List<string> { "p1", "p2" };
    }

    private GimmickEngine Build(params GimmickRule[] rules)
    {
        var refs = new GimmickRuleConverter.WorldRefs
        {
            ObjectInstanceIds = new HashSet<string> { "btn" },
            ConversationIds = new HashSet<string> { "conv1" },
        };
        var converted = GimmickRuleConverter.Convert(rules, refs);
        Assert.AreEqual(0, converted.InvalidRules.Count, "テスト用ルールは妥当なはず");
        return new GimmickEngine(converted.Rules, _state, _timers, _players, (min, max) => min);
    }

    // ── ヘルパー: JSON ルール構築 ───────────────────────────────────────────────

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

    private static GimmickAction Wait(float seconds) =>
        new GimmickAction { type = "wait", floatParam = seconds };

    private static GimmickAction Call(string subId) =>
        new GimmickAction { type = "callSubroutine", targetId = subId };

    // ── 待機（9.7b）─────────────────────────────────────────────────────────────

    [Test]
    public void Wait_DefersRemainingActionsUntilTick()
    {
        var engine = Build(Rule("r1", new[] { Trig("roomStart") },
            SetWorld(0, 1), Wait(2f), SetWorld(1, 5)));

        var result = engine.Fire(GimmickEventContext.RoomStart());
        Assert.IsFalse(result.IsInfiniteLoop);
        Assert.AreEqual(1, _state.GetWorldState(0), "待機前のアクションは即実行");
        Assert.AreEqual(0, _state.GetWorldState(1), "待機後のアクションはまだ");
        Assert.IsTrue(engine.HasPendingSequences);

        engine.Tick(1f);
        Assert.AreEqual(0, _state.GetWorldState(1), "2 秒未満では再開しない");
        Assert.IsTrue(engine.HasPendingSequences);

        engine.Tick(1.5f);
        Assert.AreEqual(5, _state.GetWorldState(1), "経過後に残りを実行");
        Assert.IsFalse(engine.HasPendingSequences);
    }

    [Test]
    public void Tick_NoPending_NoOp()
    {
        var engine = Build(Rule("r1", new[] { Trig("roomStart") }, SetWorld(0, 1)));
        var result = engine.Tick(1f);
        Assert.IsFalse(result.IsInfiniteLoop);
        Assert.AreEqual(0, result.Effects.Count);
    }

    [Test]
    public void ClearSequences_DropsPending()
    {
        var engine = Build(Rule("r1", new[] { Trig("roomStart") }, Wait(3f), SetWorld(0, 9)));
        engine.Fire(GimmickEventContext.RoomStart());
        Assert.IsTrue(engine.HasPendingSequences);

        engine.ClearSequences();
        Assert.IsFalse(engine.HasPendingSequences);
        engine.Tick(5f);
        Assert.AreEqual(0, _state.GetWorldState(0), "破棄後は再開しない");
    }

    [Test]
    public void ResetAll_ClearsPendingSequences()
    {
        var waitRule = Rule("r1", new[] { Trig("roomStart") }, Wait(3f), SetWorld(0, 9));
        var resetRule = Rule("r2", new[] { Trig("objectTap", "btn") },
            new GimmickAction { type = "resetState", resetTarget = "all" });
        var engine = Build(waitRule, resetRule);

        engine.Fire(GimmickEventContext.RoomStart());
        Assert.IsTrue(engine.HasPendingSequences);

        engine.Fire(GimmickEventContext.TapObject("p1", "btn"));
        Assert.IsFalse(engine.HasPendingSequences, "状態リセット「すべて」で進行中シーケンスを中断");
    }

    // ── サブルーチン（9.8 callSubroutine / 9.5 called）──────────────────────────

    [Test]
    public void CallSubroutine_RunsCalledRuleInline()
    {
        var caller = Rule("caller", new[] { Trig("objectTap", "btn") }, Call("sub1"));
        var sub = Rule("sub", new[] { Trig("called", "sub1") }, SetWorld(0, 7));
        var engine = Build(caller, sub);

        engine.Fire(GimmickEventContext.TapObject("p1", "btn"));
        Assert.AreEqual(7, _state.GetWorldState(0), "サブルーチンがインラインで実行される");
    }

    [Test]
    public void CallSubroutine_CarriesInputPlayer()
    {
        var caller = Rule("caller", new[] { Trig("playerCountChanged") }, Call("sub1"));
        var sub = Rule("sub", new[] { Trig("called", "sub1") },
            new GimmickAction
            {
                type = "setPlayerState", stateIndex = 0, stateOp = "set",
                value = new GimmickValueJson { kind = "fixed", value = 3 }, playerTarget = "input",
            });
        var engine = Build(caller, sub);

        engine.Fire(GimmickEventContext.PlayerCountChanged("p1"));
        Assert.AreEqual(3, _state.GetPlayerState("p1", 0), "呼び出し元の入力プレイヤーを引き継ぐ");
        Assert.AreEqual(0, _state.GetPlayerState("p2", 0));
    }

    [Test]
    public void CallSubroutine_InfiniteRecursion_DetectedAsLoop()
    {
        // roomStart で起動し、自分自身（called "loop"）を呼び続ける。
        var rule = Rule("loop", new[] { Trig("roomStart"), Trig("called", "loop") }, Call("loop"));
        var engine = Build(rule);

        var result = engine.Fire(GimmickEventContext.RoomStart());
        Assert.IsTrue(result.IsInfiniteLoop, "サブルーチンの無限再帰はループ検出される");
        Assert.AreEqual("loop", result.LoopRuleId);
    }

    // ── 会話（9.13 startConversation）────────────────────────────────────────────

    [Test]
    public void StartConversation_EmitsEffectForTarget()
    {
        var rule = Rule("r1", new[] { Trig("playerCountChanged") },
            new GimmickAction { type = "startConversation", targetId = "conv1", playerTarget = "input" });
        var engine = Build(rule);

        var result = engine.Fire(GimmickEventContext.PlayerCountChanged("p1"));
        Assert.AreEqual(1, result.Effects.Count);
        var effect = result.Effects[0] as StartConversationEffect;
        Assert.IsNotNull(effect);
        Assert.AreEqual("p1", effect.PlayerId);
        Assert.AreEqual("conv1", effect.ConversationId);
    }
}
