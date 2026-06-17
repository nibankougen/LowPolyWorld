using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

public class GimmickRuleConverterTests
{
    // ── ヘルパー ──────────────────────────────────────────────────────────────

    private static GimmickTrigger RoomStartTrigger() => new GimmickTrigger { type = "roomStart" };

    private static GimmickAction SetWorldAction(int index = 0, int value = 1) =>
        new GimmickAction
        {
            type = "setWorldState",
            stateIndex = index,
            stateOp = "set",
            value = new GimmickValueJson { kind = "fixed", value = value },
        };

    private static GimmickRule Rule(
        string id = "r1",
        GimmickTrigger[] triggers = null,
        GimmickCondition[] conditions = null,
        GimmickAction[] actions = null) =>
        new GimmickRule
        {
            ruleId = id,
            label = id,
            triggers = triggers ?? new[] { RoomStartTrigger() },
            conditions = conditions ?? System.Array.Empty<GimmickCondition>(),
            actions = actions ?? new[] { SetWorldAction() },
        };

    private static GimmickRuleConverter.WorldRefs FullRefs() =>
        new GimmickRuleConverter.WorldRefs
        {
            ObjectInstanceIds = new HashSet<string> { "inst_door", "inst_key", "area_goal" },
            ObjectTypeIds = new HashSet<string> { "type_key", "type_sword", "type_door_open" },
            ExitPortalIds = new HashSet<string> { "portal_exit_1" },
            EffectIds = new HashSet<string> { "fx_glow" },
            SoundIds = new HashSet<string> { "se_chime", "bgmFunNightStage" },
            MarkerIds = new HashSet<string> { "marker_oni" },
            ConversationIds = new HashSet<string> { "conv_intro" },
        };

    // ── 基本変換 ──────────────────────────────────────────────────────────────

    [Test]
    public void Convert_ValidRule_ProducesRuntimeRule()
    {
        var result = GimmickRuleConverter.Convert(new[] { Rule() });

        Assert.AreEqual(1, result.Rules.Count);
        Assert.AreEqual(0, result.InvalidRules.Count);
        Assert.AreEqual("r1", result.Rules[0].RuleId);
        Assert.AreEqual(GimmickEventType.RoomStart, result.Rules[0].Triggers[0].EventType);
        Assert.AreEqual(GimmickActionType.SetWorldState, result.Rules[0].Actions[0].Type);
    }

    [Test]
    public void Convert_NullOrEmpty_ReturnsEmpty()
    {
        Assert.AreEqual(0, GimmickRuleConverter.Convert(null).Rules.Count);
        Assert.AreEqual(0, GimmickRuleConverter.Convert(
            System.Array.Empty<GimmickRule>()).Rules.Count);
    }

    [Test]
    public void Convert_InvalidRule_DoesNotAffectOtherRules()
    {
        var bad = Rule("bad", actions: new[] { new GimmickAction { type = "unknown_action" } });
        var good = Rule("good");

        var result = GimmickRuleConverter.Convert(new[] { bad, good });

        Assert.AreEqual(1, result.Rules.Count, "不正ルールのみ無効化して継続");
        Assert.AreEqual("good", result.Rules[0].RuleId);
        Assert.AreEqual(1, result.InvalidRules.Count);
        Assert.AreEqual("bad", result.InvalidRules[0].RuleId);
        Assert.IsNotEmpty(result.InvalidRules[0].Reasons);
    }

    [Test]
    public void Convert_Over100Rules_InvalidatesExcess()
    {
        var rules = new GimmickRule[GimmickRuleConverter.MaxRules + 2];
        for (int i = 0; i < rules.Length; i++)
            rules[i] = Rule($"r{i}");

        var result = GimmickRuleConverter.Convert(rules);

        Assert.AreEqual(GimmickRuleConverter.MaxRules, result.Rules.Count);
        Assert.AreEqual(2, result.InvalidRules.Count, "101 個目以降は無効化");
    }

    // ── 個数制限 ──────────────────────────────────────────────────────────────

