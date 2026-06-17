using System;
using System.Collections.Generic;

/// <summary>
/// ルール編集画面（screens-and-modes.md 11.7.4）で、選択した入力イベント / 条件 / アクションの
/// 種別ごとに**どのパラメータ入力欄を出すか**を決める純粋 C# スキーマ。
///
/// UI（<see cref="RuleEditController"/>）はここが返すトークン列を順に展開して入力欄を生成する。
/// データのバインド先（<see cref="GimmickTrigger"/> / <see cref="GimmickCondition"/> /
/// <see cref="GimmickAction"/> のフィールド）は各トークンが一意に対応する。妥当性の最終検証は
/// 保存・公開時に <see cref="GimmickRuleConverter"/> が行う（ここは表示する欄の決定のみ）。
/// </summary>
public static class GimmickParamSchema
{
    /// <summary>各入力欄の種類。1 トークン = 1 入力欄（バインド先フィールドまで一意）。</summary>
    public enum Param
    {
        // ── 入力イベント ──
        TrigObjectId,     // targetId（対象オブジェクト・空 = 全て）
        TrigAreaId,       // targetId（対象エリア）
        TrigTimerIndex,   // timerIndex（タイマー番号）
        TrigTimerSeconds, // timerSeconds（到達秒）
        TrigSubroutineId, // targetId（サブルーチン ID）

        // ── 条件 ──
        CondWorldStateIndex,  // stateIndex（ワールドステート番号）
        CondPlayerStateIndex, // stateIndex（プレイヤーステート番号）
        CondTimerIndex,       // timerIndex
        CondCompareOp,        // op（比較演算）
        CondThreshold,        // threshold（値参照）
        CondPlayerTarget,     // playerTarget
        CondInventoryType,    // objectId（オブジェクト種別 ID）
        CondDistanceGrid,     // distanceGrid（グリッド距離）

        // ── アクション ──
        ActWorldStateIndex,   // stateIndex（ワールドステート番号）
        ActPlayerStateIndex,  // stateIndex（プレイヤーステート番号）
        ActStateOp,           // stateOp（代入 / 加算 / 減算）
        ActValue,             // value（値参照）
        ActPlayerTarget,      // playerTarget
        ActTimerIndex,        // timerIndex
        ActObjectId,          // targetId（対象オブジェクト）
        ActVisible,           // visible（表示する / 隠す）
        ActChangeTypeId,      // stringParam（切り替え先の種別 ID）
        ActGrantTypeId,       // targetId（付与する種別 ID）
        ActSoundId,           // targetId（効果音 ID）
        ActVolume,            // floatParam（音量 0〜100）
        ActPitch,             // pitch（0.5〜2.0）
        ActPlaybackRate,      // playbackRate（0.5〜2.0）
        ActBgmId,             // targetId（BGM ID・none で停止）
        ActMovePosition,      // position（移動先グリッド座標）
        ActMoveSpeed,         // floatParam（移動速度）
        ActPortalExitId,      // targetId（出口ポータル ID）
        ActResetTarget,       // resetTarget
        ActEffectId,          // targetId（エフェクト ID）
        ActMoveSpeedPercent,  // floatParam（移動速度 0〜200%）
        ActMarkerId,          // targetId（頭上マーカー ID）
        ActConversationId,    // targetId（会話 ID）
        ActSubroutineId,      // targetId（サブルーチン ID）
        ActWaitSeconds,       // floatParam（待機秒数 0〜60）
        ActMessage,           // texts（文字メッセージ・言語別）
    }

    private static readonly Param[] Empty = Array.Empty<Param>();

    // ── 入力イベント ──────────────────────────────────────────────────────────

    public static IReadOnlyList<Param> ForTrigger(string type) => type switch
    {
        "playerTouchObject" or "objectTap" or "inRoomPortalUsed" => new[] { Param.TrigObjectId },
        "areaEnter" or "areaExit" => new[] { Param.TrigAreaId },
        "timerReached" => new[] { Param.TrigTimerIndex, Param.TrigTimerSeconds },
        "called" => new[] { Param.TrigSubroutineId },
        // roomStart / playerCountChanged / respawn / actionButton / playerTouchPlayer はパラメータなし
        _ => Empty,
    };

    // ── 条件 ──────────────────────────────────────────────────────────────────

    public static IReadOnlyList<Param> ForCondition(string type) => type switch
    {
        "worldState" => new[] { Param.CondWorldStateIndex, Param.CondCompareOp, Param.CondThreshold },
        "playerState" => new[]
        {
            Param.CondPlayerStateIndex, Param.CondPlayerTarget, Param.CondCompareOp, Param.CondThreshold,
        },
        "playerCount" => new[] { Param.CondCompareOp, Param.CondThreshold },
        "playerNumber" => new[] { Param.CondPlayerTarget, Param.CondCompareOp, Param.CondThreshold },
        "timerCompare" => new[] { Param.CondTimerIndex, Param.CondCompareOp, Param.CondThreshold },
        "hasObject" => new[] { Param.CondInventoryType, Param.CondPlayerTarget },
        "playerDistance" or "playerLineOfSight" => new[] { Param.CondPlayerTarget, Param.CondDistanceGrid },
        // playersOverlapping はパラメータなし
        _ => Empty,
    };

