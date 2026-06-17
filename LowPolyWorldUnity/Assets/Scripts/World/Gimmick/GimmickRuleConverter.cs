using System.Collections.Generic;

/// <summary>
/// ワールド定義 JSON のギミックルールをランタイム表現（RuntimeGimmickRule）へ変換する
/// ロジッククラス（world-creation.md セクション 9.11）。
///
/// ワールド定義は UGC 由来のため、クライアント読み込み時に防御的に再検証し、
/// 不正なルールは**そのルールのみ無効化**して読み込みを継続する
/// （ワールド全体の読み込みは拒否しない）。無効化したルールは理由付きで返すので、
/// 呼び出し側でログに記録する。サーバー側（Go API）は保存・公開時に同等の検証で拒否する。
/// </summary>
public static class GimmickRuleConverter
{
    public const int MaxRules = 100;
    public const int MaxTriggers = 20;
    public const int MaxConditions = 20;
    public const int MaxActions = 20;
    public const int MaxMessageLength = 80;
    public const float MaxDistanceGrid = 126f;
    public const float GridToMeters = 0.5f; // 1 グリッド = 0.5m

    /// <summary>無効化されたルールの情報（ログ用）。</summary>
    public class InvalidRule
    {
        public string RuleId { get; }
        public string Label { get; }
        public IReadOnlyList<string> Reasons { get; }

        public InvalidRule(string ruleId, string label, IReadOnlyList<string> reasons)
        {
            RuleId = ruleId ?? "";
            Label = label ?? "";
            Reasons = reasons ?? System.Array.Empty<string>();
        }
    }

    /// <summary>変換結果。Rules = 有効ルール（定義順維持）/ InvalidRules = 無効化したルール。</summary>
    public class Result
    {
        public IReadOnlyList<RuntimeGimmickRule> Rules { get; }
        public IReadOnlyList<InvalidRule> InvalidRules { get; }

        public Result(IReadOnlyList<RuntimeGimmickRule> rules, IReadOnlyList<InvalidRule> invalid)
        {
            Rules = rules;
            InvalidRules = invalid;
        }
    }

    /// <summary>
    /// 実在チェック用の参照 ID 集合。null のフィールドはそのチェックをスキップする
    /// （対応システム未実装の段階でも変換できるようにするため）。
    /// </summary>
    public class WorldRefs
    {
        public HashSet<string> ObjectInstanceIds; // 配置オブジェクト・エリア・入口ポータルのインスタンス ID
        public HashSet<string> ObjectTypeIds;     // オブジェクト種別 + 保存バリアント ID
        public HashSet<string> ExitPortalIds;     // ルーム内出口ポータル ID
        public HashSet<string> EffectIds;         // 内蔵エフェクト ID
        public HashSet<string> SoundIds;          // 内蔵効果音 / BGM トラック ID
        public HashSet<string> MarkerIds;         // 内蔵頭上マーカー ID
        public HashSet<string> ConversationIds;   // 会話 ID（9.13）
    }

    // ── 変換 ──────────────────────────────────────────────────────────────────

    public static Result Convert(GimmickRule[] jsonRules, WorldRefs refs = null)
    {
        var rules = new List<RuntimeGimmickRule>();
        var invalid = new List<InvalidRule>();
        if (jsonRules == null)
            return new Result(rules, invalid);

        for (int i = 0; i < jsonRules.Length; i++)
        {
            var json = jsonRules[i];
            if (json == null)
                continue;

            var errors = new List<string>();
            if (i >= MaxRules)
                errors.Add($"ルール数が上限 {MaxRules} を超えています");

            var rule = ConvertRule(json, refs, errors);
            if (errors.Count == 0)
                rules.Add(rule);
            else
                invalid.Add(new InvalidRule(json.ruleId, json.label, errors));
        }

        return new Result(rules, invalid);
    }

