using System;
using UnityEngine;

/// <summary>
/// Pure C# logic for managing 3 audio volume categories.
/// 音量状態管理・クランプ・変更イベント通知を担当。
/// PlayerPrefs への読み書きは AudioManager が行う。
/// </summary>
public class WorldSettingsLogic
{
    public event Action<float> OnVoiceVolumeChanged;
    public event Action<float> OnWorldSfxVolumeChanged;
    public event Action<float> OnSystemSfxVolumeChanged;

    private float _voiceVolume;
    private float _worldSfxVolume;
    private float _systemSfxVolume;

    public float VoiceVolume => _voiceVolume;
    public float WorldSfxVolume => _worldSfxVolume;
    public float SystemSfxVolume => _systemSfxVolume;

    public WorldSettingsLogic(
        float initialVoice = 1.0f,
        float initialWorldSfx = 1.0f,
        float initialSystemSfx = 1.0f
    )
    {
        _voiceVolume = Mathf.Clamp01(initialVoice);
        _worldSfxVolume = Mathf.Clamp01(initialWorldSfx);
        _systemSfxVolume = Mathf.Clamp01(initialSystemSfx);
    }

    public void SetVoiceVolume(float value)
    {
        float clamped = Mathf.Clamp01(value);
        if (Mathf.Approximately(clamped, _voiceVolume))
            return;
        _voiceVolume = clamped;
        OnVoiceVolumeChanged?.Invoke(clamped);
    }

    public void SetWorldSfxVolume(float value)
    {
        float clamped = Mathf.Clamp01(value);
        if (Mathf.Approximately(clamped, _worldSfxVolume))
            return;
        _worldSfxVolume = clamped;
        OnWorldSfxVolumeChanged?.Invoke(clamped);
    }

    public void SetSystemSfxVolume(float value)
    {
        float clamped = Mathf.Clamp01(value);
        if (Mathf.Approximately(clamped, _systemSfxVolume))
            return;
        _systemSfxVolume = clamped;
        OnSystemSfxVolumeChanged?.Invoke(clamped);
    }
}
