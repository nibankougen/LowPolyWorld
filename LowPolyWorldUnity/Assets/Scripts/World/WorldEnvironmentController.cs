using UnityEngine;

/// <summary>
/// ワールド環境設定（フォグ・環境カラー・背景・スクリーンエフェクト）を
/// WorldDefinitionJson から一括適用する MonoBehaviour（world-creation.md セクション 15.17〜15.19）。
///
/// WorldScene の Hierarchy に配置し、WorldCreationManager から Apply() を呼び出す。
/// ワールド退出時は OnDestroy で自動リセットされる。
/// </summary>
public class WorldEnvironmentController : MonoBehaviour
{
    // _AmbientColor グローバルシェーダープロパティ ID（全 LowPoly/Unlit シェーダーに適用）
    private static readonly int AmbientColorId = Shader.PropertyToID("_AmbientColor");

    [SerializeField]
    private ScreenEffectController _screenEffect;

    [SerializeField]
    private WorldBackgroundController _background;

    private void OnDestroy()
    {
        ResetAll();
    }

    // ── 公開 API ─────────────────────────────────────────────────────────────

    /// <summary>
    /// ワールド定義に従って全環境設定を適用する。
    /// WorldCreationManager.ApplyEnvironment() から呼び出す。
    /// </summary>
    public void Apply(WorldDefinitionJson def)
    {
        ApplyAmbientColor(def?.ambientColor);
        ApplyFog(def?.fog);
        _screenEffect?.Apply(def?.screenEffect);
        _background?.Apply(def?.background);
    }

    /// <summary>ワールド退出時に全環境設定をデフォルトへ戻す。</summary>
    public void ResetAll()
    {
        Shader.SetGlobalColor(AmbientColorId, Color.white);
        RenderSettings.fog = false;
        _screenEffect?.Reset();
        _background?.Reset();
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private static void ApplyAmbientColor(string hex)
    {
        var color = WorldEnvironmentLogic.ParseHexColor(
            string.IsNullOrEmpty(hex) ? "#FFFFFF" : hex);
        color = WorldEnvironmentLogic.ClampAmbientColor(color);
        Shader.SetGlobalColor(AmbientColorId, color);
    }

    private static void ApplyFog(FogData fog)
    {
        if (fog == null || !fog.enabled)
        {
            RenderSettings.fog = false;
            return;
        }

        var clamped = WorldEnvironmentLogic.ClampFog(fog);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = WorldEnvironmentLogic.ParseHexColor(clamped.color);
        RenderSettings.fogStartDistance = clamped.startDistance;
        RenderSettings.fogEndDistance = clamped.endDistance;
    }
}
