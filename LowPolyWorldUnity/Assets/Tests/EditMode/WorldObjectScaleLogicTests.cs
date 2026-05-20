using NUnit.Framework;

public class WorldObjectScaleLogicTests
{
    // ── インスタンスサイズ設定 ────────────────────────────────────────────────────

    [Test]
    public void Constructor_ExactMultiples_StoresValues()
    {
        var logic = new WorldObjectScaleLogic(1.0f, 2.0f, 0.5f);
        Assert.AreEqual(1.0f, logic.Width, 0.001f);
        Assert.AreEqual(2.0f, logic.Depth, 0.001f);
        Assert.AreEqual(0.5f, logic.Height, 0.001f);
    }

    [Test]
    public void TrySetScale_Unlocked_SetsSnappedValues()
    {
        var logic = new WorldObjectScaleLogic(1.0f, 1.0f, 1.0f);
        bool ok = logic.TrySetScale(2.0f, 3.0f, 4.0f);
        Assert.IsTrue(ok);
        Assert.AreEqual(2.0f, logic.Width, 0.001f);
        Assert.AreEqual(3.0f, logic.Depth, 0.001f);
        Assert.AreEqual(4.0f, logic.Height, 0.001f);
    }

    // ── 0.25m スナップ ─────────────────────────────────────────────────────────────

    [Test]
    public void SnapValue_ExactMultiple_ReturnsSame()
    {
        Assert.AreEqual(0.25f, WorldObjectScaleLogic.SnapValue(0.25f), 0.001f);
        Assert.AreEqual(0.75f, WorldObjectScaleLogic.SnapValue(0.75f), 0.001f);
        Assert.AreEqual(2.0f, WorldObjectScaleLogic.SnapValue(2.0f), 0.001f);
    }

    [Test]
    public void SnapValue_BelowHalfwayPoint_SnapsDown()
    {
        // 0.37 / 0.25 = 1.48 → round → 1 → 0.25
        Assert.AreEqual(0.25f, WorldObjectScaleLogic.SnapValue(0.37f), 0.001f);
    }

    [Test]
    public void SnapValue_AboveHalfwayPoint_SnapsUp()
    {
        // 0.63 / 0.25 = 2.52 → round → 3 → 0.75
        Assert.AreEqual(0.75f, WorldObjectScaleLogic.SnapValue(0.63f), 0.001f);
    }

    [Test]
    public void SnapValue_Zero_ClampsToMinimum()
    {
        Assert.AreEqual(0.25f, WorldObjectScaleLogic.SnapValue(0.0f), 0.001f);
    }

    [Test]
    public void SnapValue_Negative_ClampsToMinimum()
    {
        Assert.AreEqual(0.25f, WorldObjectScaleLogic.SnapValue(-1.0f), 0.001f);
    }

    [Test]
    public void SnapValue_SmallPositive_ClampsToMinimum()
    {
        // 0.1 / 0.25 = 0.4 → round → 0 → clamped to 0.25
        Assert.AreEqual(0.25f, WorldObjectScaleLogic.SnapValue(0.1f), 0.001f);
    }

    [Test]
    public void Constructor_NonMultiple_SnapsOnInit()
    {
        var logic = new WorldObjectScaleLogic(0.63f, 0.37f, 0.1f);
        Assert.AreEqual(0.75f, logic.Width, 0.001f);
        Assert.AreEqual(0.25f, logic.Depth, 0.001f);
        Assert.AreEqual(0.25f, logic.Height, 0.001f);
    }

    // ── スケールロック ─────────────────────────────────────────────────────────────

    [Test]
    public void ScaleLocked_Default_IsFalse()
    {
        var logic = new WorldObjectScaleLogic(1.0f, 1.0f, 1.0f);
        Assert.IsFalse(logic.ScaleLocked);
    }

    [Test]
    public void ScaleLocked_SetTrue_IsTrue()
    {
        var logic = new WorldObjectScaleLogic(1.0f, 1.0f, 1.0f, scaleLocked: true);
        Assert.IsTrue(logic.ScaleLocked);
    }

    [Test]
    public void TrySetScale_WhenLocked_ReturnsFalse()
    {
        var logic = new WorldObjectScaleLogic(1.0f, 1.0f, 1.0f, scaleLocked: true);
        bool ok = logic.TrySetScale(2.0f, 2.0f, 2.0f);
        Assert.IsFalse(ok);
    }

    [Test]
    public void TrySetScale_WhenLocked_DoesNotChangeValues()
    {
        var logic = new WorldObjectScaleLogic(1.0f, 1.0f, 1.0f, scaleLocked: true);
        logic.TrySetScale(2.0f, 3.0f, 4.0f);
        Assert.AreEqual(1.0f, logic.Width, 0.001f);
        Assert.AreEqual(1.0f, logic.Depth, 0.001f);
        Assert.AreEqual(1.0f, logic.Height, 0.001f);
    }
}
