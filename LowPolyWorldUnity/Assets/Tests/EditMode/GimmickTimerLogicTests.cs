using NUnit.Framework;

public class GimmickTimerLogicTests
{
    // テスト用の固定時刻プロバイダー
    private sealed class FakeClock : GimmickTimerLogic.ITimeProvider
    {
        public double NowSeconds { get; set; }
    }

    private FakeClock _clock;
    private GimmickTimerLogic _timer;

    [SetUp]
    public void SetUp()
    {
        _clock = new FakeClock { NowSeconds = 1000.0 };
        _timer = new GimmickTimerLogic(_clock);
    }

    [Test]
    public void InitialElapsed_IsZero()
    {
        Assert.AreEqual(0.0, _timer.GetElapsed(0), 0.001);
    }

    [Test]
    public void Start_ThenAdvanceClock_ElapsedIncreases()
    {
        _timer.Start(0);
        _clock.NowSeconds = 1005.0;
        Assert.AreEqual(5.0, _timer.GetElapsed(0), 0.001);
    }

    [Test]
    public void Stop_PreservesElapsed()
    {
        _timer.Start(0);
        _clock.NowSeconds = 1003.0;
        _timer.Stop(0);
        _clock.NowSeconds = 1010.0; // 時間を進めても変わらない
        Assert.AreEqual(3.0, _timer.GetElapsed(0), 0.001);
    }

    [Test]
    public void Reset_ClearsToZero()
    {
        _timer.Start(0);
        _clock.NowSeconds = 1005.0;
        _timer.Reset(0);
        Assert.AreEqual(0.0, _timer.GetElapsed(0), 0.001);
        Assert.IsFalse(_timer.IsRunning(0));
    }

    [Test]
    public void Start_WhenAlreadyRunning_DoesNotResetStart()
    {
        _timer.Start(0);
        _clock.NowSeconds = 1002.0;
        _timer.Start(0); // 2回目の Start は無視
        _clock.NowSeconds = 1005.0;
        Assert.AreEqual(5.0, _timer.GetElapsed(0), 0.001, "最初の Start 基準で計測");
    }

    [Test]
    public void StopAndRestart_ContinuesFromPreviousElapsed()
    {
        _timer.Start(0);
        _clock.NowSeconds = 1003.0;
        _timer.Stop(0); // 3秒経過で停止

        _clock.NowSeconds = 1010.0;
        _timer.Start(0); // 再開
        _clock.NowSeconds = 1012.0;

        Assert.AreEqual(5.0, _timer.GetElapsed(0), 0.001,
            "停止時の3秒 + 再開後の2秒 = 5秒");
    }

    [Test]
    public void InvalidIndex_Throws()
    {
        Assert.Throws<System.ArgumentOutOfRangeException>(() => _timer.Start(-1));
        Assert.Throws<System.ArgumentOutOfRangeException>(
            () => _timer.GetElapsed(GimmickStateManager.MaxTimers));
    }

    [Test]
    public void Snapshot_RoundTrip_PreservesState()
    {
        _timer.Start(0);
        _clock.NowSeconds = 1005.0;
        _timer.Stop(0);

        var snapshot = _timer.GetSnapshot(0);

        var timer2 = new GimmickTimerLogic(_clock);
        timer2.ApplySnapshot(0, snapshot);

        Assert.AreEqual(_timer.GetElapsed(0), timer2.GetElapsed(0), 0.001);
        Assert.AreEqual(_timer.IsRunning(0), timer2.IsRunning(0));
    }

    [Test]
    public void MultipleTimers_IndependentState()
    {
        _timer.Start(0);
        _clock.NowSeconds = 1002.0;
        _timer.Start(1);
        _clock.NowSeconds = 1005.0;

        Assert.AreEqual(5.0, _timer.GetElapsed(0), 0.001);
        Assert.AreEqual(3.0, _timer.GetElapsed(1), 0.001);
    }
}
