using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// アバター頭上の World Space Canvas に配置する名前タグ MonoBehaviour。
/// 表示名・公認バッジ・発話インジケーターを管理し、毎フレームカメラ方向を向く。
/// </summary>
[RequireComponent(typeof(Canvas))]
public class NameTagController : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _nameText;
    [SerializeField] private Image _verifiedBadge;
    [SerializeField] private Image _voiceIndicator;

    private Transform _cameraTransform;
    private bool _voiceActive;
    private float _blinkTimer;

    private const float BlinkInterval = 0.4f;

    private void Start()
    {
        if (Camera.main != null)
            _cameraTransform = Camera.main.transform;

        if (_voiceIndicator != null)
            _voiceIndicator.gameObject.SetActive(false);
    }

    /// <summary>表示名と公認バッジを設定する。</summary>
    public void SetNameTag(string displayName, bool isVerified)
    {
        if (_nameText != null)
            _nameText.text = NameTagLogic.ResolveDisplayName(displayName);

        if (_verifiedBadge != null)
            _verifiedBadge.gameObject.SetActive(NameTagLogic.ShouldShowVerifiedBadge(isVerified));
    }

    /// <summary>発話状態を設定する。true のとき音声インジケーターが点滅する。</summary>
    public void SetVoiceActive(bool active)
    {
        _voiceActive = active;
        _blinkTimer = 0f;

        if (_voiceIndicator != null)
            _voiceIndicator.gameObject.SetActive(active);
    }

    private void Update()
    {
        FaceCamera();

        if (_voiceActive)
            UpdateVoiceIndicatorBlink();
    }

    private void FaceCamera()
    {
        if (_cameraTransform == null) return;
        transform.LookAt(
            transform.position + _cameraTransform.rotation * Vector3.forward,
            _cameraTransform.rotation * Vector3.up
        );
    }

    private void UpdateVoiceIndicatorBlink()
    {
        if (_voiceIndicator == null) return;

        _blinkTimer += Time.deltaTime;
        if (_blinkTimer >= BlinkInterval)
        {
            _blinkTimer -= BlinkInterval;
            _voiceIndicator.gameObject.SetActive(!_voiceIndicator.gameObject.activeSelf);
        }
    }
}
