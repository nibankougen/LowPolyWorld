using System;
using System.Collections.Generic;

/// <summary>
/// ギミックシステムのコア評価エンジン（world-creation.md セクション 9.4〜9.10）。
///
/// 評価順序:
/// 1. 入力イベントのいずれかがトリガーに一致（OR 結合）
/// 2. 全条件を満たす（AND 結合）
/// 3. アクションを定義順に実行
/// 4. 同一フレームで複数ルールが発火した場合: 定義順に実行
/// 5. 連鎖アクション数が MaxChainCount を超えた場合: 無限ループ判定
///
/// 物理判定が必要な条件（PlayerDistance / PlayersOverlapping / PlayerLineOfSight）は
/// IPhysicsQuery、インベントリ参照条件（HasInventoryObject）は IInventoryQuery の
/// インターフェース経由で外部から提供する。
/// </summary>
public class GimmickEngine
{
    public const int MaxChainCount = 100;

    private readonly IReadOnlyList<RuntimeGimmickRule> _rules;
    private readonly GimmickStateManager _state;
    private readonly GimmickValueResolver _resolver;
    private readonly GimmickTimerLogic _timers;
    private readonly IPhysicsQuery _physics;
    private readonly IInventoryQuery _inventory;
    private readonly IReadOnlyList<string> _allPlayerIds;

    public GimmickEngine(
        IReadOnlyList<RuntimeGimmickRule> rules,
        GimmickStateManager state,
        GimmickTimerLogic timers,
        IReadOnlyList<string> allPlayerIds = null,
        Func<int, int, int> randomProvider = null,
        IPhysicsQuery physics = null,
        IInventoryQuery inventory = null)
    {
        _rules = rules ?? Array.Empty<RuntimeGimmickRule>();
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _timers = timers ?? throw new ArgumentNullException(nameof(timers));
        _allPlayerIds = allPlayerIds ?? Array.Empty<string>();
        _resolver = new GimmickValueResolver(state, randomProvider);
        _physics = physics ?? NullPhysicsQuery.Instance;
        _inventory = inventory ?? NullInventoryQuery.Instance;
    }

    // ── 公開 API ─────────────────────────────────────────────────────────────

    /// <summary>
    /// イベントを発火し、条件を評価してアクションを実行する。
    /// 無限ループを検出した場合は IsInfiniteLoop = true の結果を返す。
    /// </summary>
    public GimmickExecutionResult Fire(GimmickEventContext ctx)
    {
        if (ctx == null) throw new ArgumentNullException(nameof(ctx));

        int chainCount = 0;
        var effects = new List<GimmickEffect>();

        foreach (var rule in _rules)
        {
            if (!MatchesAnyTrigger(rule, ctx))
                continue;

            // 距離・視線・重なり条件は相手プレイヤーを動的に確定する。
            // 確定した相手は同一ルール内の後続条件・アクションにのみ有効。
            var ruleCtx = ctx;
            if (!EvaluateConditions(rule, ref ruleCtx))
                continue;

            foreach (var action in rule.Actions)
            {
                if (++chainCount > MaxChainCount)
                    return GimmickExecutionResult.InfiniteLoop(rule.RuleId);

                ExecuteAction(action, ruleCtx, effects);
            }
        }

        return GimmickExecutionResult.Success(effects);
    }

    // ── トリガー判定 ─────────────────────────────────────────────────────────

    private static bool MatchesAnyTrigger(RuntimeGimmickRule rule, GimmickEventContext ctx)
    {
        foreach (var trigger in rule.Triggers)
        {
            if (TriggerMatches(trigger, ctx))
                return true;
        }
        return false;
    }

    private static bool TriggerMatches(RuntimeGimmickTrigger trigger, GimmickEventContext ctx)
    {
        if (trigger.EventType != ctx.EventType)
            return false;

        return trigger.EventType switch
        {
            // オブジェクト系: targetId が "" 以外の場合は一致チェック
            GimmickEventType.PlayerTouchObject
                or GimmickEventType.ObjectTap
                or GimmickEventType.AreaEnter
                or GimmickEventType.InRoomPortalUsed =>
                string.IsNullOrEmpty(trigger.TargetId) || trigger.TargetId == ctx.ObjectId,

            // タイマー: インデックスと目標秒を確認
            GimmickEventType.TimerReached =>
                trigger.TargetId == ctx.TimerIndex.ToString()
                && Math.Abs(trigger.TimerTargetSeconds - ctx.TimerTargetSeconds) < 0.001,

            // その他は EventType 一致のみ
            _ => true,
        };
    }

    // ── 条件評価（AND 結合） ──────────────────────────────────────────────────

    private bool EvaluateConditions(RuntimeGimmickRule rule, ref GimmickEventContext ctx)
    {
        foreach (var cond in rule.Conditions)
        {
            if (!EvaluateCondition(cond, ref ctx))
                return false;
        }
        return true;
    }

