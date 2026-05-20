using NUnit.Framework;

public class FallbackAvatarLogicTests
{
    // ── ShouldUseFallback ─────────────────────────────────────────────────────

    [Test]
    public void ShouldUseFallback_PendingAndNotLocal_ReturnsTrue()
    {
        Assert.IsTrue(FallbackAvatarLogic.ShouldUseFallback("pending", isLocal: false));
    }

    [Test]
    public void ShouldUseFallback_PendingButLocal_ReturnsFalse()
    {
        Assert.IsFalse(FallbackAvatarLogic.ShouldUseFallback("pending", isLocal: true));
    }

    [Test]
    public void ShouldUseFallback_ApprovedAndNotLocal_ReturnsFalse()
    {
        Assert.IsFalse(FallbackAvatarLogic.ShouldUseFallback("approved", isLocal: false));
    }

    [Test]
    public void ShouldUseFallback_RejectedAndNotLocal_ReturnsFalse()
    {
        Assert.IsFalse(FallbackAvatarLogic.ShouldUseFallback("rejected", isLocal: false));
    }

    [Test]
    public void ShouldUseFallback_NullStatus_ReturnsFalse()
    {
        Assert.IsFalse(FallbackAvatarLogic.ShouldUseFallback(null, isLocal: false));
    }

    [Test]
    public void ShouldUseFallback_EmptyStatus_ReturnsFalse()
    {
        Assert.IsFalse(FallbackAvatarLogic.ShouldUseFallback(string.Empty, isLocal: false));
    }
}
