using System.Collections.Generic;

/// <summary>
/// 公開前の「内部テストプレイ」によるギミック無限ループ検出
/// （world-creation.md 9.10 / screens-and-modes.md 11.7.6）。
///
/// ルームに入った瞬間に発火する <see cref="GimmickEventType.RoomStart"/> を起点に、変換済みルールを
/// 実 <see cref="GimmickEngine"/> で一度走らせ、連鎖が <see cref="GimmickEngine.MaxChainCount"/> を超える
/// （= 無限ループ）かどうかを判定する。サブルーチンの無限再帰など、入室直後に成立してしまうループを
/// 公開前に機械的に検出し、原因ルール（<see cref="GimmickExecutionResult.LoopRuleId"/>）を特定する。
///
/// 注: ルームの初期状態（ステート初期値）からの RoomStart 起点シミュレーションのため、特定の
/// プレイヤー操作やステート遷移を経て初めて成立するループまでは検出しきれない。それらは実際の
/// テストプレイ中にエンジンがライブ検出する（9.10）。本プリチェックは公開フローの自動 backstop。
/// </summary>
public static class GimmickLoopPrecheck
{
    public class Result
    {
        public bool HasLoop { get; }

        /// <summary>ループの原因となったルール ID（HasLoop=false のとき ""）。</summary>
        public string LoopRuleId { get; }

        public Result(bool hasLoop, string loopRuleId)
        {
            HasLoop = hasLoop;
            LoopRuleId = loopRuleId ?? "";
        }

        public static readonly Result None = new Result(false, "");
    }

    /// <summary>
    /// RoomStart を起点に内部テストプレイを実行してループを検出する。
    /// </summary>
    /// <param name="rules">変換済みランタイムルール（<see cref="GimmickRuleConverter.Convert"/> の結果）。</param>
    /// <param name="worldInitials">ワールドステート初期値（任意・null は全 0）。</param>
    /// <param name="playerInitials">プレイヤーステート初期値（任意・null は全 0）。</param>
    /// <param name="simulatedPlayerIds">シミュレーション用プレイヤー ID（任意・既定で 1 人）。</param>
    public static Result RunRoomStart(
        IReadOnlyList<RuntimeGimmickRule> rules,
        int[] worldInitials = null,
        int[] playerInitials = null,
        IReadOnlyList<string> simulatedPlayerIds = null)
    {
        if (rules == null || rules.Count == 0)
            return Result.None;

        var players = simulatedPlayerIds != null && simulatedPlayerIds.Count > 0
            ? simulatedPlayerIds
            : new[] { "sim_player" };

        var state = new GimmickStateManager(worldInitials, playerInitials);
        var timers = new GimmickTimerLogic();
        // 乱数は決定的に最小値を返す。物理 / インベントリ判定はエンジン既定の Null 実装で十分。
        var engine = new GimmickEngine(rules, state, timers, players, (min, max) => min);

        var result = engine.Fire(GimmickEventContext.RoomStart());
        return result.IsInfiniteLoop ? new Result(true, result.LoopRuleId) : Result.None;
    }
}