    [Test]
    public void Convert_NoTriggers_Invalid()
    {
        var rule = Rule(triggers: System.Array.Empty<GimmickTrigger>());
        var result = GimmickRuleConverter.Convert(new[] { rule });
        Assert.AreEqual(0, result.Rules.Count, "入力イベントは 1 個以上必要");
    }

    [Test]
    public void Convert_NoActions_Invalid()
    {
        var rule = Rule(actions: System.Array.Empty<GimmickAction>());
        var result = GimmickRuleConverter.Convert(new[] { rule });
        Assert.AreEqual(0, result.Rules.Count, "アクションは 1 個以上必要");
    }

    [Test]
    public void Convert_TooManyActions_Invalid()
    {
        var actions = new GimmickAction[GimmickRuleConverter.MaxActions + 1];
        for (int i = 0; i < actions.Length; i++)
            actions[i] = SetWorldAction();

        var result = GimmickRuleConverter.Convert(new[] { Rule(actions: actions) });
        Assert.AreEqual(0, result.Rules.Count, "アクションは最大 20 個");
    }

    // ── トリガー ──────────────────────────────────────────────────────────────

    [Test]
    public void Convert_TimerReachedTrigger_MapsIndexAndSeconds()
    {
        var trigger = new GimmickTrigger { type = "timerReached", timerIndex = 2, timerSeconds = 30.5f };
        var result = GimmickRuleConverter.Convert(new[] { Rule(triggers: new[] { trigger }) });

        Assert.AreEqual(1, result.Rules.Count);
        var rt = result.Rules[0].Triggers[0];
        Assert.AreEqual(GimmickEventType.TimerReached, rt.EventType);
        Assert.AreEqual("2", rt.TargetId, "エンジンはタイマー番号文字列で照合する");
        Assert.AreEqual(30.5, rt.TimerTargetSeconds, 0.001);
    }

    [Test]
    public void Convert_TimerTriggerIndexOutOfRange_Invalid()
    {
        var trigger = new GimmickTrigger { type = "timerReached", timerIndex = 5 };
        var result = GimmickRuleConverter.Convert(new[] { Rule(triggers: new[] { trigger }) });
        Assert.AreEqual(0, result.Rules.Count);
    }

    [Test]
    public void Convert_ObjectTrigger_ChecksExistenceOnlyWhenRefsGiven()
    {
        var trigger = new GimmickTrigger { type = "objectTap", targetId = "inst_missing" };
        var rule = Rule(triggers: new[] { trigger });

        Assert.AreEqual(1, GimmickRuleConverter.Convert(new[] { rule }).Rules.Count,
            "refs なし: 実在チェックをスキップ");
        Assert.AreEqual(0, GimmickRuleConverter.Convert(new[] { rule }, FullRefs()).Rules.Count,
            "refs あり: 存在しない ID は無効");
    }

    [Test]
    public void Convert_ObjectTriggerEmptyTarget_MatchesAllAndValid()
    {
        var trigger = new GimmickTrigger { type = "playerTouchObject", targetId = "" };
        var result = GimmickRuleConverter.Convert(
            new[] { Rule(triggers: new[] { trigger }) }, FullRefs());
        Assert.AreEqual(1, result.Rules.Count, "空 = 全対象は有効");
    }

    // ── 条件 ──────────────────────────────────────────────────────────────────

    [Test]
    public void Convert_ModEqualsConditionWithModByBelow2_Invalid()
    {
        var cond = new GimmickCondition { type = "worldState", op = "mod_eq", modBy = 1 };
        var result = GimmickRuleConverter.Convert(new[] { Rule(conditions: new[] { cond }) });
        Assert.AreEqual(0, result.Rules.Count, "剰余の除数は 2 以上");
    }

    [Test]
    public void Convert_PlayerStateRank_Valid()
    {
        var cond = new GimmickCondition
        {
            type = "playerStateRank", stateIndex = 0, playerTarget = "input", rankWithin = 1, rankOrder = "top",
        };
        var result = GimmickRuleConverter.Convert(new[] { Rule(conditions: new[] { cond }) });
        Assert.AreEqual(1, result.Rules.Count);
    }