    private bool EvaluateCondition(RuntimeGimmickCondition cond, ref GimmickEventContext ctx)
    {
        switch (cond.Type)
        {
            case GimmickConditionType.WorldStateCompare:
            {
                int lhs = _state.GetWorldState(cond.StateIndex);
                int rhs = _resolver.Resolve(cond.ThresholdRef, ctx, _allPlayerIds);
                return GimmickValueResolver.Evaluate(lhs, cond.Op, rhs, cond.ModBy, cond.ModResult);
            }

            case GimmickConditionType.PlayerStateCompare:
            {
                string playerId = ResolvePlayerId(cond.PlayerTarget, ctx);
                int lhs = _state.GetPlayerState(playerId, cond.StateIndex);
                int rhs = _resolver.Resolve(cond.ThresholdRef, ctx, _allPlayerIds);
                return GimmickValueResolver.Evaluate(lhs, cond.Op, rhs, cond.ModBy, cond.ModResult);
            }

            case GimmickConditionType.PlayerCount:
            {
                int count = _allPlayerIds.Count;
                int rhs = _resolver.Resolve(cond.ThresholdRef, ctx, _allPlayerIds);
                return GimmickValueResolver.Evaluate(count, cond.Op, rhs);
            }

            case GimmickConditionType.PlayerNumber:
            {
                string playerId = ResolvePlayerId(cond.PlayerTarget, ctx);
                int number = FindPlayerIndex(_allPlayerIds, playerId) + 1; // 1-origin
                int rhs = _resolver.Resolve(cond.ThresholdRef, ctx, _allPlayerIds);
                return GimmickValueResolver.Evaluate(number, cond.Op, rhs);
            }

            case GimmickConditionType.HasInventoryObject:
            {
                string playerId = ResolvePlayerId(cond.PlayerTarget, ctx);
                return _inventory.HasObject(playerId, cond.ObjectId);
            }

            // 物理判定は IPhysicsQuery に委譲。判定相手が相手プレイヤーになる（仕様 9.6）
            case GimmickConditionType.PlayersOverlapping:
            {
                bool hit = _physics.ArePlayersOverlapping(ctx.InputPlayerId, out string opponent);
                if (hit && !ctx.HasOpponent)
                    ctx = ctx.WithOpponent(opponent);
                return hit;
            }

            case GimmickConditionType.PlayerDistance:
            {
                bool hit = _physics.FindNearestPlayer(
                    ctx.InputPlayerId, cond.PhysicsDistance, out string opponent);
                if (hit && !ctx.HasOpponent)
                    ctx = ctx.WithOpponent(opponent);
                return hit;
            }

            case GimmickConditionType.PlayerLineOfSight:
            {
                bool hit = _physics.RaycastToPlayer(
                    ctx.InputPlayerId, cond.PhysicsDistance, out string opponent);
                if (hit && !ctx.HasOpponent)
                    ctx = ctx.WithOpponent(opponent);
                return hit;
            }

            default:
                return true;
        }
    }

    // ── アクション実行 ────────────────────────────────────────────────────────

    private void ExecuteAction(
        RuntimeGimmickAction action, GimmickEventContext ctx, List<GimmickEffect> effects)
    {
        switch (action.Type)
        {
            case GimmickActionType.SetWorldState:
            {
                int delta = _resolver.Resolve(action.ValueRef, ctx, _allPlayerIds);
                _state.ApplyWorldState(action.StateIndex, action.StateOp, delta);
                effects.Add(new WorldStateChangedEffect(
                    action.StateIndex, _state.GetWorldState(action.StateIndex)));
                break;
            }

            case GimmickActionType.SetPlayerState:
            {
                int delta = _resolver.Resolve(action.ValueRef, ctx, _allPlayerIds);
                foreach (var pid in ResolvePlayerIds(action.PlayerTarget, ctx))
                {
                    _state.ApplyPlayerState(pid, action.StateIndex, action.StateOp, delta);
                    effects.Add(new PlayerStateChangedEffect(
                        pid, action.StateIndex, _state.GetPlayerState(pid, action.StateIndex)));
                }
                break;
            }

            case GimmickActionType.TimerStart:
                _timers.Start(action.TimerIndex);
                effects.Add(new TimerOperationEffect(action.TimerIndex, TimerOperationEffect.Op.Start));
                break;

            case GimmickActionType.TimerStop:
                _timers.Stop(action.TimerIndex);
                effects.Add(new TimerOperationEffect(action.TimerIndex, TimerOperationEffect.Op.Stop));
                break;

            case GimmickActionType.TimerReset:
                _timers.Reset(action.TimerIndex);
                effects.Add(new TimerOperationEffect(action.TimerIndex, TimerOperationEffect.Op.Reset));
                break;

            case GimmickActionType.ShowHideObject:
                effects.Add(new ObjectVisibilityEffect(action.TargetId, action.BoolParam));
                break;

            case GimmickActionType.ChangeObjectType:
                effects.Add(new ObjectTypeChangedEffect(action.TargetId, action.StringParam));
                break;

            case GimmickActionType.ShowMessage:
                foreach (var pid in ResolvePlayerIds(action.PlayerTarget, ctx))
                    effects.Add(new ShowMessageEffect(pid, action.StringParam));
                break;

            case GimmickActionType.PickupObject:
                foreach (var pid in ResolvePlayerIds(action.PlayerTarget, ctx))
                    effects.Add(new PickupObjectEffect(pid, action.TargetId));
                break;

            case GimmickActionType.PlaySound:
                effects.Add(new PlaySoundEffect(action.TargetId, action.FloatParam));
                break;

            case GimmickActionType.SwitchBgm:
                effects.Add(new SwitchBgmEffect(action.TargetId));
                break;

            case GimmickActionType.MoveObject:
                effects.Add(new ObjectMoveEffect(
                    action.TargetId, action.PositionParam, action.FloatParam));
                break;

            case GimmickActionType.TeleportPlayer:
                foreach (var pid in ResolvePlayerIds(action.PlayerTarget, ctx))
                    effects.Add(new TeleportPlayerEffect(pid, action.TargetId));
                break;

            case GimmickActionType.ResetState:
                ApplyResetState(action.ResetTarget, ctx);
                effects.Add(new StateResetEffect(
                    action.ResetTarget, ResetTargetPlayerId(action.ResetTarget, ctx)));
                break;

            case GimmickActionType.PlayEffect:
                foreach (var pid in ResolvePlayerIds(action.PlayerTarget, ctx))
                    effects.Add(new PlayEffectEffect(pid, action.TargetId));
                break;
        }
    }

