using System.Collections.Generic;
using UnityEngine; // Vector3 のみ使用（副作用なし値型）

// ────────────────────────────────────────────────────────────────────────────
// ギミックシステム 共通型定義 (world-creation.md セクション 9)
// ────────────────────────────────────────────────────────────────────────────

// ── イベント種別 ──────────────────────────────────────────────────────────────

public enum GimmickEventType
{
    RoomStart,
    PlayerCountChanged,
    PlayerTouchObject,
    ObjectTap,
    AreaEnter,
    AreaExit,
    TimerReached,
    ActionButton,
    PlayerTouchPlayer,
    Respawn,
    InRoomPortalUsed,
    Called, // サブルーチン呼び出し（callSubroutine アクションで発火・9.5）
}

// ── 比較演算子 ────────────────────────────────────────────────────────────────

public enum CompareOp
{
    Equal,
    NotEqual,
    GreaterThan,
    LessThan,
    GreaterOrEqual,
    LessOrEqual,
    ModEquals, // (value % modBy) == modResult
}

// ── 値参照種別 ────────────────────────────────────────────────────────────────

public enum ValueRefKind
{
    Fixed,
    WorldState,
    PlayerState,
    AllPlayersStateSum,
    RandomRange,
}

// ── 対象プレイヤー ────────────────────────────────────────────────────────────

public enum PlayerTarget
{
    InputPlayer,
    OpponentPlayer,
    AllPlayers,
}

// ── 条件種別 ──────────────────────────────────────────────────────────────────

public enum GimmickConditionType
{
    WorldStateCompare,
    PlayerStateCompare,
    PlayerCount,
    PlayerNumber,
    TimerCompare,        // 指定タイマーの経過秒（切り捨て整数）の比較
    HasInventoryObject,
    PlayersOverlapping,  // 物理判定 — IPhysicsQuery 経由
    PlayerDistance,      // 物理判定 — IPhysicsQuery 経由
    PlayerLineOfSight,   // 物理判定 — IPhysicsQuery 経由
}

// ── アクション種別 ────────────────────────────────────────────────────────────

public enum GimmickActionType
{
    SetWorldState,
    SetPlayerState,
    TimerStart,
    TimerStop,
    TimerReset,
    ShowHideObject,
    ChangeObjectType,
    ShowMessage,
    PickupObject, // 配置オブジェクトを持つ（インスタンス指定・対象は単一プレイヤーのみ）
    GrantObject,  // オブジェクトを付与する（種別指定・配置物を消費しない・全員可）
    PlaySound,
    SwitchBgm,
    MoveObject,
    TeleportPlayer,
    ResetState,
    PlayEffect,
    SetMoveSpeed,    // 移動速度変更（0〜200%・0% = 移動不可）
    SetPlayerMarker, // 頭上マーカー表示 / 非表示
    StartConversation, // 会話を開始（targetId = 会話 ID・9.13）
    Wait,              // 待機（floatParam = 秒数 0〜60・以降のアクションを遅延・9.7b）
    CallSubroutine,    // サブルーチンを呼ぶ（targetId = サブルーチン ID・9.8）
}

// ── ステート変更演算 ──────────────────────────────────────────────────────────

public enum StateOp { Set, Add, Subtract }

// ─────────────────────────────────────────────────────────────────────────────
// 値参照 — エンジンが条件・アクションの数値を解決するために使う
// ─────────────────────────────────────────────────────────────────────────────