    [Test]
    public void Convert_PlayerStateRank_InvalidParams()
    {
        var badWithin = new GimmickCondition { type = "playerStateRank", rankWithin = 0, rankOrder = "top" };
        Assert.AreEqual(0, GimmickRuleConverter.Convert(new[] { Rule(conditions: new[] { badWithin }) }).Rules.Count,
            "X 位以内は 1 以上");

        var badOrder = new GimmickCondition { type = "playerStateRank", rankWithin = 1, rankOrder = "middle" };
        Assert.AreEqual(0, GimmickRuleConverter.Convert(new[] { Rule(conditions: new[] { badOrder }) }).Rules.Count,
            "順位の方向は top / bottom のみ");
    }

    [Test]
    public void Convert_StateIndexOutOfRange_Invalid()
    {
        var worldCond = new GimmickCondition { type = "worldState", stateIndex = 10 };
        var playerCond = new GimmickCondition { type = "playerState", stateIndex = 4 };

        Assert.AreEqual(0, GimmickRuleConverter.Convert(
            new[] { Rule(conditions: new[] { worldCond }) }).Rules.Count);
        Assert.AreEqual(0, GimmickRuleConverter.Convert(
            new[] { Rule(conditions: new[] { playerCond }) }).Rules.Count);
    }

    [Test]
    public void Convert_DistanceCondition_ConvertsGridToMeters()
    {
        var cond = new GimmickCondition { type = "playerDistance", distanceGrid = 4f };
        var result = GimmickRuleConverter.Convert(new[] { Rule(conditions: new[] { cond }) });

        Assert.AreEqual(1, result.Rules.Count);
        Assert.AreEqual(2f, result.Rules[0].Conditions[0].PhysicsDistance, 0.001f,
            "4 グリッド × 0.5m = 2m");
    }

    [Test]
    public void Convert_DistanceOutOfRange_Invalid()
    {
        var zero = new GimmickCondition { type = "playerDistance", distanceGrid = 0f };
        var over = new GimmickCondition { type = "playerLineOfSight", distanceGrid = 127f };

        Assert.AreEqual(0, GimmickRuleConverter.Convert(
            new[] { Rule(conditions: new[] { zero }) }).Rules.Count);
        Assert.AreEqual(0, GimmickRuleConverter.Convert(
            new[] { Rule(conditions: new[] { over }) }).Rules.Count);
    }

    // ── アクション ────────────────────────────────────────────────────────────

    [Test]
    public void Convert_PickupObjectWithAllPlayers_Invalid()
    {
        var action = new GimmickAction
        {
            type = "pickupObject", targetId = "inst_key", playerTarget = "all",
        };
        var result = GimmickRuleConverter.Convert(
            new[] { Rule(actions: new[] { action }) }, FullRefs());
        Assert.AreEqual(0, result.Rules.Count, "「持つ」に全員は指定不可（仕様 9.8）");
    }

    [Test]
    public void Convert_ShowMessage_UsesFirstTextAndValidatesLength()
    {
        var ok = new GimmickAction
        {
            type = "showMessage",
            playerTarget = "all",
            texts = new[] { new GimmickTextJson { lang = "ja", text = "こんにちは" } },
        };
        var result = GimmickRuleConverter.Convert(new[] { Rule(actions: new[] { ok }) });
        Assert.AreEqual(1, result.Rules.Count);
        Assert.AreEqual("こんにちは", result.Rules[0].Actions[0].StringParam);

        var tooLong = new GimmickAction
        {
            type = "showMessage",
            texts = new[] { new GimmickTextJson { lang = "ja", text = new string('あ', 81) } },
        };
        Assert.AreEqual(0, GimmickRuleConverter.Convert(
            new[] { Rule(actions: new[] { tooLong }) }).Rules.Count, "80 文字超は無効");

        var empty = new GimmickAction { type = "showMessage" };
        Assert.AreEqual(0, GimmickRuleConverter.Convert(
            new[] { Rule(actions: new[] { empty }) }).Rules.Count, "テキスト未設定は無効");
    }