    private static RuntimeGimmickRule ConvertRule(
        GimmickRule json, WorldRefs refs, List<string> errors)
    {
        var triggers = json.triggers ?? System.Array.Empty<GimmickTrigger>();
        var conditions = json.conditions ?? System.Array.Empty<GimmickCondition>();
        var actions = json.actions ?? System.Array.Empty<GimmickAction>();

        if (triggers.Length < 1 || triggers.Length > MaxTriggers)
            errors.Add($"入力イベントは 1〜{MaxTriggers} 個必要です（{triggers.Length} 個）");
        if (conditions.Length > MaxConditions)
            errors.Add($"条件は最大 {MaxConditions} 個です（{conditions.Length} 個）");
        if (actions.Length < 1 || actions.Length > MaxActions)
            errors.Add($"アクションは 1〜{MaxActions} 個必要です（{actions.Length} 個）");

        var rtTriggers = new List<RuntimeGimmickTrigger>();
        foreach (var t in triggers)
        {
            var rt = ConvertTrigger(t, refs, errors);
            if (rt != null) rtTriggers.Add(rt);
        }

        var rtConditions = new List<RuntimeGimmickCondition>();
        foreach (var c in conditions)
        {
            var rc = ConvertCondition(c, refs, errors);
            if (rc != null) rtConditions.Add(rc);
        }

        var rtActions = new List<RuntimeGimmickAction>();
        foreach (var a in actions)
        {
            var ra = ConvertAction(a, refs, errors);
            if (ra != null) rtActions.Add(ra);
        }

        return new RuntimeGimmickRule(json.ruleId, json.label, rtTriggers, rtConditions, rtActions);
    }

    // ── トリガー ──────────────────────────────────────────────────────────────

    private static RuntimeGimmickTrigger ConvertTrigger(
        GimmickTrigger json, WorldRefs refs, List<string> errors)
    {
        if (!TryParseEventType(json.type, out var eventType))
        {
            errors.Add($"不明なイベント種別: \"{json.type}\"");
            return null;
        }

        if (eventType == GimmickEventType.TimerReached)
        {
            if (!IsValidTimerIndex(json.timerIndex))
                errors.Add($"タイマー番号が範囲外です: {json.timerIndex}");
            if (json.timerSeconds < 0f)
                errors.Add($"タイマー到達秒が負です: {json.timerSeconds}");
            return new RuntimeGimmickTrigger(
                eventType, json.timerIndex.ToString(), json.timerSeconds);
        }

        // オブジェクト系イベント: targetId が指定されている場合のみ実在チェック（空 = 全対象）
        if (IsObjectTargetEvent(eventType) && !string.IsNullOrEmpty(json.targetId))
            RequireId(refs?.ObjectInstanceIds, json.targetId, "イベント対象オブジェクト", errors);

        // サブルーチン: targetId（サブルーチン ID）が必須
        if (eventType == GimmickEventType.Called && string.IsNullOrEmpty(json.targetId))
            errors.Add("「呼び出された」イベントのサブルーチン ID が未指定です");

        return new RuntimeGimmickTrigger(eventType, json.targetId ?? "");
    }

    private static bool IsObjectTargetEvent(GimmickEventType type) =>
        type is GimmickEventType.PlayerTouchObject
            or GimmickEventType.ObjectTap
            or GimmickEventType.AreaEnter
            or GimmickEventType.AreaExit
            or GimmickEventType.InRoomPortalUsed;

    // ── 条件 ──────────────────────────────────────────────────────────────────

