using NUnit.Framework;
using UnityEngine;

public class ObjectPlaceholderTransformTests
{
    private const float Delta = 1e-4f;

    [Test]
    public void ResolveSize_UsesDefaultForSentinel()
    {
        var def = new IntVec3Json(4, 2, 6);
        Assert.AreEqual(def, ObjectPlaceholderTransform.ResolveSize(new IntVec3Json(0, 0, 0), def));
        Assert.AreEqual(def, ObjectPlaceholderTransform.ResolveSize(null, def));
        var explicitSize = new IntVec3Json(1, 1, 1);
        Assert.AreEqual(explicitSize, ObjectPlaceholderTransform.ResolveSize(explicitSize, def), "明示サイズを優先");
        Assert.AreEqual(
            new IntVec3Json(1, 1, 1).x,
            ObjectPlaceholderTransform.ResolveSize(null, null).x,
            "default も無ければ 1×1×1");
    }

    [Test]
    public void WorldScale_MapsWDHToXYZ()
    {
        // size = (W=4, D=2, H=6) → scale (W, H, D) × 0.25 = (1.0, 1.5, 0.5)
        var scale = ObjectPlaceholderTransform.WorldScale(new IntVec3Json(4, 2, 6), null);
        Assert.AreEqual(1.0f, scale.x, Delta, "X = W");
        Assert.AreEqual(1.5f, scale.y, Delta, "Y = H");
        Assert.AreEqual(0.5f, scale.z, Delta, "Z = D");
    }

    [Test]
    public void WorldCenter_PutsBottomCenterAtGridPosition()
    {
        // position (2, 0, 4) = (1.0m, 0m, 2.0m) 底面中心・H=6(1.5m) → 中心 Y = 0.75m
        var center = ObjectPlaceholderTransform.WorldCenter(
            new IntVec3Json(2, 0, 4), new IntVec3Json(4, 2, 6), null);
        Assert.AreEqual(1.0f, center.x, Delta);
        Assert.AreEqual(0.75f, center.y, Delta, "底面 y=0 + 高さ 1.5m の半分");
        Assert.AreEqual(2.0f, center.z, Delta);
    }

    [Test]
    public void WorldCenter_RespectsNonZeroBaseHeight()
    {
        // position.y = 4 = 2.0m 底面・H=2(0.5m) → 中心 Y = 2.25m
        var center = ObjectPlaceholderTransform.WorldCenter(
            new IntVec3Json(0, 4, 0), new IntVec3Json(2, 2, 2), null);
        Assert.AreEqual(2.25f, center.y, Delta);
    }

    [Test]
    public void WorldRotation_MultipliesStepBy45()
    {
        Assert.AreEqual(0f, ObjectPlaceholderTransform.WorldRotationDegrees(0), Delta);
        Assert.AreEqual(90f, ObjectPlaceholderTransform.WorldRotationDegrees(2), Delta);
        Assert.AreEqual(315f, ObjectPlaceholderTransform.WorldRotationDegrees(7), Delta);
        Assert.AreEqual(
            Quaternion.Euler(0f, 90f, 0f).eulerAngles.y,
            ObjectPlaceholderTransform.WorldRotation(2).eulerAngles.y,
            Delta);
    }
}
