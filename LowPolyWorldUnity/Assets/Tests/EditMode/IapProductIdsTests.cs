using NUnit.Framework;
using System.Collections.Generic;

public class IapProductIdsTests
{
    // ── AllCoinProductIds ─────────────────────────────────────────────────────

    [Test]
    public void AllCoinProductIds_Contains20Products()
    {
        Assert.AreEqual(20, IapProductIds.AllCoinProductIds.Length);
    }

    [Test]
    public void AllCoinProductIds_HasNoDuplicates()
    {
        var set = new HashSet<string>(IapProductIds.AllCoinProductIds);
        Assert.AreEqual(IapProductIds.AllCoinProductIds.Length, set.Count);
    }

    [Test]
    public void AllCoinProductIds_AllHavePositiveCoinAmount()
    {
        foreach (var id in IapProductIds.AllCoinProductIds)
            Assert.Greater(IapProductIds.CoinsForProductId(id), 0, $"Product '{id}' returned 0 coins");
    }

    // ── CoinsForProductId — 全20種の正引き ───────────────────────────────────

    [TestCase(IapProductIds.Coins0100,  100)]
    [TestCase(IapProductIds.Coins0200,  200)]
    [TestCase(IapProductIds.Coins0300,  300)]
    [TestCase(IapProductIds.Coins0400,  400)]
    [TestCase(IapProductIds.Coins0500,  500)]
    [TestCase(IapProductIds.Coins0600,  600)]
    [TestCase(IapProductIds.Coins0700,  700)]
    [TestCase(IapProductIds.Coins0800,  800)]
    [TestCase(IapProductIds.Coins0900,  900)]
    [TestCase(IapProductIds.Coins1000, 1000)]
    [TestCase(IapProductIds.Coins1500, 1500)]
    [TestCase(IapProductIds.Coins2000, 2000)]
    [TestCase(IapProductIds.Coins3000, 3000)]
    [TestCase(IapProductIds.Coins4000, 4000)]
    [TestCase(IapProductIds.Coins5000, 5000)]
    [TestCase(IapProductIds.Coins6000, 6000)]
    [TestCase(IapProductIds.Coins7000, 7000)]
    [TestCase(IapProductIds.Coins8000, 8000)]
    [TestCase(IapProductIds.Coins9000, 9000)]
    [TestCase(IapProductIds.Coins10000, 10000)]
    public void CoinsForProductId_ReturnsCorrectAmount(string productId, int expected)
    {
        Assert.AreEqual(expected, IapProductIds.CoinsForProductId(productId));
    }

    [Test]
    public void CoinsForProductId_UnknownId_ReturnsZero()
    {
        Assert.AreEqual(0, IapProductIds.CoinsForProductId("com.unknown.product"));
    }

    [Test]
    public void CoinsForProductId_EmptyString_ReturnsZero()
    {
        Assert.AreEqual(0, IapProductIds.CoinsForProductId(""));
    }

    [Test]
    public void CoinsForProductId_SubscriptionIds_ReturnZero()
    {
        Assert.AreEqual(0, IapProductIds.CoinsForProductId(IapProductIds.PremiumMonthly));
        Assert.AreEqual(0, IapProductIds.CoinsForProductId(IapProductIds.PremiumYearly));
    }

    // ── AllCoinProductIds と CoinsForProductId の整合性 ────────────────────────

    [Test]
    public void AllCoinProductIds_SumOfAllCoins_IsExpectedTotal()
    {
        // 100+200+...+1000 + 1500 + 2000+3000+...+10000
        int expected = (100 + 200 + 300 + 400 + 500 + 600 + 700 + 800 + 900 + 1000)
                     + 1500
                     + (2000 + 3000 + 4000 + 5000 + 6000 + 7000 + 8000 + 9000 + 10000);
        int actual = 0;
        foreach (var id in IapProductIds.AllCoinProductIds)
            actual += IapProductIds.CoinsForProductId(id);
        Assert.AreEqual(expected, actual);
    }
}
