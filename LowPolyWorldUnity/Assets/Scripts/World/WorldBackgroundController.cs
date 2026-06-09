using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ワールドの背景を制御する MonoBehaviour（world-creation.md セクション 7）。
///
/// 単色: メインカメラの backgroundColor を設定（ClearFlags = SolidColor）。
/// グラデーション: 背景 Canvas 上の Image に 2 色グラデーションテクスチャを生成して適用。
/// テクスチャ: 背景 Canvas 上の RawImage に URL からダウンロードしたテクスチャを適用（Phase 12 API 統合後）。
/// </summary>
public class WorldBackgroundController : MonoBehaviour
{
    [SerializeField]
    private Camera _mainCamera;

    /// <summary>グラデーション・テクスチャ背景を描画する全画面 Image（背景 Canvas 配下）。</summary>
    [SerializeField]
    private RawImage _backgroundImage;

    private Texture2D _gradientTex;

    private void OnDestroy()
    {
        DestroyGradientTex();
    }

    // ── 公開 API ─────────────────────────────────────────────────────────────

    /// <summary>BackgroundData を適用する。null の場合は単色黒にフォールバック。</summary>
    public void Apply(BackgroundData data)
    {
        if (data == null)
        {
            ApplySolid(Color.black);
            return;
        }

        switch (data.type)
        {
            case "gradient" when data.colors?.Length >= 2:
                ApplyGradient(
                    WorldEnvironmentLogic.ParseHexColor(data.colors[0]),
                    WorldEnvironmentLogic.ParseHexColor(data.colors[1]));
                break;
            case "texture":
                // テクスチャ URL からのダウンロードは Phase 12 API 統合後に実装
                ApplySolid(WorldEnvironmentLogic.ParseHexColor(
                    data.colors?.Length > 0 ? data.colors[0] : "#111111"));
                break;
            default: // "solid"
                ApplySolid(WorldEnvironmentLogic.ParseHexColor(
                    data.colors?.Length > 0 ? data.colors[0] : "#111111"));
                break;
        }
    }

    /// <summary>デフォルト（単色黒）にリセットする。ワールド退出時に呼び出す。</summary>
    public void Reset() => ApplySolid(Color.black);

    // ── Private ───────────────────────────────────────────────────────────────

    private void ApplySolid(Color color)
    {
        if (_backgroundImage != null)
            _backgroundImage.gameObject.SetActive(false);

        if (_mainCamera != null)
        {
            _mainCamera.clearFlags = CameraClearFlags.SolidColor;
            _mainCamera.backgroundColor = color;
        }
    }

    private void ApplyGradient(Color top, Color bottom)
    {
        DestroyGradientTex();

        // 2×2 テクスチャで上下グラデーションを表現（バイリニアフィルタで補間）
        _gradientTex = new Texture2D(1, 2, TextureFormat.RGBA32, mipChain: false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
        };
        _gradientTex.SetPixel(0, 0, bottom);
        _gradientTex.SetPixel(0, 1, top);
        _gradientTex.Apply();

        if (_mainCamera != null)
            _mainCamera.clearFlags = CameraClearFlags.SolidColor;

        if (_backgroundImage != null)
        {
            _backgroundImage.gameObject.SetActive(true);
            _backgroundImage.texture = _gradientTex;
        }
    }

    private void DestroyGradientTex()
    {
        if (_gradientTex != null)
        {
            Destroy(_gradientTex);
            _gradientTex = null;
        }
    }
}
