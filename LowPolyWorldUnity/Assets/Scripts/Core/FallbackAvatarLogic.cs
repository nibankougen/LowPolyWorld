/// <summary>
/// 他プレイヤーのアバターにフォールバック表示を適用するかどうかを判定するロジッククラス。
/// pending アバターは本人以外に VRM を配信しない（API が 404 を返す）ため、
/// 受信側クライアントはフォールバックアバターを代わりに表示する。
/// </summary>
public class FallbackAvatarLogic
{
    public const string PendingStatus = "pending";

    /// <summary>
    /// フォールバックアバターを使用すべきかを返す。
    /// 他プレイヤーが pending アバターを持つ場合のみ true。
    /// </summary>
    public static bool ShouldUseFallback(string moderationStatus, bool isLocal) =>
        !isLocal && moderationStatus == PendingStatus;
}
