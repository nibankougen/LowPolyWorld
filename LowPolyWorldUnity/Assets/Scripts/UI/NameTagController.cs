using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// アバター頭上の World Space Canvas に配置する名前タグ MonoBehaviour。
/// 表示名・公認バッジ・発話インジケーターを管理し、毎フレームカメラ方向を向く。
/// 撮影モード中は SetAllVisible(false) で全名札を非表示にする（screens-and-modes.md 2.7）。
/// </summary>
[RequireComponent(typeof(Canvas))]
public class NameTagController : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _nameText;
    [SerializeField] private Image _verifiedBadge;
    [SerializeField] private Image _voiceIndicator;

    private Transform _cameraTransform;
    private Canvas _canvas;
    private bool _voiceActive;
    private float _blinkTimer;

    private const float BlinkInterval = 0.4f;

    // 撮影モード中の一括非表示用レジストリ
    private static readonly List<NameTagController> ActiveTags = new();
    private static bool _globalVisible = true;

    /// <summary>
    /// 全名札の表示を一括切り替えする（撮影モード入退で PhotoModeController が呼ぶ）。
    /// 切り替え中に生成された名札（途中入室者）にも適用される。
    /// </summary>
    public static void SetAllVisible(bool visible)
    {
        _globalVisible = visible;
        foreach (var tag in ActiveTags)
            tag.ApplyGlobalVisibility();
    }

    private void Awake()
    {
        _canvas = GetComponent<Canvas>();
    }

    private void OnEnable()
    {
        ActiveTags.Add(this);
        ApplyGlobalVisibility();
    }

    private void OnDisable()
    {
        ActiveTags.Remove(this);
    }

    private void ApplyGlobalVisibility()
    {
        if (_canvas != null)
            _canvas.enabled = _globalVisible;
    }

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
