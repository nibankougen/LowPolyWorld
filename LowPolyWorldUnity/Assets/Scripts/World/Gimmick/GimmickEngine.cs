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

    // 待機（9.7b）で中断したシーケンス。Tick で経過時間を反映して再開する。
    private readonly List<PendingSequence> _pending = new();

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

        var effects = new List<GimmickEffect>();
        var st = new ExecState();
        ProcessEvent(ctx, effects, st);
        return Finish(effects, st);
    }

    /// <summary>
    /// 待機（9.7b）で中断したシーケンスの経過時間を進め、遅延が尽きたものを再開する。
    /// オーナーが毎フレーム呼び出し、生成されたエフェクトを全クライアントへ送る想定。
    /// </summary>
    public GimmickExecutionResult Tick(float deltaSeconds)
    {
        var effects = new List<GimmickEffect>();
        var st = new ExecState();

        if (_pending.Count > 0 && deltaSeconds > 0f)
        {
            // 経過を反映し、遅延が尽きたシーケンスを取り出す（再開中の追加 pending は次回 Tick へ）。
            var ready = new List<PendingSequence>();
            foreach (var seq in _pending)
            {
                seq.RemainingDelay -= deltaSeconds;
                if (seq.RemainingDelay <= 0f)
                    ready.Add(seq);
            }
            foreach (var seq in ready)
            {
                _pending.Remove(seq);
                RunActions(seq.RuleId, seq.Actions, seq.NextIndex, seq.Ctx, effects, st);
                if (st.Looped)
                    break;
            }
        }

        return Finish(effects, st);
    }

    /// <summary>進行中（待機中）のシーケンスがあるか。</summary>
    public bool HasPendingSequences => _pending.Count > 0;

    /// <summary>進行中のシーケンスを全て破棄する（退室・ルーム終了・オーナー交代時に呼ぶ — 9.7b）。</summary>
    public void ClearSequences() => _pending.Clear();

    // 無限ループ検出時は進行中シーケンスも破棄してから結果を返す。
    private GimmickExecutionResult Finish(List<GimmickEffect> effects, ExecState st)
    {
        if (st.Looped)
        {
            _pending.Clear();
            return GimmickExecutionResult.InfiniteLoop(st.LoopRuleId);
        }
        return GimmickExecutionResult.Success(effects);
    }

    // 1 つのイベントを全ルールに当てて、一致したルールのアクションを順に実行する。
    private void ProcessEvent(GimmickEventContext ctx, List<GimmickEffect> effects, ExecState st)
    {
        foreach (var rule in _rules)
        {
            if (!MatchesAnyTrigger(rule, ctx))
                continue;

            // 距離・視線・重なり条件は相手プレイヤーを動的に確定する。
            // 確定した相手は同一ルール内の後続条件・アクションにのみ有効。
            var ruleCtx = ctx;
            if (!EvaluateConditions(rule, ref ruleCtx))
                continue;

            RunActions(rule.RuleId, rule.Actions, 0, ruleCtx, effects, st);
            if (st.Looped)
                return;
        }
    }

    // startIndex から順にアクションを実行する。待機（9.7b）で中断し、callSubroutine（9.8）で
    // Called イベントをインラインで連鎖実行する。連鎖の累計が MaxChainCount を超えたらループ判定。
    private void RunActions(
        string ruleId,
        IReadOnlyList<RuntimeGimmickAction> actions,
        int startIndex,
        GimmickEventContext ctx,
        List<GimmickEffect> effects,
        ExecState st)
    {
        for (int i = startIndex; i < actions.Count; i++)
        {
            if (++st.ChainCount > MaxChainCount)
            {
                st.Looped = true;
                st.LoopRuleId = ruleId;
                return;
            }

            var action = actions[i];
            switch (action.Type)
            {
                case GimmickActionType.Wait:
                    // 残りのアクション（i+1 以降）を遅延させて中断する。
                    _pending.Add(new PendingSequence
                    {
                        RuleId = ruleId,
                        Actions = actions,
                        NextIndex = i + 1,
                        Ctx = ctx,
                        RemainingDelay = Math.Max(0f, action.FloatParam),
                    });
                    return;

                case GimmickActionType.CallSubroutine:
                    ProcessEvent(
                        GimmickEventContext.Subroutine(action.TargetId, ctx.InputPlayerId, ctx.OpponentPlayerId),
                        effects, st);
                    if (st.Looped)
                        return;
                    break;

                default:
                    ExecuteAction(action, ctx, effects);
                    break;
            }
        }
    }

    // 連鎖カウントとループ状態を 1 回の Fire / Tick 全体で共有する。
    private sealed class ExecState
    {
        public int ChainCount;
        public bool Looped;
        public string LoopRuleId;
    }

    // 待機で中断したシーケンスの再開情報。
    private sealed class PendingSequence
    {
        public string RuleId;
        public IReadOnlyList<RuntimeGimmickAction> Actions;
        public int NextIndex;
        public GimmickEventContext Ctx;
        public float RemainingDelay;
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
                or GimmickEventType.AreaExit
                or GimmickEventType.InRoomPortalUsed =>
                string.IsNullOrEmpty(trigger.TargetId) || trigger.TargetId == ctx.ObjectId,

            // タイマー: インデックスと目標秒を確認
            GimmickEventType.TimerReached =>
                trigger.TargetId == ctx.TimerIndex.ToString()
                && Math.Abs(trigger.TimerTargetSeconds - ctx.TimerTargetSeconds) < 0.001,

            // サブルーチン: ID（ObjectId に格納）が一致するもののみ
            GimmickEventType.Called => trigger.TargetId == ctx.ObjectId,

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

            case GimmickConditionType.PlayerStateRank:
            {
                string playerId = ResolvePlayerId(cond.PlayerTarget, ctx);
                return IsWithinRank(playerId, cond.StateIndex, cond.RankWithin, cond.RankFromTop);
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

            case GimmickConditionType.TimerCompare:
            {
                int lhs = (int)Math.Floor(_timers.GetElapsed(cond.TimerIndex));
                int rhs = _resolver.Resolve(cond.ThresholdRef, ctx, _allPlayerIds);
                return GimmickValueResolver.Evaluate(lhs, cond.Op, rhs, cond.ModBy, cond.ModResult);
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
            {
                // 1 つの配置オブジェクトは 1 人にしか入らないため対象は単一プレイヤーのみ（仕様 9.8）
                string pid = ResolvePlayerId(action.PlayerTarget, ctx);
                effects.Add(new PickupObjectEffect(pid, action.TargetId, isGrant: false));
                break;
            }

            case GimmickActionType.GrantObject:
                foreach (var pid in ResolvePlayerIds(action.PlayerTarget, ctx))
                    effects.Add(new PickupObjectEffect(pid, action.TargetId, isGrant: true));
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

            case GimmickActionType.SetMoveSpeed:
            {
                float percent = Math.Clamp(action.FloatParam, 0f, 200f);
                foreach (var pid in ResolvePlayerIds(action.PlayerTarget, ctx))
                    effects.Add(new PlayerMoveSpeedEffect(pid, percent));
                break;
            }

            case GimmickActionType.SetPlayerMarker:
                foreach (var pid in ResolvePlayerIds(action.PlayerTarget, ctx))
                    effects.Add(new PlayerMarkerEffect(pid, action.TargetId, action.BoolParam));
                break;

            case GimmickActionType.StartConversation:
                foreach (var pid in ResolvePlayerIds(action.PlayerTarget, ctx))
                    effects.Add(new StartConversationEffect(pid, action.TargetId));
                break;

            // Wait / CallSubroutine は RunActions が処理するためここには来ない。
        }
    }

    // ── ステートリセット ──────────────────────────────────────────────────────

    // エンジンが管理する状態（ステート・タイマー）のみここでリセットする。
    // BGM・オブジェクト表示/種類/位置・インベントリ・移動速度・頭上マーカーは
    // StateResetEffect を受けた上位レイヤーが仕様 9.8 の範囲表に従ってリセットする。
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
                // 「すべて」は全タイマーもリセット（0 で停止・仕様 9.8）
                for (int i = 0; i < GimmickTimerLogic.MaxTimers; i++)
                    _timers.Reset(i);
                // 進行中（待機中）のシーケンスも中断する（9.7b）
                _pending.Clear();
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

    // 対象プレイヤーの指定ステートが全在室者中で上位 / 下位 X 位以内か（9.6・同値は同順位）。
    private bool IsWithinRank(string playerId, int stateIndex, int within, bool fromTop)
    {
        if (string.IsNullOrEmpty(playerId) || within < 1 || _allPlayerIds.Count == 0)
            return false;
        if (FindPlayerIndex(_allPlayerIds, playerId) < 0)
            return false;

        int target = _state.GetPlayerState(playerId, stateIndex);
        // 同値は同順位（自分より「厳密に上位」の人数 + 1 が順位）。
        int ahead = 0;
        foreach (var id in _allPlayerIds)
        {
            int v = _state.GetPlayerState(id, stateIndex);
            if (fromTop ? v > target : v < target)
                ahead++;
        }
        return ahead + 1 <= within;
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
