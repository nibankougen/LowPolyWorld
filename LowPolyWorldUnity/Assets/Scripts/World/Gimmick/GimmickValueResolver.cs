using System;
using System.Collections.Generic;

/// <summary>
/// ギミックの値参照（ValueRef）を解決するロジッククラス（world-creation.md セクション 9.7）。
/// 固定値 / ワールドステート / プレイヤーステート / 全プレイヤー合計 / 範囲乱数 に対応する。
/// </summary>
public class GimmickValueResolver
{
    private readonly GimmickStateManager _state;
    private readonly Func<int, int, int> _randomProvider; // (min, max) → value（テスト注入可）

    public GimmickValueResolver(
        GimmickStateManager state,
        Func<int, int, int> randomProvider = null)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _randomProvider = randomProvider ?? DefaultRandom;
    }

    /// <summary>
    /// ValueRef をコンテキストに基づいて解決し、整数値を返す。
    /// </summary>
    public int Resolve(ValueRef valueRef, GimmickEventContext ctx, IReadOnlyList<string> allPlayerIds)
    {
        return valueRef.Kind switch
        {
            ValueRefKind.Fixed => valueRef.FixedValue,
            ValueRefKind.WorldState => _state.GetWorldState(valueRef.StateIndex),
            ValueRefKind.PlayerState => ResolvePlayerState(valueRef, ctx),
            ValueRefKind.AllPlayersStateSum => ResolveAllPlayersSum(valueRef, allPlayerIds),
            ValueRefKind.RandomRange => _randomProvider(valueRef.RandomMin, valueRef.RandomMax),
            _ => 0,
        };
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private int ResolvePlayerState(ValueRef valueRef, GimmickEventContext ctx)
    {
        string playerId = valueRef.PlayerTarget switch
        {
            PlayerTarget.InputPlayer => ctx.InputPlayerId,
            PlayerTarget.OpponentPlayer => ctx.OpponentPlayerId ?? ctx.InputPlayerId,
            _ => ctx.InputPlayerId,
        };
        return _state.GetPlayerState(playerId, valueRef.StateIndex);
    }

    private int ResolveAllPlayersSum(ValueRef valueRef, IReadOnlyList<string> allPlayerIds)
    {
        if (allPlayerIds == null) return 0;
        int total = 0;
        foreach (var id in allPlayerIds)
            total += _state.GetPlayerState(id, valueRef.StateIndex);
        return total;
    }

    // 範囲乱数のデフォルト実装。本番ではルームオーナーが生成した値を
    // randomProvider 経由で全プレイヤーに共有する（world-creation.md セクション 9.7）。
    private static readonly Random SharedRandom = new Random();

    private static int DefaultRandom(int min, int max) =>
        min >= max ? min : SharedRandom.Next(min, max + 1);

    // ── 比較演算評価 ──────────────────────────────────────────────────────────

    /// <summary>
    /// 左辺値 (lhs) と閾値 (threshold) を演算子で比較する。
    /// mod_eq の場合は (lhs % modBy) == modResult で判定する。
    /// </summary>
    public static bool Evaluate(int lhs, CompareOp op, int threshold, int modBy = 2, int modResult = 0) =>
        op switch
        {
            CompareOp.Equal => lhs == threshold,
            CompareOp.NotEqual => lhs != threshold,
            CompareOp.GreaterThan => lhs > threshold,
            CompareOp.LessThan => lhs < threshold,
            CompareOp.GreaterOrEqual => lhs >= threshold,
            CompareOp.LessOrEqual => lhs <= threshold,
            CompareOp.ModEquals => modBy >= 2 && lhs % modBy == modResult,
            _ => false,
        };
}
