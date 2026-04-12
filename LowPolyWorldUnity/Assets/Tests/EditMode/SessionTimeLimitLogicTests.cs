using NUnit.Framework;

public class SessionTimeLimitLogicTests
{
    // ---- 残り時間計算 ----

    [Test]
    public void RemainingSeconds_InitiallyEqualsTotalDuration()
    {
        var logic = new SessionTimeLimitLogic(isPremium: false);
        Assert.AreEqual(SessionTimeLimitLogic.NormalUserDurationSeconds, logic.RemainingSeconds, 0.001f);
    }

    [Test]
    public void RemainingSeconds_DecreasesAfterTick()
    {
        var logic = new SessionTimeLimitLogic(isPremium: false);
        logic.Tick(10f);
        Assert.AreEqual(SessionTimeLimitLogic.NormalUserDurationSeconds - 10f, logic.RemainingSeconds, 0.001f);
    }

    [Test]
    public void RemainingSeconds_NeverBelowZero()
    {
        var logic = new SessionTimeLimitLogic(isPremium: false);
        logic.Tick(SessionTimeLimitLogic.NormalUserDurationSeconds + 1000f);
        Assert.AreEqual(0f, logic.RemainingSeconds, 0.001f);
    }

    [Test]
    public void PremiumUser_HasLongerDuration()
    {
        var normal = new SessionTimeLimitLogic(isPremium: false);
        var premium = new SessionTimeLimitLogic(isPremium: true);
        Assert.Greater(premium.TotalDuration, normal.TotalDuration);
    }

    // ---- 警告イベント ----

    [Test]
    public void Warning_FiredAt10MinRemaining()
    {
        var logic = new SessionTimeLimitLogic(isPremium: false);
        float? firedAt = null;
        logic.OnWarning += t => firedAt = t;

        float tickTo = SessionTimeLimitLogic.NormalUserDurationSeconds - 600f + 1f;
        logic.Tick(tickTo);

        Assert.AreEqual(600f, firedAt, 0.001f);
    }

    [Test]
    public void Warning_FiredAt5MinRemaining()
    {
        var logic = new SessionTimeLimitLogic(isPremium: false);
        float? firedAt = null;
        logic.OnWarning += t => firedAt = t;

        float tickTo = SessionTimeLimitLogic.NormalUserDurationSeconds - 300f + 1f;
        logic.Tick(tickTo);

        Assert.AreEqual(300f, firedAt, 0.001f);
    }

    [Test]
    public void Warning_FiredAt1MinRemaining()
    {
        var logic = new SessionTimeLimitLogic(isPremium: false);
        float? firedAt = null;
        logic.OnWarning += t => firedAt = t;

        float tickTo = SessionTimeLimitLogic.NormalUserDurationSeconds - 60f + 1f;
        logic.Tick(tickTo);

        Assert.AreEqual(60f, firedAt, 0.001f);
    }

    [Test]
    public void Warning_NotFiredTwiceForSameThreshold()
    {
        var logic = new SessionTimeLimitLogic(isPremium: false);
        int count = 0;
        logic.OnWarning += _ => count++;

        float tickTo = SessionTimeLimitLogic.NormalUserDurationSeconds - 600f + 1f;
        logic.Tick(tickTo);
        logic.Tick(1f); // もう一度 tick しても再発火しない

        Assert.AreEqual(1, count);
    }

    [Test]
    public void AllThreeWarnings_FiredInOrder()
    {
        var logic = new SessionTimeLimitLogic(isPremium: false);
        var fired = new System.Collections.Generic.List<float>();
        logic.OnWarning += t => fired.Add(t);

        logic.Tick(SessionTimeLimitLogic.NormalUserDurationSeconds + 10f);

        Assert.AreEqual(3, fired.Count);
        Assert.AreEqual(600f, fired[0], 0.001f);
        Assert.AreEqual(300f, fired[1], 0.001f);
        Assert.AreEqual(60f, fired[2], 0.001f);
    }

    // ---- 期限切れイベント ----

    [Test]
    public void Expired_FiredWhenTimeRunsOut()
    {
        var logic = new SessionTimeLimitLogic(isPremium: false);
        bool fired = false;
        logic.OnExpired += () => fired = true;

        logic.Tick(SessionTimeLimitLogic.NormalUserDurationSeconds + 1f);

        Assert.IsTrue(fired);
    }

    [Test]
    public void Expired_FiredOnlyOnce()
    {
        var logic = new SessionTimeLimitLogic(isPremium: false);
        int count = 0;
        logic.OnExpired += () => count++;

        logic.Tick(SessionTimeLimitLogic.NormalUserDurationSeconds + 1f);
        logic.Tick(100f);
        logic.Tick(100f);

        Assert.AreEqual(1, count);
    }

    [Test]
    public void IsExpired_TrueAfterExpiry()
    {
        var logic = new SessionTimeLimitLogic(isPremium: false);
        logic.Tick(SessionTimeLimitLogic.NormalUserDurationSeconds + 1f);
        Assert.IsTrue(logic.IsExpired);
    }

    [Test]
    public void IsExpired_FalseBeforeExpiry()
    {
        var logic = new SessionTimeLimitLogic(isPremium: false);
        logic.Tick(10f);
        Assert.IsFalse(logic.IsExpired);
    }
}
