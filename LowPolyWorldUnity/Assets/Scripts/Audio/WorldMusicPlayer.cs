using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// ワールド BGM の再生・フェードを管理する MonoBehaviour。
/// 環境音と BGM を統合した内蔵ライブラリからトラックを再生する。
/// デフォルトトラックはワールド入場時に SetDefault() で設定し、
/// ギミックアクションからは SwitchTo() / ResetToDefault() で切り替える。
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class WorldMusicPlayer : MonoBehaviour
{
    [SerializeField] private AudioMixerGroup _worldSfxGroup;
    [SerializeField] private float _fadeDuration = 1.5f;

    [Header("Ambient Clips")]
    [SerializeField] private AudioClip _rainClip;
    [SerializeField] private AudioClip _oceanClip;
    [SerializeField] private AudioClip _windClip;
    [SerializeField] private AudioClip _caveClip;
    [SerializeField] private AudioClip _darkFactoryClip;

    [Header("BGM Clips")]
    [SerializeField] private AudioClip _bgmFunNightStageClip;
    [SerializeField] private AudioClip _bgmBrightPlainsClip;
    [SerializeField] private AudioClip _bgmATenseMomentClip;

    private AudioSource _audioSource;
    private Coroutine _fadeCoroutine;
    private readonly WorldMusicLogic _logic = new();

    private void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
        _audioSource.loop = true;
        _audioSource.playOnAwake = false;
        _audioSource.outputAudioMixerGroup = _worldSfxGroup;
        _audioSource.volume = 0f;
    }

    /// <summary>
    /// ワールド入場時: デフォルトトラックを設定して再生開始する。
    /// volume は 0〜1（WorldSFX ユーザー設定とは別の乗算値）。
    /// </summary>
    public void SetDefault(string soundId, float volume)
    {
        var state = _logic.SetDefault(soundId, volume);
        ApplyState(state);
    }

    /// <summary>
    /// ギミック「BGM を切り替える」アクション。
    /// </summary>
    public void SwitchTo(string soundId, float volume, bool loop = true)
    {
        var state = _logic.SwitchTo(soundId, volume, loop);
        ApplyState(state);
    }

    /// <summary>
    /// ギミック「状態リセット」アクション — デフォルトトラックへ復帰する。
    /// </summary>
    public void ResetToDefault()
    {
        var state = _logic.ResetToDefault();
        ApplyState(state);
    }

    /// <summary>
    /// ワールド退場時に停止する。
    /// </summary>
    public void Stop()
    {
        if (!_audioSource.isPlaying)
            return;
        if (_fadeCoroutine != null)
            StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = StartCoroutine(FadeOutThenStop());
    }

    private void ApplyState(WorldMusicLogic.TrackState state)
    {
        if (state.SoundId == "none")
        {
            FadeOutAndStop();
            return;
        }

        var clip = GetClip(state.SoundId);
        if (clip == null)
        {
            Debug.LogWarning($"[WorldMusicPlayer] Clip not found for soundId: {state.SoundId}");
            return;
        }

        if (_fadeCoroutine != null)
            StopCoroutine(_fadeCoroutine);

        _audioSource.loop = state.Loop;

        if (_audioSource.isPlaying && _audioSource.clip == clip)
        {
            _fadeCoroutine = StartCoroutine(FadeTo(state.Volume));
        }
        else
        {
            _fadeCoroutine = StartCoroutine(CrossfadeTo(clip, state.Volume));
        }
    }

    private void FadeOutAndStop()
    {
        if (_fadeCoroutine != null)
            StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = StartCoroutine(FadeOutThenStop());
    }

    private IEnumerator CrossfadeTo(AudioClip clip, float targetVolume)
    {
        float startVol = _audioSource.volume;
        float elapsed = 0f;
        float half = _fadeDuration * 0.5f;

        while (elapsed < half)
        {
            elapsed += Time.deltaTime;
            _audioSource.volume = Mathf.Lerp(startVol, 0f, elapsed / half);
            yield return null;
        }

        _audioSource.Stop();
        _audioSource.clip = clip;
        _audioSource.Play();

        elapsed = 0f;
        while (elapsed < half)
        {
            elapsed += Time.deltaTime;
            _audioSource.volume = Mathf.Lerp(0f, targetVolume, elapsed / half);
            yield return null;
        }

        _audioSource.volume = targetVolume;
        _fadeCoroutine = null;
    }

    private IEnumerator FadeTo(float targetVolume)
    {
        float startVol = _audioSource.volume;
        float elapsed = 0f;
        while (elapsed < _fadeDuration)
        {
            elapsed += Time.deltaTime;
            _audioSource.volume = Mathf.Lerp(startVol, targetVolume, elapsed / _fadeDuration);
            yield return null;
        }
        _audioSource.volume = targetVolume;
        _fadeCoroutine = null;
    }

    private IEnumerator FadeOutThenStop()
    {
        float startVol = _audioSource.volume;
        float elapsed = 0f;
        while (elapsed < _fadeDuration)
        {
            elapsed += Time.deltaTime;
            _audioSource.volume = Mathf.Lerp(startVol, 0f, elapsed / _fadeDuration);
            yield return null;
        }
        _audioSource.Stop();
        _audioSource.volume = 0f;
        _fadeCoroutine = null;
    }

    private AudioClip GetClip(string soundId) =>
        soundId switch
        {
            "rain" => _rainClip,
            "ocean" => _oceanClip,
            "wind" => _windClip,
            "cave" => _caveClip,
            "darkFactory" => _darkFactoryClip,
            "bgmFunNightStage" => _bgmFunNightStageClip,
            "bgmBrightPlains" => _bgmBrightPlainsClip,
            "bgmATenseMoment" => _bgmATenseMomentClip,
            _ => null,
        };
}
