using System;
using System.Collections.Generic;

/// <summary>
/// 1 つの会話（<see cref="ConversationJson"/>）の中身を編集する純粋 C# ロジック（world-creation.md 9.13）。
///
/// 担当: セリフ行の追加 / 削除 / 並び替え（最大 50）・本文 / 話者の多言語テキスト編集・
/// 選択肢の追加 / 削除（行あたり最大 4）・ジャンプ先設定・到達 / 選択時のステート変更。
/// 編集は対象 <see cref="ConversationJson"/> を直接書き換える（<see cref="Conversation"/> は常に最新）。
/// 妥当性の最終検証は保存・公開時に <see cref="ConversationValidator"/> が行う。
/// </summary>
public class ConversationEditLogic
{
    public const int MaxLines = 50;
    public const int MaxChoices = 4;
    public const int TextMaxLength = 80;       // 本文
    public const int SpeakerMaxLength = 40;    // 話者名
    public const int ChoiceTextMaxLength = 40; // 選択肢

    public const string GotoNext = "";   // 次の行へ
    public const string GotoEnd = "end"; // 会話終了

    private readonly ConversationJson _conversation;
    private readonly List<ConversationLineJson> _lines;

    public ConversationEditLogic(ConversationJson conversation)
    {
        _conversation = conversation ?? throw new ArgumentNullException(nameof(conversation));
        _lines = new List<ConversationLineJson>(conversation.lines ?? Array.Empty<ConversationLineJson>());
        Sync();
    }

    public ConversationJson Conversation => _conversation;
    public IReadOnlyList<ConversationLineJson> Lines => _lines;
    public bool CanAddLine => _lines.Count < MaxLines;

    // ── セリフ行 ───────────────────────────────────────────────────────────────

    /// <summary>セリフ行を末尾に追加して返す。上限（50）到達時は null。</summary>
    public ConversationLineJson AddLine()
    {
        if (!CanAddLine)
            return null;
        var line = new ConversationLineJson { lineId = NewId("line") };
        _lines.Add(line);
        Sync();
        return line;
    }

    /// <summary>行を削除する。その行 ID を指していたジャンプ先は「次へ」（空）に戻す。</summary>
    public bool RemoveLine(string lineId)
    {
        int idx = IndexOfLine(lineId);
        if (idx < 0)
            return false;
        _lines.RemoveAt(idx);
        ClearDanglingGoto(lineId);
        Sync();
        return true;
    }

    public bool MoveLine(string lineId, int newIndex)
    {
        int idx = IndexOfLine(lineId);
        if (idx < 0)
            return false;
        newIndex = newIndex < 0 ? 0 : newIndex >= _lines.Count ? _lines.Count - 1 : newIndex;
        if (newIndex == idx)
            return true;
        var line = _lines[idx];
        _lines.RemoveAt(idx);
        _lines.Insert(newIndex, line);
        Sync();
        return true;
    }

    // ── テキスト（多言語）──────────────────────────────────────────────────────

    /// <summary>本文を設定する（lang 既存なら上書き・新規なら追加・80 文字に切り詰め・空テキスト拒否）。</summary>
    public bool SetLineText(string lineId, string lang, string text) =>
        SetText(FindLine(lineId)?.texts, text, lang, TextMaxLength, arr => FindLine(lineId).texts = arr);

    /// <summary>話者名を設定する（40 文字・空テキストは削除扱いで拒否）。</summary>
    public bool SetLineSpeaker(string lineId, string lang, string text) =>
        SetText(FindLine(lineId)?.speakers, text, lang, SpeakerMaxLength, arr => FindLine(lineId).speakers = arr);

    public bool RemoveLineText(string lineId, string lang) =>
        RemoveText(FindLine(lineId)?.texts, lang, arr => FindLine(lineId).texts = arr);

    /// <summary>指定言語の話者名を削除する（任意項目のため空入力時のクリアに使う）。</summary>
    public bool RemoveLineSpeaker(string lineId, string lang) =>
        RemoveText(FindLine(lineId)?.speakers, lang, arr => FindLine(lineId).speakers = arr);

    // ── 分岐 ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// 行のジャンプ先を設定する。"" = 次へ / "end" = 終了 / それ以外は同一会話内に実在する行 ID のみ受理。
    /// </summary>
    public bool SetLineGoto(string lineId, string gotoLineId)
    {
        var line = FindLine(lineId);
        if (line == null || !IsValidGotoTarget(gotoLineId, lineId))
            return false;
        line.gotoLineId = gotoLineId ?? "";
        return true;
    }

    /// <summary>行到達時のステート変更を設定する（null = 変更なし）。</summary>
    public bool SetLineOnReach(string lineId, ConversationEffectJson effect)
    {
        var line = FindLine(lineId);
        if (line == null)
            return false;
        line.onReach = NormalizeEffect(effect);
        return true;
    }

    // ── 選択肢 ─────────────────────────────────────────────────────────────────

