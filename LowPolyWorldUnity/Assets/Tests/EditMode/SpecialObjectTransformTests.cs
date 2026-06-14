using NUnit.Framework;
using UnityEngine;

public class SpecialObjectTransformTests
{
    private static WorldDefinitionJson Def(SpecialObjectsData so) =>
        new() { specialObjects = so };

    // ── ResolveWorldPosition / ResolveFacingDegrees ─────────────────────────────

    [Test]
    public void ResolveWorldPosition_GridTimesHalfMeter()
    {
        // グリッド (4,6,8) → m (2,3,4)
        Assert.AreEqual(new Vector3(2f, 3f, 4f), SpecialObjectTransform.ResolveWorldPosition(new IntVec3Json(4, 6, 8)));
    }

    [Test]
    public void ResolveWorldPosition_Null_Zero()
    {
        Assert.AreEqual(Vector3.zero, SpecialObjectTransform.ResolveWorldPosition(null));
    }

    [Test]
    public void ResolveFacingDegrees_StepsToDegrees()
    {
        Assert.AreEqual(0f, SpecialObjectTransform.ResolveFacingDegrees(0));
        Assert.AreEqual(90f, SpecialObjectTransform.ResolveFacingDegrees(2));
        Assert.AreEqual(315f, SpecialObjectTransform.ResolveFacingDegrees(7));
        Assert.AreEqual(0f, SpecialObjectTransform.ResolveFacingDegrees(8), "8 段は正規化されて 0°");
    }

    // ── TryGetSpawn ─────────────────────────────────────────────────────────────

    [Test]
    public void TryGetSpawn_Set_ReturnsPositionAndFacing()
    {
        var so = new SpecialObjectsData
        {
            spawn = new SpawnPointData { isSet = true, position = new IntVec3Json(10, 0, -4), rotationY = 2 },
        };
        Assert.IsTrue(SpecialObjectTransform.TryGetSpawn(Def(so), out var t));
        Assert.AreEqual(new Vector3(5f, 0f, -2f), t.Position);
        Assert.AreEqual(90f, t.FacingDegrees);
    }

    [Test]
    public void TryGetSpawn_NotSet_False()
    {
        var so = new SpecialObjectsData { spawn = new SpawnPointData { isSet = false } };
        Assert.IsFalse(SpecialObjectTransform.TryGetSpawn(Def(so), out _));
    }

    [Test]
    public void TryGetSpawn_NullDef_False()
    {
        Assert.IsFalse(SpecialObjectTransform.TryGetSpawn(null, out _));
        Assert.IsFalse(SpecialObjectTransform.TryGetSpawn(new WorldDefinitionJson(), out _));
    }

    // ── TryResolveExitPortal（ギミック warp 用）──────────────────────────────────

    [Test]
    public void TryResolveExitPortal_Found_ReturnsExitTarget()
    {
        var so = new SpecialObjectsData
        {
            portals = new[]
            {
                new PortalInstance
                {
                    entryId = "e1", exitId = "x1",
                    entryPosition = new IntVec3Json(0, 0, 0),
                    exitPosition = new IntVec3Json(8, 0, 0), exitRotationY = 4,
                },
            },
        };
        Assert.IsTrue(SpecialObjectTransform.TryResolveExitPortal(Def(so), "x1", out var t));
        Assert.AreEqual(new Vector3(4f, 0f, 0f), t.Position);
        Assert.AreEqual(180f, t.FacingDegrees);
    }

    [Test]
    public void TryResolveExitPortal_NotFound_False()
    {
        var so = new SpecialObjectsData
        {
            portals = new[] { new PortalInstance { entryId = "e1", exitId = "x1" } },
        };
        Assert.IsFalse(SpecialObjectTransform.TryResolveExitPortal(Def(so), "missing", out _));
    }

    [Test]
    public void TryResolveExitPortal_EmptyIdOrNullPortals_False()
    {
        Assert.IsFalse(SpecialObjectTransform.TryResolveExitPortal(Def(new SpecialObjectsData()), "x1", out _));
        var so = new SpecialObjectsData { portals = new[] { new PortalInstance { exitId = "x1" } } };
        Assert.IsFalse(SpecialObjectTransform.TryResolveExitPortal(Def(so), "", out _));
        Assert.IsFalse(SpecialObjectTransform.TryResolveExitPortal(Def(so), null, out _));
    }

    // ── TryGetEntryExitTarget（組み込み転送用）──────────────────────────────────

    [Test]
    public void TryGetEntryExitTarget_PairedExit_Resolved()
    {
        var so = new SpecialObjectsData
        {
            portals = new[]
            {
                new PortalInstance
                {
                    entryId = "e1", exitId = "x1",
                    entryPosition = new IntVec3Json(0, 0, 0),
                    exitPosition = new IntVec3Json(-6, 2, 10), exitRotationY = 0,
                },
            },
        };
        Assert.IsTrue(SpecialObjectTransform.TryGetEntryExitTarget(Def(so), "e1", out var t));
        Assert.AreEqual(new Vector3(-3f, 1f, 5f), t.Position);
        Assert.AreEqual(0f, t.FacingDegrees);
    }

    [Test]
    public void TryGetEntryExitTarget_NoExitSet_False()
    {
        var so = new SpecialObjectsData
        {
            portals = new[] { new PortalInstance { entryId = "e1", exitId = "", entryPosition = new IntVec3Json(0, 0, 0) } },
        };
        Assert.IsFalse(SpecialObjectTransform.TryGetEntryExitTarget(Def(so), "e1", out _), "出口未設定は転送不可");
    }

    [Test]
    public void TryGetEntryExitTarget_EntryNotFound_False()
    {
        var so = new SpecialObjectsData
        {
            portals = new[] { new PortalInstance { entryId = "e1", exitId = "x1" } },
        };
        Assert.IsFalse(SpecialObjectTransform.TryGetEntryExitTarget(Def(so), "missing", out _));
    }
}
