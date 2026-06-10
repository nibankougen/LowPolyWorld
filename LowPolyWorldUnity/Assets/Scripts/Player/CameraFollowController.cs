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

    // 撮影モード中のカメラオフセット（PhotoModeController が設定する）
    private float _photoZoomOffset;
    private Vector2 _photoSlideOffset;
    private bool _photoOffsetActive;

    /// <summary>PlayerController が参照するヨー角（度）。</summary>
    public float Yaw => _logic?.Yaw ?? 0f;

    private void Awake()
    {
        _logic = new CameraFollowLogic(_distance, _heightOffset, _initialPitch);
    }

    /// <summary>撮影モード用カメラオフセットを設定する。PhotoModeController から毎フレーム呼ぶ。</summary>
    public void SetPhotoOffset(float zoom, Vector2 slide)
    {
        _photoZoomOffset = zoom;
        _photoSlideOffset = slide;
        _photoOffsetActive = true;
    }

    /// <summary>撮影モード終了時にオフセットをクリアする。</summary>
    public void ClearPhotoOffset()
    {
        _photoZoomOffset = 0f;
        _photoSlideOffset = Vector2.zero;
        _photoOffsetActive = false;
    }

    private void LateUpdate()
    {
        if (_target == null)
            return;

        var playerController = _target.GetComponent<PlayerController>();
        if (playerController != null && !playerController.IsPhotoMode)
        {
            var lookDelta = playerController.LookDelta;
            float sensitivity = Application.isMobilePlatform ? _touchSensitivity : _mouseSensitivity;
            _logic.ApplyLookDelta(lookDelta.x, lookDelta.y, sensitivity);
        }

        var basePos = _logic.GetCameraPosition(_target.position);
        var rot = _logic.GetCameraRotation();

        // 撮影モードオフセットをカメラローカル軸で適用する
        var fwd = rot * Vector3.forward;
        var right = rot * Vector3.right;
        var up = rot * Vector3.up;
        var pos = basePos
            + fwd * (-_photoZoomOffset)       // 正値 = カメラ遠退き
            + right * _photoSlideOffset.x
            + up * _photoSlideOffset.y;

        // 撮影モード中はワールド下端より下に行けない（地表の底面メッシュは
        // 生成されないため — screens-and-modes.md 2.7.2 / world-creation.md 15.14）
        if (_photoOffsetActive && pos.y < CameraPhotoModeLogic.MinCameraY)
            pos.y = CameraPhotoModeLogic.MinCameraY;

        transform.position = pos;
        transform.rotation = rot;
    }
}
