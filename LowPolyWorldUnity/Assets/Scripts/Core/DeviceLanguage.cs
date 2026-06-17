using UnityEngine;

/// <summary>
/// 端末のシステム言語を、アプリの対応言語コード（<see cref="SupportedLanguages"/>）へ解決する
/// エンジン境界ヘルパー。言語別テキスト入力（ギミックの文字メッセージ 9.8・会話 9.13 等）の
/// 既定言語として用いる。未対応の言語は英語（<see cref="SupportedLanguages.Fallback"/>）にフォールバック。
/// </summary>
public static class DeviceLanguage
{
    /// <summary>端末のシステム言語に対応する言語コードを返す。</summary>
    public static string CurrentCode() => CodeFor(Application.systemLanguage);

    /// <summary><see cref="SystemLanguage"/> を対応言語コードへ写す（未対応は英語）。</summary>
    public static string CodeFor(SystemLanguage lang) => lang switch
    {
        SystemLanguage.Japanese => "ja",
        SystemLanguage.English => "en",
        SystemLanguage.Chinese => "zh-Hans",
        SystemLanguage.ChineseSimplified => "zh-Hans",
        SystemLanguage.ChineseTraditional => "zh-Hant",
        SystemLanguage.Korean => "ko",
        SystemLanguage.French => "fr",
        SystemLanguage.Spanish => "es",
        SystemLanguage.Italian => "it",
        SystemLanguage.German => "de",
        SystemLanguage.Portuguese => "pt-BR",
        _ => SupportedLanguages.Fallback,
    };
}