    private static RuntimeGimmickCondition ConvertCondition(
        GimmickCondition json, WorldRefs refs, List<string> errors)
    {
        if (!TryParseConditionType(json.type, out var condType))
        {
            errors.Add($"不明な条件種別: \"{json.type}\"");
            return null;
        }
        if (!TryParseCompareOp(json.op, out var op))
        {
            errors.Add($"不明な比較演算子: \"{json.op}\"");
            return null;
        }
        if (op == CompareOp.ModEquals && json.modBy < 2)
            errors.Add($"剰余比較の除数は 2 以上が必要です: {json.modBy}");
        if (!TryParsePlayerTarget(json.playerTarget, out var playerTarget))
        {
            errors.Add($"不明な対象プレイヤー: \"{json.playerTarget}\"");
            return null;
        }

        switch (condType)
        {
            case GimmickConditionType.WorldStateCompare:
                if (!IsValidWorldStateIndex(json.stateIndex))
                    errors.Add($"ワールドステート番号が範囲外です: {json.stateIndex}");
                break;
            case GimmickConditionType.PlayerStateCompare:
                if (!IsValidPlayerStateIndex(json.stateIndex))
                    errors.Add($"プレイヤーステート番号が範囲外です: {json.stateIndex}");
                break;
            case GimmickConditionType.PlayerStateRank:
                if (!IsValidPlayerStateIndex(json.stateIndex))
                    errors.Add($"プレイヤーステート番号が範囲外です: {json.stateIndex}");
                if (json.rankWithin < 1)
                    errors.Add($"順位条件の X 位以内は 1 以上が必要です: {json.rankWithin}");
                if (json.rankOrder != "top" && json.rankOrder != "bottom")
                    errors.Add($"順位条件の方向が不正です: \"{json.rankOrder}\"");
                break;
            case GimmickConditionType.TimerCompare:
                if (!IsValidTimerIndex(json.timerIndex))
                    errors.Add($"タイマー番号が範囲外です: {json.timerIndex}");
                break;
            case GimmickConditionType.HasInventoryObject:
                if (string.IsNullOrEmpty(json.objectId))
                    errors.Add("インベントリ条件のオブジェクト種別が未指定です");
                else
                    RequireId(refs?.ObjectTypeIds, json.objectId, "インベントリ条件の種別", errors);
                break;
            case GimmickConditionType.PlayerDistance:
            case GimmickConditionType.PlayerLineOfSight:
                if (json.distanceGrid <= 0f || json.distanceGrid > MaxDistanceGrid)
                    errors.Add($"距離は 0 より大きく {MaxDistanceGrid} グリッド以下が必要です: {json.distanceGrid}");
                break;
        }

        var threshold = ConvertValueRef(json.threshold, errors);

        return new RuntimeGimmickCondition(
            condType,
            stateIndex: json.stateIndex,
            op: op,
            thresholdRef: threshold,
            modBy: json.modBy,
            modResult: json.modResult,
            playerTarget: playerTarget,
            objectId: json.objectId,
            physicsDistance: json.distanceGrid * GridToMeters,
            timerIndex: json.timerIndex,
            rankWithin: json.rankWithin,
            rankFromTop: json.rankOrder != "bottom");
    }

    // ── アクション ────────────────────────────────────────────────────────────

