using NUnit.Framework;

public class ObjectGizmoLogicTests
{
    private static WorldObjectInstance NewObj() => new WorldObjectInstance
    {
        instanceId = "o1",
        objectTypeId = "desk",
        position = new IntVec3Json(0, 0, 0),
        rotationY = 0,
        size = new IntVec3Json(),
    };

    // ── 移動 ──────────────────────────────────────────────────────────────────

    [Test]
    public void TryMoveTo_WithinBounds_MovesAndReturnsTrue()
    {
        var obj = NewObj();
        Assert.IsTrue(ObjectGizmoLogic.TryMoveTo(obj, new IntVec3Json(10, 5, -10)));
        Assert.AreEqual(10, obj.position.x);
        Assert.AreEqual(5, obj.position.y);
        Assert.AreEqual(-10, obj.position.z);
    }

    [Test]
    public void TryMoveTo_OutOfBounds_RejectsWithoutChange()
    {
        var obj = NewObj();
        Assert.IsFalse(ObjectGizmoLogic.TryMoveTo(obj, new IntVec3Json(32, 0, 0)));
        Assert.AreEqual(0, obj.position.x, "範囲外は変更しない（グループ一括キャンセル用）");
    }

    [Test]
    public void TryMoveBy_AccumulatesFromCurrent()
    {
        var obj = NewObj();
        ObjectGizmoLogic.TryMoveBy(obj, 3, 0, 0);
        Assert.IsTrue(ObjectGizmoLogic.TryMoveBy(obj, 2, 1, -1));
        Assert.AreEqual(5, obj.position.x);
        Assert.AreEqual(1, obj.position.y);
        Assert.AreEqual(-1, obj.position.z);
    }

    [Test]
    public void TryMoveBy_BeyondEdge_RejectsWithoutChange()
    {
        var obj = NewObj();
        ObjectGizmoLogic.TryMoveTo(obj, new IntVec3Json(31, 0, 0));
        Assert.IsFalse(ObjectGizmoLogic.TryMoveBy(obj, 1, 0, 0), "31 から +1 は範囲外");
        Assert.AreEqual(31, obj.position.x);
    }

    // ── 回転 ──────────────────────────────────────────────────────────────────

    [Test]
    public void RotateBy_WrapsThrough0To7()
    {
        var obj = NewObj();
        ObjectGizmoLogic.RotateBy(obj, 1);
        Assert.AreEqual(1, obj.rotationY);
        ObjectGizmoLogic.RotateBy(obj, 7);
        Assert.AreEqual(0, obj.rotationY, "1 + 7 = 8 → 0 に巻き戻し");
        ObjectGizmoLogic.RotateBy(obj, -1);
        Assert.AreEqual(7, obj.rotationY);
    }

    // ── 拡大縮小 ──────────────────────────────────────────────────────────────

    [Test]
    public void TryScaleBy_FromSentinel_ResolvesDefaultSize()
    {
        var obj = NewObj(); // size = (0,0,0) センチネル
        var def = new IntVec3Json(4, 6, 4); // 種別デフォルト（0.25m 単位）
        Assert.IsTrue(ObjectGizmoLogic.TryScaleBy(obj, 1, -1, 0, def, scaleLocked: false));
        Assert.AreEqual(5, obj.size.x);
        Assert.AreEqual(5, obj.size.y);
        Assert.AreEqual(4, obj.size.z);
    }

    [Test]
    public void TryScaleBy_ScaleLocked_Rejects()
    {
        var obj = NewObj();
        Assert.IsFalse(ObjectGizmoLogic.TryScaleBy(obj, 1, 0, 0, new IntVec3Json(4, 4, 4), scaleLocked: true));
        Assert.IsTrue(obj.size.IsZero, "ロック中は変更しない");
    }

    [Test]
    public void TryScaleBy_BelowMinimum_RejectsWithoutChange()
    {
        var obj = NewObj();
        obj.size = new IntVec3Json(2, 1, 3);
        Assert.IsFalse(
            ObjectGizmoLogic.TryScaleBy(obj, 0, -1, 0, null, scaleLocked: false),
            "1 から -1 = 0 < 最小 1 → キャンセル");
        Assert.AreEqual(1, obj.size.y, "変更しない");
    }

    [Test]
    public void TryScaleBy_AtMinimum_StaysAtMinimum()
    {
        var obj = NewObj();
        obj.size = new IntVec3Json(1, 1, 1);
        Assert.IsTrue(ObjectGizmoLogic.TryScaleBy(obj, 2, 0, 0, null, scaleLocked: false));
        Assert.AreEqual(3, obj.size.x);
        Assert.AreEqual(1, obj.size.y);
    }
}
