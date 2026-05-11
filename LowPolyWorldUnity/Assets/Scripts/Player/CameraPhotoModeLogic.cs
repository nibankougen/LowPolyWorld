using UnityEngine;

/// <summary>
/// 撮影モード専用カメラ操作の純粋 C# ロジック。
/// 2 本指ピンチ → ズームオフセット更新
/// 2 本指スライド → 平行移動オフセット更新（正規化スクリーン座標）
/// 仕様: screens-and-modes.md セクション 2.7.2
/// </summary>
public class CameraPhotoModeLogic
{
    public const float ZoomSensitivity = 0.015f; // ピクセルあたりのズーム距離変化
    public const float SlideSensitivity = 2.0f;  // 正規化スクリーン1単位あたりの移動量
    public const float MinZoom = -3f;             // 最接近オフセット
    public const float MaxZoom = 10f;             // 最遠退オフセット

    /// <summary>正規カメラ位置からの前後オフセット（+ = 遠退き）。</summary>
    public float ZoomOffset { get; private set; }

    /// <summary>カメラ平面上の平行移動オフセット（ワールド単位 x, y = 右/上）。</summary>
    public Vector2 SlideOffset { get; private set; }

    private float _prevPinchDist = -1f;
    private Vector2 _prevMidPoint;
    private bool _hasPrev;

    /// <summary>2 本指が始まったフレームに呼ぶ。</summary>
    public void BeginTwoFingers(Vector2 p0, Vector2 p1)
    {
        _prevPinchDist = Vector2.Distance(p0, p1);
        _prevMidPoint = (p0 + p1) * 0.5f;
        _hasPrev = true;
    }

    /// <summary>2 本指移動中に毎フレーム呼ぶ。</summary>
    public void UpdateTwoFingers(Vector2 p0, Vector2 p1, float screenHeight)
    {
        if (!_hasPrev)
        {
            BeginTwoFingers(p0, p1);
            return;
        }

        float dist = Vector2.Distance(p0, p1);
        float pinchDelta = dist - _prevPinchDist;
        ZoomOffset = Mathf.Clamp(ZoomOffset + pinchDelta * ZoomSensitivity, MinZoom, MaxZoom);
        _prevPinchDist = dist;

        Vector2 mid = (p0 + p1) * 0.5f;
        Vector2 rawDelta = mid - _prevMidPoint;
        // スクリーン高さで正規化して SlideSensitivity を掛ける
        float scale = SlideSensitivity / Mathf.Max(1f, screenHeight);
        SlideOffset += new Vector2(-rawDelta.x * scale, rawDelta.y * scale);
        _prevMidPoint = mid;
    }

    /// <summary>2 本指が離れたフレームに呼ぶ。</summary>
    public void EndTwoFingers()
    {
        _prevPinchDist = -1f;
        _hasPrev = false;
    }

    /// <summary>撮影モード終了時に状態をリセットする。</summary>
    public void Reset()
    {
        ZoomOffset = 0f;
        SlideOffset = Vector2.zero;
        _prevPinchDist = -1f;
        _hasPrev = false;
    }
}
