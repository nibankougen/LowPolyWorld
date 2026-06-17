using UnityEngine;
using UnityEngine.Localization.Settings;

/// <summary>
/// アプリの**設定言語**（表示言語）を、対応言語コード（<see cref="SupportedLanguages"/>）へ解決する
/// エンジン境界ヘルパー。言語別テキスト入力（ギミックの文字メッセージ 9.8・会話 9.13 等）の
/// 既定言語として用いる。
///
/// 設定言語は <see cref="LocalizationSettings.SelectedLocale"/>（既定でシステム言語に追従）から取得し、
/// 未対応の言語が得られた場合は英語（<see cref="SupportedLanguages.Fallback"/>）にフォールバックする。
/// </summary>
public static class DeviceLanguage
{
    /// <summary>アプリの設定言語に対応する言語コードを返す（対応外は英語）。</summary>
    public static string CurrentCode() => Normalize(SelectedOrSystemCode());

    /// <summary>対応言語ならそのまま、未対応 / 空なら英語にフォールバックする。</summary>
    public static string Normalize(string code) =>
        SupportedLanguages.IsSupported(code) ? code : SupportedLanguages.Fallback;

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

    // アプリの設定言語コードを取得する。未初期化時はシステム言語に追従（設定言語の既定がシステム言語のため）。
    private static string SelectedOrSystemCode()
    {
        try
        {
            var code = LocalizationSettings.SelectedLocale?.Identifier.Code;
            if (!string.IsNullOrEmpty(code))
                return code;
        }
        catch
        {
            // ローカライズ未初期化など。システム言語へフォールバックする。
        }
        return CodeFor(Application.systemLanguage);
    }
}
