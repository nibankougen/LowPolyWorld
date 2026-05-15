using System;
using System.Globalization;
using System.Text.RegularExpressions;

/// <summary>
/// @name バリデーション・日付フォーマットなど純粋ロジック（UnityEngine 非依存）。
/// 仕様: screens-and-modes.md セクション 5.1, 10.4, 18
/// </summary>
public class AccountSettingsLogic
{
    public enum NameValidationResult
    {
        Ok,
        TooShort,
        TooLong,
        InvalidChars,
    }

    private static readonly Regex NamePattern = new(@"^[a-zA-Z0-9_]+$", RegexOptions.Compiled);

    public static NameValidationResult ValidateName(string input)
    {
        if (string.IsNullOrEmpty(input) || input.Length < 3)
            return NameValidationResult.TooShort;
        if (input.Length > 15)
            return NameValidationResult.TooLong;
        if (!NamePattern.IsMatch(input))
            return NameValidationResult.InvalidChars;
        return NameValidationResult.Ok;
    }

    public static string NormalizeName(string input) => input?.ToLowerInvariant() ?? string.Empty;

    public static string ValidationMessage(NameValidationResult result) =>
        result switch
        {
            NameValidationResult.TooShort => "3文字以上で入力してください",
            NameValidationResult.TooLong => "15文字以内で入力してください",
            NameValidationResult.InvalidChars => "英数字とアンダースコア（_）のみ使用できます",
            _ => string.Empty,
        };

    /// <summary>ISO 8601 日付文字列を「yyyy年M月d日」形式に変換する。パース失敗時は null。</summary>
    public static string FormatDate(string isoDate)
    {
        if (string.IsNullOrEmpty(isoDate))
            return null;
        return DateTime.TryParse(isoDate, null, DateTimeStyles.RoundtripKind, out var dt)
            ? dt.ToLocalTime().ToString("yyyy年M月d日")
            : null;
    }
}
