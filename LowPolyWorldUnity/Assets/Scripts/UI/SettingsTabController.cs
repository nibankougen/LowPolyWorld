using System;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 音量3スライダー・コントロールボタントグル・通知設定トグルを管理するコントローラー。
/// HomeScreen 設定タブおよびインワールドメニュー設定タブの両方から使用する。
/// </summary>
public class SettingsTabController
{
    // PlayerPrefs キー (通知設定)
    private const string KeyNotifFriend = "Notif_FriendRequest";
    private const string KeyNotifWorldPub = "Notif_WorldPublished";
    private const string KeyNotifProduct = "Notif_ProductReleased";
    private const string KeyNotifCoin = "Notif_CoinExpiry";

    private readonly Slider _sliderVoice;
    private readonly Slider _sliderWorldSfx;
    private readonly Slider _sliderSystemSfx;
    private readonly Label _labelVoice;
    private readonly Label _labelWorldSfx;
    private readonly Label _labelSystemSfx;
    private readonly Toggle _toggleControlButtons;

    private WorldSettingsLogic _settings;
    private Action<bool> _onControlButtonsChanged;

    public event Action OnFriendScreenRequested;
    public event Action OnFollowScreenRequested;
    public event Action OnHiddenUsersRequested;
    public event Action OnHiddenWorldsRequested;

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

        // 通知設定トグル
        BindNotifToggle(root, "toggle-notif-friend", KeyNotifFriend);
        BindNotifToggle(root, "toggle-notif-world-pub", KeyNotifWorldPub);
        BindNotifToggle(root, "toggle-notif-product", KeyNotifProduct);
        BindNotifToggle(root, "toggle-notif-coin", KeyNotifCoin);

        // ソーシャルリンクボタン
        root.Q<Button>("btn-friend-screen")?.RegisterCallback<ClickEvent>(_ => OnFriendScreenRequested?.Invoke());
        root.Q<Button>("btn-follow-screen")?.RegisterCallback<ClickEvent>(_ => OnFollowScreenRequested?.Invoke());
        root.Q<Button>("btn-hidden-users")?.RegisterCallback<ClickEvent>(_ => OnHiddenUsersRequested?.Invoke());
        root.Q<Button>("btn-hidden-worlds")?.RegisterCallback<ClickEvent>(_ => OnHiddenWorldsRequested?.Invoke());
    }

    private static void BindNotifToggle(VisualElement root, string toggleName, string prefsKey)
    {
        var toggle = root.Q<Toggle>(toggleName);
        if (toggle == null) return;
        toggle.SetValueWithoutNotify(PlayerPrefs.GetInt(prefsKey, 1) == 1);
        toggle.RegisterValueChangedCallback(e =>
        {
            PlayerPrefs.SetInt(prefsKey, e.newValue ? 1 : 0);
            PlayerPrefs.Save();
        });
    }

    /// <summary>通知種別が有効かどうかを返す。</summary>
    public static bool IsNotifEnabled(string prefsKey) =>
        PlayerPrefs.GetInt(prefsKey, 1) == 1;

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
