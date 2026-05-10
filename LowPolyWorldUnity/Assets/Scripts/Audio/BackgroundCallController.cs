using UnityEngine;

/// <summary>
/// バックグラウンド通話モード（プレミアム機能）。
/// アプリがバックグラウンドに移行した際に 3D 描画を停止して CPU/GPU 負荷を下げつつ
/// Vivox 音声接続は維持する。フォアグラウンド復帰時に描画を再開する。
/// WorldScene に配置し、室退出時に破棄される。
/// </summary>
public class BackgroundCallController : MonoBehaviour
{
    private const int BackgroundFrameRate = 1;
    private const int ForegroundFrameRate = 60;

    private bool _premiumEnabled;
    private Camera _mainCamera;

    private void Start()
    {
        _premiumEnabled = UserManager.Instance?.Capabilities?.backgroundCall ?? false;
        // Camera.main はカメラが disabled になると null を返すため、有効な間に参照をキャッシュする
        _mainCamera = Camera.main;
    }

    private void OnApplicationPause(bool paused)
    {
        if (!_premiumEnabled) return;

        if (paused)
            EnterBackground();
        else
            ExitBackground();
    }

    private void EnterBackground()
    {
        if (_mainCamera != null)
            _mainCamera.enabled = false;

        Application.targetFrameRate = BackgroundFrameRate;
        // VoiceManager の接続はそのまま維持（何もしない）
    }

    private void ExitBackground()
    {
        if (_mainCamera != null)
            _mainCamera.enabled = true;

        Application.targetFrameRate = ForegroundFrameRate;
    }
}
