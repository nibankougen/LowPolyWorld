using NUnit.Framework;

public class ReconnectionLogicTests
{
    // ---- 待機時間計算 ----

    [Test]
    public void GetWaitSeconds_Attempt1_Returns1()
    {
        Assert.AreEqual(1f, ReconnectionLogic.GetWaitSeconds(1), 0.001f);
    }

    [Test]
    public void GetWaitSeconds_Attempt2_Returns2()
    {
        Assert.AreEqual(2f, ReconnectionLogic.GetWaitSeconds(2), 0.001f);
    }

    [Test]
    public void GetWaitSeconds_Attempt3_Returns4()
    {
        Assert.AreEqual(4f, ReconnectionLogic.GetWaitSeconds(3), 0.001f);
    }

    [Test]
    public void GetWaitSeconds_Attempt4_Returns8()
    {
        Assert.AreEqual(8f, ReconnectionLogic.GetWaitSeconds(4), 0.001f);
    }

    [Test]
    public void GetWaitSeconds_Attempt5_Returns16()
    {
        Assert.AreEqual(16f, ReconnectionLogic.GetWaitSeconds(5), 0.001f);
    }

    // ---- 試行フロー ----

    [Test]
    public void Start_FiresAttemptAfterWait()
    {
        var logic = new ReconnectionLogic();
        int attemptFired = 0;
        logic.OnAttemptStarted += (_, __) => attemptFired++;

        logic.Start();
        logic.Tick(1.1f); // 1秒待機後に発火

        Assert.AreEqual(1, attemptFired);
    }

    [Test]
    public void Success_FiresSuccessEvent()
    {
        var logic = new ReconnectionLogic();
        bool success = false;
        logic.OnSuccess += () => success = true;

        logic.Start();
        logic.Tick(1.1f);
        logic.NotifySuccess();

        Assert.IsTrue(success);
    }

    [Test]
    public void Failure_AfterMaxAttempts_FiresFailureEvent()
    {
        var logic = new ReconnectionLogic();
        bool failed = false;
        logic.OnFailure += () => failed = true;

        logic.Start();
        // 5回失敗させる
        for (int i = 0; i < ReconnectionLogic.MaxAttempts; i++)
        {
            float wait = ReconnectionLogic.GetWaitSeconds(i + 1) + 0.1f;
            logic.Tick(wait);
            logic.NotifyFailure();
        }

        Assert.IsTrue(failed);
    }

    [Test]
    public void Failure_BeforeMaxAttempts_DoesNotFireFailureEvent()
    {
        var logic = new ReconnectionLogic();
        bool failed = false;
        logic.OnFailure += () => failed = true;

        logic.Start();
        logic.Tick(1.1f);
        logic.NotifyFailure(); // 1回目の失敗のみ

        Assert.IsFalse(failed);
    }

    [Test]
    public void AttemptCount_IncrementsOnEachAttempt()
    {
        var logic = new ReconnectionLogic();
        logic.Start();

        logic.Tick(1.1f);
        Assert.AreEqual(1, logic.AttemptCount);

        logic.NotifyFailure();
        logic.Tick(2.1f);
        Assert.AreEqual(2, logic.AttemptCount);
    }

    [Test]
    public void AfterSuccess_NoMoreEvents()
    {
        var logic = new ReconnectionLogic();
        int successCount = 0;
        int failureCount = 0;
        logic.OnSuccess += () => successCount++;
        logic.OnFailure += () => failureCount++;

        logic.Start();
        logic.Tick(1.1f);
        logic.NotifySuccess();
        logic.NotifySuccess(); // 2回目は無視
        logic.NotifyFailure(); // 失敗通知も無視

        Assert.AreEqual(1, successCount);
        Assert.AreEqual(0, failureCount);
    }
}
