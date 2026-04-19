using System;
using System.Collections.Generic;
using NUnit.Framework;

public class CoinLedgerTests
{
    private static readonly DateTime Now = new DateTime(2026, 4, 19, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Future = Now.AddMonths(3);
    private static readonly DateTime Past = Now.AddDays(-1);

    private CoinLedger MakeLedger(int coins, int deducted = 0, int spent = 0, DateTime? validUntil = null)
    {
        var ledger = new CoinLedger();
        var lots = new List<CoinLedger.CoinLot>
        {
            new CoinLedger.CoinLot
            {
                PurchaseId = "p1",
                Coins = coins,
                ValidUntil = validUntil ?? Future,
            },
        };
        ledger.SetFromApi(lots, deducted, spent);
        return ledger;
    }

    [Test]
    public void Balance_SingleLot_ReturnsCoinsMinusDeductedAndSpent()
    {
        var ledger = MakeLedger(500, deducted: 50, spent: 100);
        Assert.AreEqual(350, ledger.GetBalance(Now));
    }

    [Test]
    public void Balance_ExpiredLot_ExcludesExpiredCoins()
    {
        var ledger = MakeLedger(500, validUntil: Past);
        Assert.AreEqual(0, ledger.GetBalance(Now));
    }

    [Test]
    public void Balance_MultipleLots_SumsOnlyValidLots()
    {
        var ledger = new CoinLedger();
        var lots = new List<CoinLedger.CoinLot>
        {
            new CoinLedger.CoinLot { PurchaseId = "a", Coins = 300, ValidUntil = Future },
            new CoinLedger.CoinLot { PurchaseId = "b", Coins = 200, ValidUntil = Past },
        };
        ledger.SetFromApi(lots, 0, 0);
        Assert.AreEqual(300, ledger.GetBalance(Now));
    }

    [Test]
    public void Balance_NegativeBalance_AllowedByDesign()
    {
        var ledger = MakeLedger(100, deducted: 150);
        Assert.AreEqual(-50, ledger.GetBalance(Now));
    }

    [Test]
    public void CanPurchaseProduct_SufficientBalance_ReturnsTrue()
    {
        var ledger = MakeLedger(500);
        Assert.IsTrue(ledger.CanPurchaseProduct(100, Now));
    }

    [Test]
    public void CanPurchaseProduct_InsufficientBalance_ReturnsFalse()
    {
        var ledger = MakeLedger(50);
        Assert.IsFalse(ledger.CanPurchaseProduct(100, Now));
    }

    [Test]
    public void CanPurchaseProduct_NegativeBalance_ReturnsFalse()
    {
        var ledger = MakeLedger(100, deducted: 150);
        Assert.IsFalse(ledger.CanPurchaseProduct(1, Now));
    }

    [Test]
    public void CanPurchaseProduct_ExactBalance_ReturnsTrue()
    {
        var ledger = MakeLedger(100);
        Assert.IsTrue(ledger.CanPurchaseProduct(100, Now));
    }

    [Test]
    public void ComputeCancellationDeduction_ValidLot_ReturnsCoinsAmount()
    {
        var ledger = new CoinLedger();
        int deducted = ledger.ComputeCancellationDeduction("p1", 300, Future, Now);
        Assert.AreEqual(300, deducted);
    }

    [Test]
    public void ComputeCancellationDeduction_ExpiredLot_ReturnsZero()
    {
        var ledger = new CoinLedger();
        int deducted = ledger.ComputeCancellationDeduction("p1", 300, Past, Now);
        Assert.AreEqual(0, deducted);
    }
}
