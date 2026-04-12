using NUnit.Framework;

public class AfkDetectionLogicTests
{
    [Test]
    public void AfkNotDetected_BeforeThreshold()
    {
        var logic = new AfkDetectionLogic(600f);
        logic.Tick(599f);
        Assert.IsFalse(logic.IsAfk);
    }

    [Test]
    public void AfkDetected_AtThreshold()
    {
        var logic = new AfkDetectionLogic(600f);
        bool fired = false;
        logic.OnAfkDetected += () => fired = true;

        logic.Tick(600f);

        Assert.IsTrue(fired);
        Assert.IsTrue(logic.IsAfk);
    }

    [Test]
    public void AfkDetected_AfterThreshold()
    {
        var logic = new AfkDetectionLogic(600f);
        bool fired = false;
        logic.OnAfkDetected += () => fired = true;

        logic.Tick(601f);

        Assert.IsTrue(fired);
    }

    [Test]
    public void NotifyInput_ResetsTimer()
    {
        var logic = new AfkDetectionLogic(600f);
        logic.Tick(500f);
        logic.NotifyInput();

        Assert.AreEqual(0f, logic.IdleElapsed, 0.001f);
    }

    [Test]
    public void NotifyInput_AfterReset_CountsFromZero()
    {
        var logic = new AfkDetectionLogic(600f);
        bool fired = false;
        logic.OnAfkDetected += () => fired = true;

        logic.Tick(500f);
        logic.NotifyInput();
        logic.Tick(599f); // リセット後 599 秒 — まだ AFK 未満

        Assert.IsFalse(fired);
    }

    [Test]
    public void NotifyInput_AfterReset_EventFiresAgainAfterThreshold()
    {
        var logic = new AfkDetectionLogic(600f);
        int count = 0;
        logic.OnAfkDetected += () => count++;

        logic.Tick(600f);         // 1回目 AFK
        logic.NotifyInput();      // リセット
        logic.Tick(600f);         // 2回目 AFK

        Assert.AreEqual(2, count);
    }

    [Test]
    public void AfkEvent_FiredOnlyOnce_WithoutReset()
    {
        var logic = new AfkDetectionLogic(600f);
        int count = 0;
        logic.OnAfkDetected += () => count++;

        logic.Tick(600f);
        logic.Tick(600f); // 二度目の Tick でも再発火しない

        Assert.AreEqual(1, count);
    }
}
