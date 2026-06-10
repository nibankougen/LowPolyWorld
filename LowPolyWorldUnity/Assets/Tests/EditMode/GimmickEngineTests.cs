using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

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

    private GimmickEngine Build(
        IReadOnlyList<RuntimeGimmickRule> rules,
        IPhysicsQuery physics = null,
        IInventoryQuery inventory = null) =>
        new GimmickEngine(rules, _state, _timers, _players, (min, max) => min, physics, inventory);

    private class FakePhysicsQuery : IPhysicsQuery
    {
        public string Opponent;
        public bool Hit;

        public bool ArePlayersOverlapping(string playerId, out string opponentId)
        {
            opponentId = Opponent;
            return Hit;
        }

        public bool FindNearestPlayer(string playerId, float maxDistance, out string opponentId)
        {
            opponentId = Opponent;
            return Hit;
        }

        public bool RaycastToPlayer(string playerId, float maxDistance, out string opponentId)
        {
            opponentId = Opponent;
            return Hit;
        }
    }

    private class FakeInventoryQuery : IInventoryQuery
    {
        public string HoldingPlayerId;
        public string HoldingObjectId;

        public bool HasObject(string playerId, string objectTypeId) =>
            playerId == HoldingPlayerId && objectTypeId == HoldingObjectId;
    }

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

    // ── 相手プレイヤー ────────────────────────────────────────────────────────

    [Test]
    public void Fire_PlayerTouchPlayer_OpponentTargetAffectsOpponent()
    {
        var rule = new RuntimeGimmickRule("r1", "",
            new[] { new RuntimeGimmickTrigger(GimmickEventType.PlayerTouchPlayer) },
            System.Array.Empty<RuntimeGimmickCondition>(),
            new[] { new RuntimeGimmickAction(GimmickActionType.SetPlayerState,
                stateIndex: 0, stateOp: StateOp.Set, valueRef: ValueRef.Fixed(7),
                playerTarget: PlayerTarget.OpponentPlayer) });

        Build(new[] { rule }).Fire(GimmickEventContext.PlayerTouchPlayer("p1", "p2"));

        Assert.AreEqual(7, _state.GetPlayerState("p2", 0), "相手プレイヤーに適用される");
        Assert.AreEqual(0, _state.GetPlayerState("p1", 0), "入力プレイヤーは変化しない");
    }

    [Test]
    public void Fire_DistanceCondition_EstablishesOpponentForActions()
    {
        var physics = new FakePhysicsQuery { Hit = true, Opponent = "p2" };

        var cond = new RuntimeGimmickCondition(
            GimmickConditionType.PlayerDistance, physicsDistance: 2f);

        var rule = new RuntimeGimmickRule("r1", "",
            new[] { new RuntimeGimmickTrigger(GimmickEventType.ActionButton) },
            new[] { cond },
            new[] { new RuntimeGimmickAction(GimmickActionType.SetPlayerState,
                stateIndex: 0, stateOp: StateOp.Set, valueRef: ValueRef.Fixed(9),
                playerTarget: PlayerTarget.OpponentPlayer) });

        Build(new[] { rule }, physics: physics).Fire(GimmickEventContext.ActionButton("p1"));

        Assert.AreEqual(9, _state.GetPlayerState("p2", 0),
            "距離条件で確定した相手プレイヤーがアクション対象になる");
    }

    [Test]
    public void Fire_OverlappingCondition_EstablishesOpponentForActions()
    {
        var physics = new FakePhysicsQuery { Hit = true, Opponent = "p2" };

        var cond = new RuntimeGimmickCondition(GimmickConditionType.PlayersOverlapping);

        var rule = new RuntimeGimmickRule("r1", "",
            new[] { new RuntimeGimmickTrigger(GimmickEventType.ActionButton) },
            new[] { cond },
            new[] { new RuntimeGimmickAction(GimmickActionType.SetPlayerState,
                stateIndex: 1, stateOp: StateOp.Set, valueRef: ValueRef.Fixed(3),
                playerTarget: PlayerTarget.OpponentPlayer) });

        Build(new[] { rule }, physics: physics).Fire(GimmickEventContext.ActionButton("p1"));

        Assert.AreEqual(3, _state.GetPlayerState("p2", 1),
            "重なり条件で確定した相手プレイヤーがアクション対象になる");
    }

    // ── 全員対象アクション ────────────────────────────────────────────────────

    [Test]
    public void Fire_SetPlayerStateAllPlayers_AppliesToAllPlayers()
    {
        var rule = new RuntimeGimmickRule("r1", "",
            new[] { new RuntimeGimmickTrigger(GimmickEventType.RoomStart) },
            System.Array.Empty<RuntimeGimmickCondition>(),
            new[] { new RuntimeGimmickAction(GimmickActionType.SetPlayerState,
                stateIndex: 0, stateOp: StateOp.Add, valueRef: ValueRef.Fixed(5),
                playerTarget: PlayerTarget.AllPlayers) });

        var result = Build(new[] { rule }).Fire(GimmickEventContext.RoomStart());

        Assert.AreEqual(5, _state.GetPlayerState("p1", 0));
        Assert.AreEqual(5, _state.GetPlayerState("p2", 0));
        Assert.AreEqual(2, result.Effects.Count, "全プレイヤー分のエフェクトが返る");
    }

    [Test]
    public void Fire_ShowMessageAllPlayers_EmitsEffectPerPlayer()
    {
        var rule = new RuntimeGimmickRule("r1", "",
            new[] { new RuntimeGimmickTrigger(GimmickEventType.RoomStart) },
            System.Array.Empty<RuntimeGimmickCondition>(),
            new[] { new RuntimeGimmickAction(GimmickActionType.ShowMessage,
                stringParam: "こんにちは", playerTarget: PlayerTarget.AllPlayers) });

        var result = Build(new[] { rule }).Fire(GimmickEventContext.RoomStart());

        Assert.AreEqual(2, result.Effects.Count);
        var ids = new List<string>();
        foreach (var effect in result.Effects)
        {
            var msg = effect as ShowMessageEffect;
            Assert.IsNotNull(msg);
            Assert.AreEqual("こんにちは", msg.Message);
            ids.Add(msg.PlayerId);
        }
        CollectionAssert.AreEquivalent(new[] { "p1", "p2" }, ids);
    }

    // ── インベントリ条件 ──────────────────────────────────────────────────────

    [Test]
    public void Fire_HasInventoryCondition_ChecksInventoryQuery()
    {
        var inventory = new FakeInventoryQuery { HoldingPlayerId = "p1", HoldingObjectId = "obj_key" };

        var hasKey = new RuntimeGimmickCondition(
            GimmickConditionType.HasInventoryObject, objectId: "obj_key");
        var hasGem = new RuntimeGimmickCondition(
            GimmickConditionType.HasInventoryObject, objectId: "obj_gem");

        var rules = new[]
        {
            new RuntimeGimmickRule("r1", "",
                new[] { new RuntimeGimmickTrigger(GimmickEventType.ActionButton) },
                new[] { hasKey },
                new[] { new RuntimeGimmickAction(GimmickActionType.SetWorldState,
                    stateIndex: 0, stateOp: StateOp.Set, valueRef: ValueRef.Fixed(1)) }),
            new RuntimeGimmickRule("r2", "",
                new[] { new RuntimeGimmickTrigger(GimmickEventType.ActionButton) },
                new[] { hasGem },
                new[] { new RuntimeGimmickAction(GimmickActionType.SetWorldState,
                    stateIndex: 1, stateOp: StateOp.Set, valueRef: ValueRef.Fixed(1)) }),
        };

        Build(rules, inventory: inventory).Fire(GimmickEventContext.ActionButton("p1"));

        Assert.AreEqual(1, _state.GetWorldState(0), "所持しているオブジェクト: 条件成立");
        Assert.AreEqual(0, _state.GetWorldState(1), "未所持のオブジェクト: 条件不成立");
    }

    // ── オブジェクト移動・持つアクション ──────────────────────────────────────

    [Test]
    public void Fire_MoveObjectAction_ReturnsMoveEffect()
    {
        var rule = new RuntimeGimmickRule("r1", "",
            new[] { new RuntimeGimmickTrigger(GimmickEventType.RoomStart) },
            System.Array.Empty<RuntimeGimmickCondition>(),
            new[] { new RuntimeGimmickAction(GimmickActionType.MoveObject,
                targetId: "obj_lift", positionParam: new Vector3(1f, 0f, 2f), floatParam: 2.5f) });

        var result = Build(new[] { rule }).Fire(GimmickEventContext.RoomStart());

        Assert.AreEqual(1, result.Effects.Count);
        var effect = result.Effects[0] as ObjectMoveEffect;
        Assert.IsNotNull(effect);
        Assert.AreEqual("obj_lift", effect.ObjectId);
        Assert.AreEqual(new Vector3(1f, 0f, 2f), effect.ToPosition);
        Assert.AreEqual(2.5f, effect.Speed);
    }

    [Test]
    public void Fire_PickupObjectAction_ReturnsPickupEffect()
    {
        var rule = new RuntimeGimmickRule("r1", "",
            new[] { new RuntimeGimmickTrigger(GimmickEventType.PlayerTouchObject, "obj_key") },
            System.Array.Empty<RuntimeGimmickCondition>(),
            new[] { new RuntimeGimmickAction(GimmickActionType.PickupObject,
                targetId: "obj_key", playerTarget: PlayerTarget.InputPlayer) });

        var result = Build(new[] { rule }).Fire(GimmickEventContext.TouchObject("p1", "obj_key"));

        Assert.AreEqual(1, result.Effects.Count);
        var effect = result.Effects[0] as PickupObjectEffect;
        Assert.IsNotNull(effect);
        Assert.AreEqual("p1", effect.PlayerId);
        Assert.AreEqual("obj_key", effect.ObjectId);
        Assert.IsFalse(effect.IsGrant, "「持つ」は配置オブジェクトの取得");
    }

    // ── ゲーム内ゲーム拡張（エリア退出 / タイマー比較 / 速度 / マーカー / 人数乱数） ──

    [Test]
    public void Fire_AreaExitTrigger_MatchesSpecificArea()
    {
        var rule = new RuntimeGimmickRule("r1", "",
            new[] { new RuntimeGimmickTrigger(GimmickEventType.AreaExit, "area_safe") },
            System.Array.Empty<RuntimeGimmickCondition>(),
            new[] { new RuntimeGimmickAction(GimmickActionType.SetWorldState,
                stateIndex: 0, stateOp: StateOp.Add, valueRef: ValueRef.Fixed(1)) });

        var engine = Build(new[] { rule });

        engine.Fire(GimmickEventContext.AreaEnter("p1", "area_safe")); // 侵入では発火しない
        Assert.AreEqual(0, _state.GetWorldState(0), "エリア侵入: 発火しない");

        engine.Fire(GimmickEventContext.AreaExit("p1", "area_other")); // 別エリア
        Assert.AreEqual(0, _state.GetWorldState(0), "別エリア: 発火しない");

        engine.Fire(GimmickEventContext.AreaExit("p1", "area_safe"));
        Assert.AreEqual(1, _state.GetWorldState(0), "対象エリアからの退出: 発火する");
    }

    private class FakeClock : GimmickTimerLogic.ITimeProvider
    {
        public double NowSeconds { get; set; }
    }

    [Test]
    public void Fire_TimerCompareCondition_ComparesElapsedSeconds()
    {
        var clock = new FakeClock { NowSeconds = 100.0 };
        var timers = new GimmickTimerLogic(clock);
        timers.Start(0);

        var cond = new RuntimeGimmickCondition(
            GimmickConditionType.TimerCompare,
            timerIndex: 0,
            op: CompareOp.LessThan,
            thresholdRef: ValueRef.Fixed(30));

        var rule = new RuntimeGimmickRule("r1", "制限時間内にゴール",
            new[] { new RuntimeGimmickTrigger(GimmickEventType.AreaEnter, "area_goal") },
            new[] { cond },
            new[] { new RuntimeGimmickAction(GimmickActionType.SetWorldState,
                stateIndex: 0, stateOp: StateOp.Set, valueRef: ValueRef.Fixed(1)) });

        var engine = new GimmickEngine(new[] { rule }, _state, timers, _players);

        clock.NowSeconds = 125.9; // 経過 25.9 秒 → 25 < 30 で成立
        engine.Fire(GimmickEventContext.AreaEnter("p1", "area_goal"));
        Assert.AreEqual(1, _state.GetWorldState(0), "制限時間内: 条件成立");

        _state.SetWorldState(0, 0);
        clock.NowSeconds = 135.0; // 経過 35 秒 → 不成立
        engine.Fire(GimmickEventContext.AreaEnter("p1", "area_goal"));
        Assert.AreEqual(0, _state.GetWorldState(0), "制限時間超過: 条件不成立");
    }

    [Test]
    public void Fire_SetMoveSpeedAllPlayers_EmitsClampedEffectPerPlayer()
    {
        var rule = new RuntimeGimmickRule("r1", "",
            new[] { new RuntimeGimmickTrigger(GimmickEventType.RoomStart) },
            System.Array.Empty<RuntimeGimmickCondition>(),
            new[] { new RuntimeGimmickAction(GimmickActionType.SetMoveSpeed,
                floatParam: 500f, playerTarget: PlayerTarget.AllPlayers) });

        var result = Build(new[] { rule }).Fire(GimmickEventContext.RoomStart());

        Assert.AreEqual(2, result.Effects.Count);
        foreach (var e in result.Effects)
        {
            var speed = e as PlayerMoveSpeedEffect;
            Assert.IsNotNull(speed);
            Assert.AreEqual(200f, speed.SpeedPercent, 0.001f, "0〜200% にクランプ");
        }
    }

    [Test]
    public void Fire_SetPlayerMarkerAction_EmitsMarkerEffect()
    {
        var rule = new RuntimeGimmickRule("r1", "鬼マーカー",
            new[] { new RuntimeGimmickTrigger(GimmickEventType.PlayerTouchPlayer) },
            System.Array.Empty<RuntimeGimmickCondition>(),
            new[] { new RuntimeGimmickAction(GimmickActionType.SetPlayerMarker,
                targetId: "marker_oni", boolParam: true,
                playerTarget: PlayerTarget.OpponentPlayer) });

        var result = Build(new[] { rule }).Fire(GimmickEventContext.PlayerTouchPlayer("p1", "p2"));

        Assert.AreEqual(1, result.Effects.Count);
        var marker = result.Effects[0] as PlayerMarkerEffect;
        Assert.IsNotNull(marker);
        Assert.AreEqual("p2", marker.PlayerId, "接触相手に鬼マーカー");
        Assert.AreEqual("marker_oni", marker.MarkerId);
        Assert.IsTrue(marker.Visible);
    }

    [Test]
    public void Fire_RandomToPlayerCount_UsesCurrentPlayerCountAsMax()
    {
        // randomProvider に (min, max) => max を渡し、最大値に現在人数が入ることを検証
        var rule = new RuntimeGimmickRule("r1", "鬼のランダム選出",
            new[] { new RuntimeGimmickTrigger(GimmickEventType.RoomStart) },
            System.Array.Empty<RuntimeGimmickCondition>(),
            new[] { new RuntimeGimmickAction(GimmickActionType.SetWorldState,
                stateIndex: 0, stateOp: StateOp.Set,
                valueRef: ValueRef.RandomToPlayerCount(1)) });

        var engine = new GimmickEngine(
            new[] { rule }, _state, _timers, _players, (min, max) => max);

        engine.Fire(GimmickEventContext.RoomStart());

        Assert.AreEqual(_players.Count, _state.GetWorldState(0),
            "乱数の最大値 = 現在人数（2）");
    }

    [Test]
    public void Fire_GrantObjectActionAllPlayers_EmitsGrantEffectPerPlayer()
    {
        var rule = new RuntimeGimmickRule("r1", "",
            new[] { new RuntimeGimmickTrigger(GimmickEventType.RoomStart) },
            System.Array.Empty<RuntimeGimmickCondition>(),
            new[] { new RuntimeGimmickAction(GimmickActionType.GrantObject,
                targetId: "type_sword", playerTarget: PlayerTarget.AllPlayers) });

        var result = Build(new[] { rule }).Fire(GimmickEventContext.RoomStart());

        Assert.AreEqual(2, result.Effects.Count, "「付与する」は全員選択可");
        var ids = new List<string>();
        foreach (var e in result.Effects)
        {
            var grant = e as PickupObjectEffect;
            Assert.IsNotNull(grant);
            Assert.IsTrue(grant.IsGrant);
            Assert.AreEqual("type_sword", grant.ObjectId);
            ids.Add(grant.PlayerId);
        }
        CollectionAssert.AreEquivalent(new[] { "p1", "p2" }, ids);
    }
}
