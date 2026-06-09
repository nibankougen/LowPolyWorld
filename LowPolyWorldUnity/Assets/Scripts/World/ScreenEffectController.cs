using UnityEngine;

/// <summary>
/// スクリーンオーバーレイエフェクトを制御する MonoBehaviour（world-creation.md セクション 15.19）。
/// ParticleSystem（雨エフェクト）を強度に応じて制御する。
/// Screen Space – Overlay Canvas 上に配置する。
/// </summary>
public class ScreenEffectController : MonoBehaviour
{
    [SerializeField]
    private ParticleSystem _rainParticles;

    // intensity=100 のときの基準放出レート（粒子/秒）
    [SerializeField]
    private float _baseEmissionRate = 300f;

    private void Awake()
    {
        SetActive(false);
    }

    // ── 公開 API ─────────────────────────────────────────────────────────────

    /// <summary>
    /// ScreenEffectData を適用する。null または type="none" のときはエフェクトを無効化する。
    /// </summary>
    public void Apply(ScreenEffectData data)
    {
        if (data == null || data.type == "none")
        {
            SetActive(false);
            return;
        }

        float normalized = WorldEnvironmentLogic.NormalizeIntensity(data.intensity);

        switch (data.type)
        {
            case "rain":
                ApplyRain(normalized);
                break;
            default:
                SetActive(false);
                break;
        }
    }

    /// <summary>エフェクトをリセット（非表示）する。ワールド退出時に呼び出す。</summary>
    public void Reset() => SetActive(false);

    // ── Private ───────────────────────────────────────────────────────────────

    private void ApplyRain(float normalizedIntensity)
    {
        if (_rainParticles == null) return;

        gameObject.SetActive(true);
        _rainParticles.gameObject.SetActive(true);

        var emission = _rainParticles.emission;
        emission.rateOverTime = _baseEmissionRate * normalizedIntensity;

        if (!_rainParticles.isPlaying)
            _rainParticles.Play();
    }

    private void SetActive(bool active)
    {
        if (_rainParticles != null)
        {
            if (!active && _rainParticles.isPlaying)
                _rainParticles.Stop(withChildren: true, stopBehavior: ParticleSystemStopBehavior.StopEmittingAndClear);
            _rainParticles.gameObject.SetActive(active);
        }
        gameObject.SetActive(active);
    }
}
