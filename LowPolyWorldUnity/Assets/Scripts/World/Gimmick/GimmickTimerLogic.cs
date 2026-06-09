using System;

/// <summary>
/// ギミックタイマーのロジッククラス（world-creation.md セクション 9.2）。
/// 最大 5 つのタイマーを管理する。
///
/// 同期方式: { startTimestamp, isRunning, elapsedAtStop } で状態を保持し、
/// 各クライアントがローカルで現在の経過秒を計算する。
/// </summary>
public class GimmickTimerLogic
{
    public const int MaxTimers = GimmickStateManager.MaxTimers;

    /// <summary>時刻プロバイダー（テスト時は差し替え可能）。</summary>
    public interface ITimeProvider
    {
        double NowSeconds { get; }
    }

    private sealed class DefaultTimeProvider : ITimeProvider
    {
        public double NowSeconds => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
    }

    // タイマー状態
    private readonly double[] _startTimestamps;
    private readonly bool[] _isRunning;
    private readonly double[] _elapsedAtStop;

    private readonly ITimeProvider _clock;

    public GimmickTimerLogic(ITimeProvider clock = null)
    {
        _clock = clock ?? new DefaultTimeProvider();
        _startTimestamps = new double[MaxTimers];
        _isRunning = new bool[MaxTimers];
        _elapsedAtStop = new double[MaxTimers];
    }

    // ── タイマー操作 ──────────────────────────────────────────────────────────

    /// <summary>タイマーを開始する。すでに動作中の場合は何もしない。</summary>
    public void Start(int index)
    {
        Validate(index);
        if (_isRunning[index]) return;
        _startTimestamps[index] = _clock.NowSeconds;
        _isRunning[index] = true;
    }

    /// <summary>タイマーを停止する。経過秒を保存する。</summary>
    public void Stop(int index)
    {
        Validate(index);
        if (!_isRunning[index]) return;
        _elapsedAtStop[index] = GetElapsed(index);
        _isRunning[index] = false;
    }

    /// <summary>タイマーをリセット（0 に戻す）し、停止状態にする。</summary>
    public void Reset(int index)
    {
        Validate(index);
        _isRunning[index] = false;
        _elapsedAtStop[index] = 0;
        _startTimestamps[index] = 0;
    }

    // ── タイマー値取得 ────────────────────────────────────────────────────────

    /// <summary>現在の経過秒を返す（停止中でも保存済みの値を返す）。</summary>
    public double GetElapsed(int index)
    {
        Validate(index);
        if (!_isRunning[index])
            return _elapsedAtStop[index];
        return _elapsedAtStop[index] + (_clock.NowSeconds - _startTimestamps[index]);
    }

    public bool IsRunning(int index)
    {
        Validate(index);
        return _isRunning[index];
    }

    // ── 同期スナップショット ──────────────────────────────────────────────────

    public readonly struct TimerSnapshot
    {
        public double StartTimestamp { get; }
        public bool IsRunning { get; }
        public double ElapsedAtStop { get; }

        public TimerSnapshot(double ts, bool running, double elapsed)
        {
            StartTimestamp = ts;
            IsRunning = running;
            ElapsedAtStop = elapsed;
        }
    }

    /// <summary>入室同期用のスナップショットを取得する。</summary>
    public TimerSnapshot GetSnapshot(int index)
    {
        Validate(index);
        return new TimerSnapshot(_startTimestamps[index], _isRunning[index], _elapsedAtStop[index]);
    }

    /// <summary>他クライアントから受信したスナップショットを適用する。</summary>
    public void ApplySnapshot(int index, TimerSnapshot snapshot)
    {
        Validate(index);
        _startTimestamps[index] = snapshot.StartTimestamp;
        _isRunning[index] = snapshot.IsRunning;
        _elapsedAtStop[index] = snapshot.ElapsedAtStop;
    }

    private static void Validate(int index)
    {
        if ((uint)index >= MaxTimers)
            throw new ArgumentOutOfRangeException(nameof(index),
                $"タイマーインデックスは 0〜{MaxTimers - 1} の範囲で指定してください。");
    }
}
