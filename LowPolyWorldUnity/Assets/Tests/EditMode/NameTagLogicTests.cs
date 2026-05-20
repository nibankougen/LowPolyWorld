using NUnit.Framework;

public class NameTagLogicTests
{
    // ── ResolveDisplayName ────────────────────────────────────────────────────

    [Test]
    public void ResolveDisplayName_Null_ReturnsDefault()
    {
        Assert.AreEqual(NameTagLogic.DefaultDisplayName, NameTagLogic.ResolveDisplayName(null));
    }

    [Test]
    public void ResolveDisplayName_Empty_ReturnsDefault()
    {
        Assert.AreEqual(NameTagLogic.DefaultDisplayName, NameTagLogic.ResolveDisplayName(string.Empty));
    }

    [Test]
    public void ResolveDisplayName_Whitespace_ReturnsDefault()
    {
        Assert.AreEqual(NameTagLogic.DefaultDisplayName, NameTagLogic.ResolveDisplayName("   "));
    }

    [Test]
    public void ResolveDisplayName_Normal_ReturnsName()
    {
        Assert.AreEqual("Alice", NameTagLogic.ResolveDisplayName("Alice"));
    }

    [Test]
    public void ResolveDisplayName_WithPadding_ReturnsTrimmed()
    {
        Assert.AreEqual("Bob", NameTagLogic.ResolveDisplayName("  Bob  "));
    }

    // ── ShouldShowVerifiedBadge ───────────────────────────────────────────────

    [Test]
    public void ShouldShowVerifiedBadge_True_ReturnsTrue()
    {
        Assert.IsTrue(NameTagLogic.ShouldShowVerifiedBadge(true));
    }

    [Test]
    public void ShouldShowVerifiedBadge_False_ReturnsFalse()
    {
        Assert.IsFalse(NameTagLogic.ShouldShowVerifiedBadge(false));
    }
}