    [Test]
    public void Convert_SetMoveSpeedOutOfRange_Invalid()
    {
        var action = new GimmickAction { type = "setMoveSpeed", floatParam = 250f };
        var result = GimmickRuleConverter.Convert(new[] { Rule(actions: new[] { action }) });
        Assert.AreEqual(0, result.Rules.Count, "移動速度は 0〜200%");
    }

    [Test]
    public void Convert_MoveObject_ConvertsGridPositionToMeters()
    {
        var action = new GimmickAction
        {
            type = "moveObject",
            targetId = "inst_door",
            floatParam = 2.5f,
            position = new IntVec3Json(2, 0, 6), // グリッド整数（0.5m 単位）
        };
        var result = GimmickRuleConverter.Convert(
            new[] { Rule(actions: new[] { action }) }, FullRefs());

        Assert.AreEqual(1, result.Rules.Count);
        var rt = result.Rules[0].Actions[0];
        Assert.AreEqual(new Vector3(1f, 0f, 3f), rt.PositionParam, "2,0,6 グリッド × 0.5m = 1,0,3 m");
        Assert.AreEqual(2.5f, rt.FloatParam);
    }

    [Test]
    public void Convert_TeleportAndMarkerAndEffect_CheckIdExistence()
    {
        var refs = FullRefs();

        var teleportOk = new GimmickAction { type = "teleportPlayer", targetId = "portal_exit_1" };
        Assert.AreEqual(1, GimmickRuleConverter.Convert(
            new[] { Rule(actions: new[] { teleportOk }) }, refs).Rules.Count);

        var teleportNg = new GimmickAction { type = "teleportPlayer", targetId = "portal_missing" };
        Assert.AreEqual(0, GimmickRuleConverter.Convert(
            new[] { Rule(actions: new[] { teleportNg }) }, refs).Rules.Count);

        var markerHide = new GimmickAction { type = "setPlayerMarker", visible = false };
        Assert.AreEqual(1, GimmickRuleConverter.Convert(
            new[] { Rule(actions: new[] { markerHide }) }, refs).Rules.Count,
            "マーカー非表示は ID 不要");

        var markerShowNg = new GimmickAction
        {
            type = "setPlayerMarker", visible = true, targetId = "marker_missing",
        };
        Assert.AreEqual(0, GimmickRuleConverter.Convert(
            new[] { Rule(actions: new[] { markerShowNg }) }, refs).Rules.Count);
    }

    [Test]
    public void Convert_SwitchBgmNone_IsValid()
    {
        var action = new GimmickAction { type = "switchBgm", targetId = "none" };
        var result = GimmickRuleConverter.Convert(
            new[] { Rule(actions: new[] { action }) }, FullRefs());
        Assert.AreEqual(1, result.Rules.Count, "\"none\" は BGM 停止として有効");
    }

    // ── 値参照 ────────────────────────────────────────────────────────────────

    [Test]
    public void Convert_RandomValueRef_ValidatesMinMax()
    {
        var bad = SetWorldAction();
        bad.value = new GimmickValueJson { kind = "random", min = 10, max = 5 };
        Assert.AreEqual(0, GimmickRuleConverter.Convert(
            new[] { Rule(actions: new[] { bad }) }).Rules.Count, "min > max は無効");

        var playerCount = SetWorldAction();
        playerCount.value = new GimmickValueJson { kind = "random", min = 1, maxIsPlayerCount = true };
        var result = GimmickRuleConverter.Convert(new[] { Rule(actions: new[] { playerCount }) });
        Assert.AreEqual(1, result.Rules.Count);
        Assert.IsTrue(result.Rules[0].Actions[0].ValueRef.RandomMaxIsPlayerCount);
    }

    [Test]
    public void Convert_ValueRefStateIndexOutOfRange_Invalid()
    {
        var action = SetWorldAction();
        action.value = new GimmickValueJson { kind = "worldState", stateIndex = 10 };
        Assert.AreEqual(0, GimmickRuleConverter.Convert(
            new[] { Rule(actions: new[] { action }) }).Rules.Count);
    }

    // ── エンジン統合（変換 → 実行） ──────────────────────────────────────────

