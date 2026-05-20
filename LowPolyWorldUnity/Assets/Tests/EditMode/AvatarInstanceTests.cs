using NUnit.Framework;

public class AvatarInstanceTests
{
    // ── IsRejected 初期値 ─────────────────────────────────────────────────────

    [Test]
    public void IsRejected_DefaultsFalse()
    {
        var instance = new AvatarInstance("u1", null, 0);
        Assert.IsFalse(instance.IsRejected);
    }

    // ── MarkRejected ──────────────────────────────────────────────────────────

    [Test]
    public void MarkRejected_SetsIsRejectedTrue()
    {
        var instance = new AvatarInstance("u1", null, 0);
        instance.MarkRejected();
        Assert.IsTrue(instance.IsRejected);
    }

    [Test]
    public void MarkRejected_CalledTwice_StaysTrue()
    {
        var instance = new AvatarInstance("u1", null, 0);
        instance.MarkRejected();
        instance.MarkRejected();
        Assert.IsTrue(instance.IsRejected);
    }

    [Test]
    public void MarkRejected_DoesNotAffectOtherInstance()
    {
        var a = new AvatarInstance("u1", null, 0);
        var b = new AvatarInstance("u2", null, 1);
        a.MarkRejected();
        Assert.IsFalse(b.IsRejected);
    }

    // ── スロット番号が -1 のアバター（スロット未割り当て）────────────────────

    [Test]
    public void MarkRejected_WithNoSlot_SetsIsRejectedTrue()
    {
        var instance = new AvatarInstance("u1", null, -1);
        instance.MarkRejected();
        Assert.IsTrue(instance.IsRejected);
    }
}
