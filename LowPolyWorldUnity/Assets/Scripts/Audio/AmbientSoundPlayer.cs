using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// ワールド環境音の再生・フェードを管理する MonoBehaviour。
/// soundId を受け取り、内蔵ライブラリからループ再生してフェードイン/アウトする。
/// ワールド設定 UI との接続は Phase 12 で行う。
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class AmbientSoundPlayer : MonoBehaviour
{
    [SerializeField] private AudioMixerGroup _worldSfxGroup;
    [SerializeField] private float _fadeDuration = 1.5f;

    [Header("Ambient Clips (none/forest/rain/ocean/wind/city/cave/night)")]
    [SerializeField] private AudioClip _forestClip;
    [SerializeField] private AudioClip _rainClip;
    [SerializeField] private AudioClip _oceanClip;
    [SerializeField] private AudioClip _windClip;
    [SerializeField] private AudioClip _cityClip;
    [SerializeField] private AudioClip _caveClip;
    [SerializeField] private AudioClip _nightClip;

    private AudioSource _audioSource;
    private Coroutine _fadeCoroutine;
    private string _currentSoundId = "none";

    private void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
        _audioSource.loop = true;
        _audioSource.playOnAwake = false;
        _audioSource.outputAudioMixerGroup = _worldSfxGroup;
        _audioSource.volume = 0f;
    }

    /// <summary>
    /// 環境音を設定して再生する。"none" で停止。
    /// </summary>
    /// <param name="soundId">none/forest/rain/ocean/wind/city/cave/night</param>
    /// <param name="volume">ワールド設定音量 0〜1（WorldSFX ユーザー設定とは別の乗算値）</param>
    public void Play(string soundId, float volume)
    {
        if (soundId == _currentSoundId)
        {
            // 音量だけ更新
            if (_fadeCoroutine != null)
                StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = StartCoroutine(FadeTo(volume));
            return;
        }

        _currentSoundId = soundId;

        if (soundId == "none")
        {
            FadeOutAndStop();
            return;
        }

        var clip = GetClip(soundId);
        if (clip == null)
        {
            Debug.LogWarning($"[AmbientSoundPlayer] Clip not found for soundId: {soundId}");
            return;
        }

        if (_fadeCoroutine != null)
            StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = StartCoroutine(CrossfadeTo(clip, volume));
    }

    public void Stop() => Play("none", 0f);

    private void FadeOutAndStop()
    {
        if (_fadeCoroutine != null)
            StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = StartCoroutine(FadeOutThenStop());
    }

    private IEnumerator CrossfadeTo(AudioClip clip, float targetVolume)
    {
        // フェードアウト
        float startVol = _audioSource.volume;
        float elapsed = 0f;
        while (elapsed < _fadeDuration * 0.5f)
        {
            elapsed += Time.deltaTime;
            _audioSource.volume = Mathf.Lerp(startVol, 0f, elapsed / (_fadeDuration * 0.5f));
            yield return null;
        }

        _audioSource.Stop();
        _audioSource.clip = clip;
        _audioSource.Play();

        // フェードイン
        elapsed = 0f;
        while (elapsed < _fadeDuration * 0.5f)
        {
            elapsed += Time.deltaTime;
            _audioSource.volume = Mathf.Lerp(0f, targetVolume, elapsed / (_fadeDuration * 0.5f));
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
            "forest" => _forestClip,
            "rain" => _rainClip,
            "ocean" => _oceanClip,
            "wind" => _windClip,
            "city" => _cityClip,
            "cave" => _caveClip,
            "night" => _nightClip,
            _ => null,
        };
}