    [Test]
    public void Convert_ThenFire_ExecutesEndToEnd()
    {
        // タップで扉を消すルール（脱出ゲームの基本形）を JSON から組み立てて実行する
        var json = new GimmickRule
        {
            ruleId = "open_door",
            triggers = new[] { new GimmickTrigger { type = "objectTap", targetId = "inst_door" } },
            conditions = new[]
            {
                new GimmickCondition
                {
                    type = "hasObject", objectId = "type_key",
                    op = "eq", threshold = new GimmickValueJson { kind = "fixed", value = 0 },
                },
            },
            actions = new[]
            {
                new GimmickAction { type = "showHideObject", targetId = "inst_door", visible = false },
            },
        };

        var converted = GimmickRuleConverter.Convert(new[] { json }, FullRefs());
        Assert.AreEqual(1, converted.Rules.Count);

        var inventory = new GimmickInventoryLogic();
        inventory.TryGrant("p1", "type_key");

        var engine = new GimmickEngine(
            converted.Rules, new GimmickStateManager(), new GimmickTimerLogic(),
            new List<string> { "p1" }, inventory: inventory);

        var result = engine.Fire(GimmickEventContext.TapObject("p1", "inst_door"));

        Assert.AreEqual(1, result.Effects.Count);
        var effect = result.Effects[0] as ObjectVisibilityEffect;
        Assert.IsNotNull(effect);
        Assert.AreEqual("inst_door", effect.ObjectId);
        Assert.IsFalse(effect.Visible, "鍵を持ったプレイヤーのタップで扉が消える");
    }

    // ── 表現力拡張: 会話 / 待機 / サブルーチン ──────────────────────────────────

    [Test]
    public void Convert_StartConversation_ValidatesConversationId()
    {
        var ok = Rule(actions: new[]
        {
            new GimmickAction { type = "startConversation", targetId = "conv_intro", playerTarget = "input" },
        });
        Assert.AreEqual(1, GimmickRuleConverter.Convert(new[] { ok }, FullRefs()).Rules.Count);

        var bad = Rule(actions: new[]
        {
            new GimmickAction { type = "startConversation", targetId = "conv_missing" },
        });
        Assert.AreEqual(1, GimmickRuleConverter.Convert(new[] { bad }, FullRefs()).InvalidRules.Count);
    }

    [Test]
    public void Convert_Wait_ValidatesSeconds()
    {
        var ok = Rule(actions: new[] { new GimmickAction { type = "wait", floatParam = 2.5f } });
        Assert.AreEqual(1, GimmickRuleConverter.Convert(new[] { ok }).Rules.Count);

        var bad = Rule(actions: new[] { new GimmickAction { type = "wait", floatParam = 99f } });
        Assert.AreEqual(1, GimmickRuleConverter.Convert(new[] { bad }).InvalidRules.Count);
    }

    [Test]
    public void Convert_CallSubroutine_RequiresId()
    {
        var ok = Rule(actions: new[] { new GimmickAction { type = "callSubroutine", targetId = "sub_open" } });
        Assert.AreEqual(1, GimmickRuleConverter.Convert(new[] { ok }).Rules.Count);

        var bad = Rule(actions: new[] { new GimmickAction { type = "callSubroutine", targetId = "" } });
        Assert.AreEqual(1, GimmickRuleConverter.Convert(new[] { bad }).InvalidRules.Count);
    }

    [Test]
    public void Convert_CalledTrigger_RequiresSubroutineId()
    {
        var ok = Rule(triggers: new[] { new GimmickTrigger { type = "called", targetId = "sub_open" } });
        Assert.AreEqual(1, GimmickRuleConverter.Convert(new[] { ok }).Rules.Count);
        Assert.AreEqual(GimmickEventType.Called, GimmickRuleConverter.Convert(new[] { ok }).Rules[0].Triggers[0].EventType);

        var bad = Rule(triggers: new[] { new GimmickTrigger { type = "called", targetId = "" } });
        Assert.AreEqual(1, GimmickRuleConverter.Convert(new[] { bad }).InvalidRules.Count);
    }
}
