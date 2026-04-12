using UnityEngine;

/// <summary>
/// Unity エンジン境界のみを担当する MonoBehaviour。
/// カメラ追従ロジックは CameraFollowLogic に委譲する。
/// </summary>
public class CameraFollowController : MonoBehaviour
{
    [SerializeField] private Transform _target;
    [SerializeField] private float _distance = 5f;
    [SerializeField] private float _heightOffset = 1.5f;
    [SerializeField] private float _initialPitch = 45f;
    [SerializeField] private float _mouseSensitivity = 0.15f;
    [SerializeField] private float _touchSensitivity = 0.1f;

    private CameraFollowLogic _logic;

    /// <summary>PlayerController が参照するヨー角（度）。</summary>
    public float Yaw => _logic?.Yaw ?? 0f;

    private void Awake()
    {
        _logic = new CameraFollowLogic(_distance, _heightOffset, _initialPitch);
    }

    private void LateUpdate()
    {
        if (_target == null)
            return;

        var playerController = _target.GetComponent<PlayerController>();
        if (playerController != null)
        {
            var lookDelta = playerController.LookDelta;
            float sensitivity = Application.isMobilePlatform ? _touchSensitivity : _mouseSensitivity;
            _logic.ApplyLookDelta(lookDelta.x, lookDelta.y, sensitivity);
        }

        transform.position = _logic.GetCameraPosition(_target.position);
        transform.rotation = _logic.GetCameraRotation();
    }
}