    /// <summary>剰余比較（mod_eq）の追加欄（除数・余り）を出す条件種別か。</summary>
    public static bool SupportsModParams(string conditionType) =>
        conditionType is "worldState" or "playerState" or "timerCompare";

    // ── アクション ────────────────────────────────────────────────────────────

    public static IReadOnlyList<Param> ForAction(string type) => type switch
    {
        "setWorldState" => new[] { Param.ActWorldStateIndex, Param.ActStateOp, Param.ActValue },
        "setPlayerState" => new[]
        {
            Param.ActPlayerStateIndex, Param.ActPlayerTarget, Param.ActStateOp, Param.ActValue,
        },
        "timerStart" or "timerStop" or "timerReset" => new[] { Param.ActTimerIndex },
        "showHideObject" => new[] { Param.ActObjectId, Param.ActVisible },
        "changeObjectType" => new[] { Param.ActObjectId, Param.ActChangeTypeId },
        "showMessage" => new[] { Param.ActPlayerTarget, Param.ActMessage },
        "pickupObject" => new[] { Param.ActObjectId, Param.ActPlayerTarget },
        "grantObject" => new[] { Param.ActGrantTypeId, Param.ActPlayerTarget },
        "playSound" => new[] { Param.ActSoundId, Param.ActVolume, Param.ActPitch, Param.ActPlaybackRate },
        "switchBgm" => new[] { Param.ActBgmId },
        "moveObject" => new[] { Param.ActObjectId, Param.ActMovePosition, Param.ActMoveSpeed },
        "teleportPlayer" => new[] { Param.ActPortalExitId, Param.ActPlayerTarget },
        "resetState" => new[] { Param.ActResetTarget },
        "playEffect" => new[] { Param.ActEffectId, Param.ActPlayerTarget },
        "setMoveSpeed" => new[] { Param.ActMoveSpeedPercent, Param.ActPlayerTarget },
        "setPlayerMarker" => new[] { Param.ActVisible, Param.ActMarkerId, Param.ActPlayerTarget },
        "startConversation" => new[] { Param.ActConversationId, Param.ActPlayerTarget },
        "wait" => new[] { Param.ActWaitSeconds },
        "callSubroutine" => new[] { Param.ActSubroutineId },
        _ => Empty,
    };

    // ── 選択肢ラベル（ドロップダウン表示用・正規 ID は GimmickRuleEditLogic と一致）──────

    public static string CompareOpLabel(string id) => CompareOpLabels.TryGetValue(id, out var l) ? l : id;
    public static string PlayerTargetLabel(string id) => PlayerTargetLabels.TryGetValue(id, out var l) ? l : id;
    public static string StateOpLabel(string id) => StateOpLabels.TryGetValue(id, out var l) ? l : id;
    public static string ResetTargetLabel(string id) => ResetTargetLabels.TryGetValue(id, out var l) ? l : id;
    public static string ValueKindLabel(string id) => ValueKindLabels.TryGetValue(id, out var l) ? l : id;

    private static readonly Dictionary<string, string> CompareOpLabels = new()
    {
        { "eq", "＝ 等しい" },
        { "ne", "≠ 等しくない" },
        { "gt", "＞ より大きい" },
        { "lt", "＜ より小さい" },
        { "gte", "≧ 以上" },
        { "lte", "≦ 以下" },
        { "mod_eq", "X で割った余りが Y" },
    };

    private static readonly Dictionary<string, string> PlayerTargetLabels = new()
    {
        { "input", "入力したプレイヤー" },
        { "opponent", "相手プレイヤー" },
        { "all", "全員" },
    };

    private static readonly Dictionary<string, string> StateOpLabels = new()
    {
        { "set", "代入 (=)" },
        { "add", "加算 (+)" },
        { "sub", "減算 (−)" },
    };

    private static readonly Dictionary<string, string> ResetTargetLabels = new()
    {
        { "input", "入力プレイヤー" },
        { "opponent", "相手プレイヤー" },
        { "allPlayers", "全プレイヤー" },
        { "world", "ワールド" },
        { "all", "すべて" },
    };

    private static readonly Dictionary<string, string> ValueKindLabels = new()
    {
        { "fixed", "固定値" },
        { "worldState", "ワールド変数を参照" },
        { "playerState", "プレイヤー変数を参照" },
        { "allPlayersSum", "全プレイヤー合計" },
        { "random", "範囲乱数" },
    };
}
