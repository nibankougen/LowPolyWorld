using System;

/// <summary>
/// 放置（AFK）検出ロジック（純粋 C#）。
/// タッチ / 移動 / UI 操作を受信するとタイマーをリセットする。
/// 指定時間無操作で OnAfkDetected を発火する。
/// </summary>
public class AfkDetectionLogic
{
    /// <summary>通常ユーザーの放置判定時間（10分）。</summary>
    public const float DefaultAfkThresholdSeconds = 600f;

    /// <summary>放置判定イベント。</summary>
    public event Action OnAfkDetected;

    private readonly float _threshold;
    private float _idleElapsed;
    private bool _afkFired;

    public float IdleElapsed => _idleElapsed;
    public bool IsAfk => _afkFired;

    public AfkDetectionLogic(float thresholdSeconds = DefaultAfkThresholdSeconds)
    {
        _threshold = thresholdSeconds;
    }

    /// <summary>
    /// フレームごとに経過時間を加算する。
    /// </summary>
    public void Tick(float deltaTime)
    {
        if (_afkFired) return;

        _idleElapsed += deltaTime;

        if (_idleElapsed >= _threshold)
        {
            _afkFired = true;
            OnAfkDetected?.Invoke();
        }
    }

    /// <summary>
    /// ユーザー操作を受信してタイマーをリセットする。
    /// </summary>
    public void NotifyInput()
    {
        _idleElapsed = 0f;
        _afkFired = false;
    }
}
