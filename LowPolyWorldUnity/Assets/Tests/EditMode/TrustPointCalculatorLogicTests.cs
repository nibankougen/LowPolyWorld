using NUnit.Framework;

public class TrustPointCalculatorLogicTests
{
    private TrustPointCalculatorLogic _calc;

    [SetUp]
    public void SetUp() => _calc = new TrustPointCalculatorLogic();

    // ── 1分未満は常に 0 ──────────────────────────────────────────────────────

    [Test] public void ZeroEverything_ReturnsZero() => Assert.AreEqual(0f, _calc.Calculate(0, 0, 0f));
    [Test] public void ZeroUsers_60s_ReturnsZero() => Assert.AreEqual(0f, _calc.Calculate(0, 0, 60f));
    [Test] public void ShortDuration_59s_ReturnsZero() => Assert.AreEqual(0f, _calc.Calculate(5, 5, 59f));
    [Test] public void ShortDuration_0s_ReturnsZero() => Assert.AreEqual(0f, _calc.Calculate(10, 10, 0f));

    // ── 1分ぴったりのセッション ──────────────────────────────────────────────

    [Test] public void OneUserEach_1Min() => Assert.AreEqual(1f, _calc.Calculate(1, 1, 60f));
    [Test] public void FiveUsersEach_1Min() => Assert.AreEqual(5f, _calc.Calculate(5, 5, 60f));
    [Test] public void TenUsersEach_1Min() => Assert.AreEqual(10f, _calc.Calculate(10, 10, 60f));

    // ── 合計が奇数 → float 平均、外側 floor ──────────────────────────────────

    [TestCase(5, 6, 60f, 5f)]   // avg=5.5 * 1 → floor(5.5) = 5
    [TestCase(5, 6, 120f, 11f)] // avg=5.5 * 2 → floor(11.0) = 11
    [TestCase(3, 4, 180f, 10f)] // avg=3.5 * 3 → floor(10.5) = 10
    public void OddSum(int join, int exit, float sec, float expected)
        => Assert.AreEqual(expected, _calc.Calculate(join, exit, sec));

    // ── 長時間セッション ──────────────────────────────────────────────────────

    [Test] public void TenUsers_60Min() => Assert.AreEqual(600f, _calc.Calculate(10, 10, 3600f));
    [Test] public void FiveUsers_30Min() => Assert.AreEqual(150f, _calc.Calculate(5, 5, 1800f));

    // ── 端数秒の切り捨て ─────────────────────────────────────────────────────

    [TestCase(2, 2, 90f, 2f)]   // floor(90/60)=1
    [TestCase(2, 2, 119f, 2f)]  // floor(119/60)=1
    [TestCase(2, 2, 120f, 4f)]  // floor(120/60)=2

    public void FractionalSeconds(int join, int exit, float sec, float expected)
        => Assert.AreEqual(expected, _calc.Calculate(join, exit, sec));

    // ── 他ユーザーゼロ ───────────────────────────────────────────────────────

    [Test] public void SoloLongSession_ReturnsZero() => Assert.AreEqual(0f, _calc.Calculate(0, 0, 3600f));

    // ── 片方ゼロ: avg = 0.5 ──────────────────────────────────────────────────

    [Test]
    public void OneJoinZeroExit_10Min_ReturnsFive()
    {
        // avg = (1+0)/2 = 0.5, floorMinutes = 10 → floor(0.5 * 10) = 5
        Assert.AreEqual(5f, _calc.Calculate(1, 0, 600f));
    }

    // ── 最大プレイヤー構成（23人 × 30分）────────────────────────────────────

    [Test] public void MaxPlayers_30Min() => Assert.AreEqual(690f, _calc.Calculate(23, 23, 1800f));
}
