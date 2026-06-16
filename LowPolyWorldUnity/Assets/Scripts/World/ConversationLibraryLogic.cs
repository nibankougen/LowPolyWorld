using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

/// <summary>
/// ワールドの会話定義一覧（world-creation.md 9.13）を管理する純粋 C# ロジック。
///
/// 担当: 会話の追加 / 改名 / 削除 / 並び替え（最大 30）と <see cref="WorldDefinitionJson"/> との往復。
/// 1 会話の中身（セリフ行・選択肢）の編集は <see cref="ConversationEditLogic"/> が担当する。
/// </summary>
public class ConversationLibraryLogic
{
    public const int MaxConversations = 30;
    public const int NameMaxLength = 20;

    /// <summary>1 ワールドの全会話を合わせたセリフ行の合計上限（データ肥大防止・9.13）。</summary>
    public const int MaxTotalLines = 500;

    private static readonly Regex DefaultNamePattern = new(@"^会話(\d+)$", RegexOptions.Compiled);

    private readonly List<ConversationJson> _conversations = new();

    public IReadOnlyList<ConversationJson> Conversations => _conversations;
    public int Count => _conversations.Count;
    public bool CanAdd => Count < MaxConversations;

    /// <summary>全会話を合わせたセリフ行の合計。</summary>
    public int TotalLineCount
    {
        get
        {
            int n = 0;
            foreach (var c in _conversations)
                n += c.lines?.Length ?? 0;
            return n;
        }
    }

    /// <summary>全体のセリフ行合計が上限に達していないか（行追加可否の判定）。</summary>
    public bool CanAddLine => TotalLineCount < MaxTotalLines;

    /// <summary>名前を 1〜20 文字に整形する（前後空白除去・超過は切り詰め）。空入力は空文字。</summary>
    public static string SanitizeName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return "";
        var trimmed = name.Trim();
        return trimmed.Length > NameMaxLength ? trimmed.Substring(0, NameMaxLength) : trimmed;
    }

    /// <summary>会話を 1 つ追加して返す。上限（30）到達時は null。name 省略時は「会話N」を自動採番。</summary>
    public ConversationJson Add(string name = null)
    {
        if (!CanAdd)
            return null;
        var conv = new ConversationJson
        {
            conversationId = NewId("conv"),
            name = string.IsNullOrWhiteSpace(name) ? NextDefaultName() : SanitizeName(name),
        };
        _conversations.Add(conv);
        return conv;
    }

    /// <summary>会話名を変更する（1〜20 文字・空不可）。</summary>
    public bool Rename(string conversationId, string name)
    {
        var sanitized = SanitizeName(name);
        if (string.IsNullOrEmpty(sanitized))
            return false;
        var conv = Find(conversationId);
        if (conv == null)
            return false;
        conv.name = sanitized;
        return true;
    }

    public bool Remove(string conversationId)
    {
        int idx = IndexOf(conversationId);
        if (idx < 0)
            return false;
        _conversations.RemoveAt(idx);
        return true;
    }

    /// <summary>会話を newIndex の位置へ移動する。範囲外はクランプ。</summary>
    public bool Move(string conversationId, int newIndex)
    {
        int idx = IndexOf(conversationId);
        if (idx < 0)
            return false;
        newIndex = newIndex < 0 ? 0 : newIndex >= _conversations.Count ? _conversations.Count - 1 : newIndex;
        if (newIndex == idx)
            return true;
        var conv = _conversations[idx];
        _conversations.RemoveAt(idx);
        _conversations.Insert(newIndex, conv);
        return true;
    }

    public ConversationJson Find(string conversationId)
    {
        foreach (var c in _conversations)
            if (c.conversationId == conversationId)
                return c;
        return null;
    }

    // ── ワールド定義との往復 ───────────────────────────────────────────────────

    public void LoadFrom(WorldDefinitionJson def)
    {
        _conversations.Clear();
        if (def?.conversations != null)
            foreach (var c in def.conversations)
                if (c != null)
                    _conversations.Add(c);
    }

    public void WriteTo(WorldDefinitionJson def)
    {
        if (def == null)
            return;
        def.conversations = _conversations.ToArray();
    }

    // ── 内部 ───────────────────────────────────────────────────────────────────

    private int IndexOf(string conversationId)
    {
        for (int i = 0; i < _conversations.Count; i++)
            if (_conversations[i].conversationId == conversationId)
                return i;
        return -1;
    }

    private static string NewId(string prefix) => prefix + "_" + Guid.NewGuid().ToString("N").Substring(0, 8);

    private string NextDefaultName()
    {
        int max = 0;
        foreach (var c in _conversations)
        {
            var m = DefaultNamePattern.Match(c.name ?? "");
            if (m.Success && int.TryParse(m.Groups[1].Value, out int n) && n > max)
                max = n;
        }
        return $"会話{max + 1}";
    }
}