    private static RuntimeGimmickAction ConvertAction(
        GimmickAction json, WorldRefs refs, List<string> errors)
    {
        if (!TryParseActionType(json.type, out var actionType))
        {
            errors.Add($"不明なアクション種別: \"{json.type}\"");
            return null;
        }
        if (!TryParsePlayerTarget(json.playerTarget, out var playerTarget))
        {
            errors.Add($"不明な対象プレイヤー: \"{json.playerTarget}\"");
            return null;
        }
        if (!TryParseStateOp(json.stateOp, out var stateOp))
        {
            errors.Add($"不明なステート演算: \"{json.stateOp}\"");
            return null;
        }
        if (!TryParseResetTarget(json.resetTarget, out var resetTarget))
        {
            errors.Add($"不明なリセット対象: \"{json.resetTarget}\"");
            return null;
        }

        string stringParam = json.stringParam ?? "";

        switch (actionType)
        {
            case GimmickActionType.SetWorldState:
                if (!IsValidWorldStateIndex(json.stateIndex))
                    errors.Add($"ワールドステート番号が範囲外です: {json.stateIndex}");
                break;

            case GimmickActionType.SetPlayerState:
                if (!IsValidPlayerStateIndex(json.stateIndex))
                    errors.Add($"プレイヤーステート番号が範囲外です: {json.stateIndex}");
                break;

            case GimmickActionType.TimerStart:
            case GimmickActionType.TimerStop:
            case GimmickActionType.TimerReset:
                if (!IsValidTimerIndex(json.timerIndex))
                    errors.Add($"タイマー番号が範囲外です: {json.timerIndex}");
                break;

            case GimmickActionType.ShowHideObject:
                RequireId(refs?.ObjectInstanceIds, json.targetId, "表示切替の対象オブジェクト", errors);
                break;

            case GimmickActionType.ChangeObjectType:
                RequireId(refs?.ObjectInstanceIds, json.targetId, "種類切り替えの対象オブジェクト", errors);
                RequireId(refs?.ObjectTypeIds, json.stringParam, "切り替え先の種別", errors);
                break;

            case GimmickActionType.ShowMessage:
            {
                var texts = json.texts ?? System.Array.Empty<GimmickTextJson>();
                if (texts.Length == 0)
                    errors.Add("文字メッセージのテキストが未設定です");
                foreach (var t in texts)
                {
                    if (t == null || string.IsNullOrEmpty(t.text))
                        errors.Add("文字メッセージに空のテキストがあります");
                    else if (t.text.Length > MaxMessageLength)
                        errors.Add($"文字メッセージが {MaxMessageLength} 文字を超えています（{t.lang}: {t.text.Length} 文字）");
                }
                // ランタイムにはデフォルト言語（先頭エントリ）を渡す。
                // 閲覧者ごとの言語解決はメッセージ表示 UI 実装時に対応する。
                if (texts.Length > 0 && texts[0] != null)
                    stringParam = texts[0].text ?? "";
                break;
            }

            case GimmickActionType.PickupObject:
                RequireId(refs?.ObjectInstanceIds, json.targetId, "「持つ」の対象オブジェクト", errors);
                if (playerTarget == PlayerTarget.AllPlayers)
                    errors.Add("「オブジェクトを持つ」の対象に「全員」は指定できません");
                break;

            case GimmickActionType.GrantObject:
                RequireId(refs?.ObjectTypeIds, json.targetId, "「付与する」の種別", errors);
                break;

            case GimmickActionType.PlaySound:
                RequireId(refs?.SoundIds, json.targetId, "効果音", errors);
                if (json.floatParam < 0f || json.floatParam > 100f)
                    errors.Add($"効果音の音量は 0〜100 が必要です: {json.floatParam}");
                if (json.pitch < 0.5f || json.pitch > 2.0f)
                    errors.Add($"効果音のピッチは 0.5〜2.0 が必要です: {json.pitch}");
                if (json.playbackRate < 0.5f || json.playbackRate > 2.0f)
                    errors.Add($"効果音の再生速度は 0.5〜2.0 が必要です: {json.playbackRate}");
                break;

            case GimmickActionType.SwitchBgm:
                // "none" は BGM 停止（仕様 9.8）
                if (json.targetId != "none")
                    RequireId(refs?.SoundIds, json.targetId, "BGM トラック", errors);
                break;

            case GimmickActionType.MoveObject:
                RequireId(refs?.ObjectInstanceIds, json.targetId, "移動の対象オブジェクト", errors);
                if (json.floatParam <= 0f)
                    errors.Add($"移動速度は 0 より大きい値が必要です: {json.floatParam}");
                break;

            case GimmickActionType.TeleportPlayer:
                RequireId(refs?.ExitPortalIds, json.targetId, "ワープ先の出口ポータル", errors);
                break;

            case GimmickActionType.PlayEffect:
                RequireId(refs?.EffectIds, json.targetId, "エフェクト", errors);
                break;

            case GimmickActionType.SetMoveSpeed:
                if (json.floatParam < 0f || json.floatParam > 200f)
                    errors.Add($"移動速度変更は 0〜200% が必要です: {json.floatParam}");
                break;

            case GimmickActionType.SetPlayerMarker:
                // 非表示（visible=false）はマーカー ID 不要
                if (json.visible)
                    RequireId(refs?.MarkerIds, json.targetId, "頭上マーカー", errors);
                break;

            case GimmickActionType.StartConversation:
                RequireId(refs?.ConversationIds, json.targetId, "会話", errors);
                break;

            case GimmickActionType.Wait:
                if (json.floatParam < 0f || json.floatParam > 60f)
                    errors.Add($"待機の秒数は 0〜60 が必要です: {json.floatParam}");
                break;

            case GimmickActionType.CallSubroutine:
                if (string.IsNullOrEmpty(json.targetId))
                    errors.Add("「サブルーチンを呼ぶ」のサブルーチン ID が未指定です");
                break;
        }

        var valueRef = ConvertValueRef(json.value, errors);

        return new RuntimeGimmickAction(
            actionType,
            stateIndex: json.stateIndex,
            stateOp: stateOp,
            valueRef: valueRef,
            playerTarget: playerTarget,
            targetId: json.targetId,
            stringParam: stringParam,
            boolParam: json.visible,
            timerIndex: json.timerIndex,
            floatParam: json.floatParam,
            // JSON はグリッド整数（0.5m 単位）・ランタイムはメートル
            positionParam: json.position?.ToVector3(WorldDefinition.PositionUnit) ?? default,
            resetTarget: resetTarget);
    }

