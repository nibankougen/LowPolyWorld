using System;

/// <summary>
/// 指数バックオフ再接続ロジック（純粋 C#）。
/// 試行 n 回目の待機時間: 2^(n-1) 秒（1→2→4→8→16）
/// 最大試行回数: 5 回
/// </summary>
public class ReconnectionLogic
{
    public const int MaxAttempts = 5;

    /// <summary>再接続試行開始イベント。引数: (試行回数, 待機秒数)。</summary>
    public event Action<int, float> OnAttemptStarted;

    /// <summary>再接続成功イベント。</summary>
    public event Action OnSuccess;

    /// <summary>最大試行回数に達して失敗したイベント。</summary>
    public event Action OnFailure;

    private int _attemptCount;
    private float _waitTimer;
    private bool _waiting;
    private bool _finished;

    public int AttemptCount => _attemptCount;
    public bool IsActive => !_finished;

    /// <summary>試行 n 回目の待機秒数を返す（1-indexed）。</summary>
    public static float GetWaitSeconds(int attempt) =>
        (float)System.Math.Pow(2.0, attempt - 1);

    /// <summary>再接続シーケンスを開始する。</summary>
    public void Start()
    {
        _attemptCount = 0;
        _waitTimer = 0f;
        _waiting = false;
        _finished = false;
        BeginNextAttempt();
    }

    /// <summary>フレームごとに待機タイマーを進める。</summary>
    public void Tick(float deltaTime)
    {
        if (_finished || !_waiting) return;

        _waitTimer -= deltaTime;
        if (_waitTimer <= 0f)
        {
            _waiting = false;
            OnAttemptStarted?.Invoke(_attemptCount, GetWaitSeconds(_attemptCount));
        }
    }

    /// <summary>接続成功を通知する。</summary>
    public void NotifySuccess()
    {
        if (_finished) return;
        _finished = true;
        _waiting = false;
        OnSuccess?.Invoke();
    }

    /// <summary>接続失敗を通知する。次の試行へ進むか、上限到達で終了する。</summary>
    public void NotifyFailure()
    {
        if (_finished) return;

        if (_attemptCount >= MaxAttempts)
        {
            _finished = true;
            OnFailure?.Invoke();
            return;
        }

        BeginNextAttempt();
    }

    private void BeginNextAttempt()
    {
        _attemptCount++;
        _waitTimer = GetWaitSeconds(_attemptCount);
        _waiting = true;
    }
}
