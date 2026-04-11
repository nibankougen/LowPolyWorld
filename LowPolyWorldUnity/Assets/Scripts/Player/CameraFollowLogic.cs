using UnityEngine;

/// <summary>
/// Pure C# logic for third-person camera follow with yaw/pitch control.
/// MonoBehaviour への依存なし。
/// </summary>
public class CameraFollowLogic
{
    public const float MinPitch = -20f;
    public const float MaxPitch = 80f;

    private float _yaw;
    private float _pitch;
    private readonly float _distance;
    private readonly float _heightOffset;

    public float Yaw => _yaw;
    public float Pitch => _pitch;

    public CameraFollowLogic(float distance = 5f, float heightOffset = 1.5f, float initialPitch = 15f)
    {
        _distance = distance;
        _heightOffset = heightOffset;
        _yaw = 0f;
        _pitch = Mathf.Clamp(initialPitch, MinPitch, MaxPitch);
    }

    /// <summary>
    /// ルック入力を適用して yaw/pitch を更新する。
    /// </summary>
    /// <param name="deltaYaw">水平方向の角度変化量（度）。</param>
    /// <param name="deltaPitch">垂直方向の角度変化量（度）。正で下向き。</param>
    /// <param name="sensitivity">入力感度倍率。</param>
    public void ApplyLookDelta(float deltaYaw, float deltaPitch, float sensitivity = 1f)
    {
        _yaw += deltaYaw * sensitivity;
        _pitch = Mathf.Clamp(_pitch - deltaPitch * sensitivity, MinPitch, MaxPitch);
    }

    /// <summary>追従先の位置からカメラ位置を計算して返す。</summary>
    public Vector3 GetCameraPosition(Vector3 targetPosition)
    {
        var pivotPos = targetPosition + Vector3.up * _heightOffset;
        var rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        return pivotPos + rotation * new Vector3(0f, 0f, -_distance);
    }

    /// <summary>カメラの回転を返す。</summary>
    public Quaternion GetCameraRotation()
    {
        return Quaternion.Euler(_pitch, _yaw, 0f);
    }
}
