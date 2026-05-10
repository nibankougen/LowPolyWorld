using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// アバターのワールドスペース Canvas 上に配置する発話インジケーター。
/// VoiceManager.IsTalking を毎フレームポーリングして Image の表示を切り替える。
/// </summary>
[RequireComponent(typeof(Image))]
public class VoiceIndicatorController : MonoBehaviour
{
    private Image _image;
    private string _vivoxId;
    private bool _lastTalking;

    private void Awake()
    {
        _image = GetComponent<Image>();
        _image.enabled = false;
    }

    /// <summary>監視対象の vivoxId を設定する（アバター生成時に呼び出す）。</summary>
    public void SetVivoxId(string vivoxId)
    {
        _vivoxId = vivoxId;
        _lastTalking = false;
        _image.enabled = false;
    }

    private void Update()
    {
        if (string.IsNullOrEmpty(_vivoxId) || VoiceManager.Instance == null) return;

        bool talking = VoiceManager.Instance.IsTalking(_vivoxId);
        if (talking == _lastTalking) return;

        _lastTalking = talking;
        _image.enabled = talking;
    }
}
