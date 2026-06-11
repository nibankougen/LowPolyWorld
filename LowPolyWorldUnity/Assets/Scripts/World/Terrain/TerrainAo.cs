/// <summary>
/// 頂点カラーによる簡易アンビエントオクルージョンの定数と明度計算
/// （world-creation.md セクション 15.16）。視覚調整時はここの定数のみ変更する。
/// </summary>
public static class TerrainAo
{
    public const float WeightStandard = 1.00f;    // グループ1・3 の通常ウェイト
    public const float RampHighPrimary = 0.75f;   // グループ2 高端・グループA主ウェイト
    public const float RampHighSecondary = 0.50f; // グループ2 高端・グループA副ウェイト（max のみ採用）
    public const float RampHighSide = 0.50f;      // グループ2 高端・グループB側方ウェイト
    public const float RampLowPrimary = 0.75f;    // グループ2 低端・グループA主ウェイト
    public const float RampLowSecondary = 0.25f;  // グループ2 低端・グループA副ウェイト（max のみ採用）
    public const float RampLowSide = 0.50f;       // グループ2 低端・グループBウェイト
    public const float Normalize = 3.00f;         // 正規化係数（全グループ共通。グループ1は最大4参照・clampで明度0）

    /// <summary>隣接ブロックがない状態での頂点カラー明度（ベース暗さ）。</summary>
    public const float BaseBrightness = 0.75f;

    /// <summary>darkness（参照ブロックのウェイト合算）→ 頂点カラー明度（0〜0.75）。</summary>
    public static float Brightness(float darkness)
    {
        float t = 1f - darkness / Normalize;
        if (t < 0f)
            t = 0f;
        else if (t > 1f)
            t = 1f;
        return BaseBrightness * t;
    }
}
