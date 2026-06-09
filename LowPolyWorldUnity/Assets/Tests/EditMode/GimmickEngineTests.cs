using NUnit.Framework;
using System.Collections.Generic;

public class GimmickEngineTests
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

    private GimmickEngine Build(IReadOnlyList<RuntimeGimmickRule> rules) =>
        new GimmickEngine(rules, _state, _timers, _players, (min, max) => min);

    // ── OR 発火 ────────────────────────────────────────────────────────────────

    [Test]
    public void Fire_SingleMatchingTrigger_ExecutesActions()
    {
        var rule = new RuntimeGimmickRule("r1", "テスト",
            new[] { new RuntimeGimmickTrigger(GimmickEventType.RoomStart) },
            System.Array.Empty<RuntimeGimmickCondition>(),
            new[] { new RuntimeGimmickAction(GimmickActionType.SetWorldState,
                stateIndex: 0, stateOp: StateOp.Set, valueRef: ValueRef.Fixed(42)) });

        var result = Build(new[] { rule }).Fire(GimmickEventContext.RoomStart());

        Assert.IsFalse(result.IsInfiniteLoop);
        Assert.AreEqual(42, _state.GetWorldState(0));
    }

    [Test]
    public void Fire_TwoTriggers_EitherFires()
    {
        var rule = new RuntimeGimmickRule("r1", "OR テスト",
            new[]
            {
                new RuntimeGimmickTrigger(GimmickEventType.RoomStart),
                new RuntimeGimmickTrigger(GimmickEventType.ActionButton),
            },
            System.Array.Empty<RuntimeGimmickCondition>(),
            new[] { new RuntimeGimmickAction(GimmickActionType.SetWorldState,
                stateIndex: 0, stateOp: StateOp.Add, valueRef: ValueRef.Fixed(1)) });

        var engine = Build(new[] { rule });
        engine.Fire(GimmickEventContext.RoomStart());
        engine.Fire(GimmickEventContext.ActionButton("p1"));

        Assert.AreEqual(2, _state.GetWorldState(0), "OR結合: どちらか一方が発火すればルールが起動");
    }

    [Test]
    public void Fire_NonMatchingTrigger_NoAction()
    {
        var rule = new RuntimeGimmickRule("r1", "",
            new[] { new RuntimeGimmickTrigger(GimmickEventType.ActionButton) },
            System.Array.Empty<RuntimeGimmickCondition>(),
            new[] { new RuntimeGimmickAction(GimmickActionType.SetWorldState,
                stateIndex: 0, stateOp: StateOp.Set, valueRef: ValueRef.Fixed(99)) });

        Build(new[] { rule }).Fire(GimmickEventContext.RoomStart());

        Assert.AreEqual(0, _state.GetWorldState(0), "トリガー不一致: アクション実行なし");
    }

    // ── AND 条件 ───────────────────────────────────────────────────────────────

    [Test]
    public void Fire_ConditionMet_ExecutesActions()
    {
        _state.SetWorldState(0, 5);

        var cond = new RuntimeGimmickCondition(
            GimmickConditionType.WorldStateCompare,
            stateIndex: 0,
            op: CompareOp.GreaterOrEqual,
            thresholdRef: ValueRef.Fixed(5));

        var rule = new RuntimeGimmickRule("r1", "",
            new[] { new RuntimeGimmickTrigger(GimmickEventType.RoomStart) },
            new[] { cond },
            new[] { new RuntimeGimmickAction(GimmickActionType.SetWorldState,
                stateIndex: 1, stateOp: StateOp.Set, valueRef: ValueRef.Fixed(100)) });

        Build(new[] { rule }).Fire(GimmickEventContext.RoomStart());

        Assert.AreEqual(100, _state.GetWorldState(1));
    }

    [Test]
    public void Fire_ConditionNotMet_SkipsActions()
    {
        _state.SetWorldState(0, 3);

        var cond = new RuntimeGimmickCondition(
            GimmickConditionType.WorldStateCompare,
            stateIndex: 0,
            op: CompareOp.GreaterOrEqual,
            thresholdRef: ValueRef.Fixed(5));

        var rule = new RuntimeGimmickRule("r1", "",
            new[] { new RuntimeGimmickTrigger(GimmickEventType.RoomStart) },
            new[] { cond },
            new[] { new RuntimeGimmickAction(GimmickActionType.SetWorldState,
                stateIndex: 1, stateOp: StateOp.Set, valueRef: ValueRef.Fixed(100)) });

        Build(new[] { rule }).Fire(GimmickEventContext.RoomStart());

        Assert.AreEqual(0, _state.GetWorldState(1), "条件不成立: アクション実行なし");
    }

    [Test]
    public void Fire_MultipleConditions_AllMustPass()
    {
        _state.SetWorldState(0, 5);
        _state.SetWorldState(1, 3); // この条件は成立しない

        var conds = new[]
        {
            new RuntimeGimmickCondition(GimmickConditionType.WorldStateCompare, 0,
                CompareOp.Equal, ValueRef.Fixed(5)),
            new RuntimeGimmickCondition(GimmickConditionType.WorldStateCompare, 1,
                CompareOp.Equal, ValueRef.Fixed(10)), // 不成立
        };

        var rule = new RuntimeGimmickRule("r1", "",
            new[] { new RuntimeGimmickTrigger(GimmickEventType.RoomStart) },
            conds,
            new[] { new RuntimeGimmickAction(GimmickActionType.SetWorldState,
                stateIndex: 2, stateOp: StateOp.Set, valueRef: ValueRef.Fixed(1)) });

        Build(new[] { rule }).Fire(GimmickEventContext.RoomStart());

        Assert.AreEqual(0, _state.GetWorldState(2), "AND結合: どちらか不成立なら実行しない");
    }

    // ── 定義順実行 ────────────────────────────────────────────────────────────

    [Test]
    public void Fire_MultipleRules_ExecuteInOrder()
    {
        var rule1 = new RuntimeGimmickRule("r1", "",
            new[] { new RuntimeGimmickTrigger(GimmickEventType.RoomStart) },
            System.Array.Empty<RuntimeGimmickCondition>(),
            new[] { new RuntimeGimmickAction(GimmickActionType.SetWorldState,
                stateIndex: 0, stateOp: StateOp.Set, valueRef: ValueRef.Fixed(1)) });

        var rule2 = new RuntimeGimmickRule("r2", "",
            new[] { new RuntimeGimmickTrigger(GimmickEventType.RoomStart) },
            System.Array.Empty<RuntimeGimmickCondition>(),
            new[] { new RuntimeGimmickAction(GimmickActionType.SetWorldState,
                stateIndex: 0, stateOp: StateOp.Add, valueRef: ValueRef.Fixed(9)) });

        Build(new[] { rule1, rule2 }).Fire(GimmickEventContext.RoomStart());

        Assert.AreEqual(10, _state.GetWorldState(0),
            "定義順: rule1 が先に 1 をセット → rule2 が 9 を加算 → 10");
    }

    // ── 無限ループ検出 ────────────────────────────────────────────────────────

    [Test]
    public void Fire_ChainExceedsMax_ReturnsInfiniteLoop()
    {
        // 1 ルールに MaxChainCount+1 個のアクションを持たせる
        var actions = new RuntimeGimmickAction[GimmickEngine.MaxChainCount + 1];
        for (int i = 0; i < actions.Length; i++)
            actions[i] = new RuntimeGimmickAction(GimmickActionType.SetWorldState,
                stateIndex: 0, stateOp: StateOp.Add, valueRef: ValueRef.Fixed(1));

        var rule = new RuntimeGimmickRule("loop_rule", "ループルール",
            new[] { new RuntimeGimmickTrigger(GimmickEventType.RoomStart) },
            System.Array.Empty<RuntimeGimmickCondition>(),
            actions);

        var result = Build(new[] { rule }).Fire(GimmickEventContext.RoomStart());

        Assert.IsTrue(result.IsInfiniteLoop);
        Assert.AreEqual("loop_rule", result.LoopRuleId);
    }

    [Test]
    public void Fire_ExactlyMaxChain_DoesNotLoop()
    {
        var actions = new RuntimeGimmickAction[GimmickEngine.MaxChainCount];
        for (int i = 0; i < actions.Length; i++)
            actions[i] = new RuntimeGimmickAction(GimmickActionType.SetWorldState,
                stateIndex: 0, stateOp: StateOp.Add, valueRef: ValueRef.Fixed(1));

        var rule = new RuntimeGimmickRule("ok_rule", "",
            new[] { new RuntimeGimmickTrigger(GimmickEventType.RoomStart) },
            System.Array.Empty<RuntimeGimmickCondition>(),
            actions);

        var result = Build(new[] { rule }).Fire(GimmickEventContext.RoomStart());

        Assert.IsFalse(result.IsInfiniteLoop, "ちょうど MaxChainCount はループ判定しない");
        Assert.AreEqual(GimmickEngine.MaxChainCount, _state.GetWorldState(0));
    }

    // ── エフェクト返却 ────────────────────────────────────────────────────────

    [Test]
    public void Fire_StateChangeAction_ReturnsEffect()
    {
        var rule = new RuntimeGimmickRule("r1", "",
            new[] { new RuntimeGimmickTrigger(GimmickEventType.RoomStart) },
            System.Array.Empty<RuntimeGimmickCondition>(),
            new[] { new RuntimeGimmickAction(GimmickActionType.SetWorldState,
                stateIndex: 2, stateOp: StateOp.Set, valueRef: ValueRef.Fixed(55)) });

        var result = Build(new[] { rule }).Fire(GimmickEventContext.RoomStart());

        Assert.AreEqual(1, result.Effects.Count);
        var effect = result.Effects[0] as WorldStateChangedEffect;
        Assert.IsNotNull(effect);
        Assert.AreEqual(2, effect.StateIndex);
        Assert.AreEqual(55, effect.NewValue);
    }

    [Test]
    public void Fire_TimerStartAction_ReturnsTimerEffect()
    {
        var rule = new RuntimeGimmickRule("r1", "",
            new[] { new RuntimeGimmickTrigger(GimmickEventType.RoomStart) },
            System.Array.Empty<RuntimeGimmickCondition>(),
            new[] { new RuntimeGimmickAction(GimmickActionType.TimerStart, timerIndex: 0) });

        var result = Build(new[] { rule }).Fire(GimmickEventContext.RoomStart());

        Assert.AreEqual(1, result.Effects.Count);
        var effect = result.Effects[0] as TimerOperationEffect;
        Assert.IsNotNull(effect);
        Assert.AreEqual(TimerOperationEffect.Op.Start, effect.Operation);
    }

    // ── オブジェクト系トリガー ────────────────────────────────────────────────

    [Test]
    public void Fire_TouchObjectTrigger_MatchesSpecificObject()
    {
        var rule = new RuntimeGimmickRule("r1", "",
            new[] { new RuntimeGimmickTrigger(GimmickEventType.PlayerTouchObject, "obj_door") },
            System.Array.Empty<RuntimeGimmickCondition>(),
            new[] { new RuntimeGimmickAction(GimmickActionType.SetWorldState,
                stateIndex: 0, stateOp: StateOp.Set, valueRef: ValueRef.Fixed(1)) });

        var engine = Build(new[] { rule });

        engine.Fire(GimmickEventContext.TouchObject("p1", "obj_wall")); // 別オブジェクト
        Assert.AreEqual(0, _state.GetWorldState(0), "別オブジェクト: 発火しない");

        engine.Fire(GimmickEventContext.TouchObject("p1", "obj_door"));
        Assert.AreEqual(1, _state.GetWorldState(0), "対象オブジェクト: 発火する");
    }

    [Test]
    public void Fire_TouchObjectTrigger_EmptyTargetMatchesAll()
    {
        var rule = new RuntimeGimmickRule("r1", "",
            new[] { new RuntimeGimmickTrigger(GimmickEventType.PlayerTouchObject, "") },
            System.Array.Empty<RuntimeGimmickCondition>(),
            new[] { new RuntimeGimmickAction(GimmickActionType.SetWorldState,
                stateIndex: 0, stateOp: StateOp.Add, valueRef: ValueRef.Fixed(1)) });

        var engine = Build(new[] { rule });
        engine.Fire(GimmickEventContext.TouchObject("p1", "any_object_1"));
        engine.Fire(GimmickEventContext.TouchObject("p1", "any_object_2"));

        Assert.AreEqual(2, _state.GetWorldState(0), "TargetId 空: 全オブジェクトで発火");
    }

    // ── リセットアクション ────────────────────────────────────────────────────

    [Test]
    public void Fire_ResetWorldAction_ResetsWorldState()
    {
        _state.SetWorldState(0, 100);

        var rule = new RuntimeGimmickRule("r1", "",
            new[] { new RuntimeGimmickTrigger(GimmickEventType.RoomStart) },
            System.Array.Empty<RuntimeGimmickCondition>(),
            new[] { new RuntimeGimmickAction(GimmickActionType.ResetState,
                resetTarget: ResetTarget.World) });

        Build(new[] { rule }).Fire(GimmickEventContext.RoomStart());

        Assert.AreEqual(0, _state.GetWorldState(0));
    }

    // ── プレイヤー数条件 ──────────────────────────────────────────────────────

    [Test]
    public void Fire_PlayerCountCondition_UsesActualCount()
    {
        var cond = new RuntimeGimmickCondition(
            GimmickConditionType.PlayerCount,
            op: CompareOp.Equal,
            thresholdRef: ValueRef.Fixed(2));

        var rule = new RuntimeGimmickRule("r1", "",
            new[] { new RuntimeGimmickTrigger(GimmickEventType.RoomStart) },
            new[] { cond },
            new[] { new RuntimeGimmickAction(GimmickActionType.SetWorldState,
                stateIndex: 0, stateOp: StateOp.Set, valueRef: ValueRef.Fixed(1)) });

        // _players = ["p1","p2"] → count=2 → 条件成立
        Build(new[] { rule }).Fire(GimmickEventContext.RoomStart());
        Assert.AreEqual(1, _state.GetWorldState(0));
    }
}
