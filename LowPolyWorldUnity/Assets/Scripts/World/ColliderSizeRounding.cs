/// <summary>
/// GLB アップロード時の AABB 寸法を 0.25m 単位に切り上げるユーティリティクラス（純粋 C#）。
/// 全軸 0 のモデルは装飾オブジェクト（コライダーなし）として判定する。
/// </summary>
public static class ColliderSizeRounding
{
    public const float SnapUnit = 0.25f;

    /// <summary>
    /// float 値を 0.25m 単位に切り上げる（天井関数）。
    /// 0 以下は 0 を返す（装飾オブジェクト軸の値として有効）。
    /// </summary>
    public static float RoundUp(float value)
    {
        if (value <= 0f)
            return 0f;
        return (float)(System.Math.Ceiling((double)value / SnapUnit) * SnapUnit);
    }

    /// <summary>W・D・H がすべて 0 のとき装飾オブジェクトと判定する。</summary>
    public static bool IsDecoration(float w, float d, float h) => w == 0f && d == 0f && h == 0f;
}