    // ── 値参照 ────────────────────────────────────────────────────────────────

    private static ValueRef ConvertValueRef(GimmickValueJson json, List<string> errors)
    {
        if (json == null)
            return ValueRef.Fixed(0);

        switch (json.kind)
        {
            case "fixed":
                return ValueRef.Fixed(json.value);

            case "worldState":
                if (!IsValidWorldStateIndex(json.stateIndex))
                    errors.Add($"値参照のワールドステート番号が範囲外です: {json.stateIndex}");
                return ValueRef.World(json.stateIndex);

            case "playerState":
            {
                if (!IsValidPlayerStateIndex(json.stateIndex))
                    errors.Add($"値参照のプレイヤーステート番号が範囲外です: {json.stateIndex}");
                if (!TryParsePlayerTarget(json.playerTarget, out var target) || target == PlayerTarget.AllPlayers)
                {
                    errors.Add($"値参照の対象プレイヤーが不正です: \"{json.playerTarget}\"");
                    target = PlayerTarget.InputPlayer;
                }
                return ValueRef.Player(target, json.stateIndex);
            }

            case "allPlayersSum":
                if (!IsValidPlayerStateIndex(json.stateIndex))
                    errors.Add($"値参照のプレイヤーステート番号が範囲外です: {json.stateIndex}");
                return ValueRef.AllPlayersSum(json.stateIndex);

            case "random":
                if (json.maxIsPlayerCount)
                    return ValueRef.RandomToPlayerCount(json.min);
                if (json.min > json.max)
                    errors.Add($"範囲乱数は 最小値 ≤ 最大値 が必要です: {json.min}〜{json.max}");
                return ValueRef.Random(json.min, json.max);

            default:
                errors.Add($"不明な値参照種別: \"{json.kind}\"");
                return ValueRef.Fixed(0);
        }
    }

    // ── 文字列 → enum 変換 ────────────────────────────────────────────────────

    private static bool TryParseEventType(string s, out GimmickEventType type)
    {
        type = s switch
        {
            "roomStart" => GimmickEventType.RoomStart,
            "playerCountChanged" => GimmickEventType.PlayerCountChanged,
            "playerTouchObject" => GimmickEventType.PlayerTouchObject,
            "objectTap" => GimmickEventType.ObjectTap,
            "areaEnter" => GimmickEventType.AreaEnter,
            "areaExit" => GimmickEventType.AreaExit,
            "timerReached" => GimmickEventType.TimerReached,
            "actionButton" => GimmickEventType.ActionButton,
            "playerTouchPlayer" => GimmickEventType.PlayerTouchPlayer,
            "respawn" => GimmickEventType.Respawn,
            "inRoomPortalUsed" => GimmickEventType.InRoomPortalUsed,
            "called" => GimmickEventType.Called,
            _ => (GimmickEventType)(-1),
        };
        return (int)type >= 0;
    }

    private static bool TryParseConditionType(string s, out GimmickConditionType type)
    {
        type = s switch
        {
            "worldState" => GimmickConditionType.WorldStateCompare,
            "playerState" => GimmickConditionType.PlayerStateCompare,
            "playerStateRank" => GimmickConditionType.PlayerStateRank,
            "playerCount" => GimmickConditionType.PlayerCount,
            "playerNumber" => GimmickConditionType.PlayerNumber,
            "timerCompare" => GimmickConditionType.TimerCompare,
            "hasObject" => GimmickConditionType.HasInventoryObject,
            "playersOverlapping" => GimmickConditionType.PlayersOverlapping,
            "playerDistance" => GimmickConditionType.PlayerDistance,
            "playerLineOfSight" => GimmickConditionType.PlayerLineOfSight,
            _ => (GimmickConditionType)(-1),
        };
        return (int)type >= 0;
    }

