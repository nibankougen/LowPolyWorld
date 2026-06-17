using System;
using System.Collections.Generic;

/// <summary>
/// ギミックのルール編集画面（screens-and-modes.md 11.7.4）の編集状態を管理する純粋 C# ロジック。
///
/// 担当: 単一 <see cref="GimmickRule"/> の入力イベント（OR・最大 20）・条件（AND・最大 20）・
/// アクション（順次・最大 20）の追加 / 削除 / 並び替え / 種別変更、および文字メッセージの言語別編集。
/// 編集結果は常に対象 <see cref="GimmickRule"/> の配列へ反映する（<see cref="Rule"/> は常に最新）。
///
/// 値参照（<see cref="GimmickValueJson"/>）や各フィールドの細かい数値は、UI が公開オブジェクトを
/// 直接編集する。妥当性の最終検証は保存・公開時に <see cref="GimmickRuleConverter"/> が行う。
/// </summary>
public class GimmickRuleEditLogic
{
    public const int MaxTriggers = GimmickRuleConverter.MaxTriggers;     // 20
    public const int MaxConditions = GimmickRuleConverter.MaxConditions; // 20
    public const int MaxActions = GimmickRuleConverter.MaxActions;       // 20
    public const int MaxMessageLength = GimmickRuleConverter.MaxMessageLength; // 80

    // ── 選択肢（ドロップダウン用の正規 ID 一覧。GimmickRuleConverter のパースと一致させる）──

    public static readonly string[] TriggerTypes =
    {
        "roomStart", "playerCountChanged", "playerTouchObject", "objectTap",
        "areaEnter", "areaExit", "timerReached", "actionButton",
        "playerTouchPlayer", "respawn", "inRoomPortalUsed", "called",
    };

    public static readonly string[] ConditionTypes =
    {
        "worldState", "playerState", "playerStateRank", "playerCount", "playerNumber", "timerCompare",
        "hasObject", "playersOverlapping", "playerDistance", "playerLineOfSight",
    };

    public static readonly string[] ActionTypes =
    {
        "setWorldState", "setPlayerState", "timerStart", "timerStop", "timerReset",
        "showHideObject", "changeObjectType", "showMessage", "pickupObject", "grantObject",
        "playSound", "switchBgm", "moveObject", "teleportPlayer", "resetState",
        "playEffect", "setMoveSpeed", "setPlayerMarker",
        "startConversation", "wait", "callSubroutine",
    };

    public static readonly string[] CompareOps = { "eq", "ne", "gt", "lt", "gte", "lte", "mod_eq" };
    public static readonly string[] PlayerTargets = { "input", "opponent", "all" };
    public static readonly string[] StateOps = { "set", "add", "sub" };
    public static readonly string[] ValueKinds = { "fixed", "worldState", "playerState", "allPlayersSum", "random" };
    public static readonly string[] ResetTargets = { "input", "opponent", "allPlayers", "world", "all" };
    public static readonly string[] RankOrders = { "top", "bottom" }; // 順位条件の方向（大きい方 / 小さい方から）

    private readonly GimmickRule _rule;
    private readonly List<GimmickTrigger> _triggers;
    private readonly List<GimmickCondition> _conditions;
    private readonly List<GimmickAction> _actions;

    public GimmickRuleEditLogic(GimmickRule rule)
    {
        _rule = rule ?? throw new ArgumentNullException(nameof(rule));
        _triggers = new List<GimmickTrigger>(rule.triggers ?? Array.Empty<GimmickTrigger>());
        _conditions = new List<GimmickCondition>(rule.conditions ?? Array.Empty<GimmickCondition>());
        _actions = new List<GimmickAction>(rule.actions ?? Array.Empty<GimmickAction>());
        Sync();
    }

    public GimmickRule Rule => _rule;
    public IReadOnlyList<GimmickTrigger> Triggers => _triggers;
    public IReadOnlyList<GimmickCondition> Conditions => _conditions;
    public IReadOnlyList<GimmickAction> Actions => _actions;

    public bool CanAddTrigger => _triggers.Count < MaxTriggers;
    public bool CanAddCondition => _conditions.Count < MaxConditions;
    public bool CanAddAction => _actions.Count < MaxActions;

    // ── 入力イベント（OR 結合）────────────────────────────────────────────────

    /// <summary>入力イベントを末尾に追加して返す。上限到達時は null。type 省略時は先頭種別。</summary>
    public GimmickTrigger AddTrigger(string type = null)
    {
        if (!CanAddTrigger)
            return null;
        var t = new GimmickTrigger { type = NormalizeType(type, TriggerTypes) };
        _triggers.Add(t);
        Sync();
        return t;
    }

    public bool RemoveTrigger(int index) => RemoveAt(_triggers, index);

    public bool MoveTrigger(int from, int to) => Move(_triggers, from, to);

