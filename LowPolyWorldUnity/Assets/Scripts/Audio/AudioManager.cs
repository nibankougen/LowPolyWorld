using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// DontDestroyOnLoad オーディオ管理 MonoBehaviour。
/// WorldSettingsLogic を所有し、PlayerPrefs への読み書きと AudioMixer への適用を行う。
/// </summary>
public class AudioManager : MonoBehaviour
{
    private const string KeyVoice = "Vol_Voice";
    private const string KeyWorldSfx = "Vol_WorldSFX";
    private const string KeySystemSfx = "Vol_SystemSFX";

    private const string MixerParamWorldSfx = "Vol_WorldSFX_Exposed";
    private const string MixerParamSystemSfx = "Vol_SystemSFX_Exposed";

    [SerializeField] private AudioMixer _mixer;

    public static AudioManager Instance { get; private set; }
    public WorldSettingsLogic Settings { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        float voice = PlayerPrefs.GetFloat(KeyVoice, 1.0f);
        float worldSfx = PlayerPrefs.GetFloat(KeyWorldSfx, 1.0f);
        float systemSfx = PlayerPrefs.GetFloat(KeySystemSfx, 1.0f);

        Settings = new WorldSettingsLogic(voice, worldSfx, systemSfx);

        Settings.OnVoiceVolumeChanged += OnVoiceChanged;
        Settings.OnWorldSfxVolumeChanged += OnWorldSfxChanged;
        Settings.OnSystemSfxVolumeChanged += OnSystemSfxChanged;

        ApplyAllToMixer();
    }

    private void OnDestroy()
    {
        if (Settings == null)
            return;
        Settings.OnVoiceVolumeChanged -= OnVoiceChanged;
        Settings.OnWorldSfxVolumeChanged -= OnWorldSfxChanged;
        Settings.OnSystemSfxVolumeChanged -= OnSystemSfxChanged;
    }

    private void OnVoiceChanged(float value)
    {
        // Phase 6 で Vivox SDK へ適用
        PlayerPrefs.SetFloat(KeyVoice, value);
        PlayerPrefs.Save();
    }

    private void OnWorldSfxChanged(float value)
    {
        ApplyToMixer(MixerParamWorldSfx, value);
        PlayerPrefs.SetFloat(KeyWorldSfx, value);
        PlayerPrefs.Save();
    }

    private void OnSystemSfxChanged(float value)
    {
        ApplyToMixer(MixerParamSystemSfx, value);
        PlayerPrefs.SetFloat(KeySystemSfx, value);
        PlayerPrefs.Save();
    }

    private void ApplyAllToMixer()
    {
        ApplyToMixer(MixerParamWorldSfx, Settings.WorldSfxVolume);
        ApplyToMixer(MixerParamSystemSfx, Settings.SystemSfxVolume);
    }

    private void ApplyToMixer(string paramName, float linearValue)
    {
        if (_mixer == null)
            return;
        float dB = linearValue > 0.0001f ? 20f * Mathf.Log10(linearValue) : -80f;
        _mixer.SetFloat(paramName, dB);
    }
}
