using UnityEngine;

/// <summary>
/// アバター/アクセサリ編集画面の 3D プレビューカメラ操作ロジック（純粋 C#）。
/// 1本指: カメラ回転 / 2本指: パン・ズーム
/// カメラが常に対象を映している状態を維持する。
/// </summary>
public class EditPreviewCameraLogic
{
    public const float MinDistance = 0.5f;
    public const float MaxDistance = 5f;
    public const float MinPitch = -30f;
    public const float MaxPitch = 80f;

    private float _yaw;
    private float _pitch;
    private float _distance;
    private Vector3 _pivotOffset;

    private readonly float _rotationSensitivity;
    private readonly float _panSensitivity;
    private readonly float _zoomSensitivity;

    public float Yaw => _yaw;
    public float Pitch => _pitch;
    public float Distance => _distance;
    public Vector3 PivotOffset => _pivotOffset;

    public EditPreviewCameraLogic(
        float initialDistance = 2.0f,
        float initialPitch = 15f,
        float rotationSensitivity = 0.3f,
        float panSensitivity = 0.005f,
        float zoomSensitivity = 0.01f
    )
    {
        _distance = Mathf.Clamp(initialDistance, MinDistance, MaxDistance);
        _pitch = initialPitch;
        _rotationSensitivity = rotationSensitivity;
        _panSensitivity = panSensitivity;
        _zoomSensitivity = zoomSensitivity;
    }

    /// <summary>1本指ドラッグでカメラを回転させる。</summary>
    public void ApplyRotation(float deltaX, float deltaY)
    {
        _yaw = (_yaw + deltaX * _rotationSensitivity) % 360f;
        _pitch = Mathf.Clamp(_pitch - deltaY * _rotationSensitivity, MinPitch, MaxPitch);
    }

    /// <summary>2本指パンでピボットオフセットを移動させる。</summary>
    public void ApplyPan(Vector2 delta)
    {
        var right = Quaternion.Euler(0, _yaw, 0) * Vector3.right;
        var up = Vector3.up;
        _pivotOffset += (-right * delta.x + -up * delta.y) * _panSensitivity * _distance;
    }

    /// <summary>2本指ピンチでズームを変更する。</summary>
    /// <param name="pinchDelta">正: ズームイン / 負: ズームアウト</param>
    public void ApplyZoom(float pinchDelta)
    {
        _distance = Mathf.Clamp(_distance - pinchDelta * _zoomSensitivity, MinDistance, MaxDistance);
    }

    /// <summary>
    /// ターゲット位置からカメラ位置を計算して返す。
    /// </summary>
    public Vector3 GetCameraPosition(Vector3 targetPosition)
    {
        var pivot = targetPosition + _pivotOffset;
        var rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        return pivot + rotation * Vector3.back * _distance;
    }

    /// <summary>カメラ回転を返す。</summary>
    public Quaternion GetCameraRotation() => Quaternion.Euler(_pitch, _yaw, 0f);

    /// <summary>カメラをリセットする。</summary>
    public void Reset(float distance = 2.0f, float pitch = 15f)
    {
        _yaw = 0f;
        _pitch = pitch;
        _distance = Mathf.Clamp(distance, MinDistance, MaxDistance);
        _pivotOffset = Vector3.zero;
    }
}
