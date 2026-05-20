using NUnit.Framework;

public class ColliderSizeRoundingTests
{
    // ── RoundUp — 0.25m 単位切り上げ ─────────────────────────────────────────────

    [Test]
    public void RoundUp_ExactMultiple_ReturnsSame()
    {
        Assert.AreEqual(0.25f, ColliderSizeRounding.RoundUp(0.25f), 0.001f);
        Assert.AreEqual(0.50f, ColliderSizeRounding.RoundUp(0.50f), 0.001f);
        Assert.AreEqual(1.00f, ColliderSizeRounding.RoundUp(1.00f), 0.001f);
        Assert.AreEqual(2.75f, ColliderSizeRounding.RoundUp(2.75f), 0.001f);
    }

    [Test]
    public void RoundUp_SmallPositive_CeilsTo025()
    {
        // 0.01 / 0.25 = 0.04 → ceil → 1 → 0.25
        Assert.AreEqual(0.25f, ColliderSizeRounding.RoundUp(0.01f), 0.001f);
        // 0.24 / 0.25 = 0.96 → ceil → 1 → 0.25
        Assert.AreEqual(0.25f, ColliderSizeRounding.RoundUp(0.24f), 0.001f);
    }

    [Test]
    public void RoundUp_JustAboveMultiple_CeilsUp()
    {
        // 0.26 / 0.25 = 1.04 → ceil → 2 → 0.50
        Assert.AreEqual(0.50f, ColliderSizeRounding.RoundUp(0.26f), 0.001f);
    }

    [Test]
    public void RoundUp_LargeIrregular_CeilsUp()
    {
        // 1.83 / 0.25 = 7.32 → ceil → 8 → 2.00
        Assert.AreEqual(2.00f, ColliderSizeRounding.RoundUp(1.83f), 0.001f);
        // 0.91 / 0.25 = 3.64 → ceil → 4 → 1.00
        Assert.AreEqual(1.00f, ColliderSizeRounding.RoundUp(0.91f), 0.001f);
    }

    [Test]
    public void RoundUp_Zero_ReturnsZero()
    {
        Assert.AreEqual(0.0f, ColliderSizeRounding.RoundUp(0.0f), 0.001f);
    }

    [Test]
    public void RoundUp_Negative_ReturnsZero()
    {
        Assert.AreEqual(0.0f, ColliderSizeRounding.RoundUp(-0.5f), 0.001f);
        Assert.AreEqual(0.0f, ColliderSizeRounding.RoundUp(-1.0f), 0.001f);
    }

    // ── IsDecoration — 全軸 0 判定 ────────────────────────────────────────────────

    [Test]
    public void IsDecoration_AllZero_ReturnsTrue()
    {
        Assert.IsTrue(ColliderSizeRounding.IsDecoration(0f, 0f, 0f));
    }

    [Test]
    public void IsDecoration_OnlyWidthNonZero_ReturnsFalse()
    {
        Assert.IsFalse(ColliderSizeRounding.IsDecoration(0.25f, 0f, 0f));
    }

    [Test]
    public void IsDecoration_OnlyDepthNonZero_ReturnsFalse()
    {
        Assert.IsFalse(ColliderSizeRounding.IsDecoration(0f, 0.25f, 0f));
    }

    [Test]
    public void IsDecoration_OnlyHeightNonZero_ReturnsFalse()
    {
        Assert.IsFalse(ColliderSizeRounding.IsDecoration(0f, 0f, 0.25f));
    }

    [Test]
    public void IsDecoration_AllNonZero_ReturnsFalse()
    {
        Assert.IsFalse(ColliderSizeRounding.IsDecoration(1.0f, 1.0f, 1.0f));
    }
}