    // ── ステートリセット ──────────────────────────────────────────────────────

    private void ApplyResetState(ResetTarget target, GimmickEventContext ctx)
    {
        switch (target)
        {
            case ResetTarget.InputPlayer:
                _state.ResetPlayerStates(ctx.InputPlayerId);
                break;
            case ResetTarget.OpponentPlayer when ctx.HasOpponent:
                _state.ResetPlayerStates(ctx.OpponentPlayerId);
                break;
            case ResetTarget.AllPlayers:
                foreach (var pid in _allPlayerIds)
                    _state.ResetPlayerStates(pid);
                break;
            case ResetTarget.World:
                _state.ResetWorldStates();
                break;
            case ResetTarget.All:
                _state.ResetAll();
                break;
        }
    }

    // ── ヘルパー ──────────────────────────────────────────────────────────────

    private static int FindPlayerIndex(IReadOnlyList<string> list, string id)
    {
        for (int i = 0; i < list.Count; i++)
            if (list[i] == id) return i;
        return -1;
    }

    private string ResolvePlayerId(PlayerTarget target, GimmickEventContext ctx) =>
        target switch
        {
            PlayerTarget.InputPlayer => ctx.InputPlayerId,
            PlayerTarget.OpponentPlayer => ctx.HasOpponent ? ctx.OpponentPlayerId : ctx.InputPlayerId,
            _ => ctx.InputPlayerId,
        };

    private static string ResetTargetPlayerId(ResetTarget target, GimmickEventContext ctx) =>
        target switch
        {
            ResetTarget.InputPlayer => ctx.InputPlayerId,
            ResetTarget.OpponentPlayer => ctx.HasOpponent ? ctx.OpponentPlayerId : ctx.InputPlayerId,
            _ => "",
        };

    private IEnumerable<string> ResolvePlayerIds(PlayerTarget target, GimmickEventContext ctx)
    {
        if (target == PlayerTarget.AllPlayers)
            return _allPlayerIds;
        return new[] { ResolvePlayerId(target, ctx) };
    }
}

// ── 物理クエリインターフェース（Unity 側で実装）──────────────────────────────

/// <summary>
/// 物理判定が必要なギミック条件の抽象インターフェース。
/// EditMode テストでは NullPhysicsQuery（常に false）を使用する。
/// </summary>
public interface IPhysicsQuery
{
    bool ArePlayersOverlapping(string playerId, out string opponentId);
    bool FindNearestPlayer(string playerId, float maxDistance, out string opponentId);
    bool RaycastToPlayer(string playerId, float maxDistance, out string opponentId);
}

public sealed class NullPhysicsQuery : IPhysicsQuery
{
    public static readonly NullPhysicsQuery Instance = new();

    public bool ArePlayersOverlapping(string playerId, out string opponentId)
    {
        opponentId = null;
        return false;
    }

    public bool FindNearestPlayer(string playerId, float maxDistance, out string opponentId)
    {
        opponentId = null;
        return false;
    }

    public bool RaycastToPlayer(string playerId, float maxDistance, out string opponentId)
    {
        opponentId = null;
        return false;
    }
}

// ── インベントリクエリインターフェース（インベントリシステム実装時に接続）────

/// <summary>
/// 「特定のオブジェクトを持っているか」条件（world-creation.md セクション 9.3）の
/// 抽象インターフェース。EditMode テストでは NullInventoryQuery（常に false）を使用する。
/// </summary>
public interface IInventoryQuery
{
    bool HasObject(string playerId, string objectTypeId);
}

public sealed class NullInventoryQuery : IInventoryQuery
{
    public static readonly NullInventoryQuery Instance = new();

    public bool HasObject(string playerId, string objectTypeId) => false;
}