    /// <summary>入力イベントの種別を変更する。未知の種別は拒否。</summary>
    public bool SetTriggerType(int index, string type)
    {
        if (!IsValid(index, _triggers.Count) || !IsKnown(type, TriggerTypes))
            return false;
        _triggers[index].type = type;
        Sync();
        return true;
    }

    // ── 条件（AND 結合）──────────────────────────────────────────────────────

    /// <summary>条件を末尾に追加して返す。上限到達時は null。type 省略時は先頭種別。</summary>
    public GimmickCondition AddCondition(string type = null)
    {
        if (!CanAddCondition)
            return null;
        var c = new GimmickCondition { type = NormalizeType(type, ConditionTypes) };
        _conditions.Add(c);
        Sync();
        return c;
    }

    public bool RemoveCondition(int index) => RemoveAt(_conditions, index);

    public bool MoveCondition(int from, int to) => Move(_conditions, from, to);

    public bool SetConditionType(int index, string type)
    {
        if (!IsValid(index, _conditions.Count) || !IsKnown(type, ConditionTypes))
            return false;
        _conditions[index].type = type;
        Sync();
        return true;
    }

    // ── アクション（順次実行）────────────────────────────────────────────────

    /// <summary>アクションを末尾に追加して返す。上限到達時は null。type 省略時は先頭種別。</summary>
    public GimmickAction AddAction(string type = null)
    {
        if (!CanAddAction)
            return null;
        var a = new GimmickAction { type = NormalizeType(type, ActionTypes) };
        _actions.Add(a);
        Sync();
        return a;
    }

    public bool RemoveAction(int index) => RemoveAt(_actions, index);

    public bool MoveAction(int from, int to) => Move(_actions, from, to);

    public bool SetActionType(int index, string type)
    {
        if (!IsValid(index, _actions.Count) || !IsKnown(type, ActionTypes))
            return false;
        _actions[index].type = type;
        Sync();
        return true;
    }

    // ── 文字メッセージアクションの言語別テキスト ──────────────────────────────

    /// <summary>
    /// showMessage アクションに言語別テキストを設定する（lang 既存なら上書き・新規なら追加）。
    /// テキストは 80 文字以内に切り詰める。lang 省略（空）= デフォルト言語。
    /// 対象が showMessage でない / index 範囲外 / テキスト空 の場合は false。
    /// </summary>
    public bool SetActionMessage(int actionIndex, string lang, string text)
    {
        if (!IsValid(actionIndex, _actions.Count))
            return false;
        var action = _actions[actionIndex];
        if (action.type != "showMessage")
            return false;
        if (string.IsNullOrEmpty(text))
            return false;

        lang ??= "";
        if (text.Length > MaxMessageLength)
            text = text.Substring(0, MaxMessageLength);

        var texts = new List<GimmickTextJson>(action.texts ?? Array.Empty<GimmickTextJson>());
        var existing = texts.Find(t => t != null && t.lang == lang);
        if (existing != null)
            existing.text = text;
        else
            texts.Add(new GimmickTextJson { lang = lang, text = text });
        action.texts = texts.ToArray();
        return true;
    }

    /// <summary>指定言語の文字メッセージを削除する。存在しなければ false。</summary>
    public bool RemoveActionMessage(int actionIndex, string lang)
    {
        if (!IsValid(actionIndex, _actions.Count))
            return false;
        var action = _actions[actionIndex];
        if (action.type != "showMessage")
            return false;

        lang ??= "";
        var texts = new List<GimmickTextJson>(action.texts ?? Array.Empty<GimmickTextJson>());
        int removed = texts.RemoveAll(t => t != null && t.lang == lang);
        if (removed == 0)
            return false;
        action.texts = texts.ToArray();
        return true;
    }

    // ── 共通ヘルパー ──────────────────────────────────────────────────────────

    private bool RemoveAt<T>(List<T> list, int index)
    {
        if (!IsValid(index, list.Count))
            return false;
        list.RemoveAt(index);
        Sync();
        return true;
    }

    // from の要素を to の位置へ移動する。to は範囲内へクランプ。
    private bool Move<T>(List<T> list, int from, int to)
    {
        if (!IsValid(from, list.Count))
            return false;
        to = to < 0 ? 0 : to >= list.Count ? list.Count - 1 : to;
        if (to == from)
            return true;
        var item = list[from];
        list.RemoveAt(from);
        list.Insert(to, item);
        Sync();
        return true;
    }

    private static bool IsValid(int index, int count) => (uint)index < (uint)count;

    private static bool IsKnown(string type, string[] valid) => Array.IndexOf(valid, type) >= 0;

    // 既知の種別ならそのまま、未知 / 空なら先頭（既定）種別を返す。
    private static string NormalizeType(string type, string[] valid) =>
        IsKnown(type, valid) ? type : valid[0];

    // 内部リストを対象ルールの配列へ書き戻す（Rule を常に最新へ保つ）。
    private void Sync()
    {
        _rule.triggers = _triggers.ToArray();
        _rule.conditions = _conditions.ToArray();
        _rule.actions = _actions.ToArray();
    }
}
