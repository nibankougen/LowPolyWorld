using NUnit.Framework;
using UnityEngine;

public class ObjectGridSnapTests
{
    // ── 位置スナップ ──────────────────────────────────────────────────────────

    [TestCase(0f, 0)]
    [TestCase(0.5f, 1)]
    [TestCase(0.24f, 0)]
    [TestCase(0.26f, 1)]
    [TestCase(-0.5f, -1)]
    [TestCase(1.5f, 3)]
    public void SnapAxis_RoundsToHalfMeterGrid(float meters, int expected)
    {
        Assert.AreEqual(expected, ObjectGridSnap.SnapAxis(meters));
    }

    [Test]
    public void SnapPosition_And_ToWorld_RoundTrip()
    {
        var grid = ObjectGridSnap.SnapPosition(new Vector3(1.4f, -0.6f, 2.5f));
        Assert.AreEqual(3, grid.x); // 1.4 / 0.5 = 2.8 → 3
        Assert.AreEqual(-1, grid.y); // -0.6 / 0.5 = -1.2 → -1
        Assert.AreEqual(5, grid.z);

        Vector3 world = ObjectGridSnap.ToWorld(grid);
        Assert.AreEqual(1.5f, world.x, 1e-4f);
        Assert.AreEqual(-0.5f, world.y, 1e-4f);
        Assert.AreEqual(2.5f, world.z, 1e-4f);
    }

    // ── 範囲判定・クランプ ────────────────────────────────────────────────────

    [Test]
    public void IsInBounds_AcceptsEdgesRejectsBeyond()
    {
        Assert.IsTrue(ObjectGridSnap.IsInBounds(new IntVec3Json(31, 15, -31)), "境界は範囲内");
        Assert.IsTrue(ObjectGridSnap.IsInBounds(new IntVec3Json(0, 0, 0)));
        Assert.IsFalse(ObjectGridSnap.IsInBounds(new IntVec3Json(32, 0, 0)), "X 32 は範囲外");
        Assert.IsFalse(ObjectGridSnap.IsInBounds(new IntVec3Json(0, 16, 0)), "Y 16 は範囲外");
        Assert.IsFalse(ObjectGridSnap.IsInBounds(new IntVec3Json(0, 0, -32)), "Z -32 は範囲外");
    }

    [Test]
    public void Clamp_ClampsEachAxisToGrid()
    {
        var c = ObjectGridSnap.Clamp(new IntVec3Json(100, -100, 40));
        Assert.AreEqual(31, c.x);
        Assert.AreEqual(-15, c.y);
        Assert.AreEqual(31, c.z);
    }

    // ── 回転 ──────────────────────────────────────────────────────────────────

    [TestCase(0, 0)]
    [TestCase(7, 7)]
    [TestCase(8, 0)]
    [TestCase(9, 1)]
    [TestCase(-1, 7)]
    [TestCase(-8, 0)]
    public void NormalizeRotationStep_WrapsTo0Through7(int input, int expected)
    {
        Assert.AreEqual(expected, ObjectGridSnap.NormalizeRotationStep(input));
    }

    [TestCase(0f, 0)]
    [TestCase(45f, 1)]
    [TestCase(90f, 2)]
    [TestCase(360f, 0)]
    [TestCase(-45f, 7)]
    [TestCase(20f, 0)]
    [TestCase(30f, 1)]
    public void RotationStepFromDegrees_SnapsTo45(float degrees, int expected)
    {
        Assert.AreEqual(expected, ObjectGridSnap.RotationStepFromDegrees(degrees));
    }

    [Test]
    public void RotationToDegrees_MultipliesBy45()
    {
        Assert.AreEqual(0, ObjectGridSnap.RotationToDegrees(0));
        Assert.AreEqual(135, ObjectGridSnap.RotationToDegrees(3));
        Assert.AreEqual(315, ObjectGridSnap.RotationToDegrees(7));
        Assert.AreEqual(45, ObjectGridSnap.RotationToDegrees(9), "9 → 1 段 → 45°");
    }

    // ── プレイ可能範囲（64m 立方） ────────────────────────────────────────────

    [Test]
    public void IsInsidePlayArea_AcceptsWithin32mRejectsBeyond()
    {
        Assert.IsTrue(ObjectGridSnap.IsInsidePlayArea(new Vector3(0, 0, 0)));
        Assert.IsTrue(ObjectGridSnap.IsInsidePlayArea(new Vector3(32f, -32f, 32f)), "境界はプレイ範囲内");
        Assert.IsFalse(ObjectGridSnap.IsInsidePlayArea(new Vector3(32.1f, 0, 0)));
        Assert.IsFalse(ObjectGridSnap.IsInsidePlayArea(new Vector3(0, 0, -33f)));
    }
}
