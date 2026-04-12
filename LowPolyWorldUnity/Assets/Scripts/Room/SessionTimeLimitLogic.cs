using System;

/// <summary>
/// ルームセッション残り時間の計算と警告イベント発火を担う純粋 C# クラス。
/// 通常ユーザー: 90分 / プレミアム: 12時間
/// 警告タイミング: 残り10分・5分・1分
/// </summary>
public class SessionTimeLimitLogic
{
    public const float NormalUserDurationSeconds = 90f * 60f;   // 5400s
    public const float PremiumUserDurationSeconds = 12f * 60f * 60f; // 43200s

    public static readonly float[] WarningThresholds = { 600f, 300f, 60f }; // 10min, 5min, 1min

    /// <summary>警告イベント。引数は残り秒数（600 / 300 / 60）。</summary>
    public event Action<float> OnWarning;

    /// <summary>制限時間到達イベント。1回のみ発火する。</summary>
    public event Action OnExpired;

    private readonly float _totalDuration;
    private float _elapsed;
    private bool _expired;
    private readonly bool[] _warningFired = new bool[WarningThresholds.Length];

    public float TotalDuration => _totalDuration;
    public float Elapsed => _elapsed;
    public float RemainingSeconds => Math.Max(0f, _totalDuration - _elapsed);
    public bool IsExpired => _expired;

    public SessionTimeLimitLogic(bool isPremium = false)
    {
        _totalDuration = isPremium ? PremiumUserDurationSeconds : NormalUserDurationSeconds;
    }

    /// <summary>
    /// フレームごとに経過時間を加算し、警告・期限切れを判定する。
    /// </summary>
    public void Tick(float deltaTime)
    {
        if (_expired) return;

        _elapsed += deltaTime;

        float remaining = RemainingSeconds;

        // 警告判定（閾値を下回ったタイミングで1回だけ発火）
        for (int i = 0; i < WarningThresholds.Length; i++)
        {
            if (!_warningFired[i] && remaining <= WarningThresholds[i])
            {
                _warningFired[i] = true;
                OnWarning?.Invoke(WarningThresholds[i]);
            }
        }

        // 期限切れ判定
        if (remaining <= 0f)
        {
            _expired = true;
            OnExpired?.Invoke();
        }
    }
}
