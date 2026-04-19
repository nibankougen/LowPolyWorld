using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.Vivox;

/// <summary>
/// WorldScene 専用 Vivox 音声マネージャー。ルーム入場時に生成・退室時に破棄。
/// 仕様: unity-game-abstract.md セクション 6
/// </summary>
public class VoiceManager : MonoBehaviour
{
    public static VoiceManager Instance { get; private set; }

    // Distance attenuation parameters (unity-game-abstract.md §6)
    private const int MaxDistance  = 32;   // 無音になる距離 (m)
    private const int MinDistance  = 2;    // 最大音量距離 (m)
    private const float Rolloff    = 1f;
    // Vivox volume range: -50〜50 (default 0). Map normalizedVolume 0f-1f → -50〜50.
    private const int VivoxVolMin  = -50;
    private const int VivoxVolMax  = 50;

    private string _channelName;
    private CancellationTokenSource _cts;
    private bool _joined;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
        _cts?.Cancel();
        _cts?.Dispose();
    }

    // ── 公開 API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Vivox 初期化 → ログイン → ワールドチャンネル参加。
    /// vivoxId は /startup の User.vivoxId（仮名 UUID）。
    /// worldId を元にチャンネル名を決定する（"world_&lt;worldId&gt;"）。
    /// </summary>
    public async Task InitializeAsync(string vivoxId, string worldId, CancellationToken ct = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _channelName = $"world_{worldId}";

        try
        {
            await VivoxService.Instance.InitializeAsync();

            var loginOptions = new LoginOptions { DisplayName = vivoxId };
            await VivoxService.Instance.LoginAsync(loginOptions);

            var props = new Channel3DProperties(MaxDistance, MinDistance, Rolloff, AudioFadeModel.InverseByDistance);
            await VivoxService.Instance.JoinPositionalChannelAsync(
                _channelName,
                ChatCapability.AudioOnly,
                props
            );
            _joined = true;
        }
        catch (OperationCanceledException) { }
        catch (Exception e)
        {
            Debug.LogError($"[VoiceManager] Initialize failed: {e.Message}");
        }
    }

    /// <summary>
    /// Vivox チャンネル退出 → ログアウト。退室フローで呼び出す。
    /// </summary>
    public async Task ShutdownAsync()
    {
        _cts?.Cancel();
        if (!_joined) return;
        try
        {
            await VivoxService.Instance.LeaveChannelAsync(_channelName);
            await VivoxService.Instance.LogoutAsync();
            _joined = false;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[VoiceManager] Shutdown error: {e.Message}");
        }
    }

    /// <summary>
    /// 自プレイヤーの 3D 位置を Vivox SDK に通知する。
    /// PlayerController の LateUpdate から毎フレーム呼び出す。
    /// </summary>
    public void UpdateListenerPosition(GameObject playerObject)
    {
        if (!_joined || string.IsNullOrEmpty(_channelName) || playerObject == null) return;
        try
        {
            VivoxService.Instance.Set3DPosition(playerObject, _channelName);
        }
        catch (Exception) { }
    }

    /// <summary>
    /// 通話音量を Vivox SDK の出力音量に反映する。
    /// normalizedVolume: 0f〜1f（PlayerPrefs の Vol_Voice 値）。
    /// WorldSettingsLogic.OnVoiceVolumeChanged イベントから呼び出す。
    /// </summary>
    public void SetReceiveVolume(float normalizedVolume)
    {
        try
        {
            int vol = Mathf.RoundToInt(Mathf.Lerp(VivoxVolMin, VivoxVolMax, normalizedVolume));
            VivoxService.Instance.SetOutputDeviceVolume(vol);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[VoiceManager] SetReceiveVolume error: {e.Message}");
        }
    }
}
