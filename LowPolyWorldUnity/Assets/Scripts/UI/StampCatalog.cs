using System.Collections.Generic;

/// <summary>スタンプの分類。</summary>
public enum StampCategory
{
    Default,
    Premium,
    Purchased,
}

/// <summary>スタンプ定義。ID はネットワーク越しに使用しないローカル識別子。</summary>
public class StampDefinition
{
    public string Id { get; }
    public string Label { get; }           // 表示用テキスト / emoji (MVP: 画像の代わり)
    public StampCategory Category { get; }
    public bool IsColorable { get; }       // 色変えスタンプ
    public bool IsText { get; }            // 文字入れスタンプ
    public TextStampVariant TextVariant { get; } // IsText == true の場合のみ有効

    public StampDefinition(
        string id, string label,
        StampCategory category,
        bool colorable = false,
        bool isText = false,
        TextStampVariant textVariant = TextStampVariant.Clear)
    {
        Id = id;
        Label = label;
        Category = category;
        IsColorable = colorable || isText; // テキストスタンプは常に色変え可
        IsText = isText;
        TextVariant = textVariant;
    }
}

/// <summary>文字入れスタンプの背景スタイル。</summary>
public enum TextStampVariant
{
    Clear,       // 透明背景
    WhiteRound,  // 角丸白背景
    BlackRound,  // 角丸黒背景
}

/// <summary>
/// スタンプカタログ。
/// 将来はショップAPIから取得するが、Phase 9 では静的リストを使用する。
/// </summary>
public static class StampCatalog
{
    public static readonly IReadOnlyList<StampDefinition> All = new List<StampDefinition>
    {
        // ── デフォルト ──────────────────────────────────────────────────────
        new("star",      "⭐", StampCategory.Default),
        new("heart",     "❤️", StampCategory.Default, colorable: true),
        new("thumbsup",  "👍", StampCategory.Default),
        new("flower",    "🌸", StampCategory.Default, colorable: true),
        new("crown",     "👑", StampCategory.Default, colorable: true),
        new("lightning", "⚡", StampCategory.Default, colorable: true),
        new("sparkle",   "✨", StampCategory.Default),
        new("note",      "🎵", StampCategory.Default, colorable: true),
        // テキストスタンプ 3 種
        new("text_clear",  "A",  StampCategory.Default, isText: true, textVariant: TextStampVariant.Clear),
        new("text_white",  "A",  StampCategory.Default, isText: true, textVariant: TextStampVariant.WhiteRound),
        new("text_black",  "A",  StampCategory.Default, isText: true, textVariant: TextStampVariant.BlackRound),

        // ── プレミアム ────────────────────────────────────────────────────
        new("diamond",       "💎", StampCategory.Premium, colorable: true),
        new("wings",         "🦋", StampCategory.Premium, colorable: true),
        new("fire",          "🔥", StampCategory.Premium, colorable: true),
        new("rainbow",       "🌈", StampCategory.Premium),
        new("crystal",       "🔮", StampCategory.Premium, colorable: true),
        new("yuyu_nice",     "",   StampCategory.Premium),
    };
}
