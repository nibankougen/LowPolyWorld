using UnityEngine;

/// <summary>
/// ワールド環境設定のバリデーション・変換ロジック（world-creation.md セクション 15.17〜15.19）。
/// Color は副作用のない値型のため UnityEngine 参照を許可する。
/// </summary>
public static class WorldEnvironmentLogic
{
    /// <summary>環境カラーの最低明度（HSV V 値）。</summary>
    public const float MinAmbientBrightness = 0.25f;

    // ── 色変換 ────────────────────────────────────────────────────────────────

    /// <summary>
    /// "#RRGGBB" または "#RRGGBBAA" 形式の文字列を Color に変換する。
    /// 解析に失敗した場合は白（Color.white）を返す。
    /// </summary>
    public static Color ParseHexColor(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
            return Color.white;
        if (!ColorUtility.TryParseHtmlString(hex, out var color))
            return Color.white;
        return color;
    }

    // ── 環境カラーバリデーション ──────────────────────────────────────────────

    /// <summary>
    /// 環境カラーの HSV V 値が MinAmbientBrightness（0.25）以上か検証する。
    /// </summary>
    public static bool IsValidAmbientColor(Color color)
    {
        Color.RGBToHSV(color, out _, out _, out float v);
        return v >= MinAmbientBrightness;
    }

    /// <summary>
    /// 環境カラーの HSV V 値が MinAmbientBrightness 未満の場合、V を 0.25 に補正して返す。
    /// 色相・彩度は保持する。
    /// </summary>
    public static Color ClampAmbientColor(Color color)
    {
        Color.RGBToHSV(color, out float h, out float s, out float v);
        if (v < MinAmbientBrightness)
            v = MinAmbientBrightness;
        return Color.HSVToRGB(h, s, v);
    }

    // ── フォグ ────────────────────────────────────────────────────────────────

    /// <summary>FogData のバリデーション: endDistance > startDistance。</summary>
    public static bool IsValidFog(FogData fog) =>
        fog != null && fog.endDistance > fog.startDistance;

    /// <summary>
    /// FogData を補正して endDistance > startDistance を保証した新しいインスタンスを返す。
    /// 入力オブジェクトは変更しない（copy-on-write）。
    /// </summary>
    public static FogData ClampFog(FogData fog)
    {
        if (fog == null) return new FogData();
        return new FogData
        {
            enabled = fog.enabled,
            color = fog.color,
            startDistance = fog.startDistance,
            endDistance = fog.endDistance <= fog.startDistance
                ? fog.startDistance + 0.5f
                : fog.endDistance,
        };
    }

    // ── スクリーンエフェクト ──────────────────────────────────────────────────

    /// <summary>intensity（0〜100）を正規化して 0.0〜1.0 の float に変換する。</summary>
    public static float NormalizeIntensity(int intensity) =>
        UnityEngine.Mathf.Clamp01(intensity / 100f);
}
