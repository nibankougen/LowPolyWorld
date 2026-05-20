/// <summary>
/// マイプロフィール画面のロジック（UnityEngine 非依存）。
/// 仕様: screens-and-modes.md セクション 22.1
/// </summary>
public class MyProfileLogic
{
    public const int MaxDisplayNameLength = 30;

    public enum DisplayNameValidationResult
    {
        Ok,
        Empty,
        TooLong,
    }

    /// <summary>表示名バリデーション（空・30文字超を弾く）。</summary>
    public static DisplayNameValidationResult ValidateDisplayName(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return DisplayNameValidationResult.Empty;
        if (input.Length > MaxDisplayNameLength)
            return DisplayNameValidationResult.TooLong;
        return DisplayNameValidationResult.Ok;
    }

    public static string ValidationMessage(DisplayNameValidationResult result) =>
        result switch
        {
            DisplayNameValidationResult.Empty => "表示名を入力してください",
            DisplayNameValidationResult.TooLong => $"{MaxDisplayNameLength}文字以内で入力してください",
            _ => string.Empty,
        };

    /// <summary>
    /// フォロワー数などを表示文字列に変換する。
    /// 0〜9,999 はカンマ区切り。10,000 以上は "9,999+" に丸める。
    /// </summary>
    public static string FormatSocialCount(int count)
    {
        if (count < 0)
            count = 0;
        if (count >= 10_000)
            return "9,999+";
        return count.ToString("N0");
    }
}