    /// <summary>行に選択肢を追加して返す。行が無い / 上限（4）到達時は null。</summary>
    public ConversationChoiceJson AddChoice(string lineId)
    {
        var line = FindLine(lineId);
        if (line == null)
            return null;
        var choices = new List<ConversationChoiceJson>(line.choices ?? Array.Empty<ConversationChoiceJson>());
        if (choices.Count >= MaxChoices)
            return null;
        var choice = new ConversationChoiceJson();
        choices.Add(choice);
        line.choices = choices.ToArray();
        return choice;
    }

    public bool RemoveChoice(string lineId, int choiceIndex)
    {
        var line = FindLine(lineId);
        if (line == null || !IsValid(choiceIndex, line.choices.Length))
            return false;
        var choices = new List<ConversationChoiceJson>(line.choices);
        choices.RemoveAt(choiceIndex);
        line.choices = choices.ToArray();
        return true;
    }

    public bool SetChoiceText(string lineId, int choiceIndex, string lang, string text)
    {
        var choice = FindChoice(lineId, choiceIndex);
        if (choice == null)
            return false;
        return SetText(choice.texts, text, lang, ChoiceTextMaxLength, arr => choice.texts = arr);
    }

    /// <summary>指定言語の選択肢テキストを削除する（言語別入力の空入力時のクリアに使う）。</summary>
    public bool RemoveChoiceText(string lineId, int choiceIndex, string lang)
    {
        var choice = FindChoice(lineId, choiceIndex);
        if (choice == null)
            return false;
        return RemoveText(choice.texts, lang, arr => choice.texts = arr);
    }

    /// <summary>選択肢のジャンプ先を設定する（"" / "end" / 実在行 ID）。</summary>
    public bool SetChoiceGoto(string lineId, int choiceIndex, string gotoLineId)
    {
        var choice = FindChoice(lineId, choiceIndex);
        if (choice == null || !IsValidGotoTarget(gotoLineId, null))
            return false;
        choice.gotoLineId = gotoLineId ?? "";
        return true;
    }

    public bool SetChoiceEffect(string lineId, int choiceIndex, ConversationEffectJson effect)
    {
        var choice = FindChoice(lineId, choiceIndex);
        if (choice == null)
            return false;
        choice.effect = NormalizeEffect(effect);
        return true;
    }

    // ── ヘルパー ───────────────────────────────────────────────────────────────

    // "" / "end" は常に有効。それ以外は自分以外の実在行 ID（excludeSelf は行のジャンプ先で自己参照を許す場合 null）。
    private bool IsValidGotoTarget(string gotoLineId, string excludeSelf)
    {
        if (string.IsNullOrEmpty(gotoLineId) || gotoLineId == GotoEnd)
            return true;
        foreach (var l in _lines)
            if (l.lineId == gotoLineId)
                return true;
        return false;
    }

    private void ClearDanglingGoto(string removedLineId)
    {
        foreach (var line in _lines)
        {
            if (line.gotoLineId == removedLineId)
                line.gotoLineId = "";
            if (line.choices != null)
                foreach (var c in line.choices)
                    if (c != null && c.gotoLineId == removedLineId)
                        c.gotoLineId = "";
        }
    }

    private static ConversationEffectJson NormalizeEffect(ConversationEffectJson effect)
    {
        if (effect == null)
            return new ConversationEffectJson { kind = "none" };
        effect.value = effect.value < 0 ? 0 : effect.value > 255 ? 255 : effect.value;
        return effect;
    }

    private static bool SetText(
        GimmickTextJson[] current, string text, string lang, int maxLen, Action<GimmickTextJson[]> assign)
    {
        if (current == null || string.IsNullOrEmpty(text))
            return false;
        lang ??= "";
        if (text.Length > maxLen)
            text = text.Substring(0, maxLen);

        var list = new List<GimmickTextJson>(current);
        var existing = list.Find(t => t != null && t.lang == lang);
        if (existing != null)
            existing.text = text;
        else
            list.Add(new GimmickTextJson { lang = lang, text = text });
        assign(list.ToArray());
        return true;
    }

    private static bool RemoveText(GimmickTextJson[] current, string lang, Action<GimmickTextJson[]> assign)
    {
        if (current == null)
            return false;
        lang ??= "";
        var list = new List<GimmickTextJson>(current);
        if (list.RemoveAll(t => t != null && t.lang == lang) == 0)
            return false;
        assign(list.ToArray());
        return true;
    }

    private ConversationLineJson FindLine(string lineId)
    {
        foreach (var l in _lines)
            if (l.lineId == lineId)
                return l;
        return null;
    }

    private ConversationChoiceJson FindChoice(string lineId, int choiceIndex)
    {
        var line = FindLine(lineId);
        if (line == null || !IsValid(choiceIndex, line.choices?.Length ?? 0))
            return null;
        return line.choices[choiceIndex];
    }

    private int IndexOfLine(string lineId)
    {
        for (int i = 0; i < _lines.Count; i++)
            if (_lines[i].lineId == lineId)
                return i;
        return -1;
    }

    private static bool IsValid(int index, int count) => (uint)index < (uint)count;

    private static string NewId(string prefix) => prefix + "_" + Guid.NewGuid().ToString("N").Substring(0, 8);

    private void Sync() => _conversation.lines = _lines.ToArray();
}
