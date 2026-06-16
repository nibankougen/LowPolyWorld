using System;
using System.Collections.Generic;

/// <summary>
/// 会話定義（world-creation.md 9.13）の保存・公開時バリデーション（9.11）。
///
/// UGC 由来のため構造を検証する。<see cref="ConversationEditLogic"/> がエディタ操作時に上限・長さを
/// 抑えるのに対し、ここはワールド全体としての最終検証（行数・選択肢数・テキスト有無・ジャンプ先実在・
/// ステート番号範囲など）を担う。エラー理由の一覧を返す（空 = 妥当）。
/// </summary>
public static class ConversationValidator
{
    public const int MaxWorldStateIndex = 9; // ワールドステート 0〜9
    public const int MaxPlayerStateIndex = 3; // プレイヤーステート 0〜3

    private static readonly string[] EffectKinds = { "none", "worldState", "playerState" };
    private static readonly string[] StateOps = { "set", "add", "sub" };
    private static readonly string[] PlayerTargets = { "input", "opponent", "all" };

    /// <summary>会話一覧全体を検証する（会話数上限 + 各会話）。</summary>
    public static List<string> ValidateAll(IReadOnlyList<ConversationJson> conversations)
    {
        var errors = new List<string>();
        if (conversations == null)
            return errors;
        if (conversations.Count > ConversationLibraryLogic.MaxConversations)
            errors.Add($"会話数が上限 {ConversationLibraryLogic.MaxConversations} を超えています（{conversations.Count}）");

        var seenIds = new HashSet<string>();
        foreach (var conv in conversations)
        {
            if (conv != null && !string.IsNullOrEmpty(conv.conversationId) && !seenIds.Add(conv.conversationId))
                errors.Add($"会話 ID が重複しています: {conv.conversationId}");
            Validate(conv, errors);
        }
        return errors;
    }

    /// <summary>1 会話を検証してエラー一覧を返す。</summary>
    public static List<string> Validate(ConversationJson conv)
    {
        var errors = new List<string>();
        Validate(conv, errors);
        return errors;
    }

    private static void Validate(ConversationJson conv, List<string> errors)
    {
        if (conv == null)
        {
            errors.Add("会話が null です");
            return;
        }
        string where = string.IsNullOrEmpty(conv.name) ? conv.conversationId : conv.name;
        var lines = conv.lines ?? Array.Empty<ConversationLineJson>();
        if (lines.Length > ConversationEditLogic.MaxLines)
            errors.Add($"会話「{where}」の行数が上限 {ConversationEditLogic.MaxLines} を超えています（{lines.Length}）");

        // ジャンプ先の実在チェック用に行 ID を集める。
        var lineIds = new HashSet<string>();
        foreach (var l in lines)
            if (l != null && !string.IsNullOrEmpty(l.lineId))
                lineIds.Add(l.lineId);

        foreach (var line in lines)
        {
            if (line == null)
            {
                errors.Add($"会話「{where}」に null の行があります");
                continue;
            }

            RequireAnyText(line.texts, ConversationEditLogic.TextMaxLength, $"会話「{where}」の本文", errors);
            CheckLengths(line.speakers, ConversationEditLogic.SpeakerMaxLength, $"会話「{where}」の話者名", errors);
            ValidateEffect(line.onReach, $"会話「{where}」の行到達時", errors);
            ValidateGoto(line.gotoLineId, lineIds, $"会話「{where}」のジャンプ先", errors);

            var choices = line.choices ?? Array.Empty<ConversationChoiceJson>();
            if (choices.Length > ConversationEditLogic.MaxChoices)
                errors.Add($"会話「{where}」の選択肢が上限 {ConversationEditLogic.MaxChoices} を超えています（{choices.Length}）");
            foreach (var c in choices)
            {
                if (c == null)
                {
                    errors.Add($"会話「{where}」に null の選択肢があります");
                    continue;
                }
                RequireAnyText(c.texts, ConversationEditLogic.ChoiceTextMaxLength, $"会話「{where}」の選択肢", errors);
                ValidateGoto(c.gotoLineId, lineIds, $"会話「{where}」の選択肢ジャンプ先", errors);
                ValidateEffect(c.effect, $"会話「{where}」の選択肢", errors);
            }
        }
    }

    private static void RequireAnyText(GimmickTextJson[] texts, int maxLen, string what, List<string> errors)
    {
        bool any = false;
        if (texts != null)
            foreach (var t in texts)
            {
                if (t == null)
                    continue;
                if (!string.IsNullOrEmpty(t.text))
                    any = true;
                if (t.text != null && t.text.Length > maxLen)
                    errors.Add($"{what}が {maxLen} 文字を超えています（{t.lang}: {t.text.Length} 文字）");
            }
        if (!any)
            errors.Add($"{what}に少なくとも 1 言語のテキストが必要です");
    }

    private static void CheckLengths(GimmickTextJson[] texts, int maxLen, string what, List<string> errors)
    {
        if (texts == null)
            return;
        foreach (var t in texts)
            if (t?.text != null && t.text.Length > maxLen)
                errors.Add($"{what}が {maxLen} 文字を超えています（{t.lang}: {t.text.Length} 文字）");
    }

    private static void ValidateGoto(string gotoLineId, HashSet<string> lineIds, string what, List<string> errors)
    {
        if (string.IsNullOrEmpty(gotoLineId) || gotoLineId == ConversationEditLogic.GotoEnd)
            return;
        if (!lineIds.Contains(gotoLineId))
            errors.Add($"{what}の行 ID が会話内に存在しません: {gotoLineId}");
    }

    private static void ValidateEffect(ConversationEffectJson effect, string what, List<string> errors)
    {
        if (effect == null || effect.kind == "none")
            return;
        if (Array.IndexOf(EffectKinds, effect.kind) < 0)
        {
            errors.Add($"{what}のステート変更種別が不正です: {effect.kind}");
            return;
        }
        if (Array.IndexOf(StateOps, effect.stateOp) < 0)
            errors.Add($"{what}のステート演算が不正です: {effect.stateOp}");
        if (effect.value < 0 || effect.value > 255)
            errors.Add($"{what}の値が範囲外です: {effect.value}");

        if (effect.kind == "worldState")
        {
            if (effect.stateIndex < 0 || effect.stateIndex > MaxWorldStateIndex)
                errors.Add($"{what}のワールドステート番号が範囲外です: {effect.stateIndex}");
        }
        else // playerState
        {
            if (effect.stateIndex < 0 || effect.stateIndex > MaxPlayerStateIndex)
                errors.Add($"{what}のプレイヤーステート番号が範囲外です: {effect.stateIndex}");
            if (Array.IndexOf(PlayerTargets, effect.playerTarget) < 0)
                errors.Add($"{what}の対象プレイヤーが不正です: {effect.playerTarget}");
        }
    }
}