public readonly struct ValueRef
{
    public ValueRefKind Kind { get; }
    public int FixedValue { get; }   // Kind=Fixed
    public int StateIndex { get; }   // Kind=WorldState / PlayerState / AllPlayersStateSum
    public PlayerTarget PlayerTarget { get; } // Kind=PlayerState
    public int RandomMin { get; }    // Kind=RandomRange
    public int RandomMax { get; }    // Kind=RandomRange
    public bool RandomMaxIsPlayerCount { get; } // Kind=RandomRange（最大値 = 現在人数）

    public static ValueRef Fixed(int v) =>
        new ValueRef(ValueRefKind.Fixed, fixedValue: v);

    public static ValueRef World(int index) =>
        new ValueRef(ValueRefKind.WorldState, stateIndex: index);

    public static ValueRef Player(PlayerTarget target, int index) =>
        new ValueRef(ValueRefKind.PlayerState, stateIndex: index, playerTarget: target);

    public static ValueRef AllPlayersSum(int index) =>
        new ValueRef(ValueRefKind.AllPlayersStateSum, stateIndex: index);

    public static ValueRef Random(int min, int max) =>
        new ValueRef(ValueRefKind.RandomRange, randomMin: min, randomMax: max);

    /// <summary>最大値 = 現在人数 の範囲乱数（鬼のランダム選出など）。</summary>
    public static ValueRef RandomToPlayerCount(int min) =>
        new ValueRef(ValueRefKind.RandomRange, randomMin: min, randomMaxIsPlayerCount: true);

    private ValueRef(
        ValueRefKind kind,
        int fixedValue = 0,
        int stateIndex = 0,
        PlayerTarget playerTarget = PlayerTarget.InputPlayer,
        int randomMin = 0,
        int randomMax = 0,
        bool randomMaxIsPlayerCount = false)
    {
        Kind = kind;
        FixedValue = fixedValue;
        StateIndex = stateIndex;
        PlayerTarget = playerTarget;
        RandomMin = randomMin;
        RandomMax = randomMax;
        RandomMaxIsPlayerCount = randomMaxIsPlayerCount;
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// イベントコンテキスト — 発火したイベントの情報
// ─────────────────────────────────────────────────────────────────────────────

public class GimmickEventContext
{
    public GimmickEventType EventType { get; }
    public string InputPlayerId { get; }
    public string OpponentPlayerId { get; } // null = 相手なし
    public string ObjectId { get; }         // オブジェクト系イベント用
    public int TimerIndex { get; }          // タイマーイベント用
    public double TimerTargetSeconds { get; }

    public bool HasOpponent => !string.IsNullOrEmpty(OpponentPlayerId);

    /// <summary>
    /// 相手プレイヤーを確定した新しいコンテキストを返す（イベント種別など他フィールドは維持）。
    /// 距離・視線・重なり条件が相手プレイヤーを動的に確定するときに使用する。
    /// </summary>
    public GimmickEventContext WithOpponent(string opponentPlayerId) =>
        new GimmickEventContext(
            EventType, InputPlayerId, opponentPlayerId, ObjectId, TimerIndex, TimerTargetSeconds);

    private GimmickEventContext(
        GimmickEventType type,
        string inputPlayerId = null,
        string opponentPlayerId = null,
        string objectId = null,
        int timerIndex = -1,
        double timerTargetSeconds = 0)
    {
        EventType = type;
        InputPlayerId = inputPlayerId ?? "";
        OpponentPlayerId = opponentPlayerId;
        ObjectId = objectId ?? "";
        TimerIndex = timerIndex;
        TimerTargetSeconds = timerTargetSeconds;
    }

    public static GimmickEventContext RoomStart() =>
        new GimmickEventContext(GimmickEventType.RoomStart);

    public static GimmickEventContext PlayerCountChanged(string playerId) =>
        new GimmickEventContext(GimmickEventType.PlayerCountChanged, inputPlayerId: playerId);

    public static GimmickEventContext TouchObject(string playerId, string objectId) =>
        new GimmickEventContext(GimmickEventType.PlayerTouchObject, playerId, objectId: objectId);

    public static GimmickEventContext TapObject(string playerId, string objectId) =>
        new GimmickEventContext(GimmickEventType.ObjectTap, playerId, objectId: objectId);

    public static GimmickEventContext AreaEnter(string playerId, string areaId) =>
        new GimmickEventContext(GimmickEventType.AreaEnter, playerId, objectId: areaId);

    public static GimmickEventContext AreaExit(string playerId, string areaId) =>
        new GimmickEventContext(GimmickEventType.AreaExit, playerId, objectId: areaId);

    public static GimmickEventContext TimerReached(string playerId, int timerIndex, double secs) =>
        new GimmickEventContext(GimmickEventType.TimerReached, playerId,
            timerIndex: timerIndex, timerTargetSeconds: secs);

    public static GimmickEventContext ActionButton(string playerId) =>
        new GimmickEventContext(GimmickEventType.ActionButton, playerId);

    public static GimmickEventContext PlayerTouchPlayer(string inputId, string opponentId) =>
        new GimmickEventContext(GimmickEventType.PlayerTouchPlayer, inputId, opponentId);

    public static GimmickEventContext Respawn(string playerId) =>
        new GimmickEventContext(GimmickEventType.Respawn, playerId);

    public static GimmickEventContext PortalUsed(string playerId, string portalId) =>
        new GimmickEventContext(GimmickEventType.InRoomPortalUsed, playerId, objectId: portalId);
}

// ─────────────────────────────────────────────────────────────────────────────
// ランタイムルール — JSON 定義を解析したエンジン用の型
// ─────────────────────────────────────────────────────────────────────────────

public class RuntimeGimmickTrigger
{
    public GimmickEventType EventType { get; }
    public string TargetId { get; } // オブジェクトID / タイマーインデックス文字列 等
    public double TimerTargetSeconds { get; }

    public RuntimeGimmickTrigger(GimmickEventType type, string targetId = "",
        double timerTargetSeconds = 0)
    {
        EventType = type;
        TargetId = targetId ?? "";
        TimerTargetSeconds = timerTargetSeconds;
    }
}

public class RuntimeGimmickCondition
{
    public GimmickConditionType Type { get; }
    public int StateIndex { get; }
    public int TimerIndex { get; }  // TimerCompare 用
    public CompareOp Op { get; }
    public ValueRef ThresholdRef { get; }
    public int ModBy { get; }     // ModEquals 用
    public int ModResult { get; } // ModEquals 用
    public PlayerTarget PlayerTarget { get; }
    public string ObjectId { get; }
    public float PhysicsDistance { get; } // 距離/視線条件用

    public RuntimeGimmickCondition(
        GimmickConditionType type,
        int stateIndex = 0,
        CompareOp op = CompareOp.Equal,
        ValueRef? thresholdRef = null,
        int modBy = 2,
        int modResult = 0,
        PlayerTarget playerTarget = PlayerTarget.InputPlayer,
        string objectId = "",
        float physicsDistance = 0f,
        int timerIndex = 0)
    {
        Type = type;
        StateIndex = stateIndex;
        TimerIndex = timerIndex;
        Op = op;
        ThresholdRef = thresholdRef ?? ValueRef.Fixed(0);
        ModBy = modBy;
        ModResult = modResult;
        PlayerTarget = playerTarget;
        ObjectId = objectId ?? "";
        PhysicsDistance = physicsDistance;
    }
}

public class RuntimeGimmickAction
{
    public GimmickActionType Type { get; }
    public int StateIndex { get; }
    public StateOp StateOp { get; }
    public ValueRef ValueRef { get; }
    public PlayerTarget PlayerTarget { get; }
    public string TargetId { get; }    // objectId / portalId 等
    public string StringParam { get; } // メッセージ文字列 / BGM soundId 等
    public bool BoolParam { get; }     // 表示/非表示
    public int TimerIndex { get; }
    public float FloatParam { get; }   // 移動速度等
    public Vector3 PositionParam { get; } // オブジェクト移動の目標座標
    public ResetTarget ResetTarget { get; }

    public RuntimeGimmickAction(
        GimmickActionType type,
        int stateIndex = 0,
        StateOp stateOp = StateOp.Set,
        ValueRef? valueRef = null,
        PlayerTarget playerTarget = PlayerTarget.InputPlayer,
        string targetId = "",
        string stringParam = "",
        bool boolParam = true,
        int timerIndex = -1,
        float floatParam = 0f,
        Vector3 positionParam = default,
        ResetTarget resetTarget = ResetTarget.All)
    {
        Type = type;
        StateIndex = stateIndex;
        StateOp = stateOp;
        ValueRef = valueRef ?? ValueRef.Fixed(0);
        PlayerTarget = playerTarget;
        TargetId = targetId ?? "";
        StringParam = stringParam ?? "";
        BoolParam = boolParam;
        TimerIndex = timerIndex;
        FloatParam = floatParam;
        PositionParam = positionParam;
        ResetTarget = resetTarget;
    }
}

public enum ResetTarget { InputPlayer, OpponentPlayer, AllPlayers, World, All }

public class RuntimeGimmickRule
{
    public string RuleId { get; }
    public string Label { get; }
    public IReadOnlyList<RuntimeGimmickTrigger> Triggers { get; }
    public IReadOnlyList<RuntimeGimmickCondition> Conditions { get; }
    public IReadOnlyList<RuntimeGimmickAction> Actions { get; }

    public RuntimeGimmickRule(
        string ruleId,
        string label,
        IReadOnlyList<RuntimeGimmickTrigger> triggers,
        IReadOnlyList<RuntimeGimmickCondition> conditions,
        IReadOnlyList<RuntimeGimmickAction> actions)
    {
        RuleId = ruleId ?? "";
        Label = label ?? "";
        Triggers = triggers ?? System.Array.Empty<RuntimeGimmickTrigger>();
        Conditions = conditions ?? System.Array.Empty<RuntimeGimmickCondition>();
        Actions = actions ?? System.Array.Empty<RuntimeGimmickAction>();
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// 実行結果 — エンジンが返す副作用リスト
// ─────────────────────────────────────────────────────────────────────────────

public abstract class GimmickEffect { }

public class WorldStateChangedEffect : GimmickEffect
{
    public int StateIndex { get; }
    public int NewValue { get; }
    public WorldStateChangedEffect(int index, int value) { StateIndex = index; NewValue = value; }
}

public class PlayerStateChangedEffect : GimmickEffect
{
    public string PlayerId { get; }
    public int StateIndex { get; }
    public int NewValue { get; }
    public PlayerStateChangedEffect(string id, int idx, int val) { PlayerId = id; StateIndex = idx; NewValue = val; }
}

public class TimerOperationEffect : GimmickEffect
{
    public enum Op { Start, Stop, Reset }
    public int TimerIndex { get; }
    public Op Operation { get; }
    public TimerOperationEffect(int idx, Op op) { TimerIndex = idx; Operation = op; }
}

public class ObjectVisibilityEffect : GimmickEffect
{
    public string ObjectId { get; }
    public bool Visible { get; }
    public ObjectVisibilityEffect(string id, bool visible) { ObjectId = id; Visible = visible; }
}

public class ObjectTypeChangedEffect : GimmickEffect
{
    public string ObjectId { get; }
    public string NewTypeId { get; }
    public ObjectTypeChangedEffect(string id, string typeId) { ObjectId = id; NewTypeId = typeId; }
}

public class ShowMessageEffect : GimmickEffect
{
    public string PlayerId { get; }
    public string Message { get; }
    public ShowMessageEffect(string playerId, string msg) { PlayerId = playerId; Message = msg; }
}

public class PickupObjectEffect : GimmickEffect
{
    public string PlayerId { get; }

    /// <summary>IsGrant=false: 配置インスタンス ID / IsGrant=true: オブジェクト種別 ID。</summary>
    public string ObjectId { get; }

    /// <summary>true = 付与（配置物を消費しない）。</summary>
    public bool IsGrant { get; }

    public PickupObjectEffect(string playerId, string objectId, bool isGrant = false)
    {
        PlayerId = playerId;
        ObjectId = objectId;
        IsGrant = isGrant;
    }
}

public class PlaySoundEffect : GimmickEffect
{
    public string SoundId { get; }
    public float Volume { get; }
    public PlaySoundEffect(string soundId, float volume = 1f) { SoundId = soundId; Volume = volume; }
}

public class SwitchBgmEffect : GimmickEffect
{
    public string SoundId { get; }
    public SwitchBgmEffect(string soundId) { SoundId = soundId; }
}

public class ObjectMoveEffect : GimmickEffect
{
    public string ObjectId { get; }
    public Vector3 ToPosition { get; }
    public float Speed { get; }
    public ObjectMoveEffect(string id, Vector3 to, float speed) { ObjectId = id; ToPosition = to; Speed = speed; }
}

public class TeleportPlayerEffect : GimmickEffect
{
    public string PlayerId { get; }
    public string ExitPortalId { get; }
    public TeleportPlayerEffect(string pid, string portalId) { PlayerId = pid; ExitPortalId = portalId; }
}

/// <summary>
/// 状態リセット（world-creation.md 9.8 範囲表）。ステート・タイマーはエンジンがリセット済み。
/// 上位レイヤーは Target に応じて以下を追加でリセットする:
/// - プレイヤー系: インベントリ返却 + 移動速度 100% + 頭上マーカー消去
/// - World: BGM オーバーライド解除 + オブジェクト表示 / 種類切り替え状態を初期化
/// - All: 上記すべて + オブジェクト位置をワールド初期配置へ
/// </summary>
public class StateResetEffect : GimmickEffect
{
    public ResetTarget Target { get; }
    public string PlayerId { get; }
    public StateResetEffect(ResetTarget target, string playerId = "") { Target = target; PlayerId = playerId; }
}

public class PlayEffectEffect : GimmickEffect
{
    public string PlayerId { get; }
    public string EffectId { get; }
    public PlayEffectEffect(string pid, string effectId) { PlayerId = pid; EffectId = effectId; }
}

public class PlayerMoveSpeedEffect : GimmickEffect
{
    public string PlayerId { get; }

    /// <summary>移動速度（0〜200%・100 = 通常速度・0 = 移動不可）。</summary>
    public float SpeedPercent { get; }

    public PlayerMoveSpeedEffect(string playerId, float speedPercent)
    {
        PlayerId = playerId;
        SpeedPercent = speedPercent;
    }
}

public class PlayerMarkerEffect : GimmickEffect
{
    public string PlayerId { get; }
    public string MarkerId { get; }
    public bool Visible { get; }

    public PlayerMarkerEffect(string playerId, string markerId, bool visible)
    {
        PlayerId = playerId;
        MarkerId = markerId;
        Visible = visible;
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// 実行結果 — Fire() の戻り値
// ─────────────────────────────────────────────────────────────────────────────

public class GimmickExecutionResult
{
    public bool IsInfiniteLoop { get; }
    public string LoopRuleId { get; }
    public IReadOnlyList<GimmickEffect> Effects { get; }

    private GimmickExecutionResult(bool loop, string ruleId, IReadOnlyList<GimmickEffect> effects)
    {
        IsInfiniteLoop = loop;
        LoopRuleId = ruleId;
        Effects = effects ?? System.Array.Empty<GimmickEffect>();
    }

    public static GimmickExecutionResult Success(IReadOnlyList<GimmickEffect> effects) =>
        new GimmickExecutionResult(false, null, effects);

    public static GimmickExecutionResult InfiniteLoop(string ruleId) =>
        new GimmickExecutionResult(true, ruleId, null);
}
