using NUnit.Framework;

public class TrustLevelPromoterLogicTests
{
    private TrustLevelPromoterLogic _promoter;

    [SetUp]
    public void SetUp() => _promoter = new TrustLevelPromoterLogic();

    // ── visitor（デフォルト）─────────────────────────────────────────────────

    [Test] public void AllZero_Visitor() => Assert.AreEqual(TrustLevelPromoterLogic.Visitor, Eval());
    [Test] public void Points299_Visitor() => Assert.AreEqual(TrustLevelPromoterLogic.Visitor, Eval(points: 299));
    [Test] public void Points500_NoFriends_Visitor() => Assert.AreEqual(TrustLevelPromoterLogic.Visitor, Eval(points: 500));
    [Test] public void OneWorld_100Likes_Visitor() => Assert.AreEqual(TrustLevelPromoterLogic.Visitor, Eval(worlds: 1, maxLikes: 100));
    [Test] public void TwoWorlds_99Likes_Visitor() => Assert.AreEqual(TrustLevelPromoterLogic.Visitor, Eval(worlds: 2, maxLikes: 99));

    // visitor への非昇格境界
    [Test] public void Points999_Visitor() => Assert.AreEqual(TrustLevelPromoterLogic.Visitor, Eval(points: 999));
    [Test] public void Points299_Friends3_Visitor() => Assert.AreEqual(TrustLevelPromoterLogic.Visitor, Eval(points: 299, friends: 3));
    [Test] public void Points300_Friends2_Visitor() => Assert.AreEqual(TrustLevelPromoterLogic.Visitor, Eval(points: 300, friends: 2));

    // ── new_user ──────────────────────────────────────────────────────────────

    [Test] public void Points1000_NewUser() => Assert.AreEqual(TrustLevelPromoterLogic.NewUser, Eval(points: 1000));
    [Test] public void Points10000_NewUser() => Assert.AreEqual(TrustLevelPromoterLogic.NewUser, Eval(points: 10000));
    [Test] public void Points300_Friends3_NewUser() => Assert.AreEqual(TrustLevelPromoterLogic.NewUser, Eval(points: 300, friends: 3));
    [Test] public void Points500_Friends5_NewUser() => Assert.AreEqual(TrustLevelPromoterLogic.NewUser, Eval(points: 500, friends: 5));

    // ── user ─────────────────────────────────────────────────────────────────

    [Test] public void Premium_NoOther_User() => Assert.AreEqual(TrustLevelPromoterLogic.User, Eval(premium: true));
    [Test] public void Premium_NoPoints_User() => Assert.AreEqual(TrustLevelPromoterLogic.User, Eval(premium: true, points: 0));
    [Test] public void Points1000_Friends5_User() => Assert.AreEqual(TrustLevelPromoterLogic.User, Eval(points: 1000, friends: 5));
    [Test] public void Points5000_Friends10_User() => Assert.AreEqual(TrustLevelPromoterLogic.User, Eval(points: 5000, friends: 10));
    [Test] public void CoinPurchase_User() => Assert.AreEqual(TrustLevelPromoterLogic.User, Eval(coin: true));
    [Test] public void CoinPurchase_NoPoints_User() => Assert.AreEqual(TrustLevelPromoterLogic.User, Eval(coin: true, points: 0));

    // user 条件不足
    [Test] public void Points1000_Friends4_NewUser() => Assert.AreEqual(TrustLevelPromoterLogic.NewUser, Eval(points: 1000, friends: 4));
    [Test] public void Points999_Friends5_NewUser() => Assert.AreEqual(TrustLevelPromoterLogic.NewUser, Eval(points: 999, friends: 5));
    [Test] public void Points1000_NoFriends_NewUser() => Assert.AreEqual(TrustLevelPromoterLogic.NewUser, Eval(points: 1000));

    // プレミアムは低ポイントでも user に昇格
    [Test] public void Premium_Points100_User() => Assert.AreEqual(TrustLevelPromoterLogic.User, Eval(premium: true, points: 100));

    // ── trusted_user ─────────────────────────────────────────────────────────

    [Test] public void TwoWorlds_100Likes_TrustedUser() => Assert.AreEqual(TrustLevelPromoterLogic.TrustedUser, Eval(worlds: 2, maxLikes: 100));
    [Test] public void ThreeWorlds_200Likes_TrustedUser() => Assert.AreEqual(TrustLevelPromoterLogic.TrustedUser, Eval(worlds: 3, maxLikes: 200));
    [Test] public void TrustedBeats_Premium() => Assert.AreEqual(TrustLevelPromoterLogic.TrustedUser, Eval(premium: true, worlds: 2, maxLikes: 100));
    [Test] public void TrustedBeats_User() => Assert.AreEqual(TrustLevelPromoterLogic.TrustedUser, Eval(points: 1000, friends: 5, worlds: 2, maxLikes: 100));

    // trusted_user 条件不足
    [Test] public void OneWorld_1000Likes_Visitor() => Assert.AreEqual(TrustLevelPromoterLogic.Visitor, Eval(worlds: 1, maxLikes: 1000));
    [Test] public void TwoWorlds_99Likes_Visitor2() => Assert.AreEqual(TrustLevelPromoterLogic.Visitor, Eval(worlds: 2, maxLikes: 99));
    [Test] public void ZeroWorlds_100Likes_Visitor() => Assert.AreEqual(TrustLevelPromoterLogic.Visitor, Eval(worlds: 0, maxLikes: 100));

    // ── ロックフラグ: EvaluateLevel はレベルを返す（呼び出し元がスキップを判断）──

    [Test] public void Locked_Still_Evaluates_NewUser() =>
        Assert.AreEqual(TrustLevelPromoterLogic.NewUser, Eval(points: 1000, locked: true));

    [Test] public void Locked_Still_Evaluates_TrustedUser() =>
        Assert.AreEqual(TrustLevelPromoterLogic.TrustedUser, Eval(worlds: 2, maxLikes: 100, locked: true));

    // ── LevelRank ──────────────────────────────────────────────────────────

    [TestCase(TrustLevelPromoterLogic.Visitor, 0)]
    [TestCase(TrustLevelPromoterLogic.NewUser, 1)]
    [TestCase(TrustLevelPromoterLogic.User, 2)]
    [TestCase(TrustLevelPromoterLogic.TrustedUser, 3)]
    [TestCase("unknown", 0)]
    [TestCase("", 0)]
    public void LevelRank_ReturnsCorrectOrdinal(string level, int expected)
        => Assert.AreEqual(expected, TrustLevelPromoterLogic.LevelRank(level));

    // ── ヘルパー ──────────────────────────────────────────────────────────────

    private string Eval(
        float points = 0, bool premium = false, bool locked = false,
        int worlds = 0, long maxLikes = 0, int friends = 0, bool coin = false)
    {
        return _promoter.Evaluate(new TrustSnapshot
        {
            TrustPoints = points,
            IsPremium = premium,
            TrustLevelLocked = locked,
            PublicWorldCount = worlds,
            MaxWorldLikes = maxLikes,
            FriendCount = friends,
            HasCoinPurchase = coin,
        });
    }
}