    private static bool TryParseActionType(string s, out GimmickActionType type)
    {
        type = s switch
        {
            "setWorldState" => GimmickActionType.SetWorldState,
            "setPlayerState" => GimmickActionType.SetPlayerState,
            "timerStart" => GimmickActionType.TimerStart,
            "timerStop" => GimmickActionType.TimerStop,
            "timerReset" => GimmickActionType.TimerReset,
            "showHideObject" => GimmickActionType.ShowHideObject,
            "changeObjectType" => GimmickActionType.ChangeObjectType,
            "showMessage" => GimmickActionType.ShowMessage,
            "pickupObject" => GimmickActionType.PickupObject,
            "grantObject" => GimmickActionType.GrantObject,
            "playSound" => GimmickActionType.PlaySound,
            "switchBgm" => GimmickActionType.SwitchBgm,
            "moveObject" => GimmickActionType.MoveObject,
            "teleportPlayer" => GimmickActionType.TeleportPlayer,
            "resetState" => GimmickActionType.ResetState,
            "playEffect" => GimmickActionType.PlayEffect,
            "setMoveSpeed" => GimmickActionType.SetMoveSpeed,
            "setPlayerMarker" => GimmickActionType.SetPlayerMarker,
            "startConversation" => GimmickActionType.StartConversation,
            "wait" => GimmickActionType.Wait,
            "callSubroutine" => GimmickActionType.CallSubroutine,
            _ => (GimmickActionType)(-1),
        };
        return (int)type >= 0;
    }

    private static bool TryParseCompareOp(string s, out CompareOp op)
    {
        op = s switch
        {
            "eq" => CompareOp.Equal,
            "ne" => CompareOp.NotEqual,
            "gt" => CompareOp.GreaterThan,
            "lt" => CompareOp.LessThan,
            "gte" => CompareOp.GreaterOrEqual,
            "lte" => CompareOp.LessOrEqual,
            "mod_eq" => CompareOp.ModEquals,
            _ => (CompareOp)(-1),
        };
        return (int)op >= 0;
    }

    private static bool TryParsePlayerTarget(string s, out PlayerTarget target)
    {
        target = s switch
        {
            "input" or "" or null => PlayerTarget.InputPlayer,
            "opponent" => PlayerTarget.OpponentPlayer,
            "all" => PlayerTarget.AllPlayers,
            _ => (PlayerTarget)(-1),
        };
        return (int)target >= 0;
    }

    private static bool TryParseStateOp(string s, out StateOp op)
    {
        op = s switch
        {
            "set" or "" or null => StateOp.Set,
            "add" => StateOp.Add,
            "sub" => StateOp.Subtract,
            _ => (StateOp)(-1),
        };
        return (int)op >= 0;
    }

    private static bool TryParseResetTarget(string s, out ResetTarget target)
    {
        target = s switch
        {
            "input" => ResetTarget.InputPlayer,
            "opponent" => ResetTarget.OpponentPlayer,
            "allPlayers" => ResetTarget.AllPlayers,
            "world" => ResetTarget.World,
            "all" or "" or null => ResetTarget.All,
            _ => (ResetTarget)(-1),
        };
        return (int)target >= 0;
    }

    // ── 共通ヘルパー ──────────────────────────────────────────────────────────

    private static bool IsValidWorldStateIndex(int i) =>
        (uint)i < GimmickStateManager.MaxWorldStates;

    private static bool IsValidPlayerStateIndex(int i) =>
        (uint)i < GimmickStateManager.MaxPlayerStates;

    private static bool IsValidTimerIndex(int i) => (uint)i < GimmickTimerLogic.MaxTimers;

    // ID の実在チェック。validIds が null の場合はスキップ（チェック対象システム未実装時）
    private static void RequireId(
        HashSet<string> validIds, string id, string what, List<string> errors)
    {
        if (string.IsNullOrEmpty(id))
        {
            errors.Add($"{what}が未指定です");
            return;
        }
        if (validIds != null && !validIds.Contains(id))
            errors.Add($"{what}が存在しません: \"{id}\"");
    }
}
