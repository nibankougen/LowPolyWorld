using System;
using UnityEngine.UIElements;

/// <summary>
/// 音量3スライダー・コントロールボタントグルの表示・操作を管理するコントローラー。
/// HomeScreen 設定タブおよびインワールドメニュー設定タブの両方から使用する。
/// </summary>
public class SettingsTabController
{
    private readonly Slider _sliderVoice;
    private readonly Slider _sliderWorldSfx;
    private readonly Slider _sliderSystemSfx;
    private readonly Label _labelVoice;
    private readonly Label _labelWorldSfx;
    private readonly Label _labelSystemSfx;
    private readonly Toggle _toggleControlButtons;

    private WorldSettingsLogic _settings;
    private Action<bool> _onControlButtonsChanged;

    public SettingsTabController(VisualElement root)
    {
        _sliderVoice = root.Q<Slider>("slider-voice");
        _sliderWorldSfx = root.Q<Slider>("slider-world-sfx");
        _sliderSystemSfx = root.Q<Slider>("slider-system-sfx");
        _labelVoice = root.Q<Label>("label-voice");
        _labelWorldSfx = root.Q<Label>("label-world-sfx");
        _labelSystemSfx = root.Q<Label>("label-system-sfx");
        _toggleControlButtons = root.Q<Toggle>("toggle-control-buttons");

        if (_sliderVoice != null)
            _sliderVoice.RegisterValueChangedCallback(e => _settings?.SetVoiceVolume(e.newValue));
        if (_sliderWorldSfx != null)
            _sliderWorldSfx.RegisterValueChangedCallback(e => _settings?.SetWorldSfxVolume(e.newValue));
        if (_sliderSystemSfx != null)
            _sliderSystemSfx.RegisterValueChangedCallback(e => _settings?.SetSystemSfxVolume(e.newValue));
        if (_toggleControlButtons != null)
            _toggleControlButtons.RegisterValueChangedCallback(e => _onControlButtonsChanged?.Invoke(e.newValue));
    }

    /// <summary>コントロールボタントグルの初期値とコールバックを設定する。</summary>
    public void BindControlButtons(bool initialValue, Action<bool> onChange)
    {
        _onControlButtonsChanged = onChange;
        _toggleControlButtons?.SetValueWithoutNotify(initialValue);
    }

    /// <summary>WorldSettingsLogic を紐付けてスライダーを同期する。</summary>
    public void Bind(WorldSettingsLogic settings)
    {
        if (_settings != null)
        {
            _settings.OnVoiceVolumeChanged -= OnVoiceChanged;
            _settings.OnWorldSfxVolumeChanged -= OnWorldSfxChanged;
            _settings.OnSystemSfxVolumeChanged -= OnSystemSfxChanged;
        }

        _settings = settings;

        if (_settings == null)
            return;

        _settings.OnVoiceVolumeChanged += OnVoiceChanged;
        _settings.OnWorldSfxVolumeChanged += OnWorldSfxChanged;
        _settings.OnSystemSfxVolumeChanged += OnSystemSfxChanged;

        // 現在値でスライダーを初期化
        SetSlider(_sliderVoice, _labelVoice, _settings.VoiceVolume);
        SetSlider(_sliderWorldSfx, _labelWorldSfx, _settings.WorldSfxVolume);
        SetSlider(_sliderSystemSfx, _labelSystemSfx, _settings.SystemSfxVolume);
    }

    public void Unbind()
    {
        if (_settings == null)
            return;
        _settings.OnVoiceVolumeChanged -= OnVoiceChanged;
        _settings.OnWorldSfxVolumeChanged -= OnWorldSfxChanged;
        _settings.OnSystemSfxVolumeChanged -= OnSystemSfxChanged;
        _settings = null;
    }

    private void OnVoiceChanged(float v) => SetSlider(_sliderVoice, _labelVoice, v);
    private void OnWorldSfxChanged(float v) => SetSlider(_sliderWorldSfx, _labelWorldSfx, v);
    private void OnSystemSfxChanged(float v) => SetSlider(_sliderSystemSfx, _labelSystemSfx, v);

    private static void SetSlider(Slider slider, Label label, float value)
    {
        if (slider != null)
            slider.SetValueWithoutNotify(value);
        if (label != null)
            label.text = $"{(int)(value * 100)}%";
    }
}
