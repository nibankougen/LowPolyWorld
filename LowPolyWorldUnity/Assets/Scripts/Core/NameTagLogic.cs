/// <summary>
/// アバター頭上の名前タグに表示する内容を決定するロジッククラス。
/// </summary>
public class NameTagLogic
{
    /// <summary>表示名が空のときに使うフォールバック文字列。</summary>
    public const string DefaultDisplayName = "???";

    /// <summary>表示名を正規化して返す。null / 空白のときはフォールバック名を返す。</summary>
    public static string ResolveDisplayName(string displayName) =>
        string.IsNullOrWhiteSpace(displayName) ? DefaultDisplayName : displayName.Trim();

    /// <summary>公認バッジを表示すべきかを返す。</summary>
    public static bool ShouldShowVerifiedBadge(bool isVerified) => isVerified;
}
