using System.Collections.Generic;

/// <summary>
/// アプリが対応する表示言語の一覧（純粋 C# カタログ）。
///
/// 言語別テキスト入力（ギミックの文字メッセージ 9.8・会話 9.13 等）や言語設定 UI で共有する。
/// UGC の言語別テキストは、未設定言語は英語（<see cref="Fallback"/>）優先でフォールバックする
/// （表示解決は各再生ロジック側。例: <see cref="ConversationPlaybackLogic"/>）。
/// </summary>
public static class SupportedLanguages
{
    /// <summary>デフォルト言語を表す言語コード（作者が最初に入力する欄）。</summary>
    public const string Default = "";

    /// <summary>未設定言語のフォールバック先（英語優先）。</summary>
    public const string Fallback = "en";

    public readonly struct Language
    {
        public string Code { get; }
        public string Label { get; }

        public Language(string code, string label)
        {
            Code = code;
            Label = label;
        }
    }

    /// <summary>対応言語（表示順）。コードはサーバー / 言語設定と一致させる。</summary>
    public static readonly IReadOnlyList<Language> All = new[]
    {
        new Language("ja", "日本語"),
        new Language("en", "English"),
        new Language("zh-Hans", "中文（简体）"),
        new Language("zh-Hant", "中文（繁體）"),
        new Language("ko", "한국어"),
        new Language("fr", "Français"),
        new Language("es", "Español"),
        new Language("it", "Italiano"),
        new Language("de", "Deutsch"),
        new Language("pt-BR", "Português (Brasil)"),
    };

    /// <summary>言語コードの表示ラベルを返す（未知のコードはコードそのまま）。</summary>
    public static string LabelOf(string code)
    {
        foreach (var l in All)
            if (l.Code == code)
                return l.Label;
        return code ?? "";
    }

    /// <summary>対応言語コードか（Default / 未知は false）。</summary>
    public static bool IsSupported(string code)
    {
        foreach (var l in All)
            if (l.Code == code)
                return true;
        return false;
    }
}
