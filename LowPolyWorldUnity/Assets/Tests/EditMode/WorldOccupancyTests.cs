using NUnit.Framework;
using UnityEngine;

public class WorldOccupancyTests
{
    // ── WorldOccupancy.TryGetObjectBox ──────────────────────────────────────────

    [Test]
    public void TryGetObjectBox_ExplicitSize_BottomCenterAnchor()
    {
        // 位置 (4,6,8) グリッド → m (2,3,4)。サイズ (W2,D4,H2) 0.25m = (0.5, 1.0, 0.5)m
        var obj = new WorldObjectInstance
        {
            position = new IntVec3Json(4, 6, 8),
            size = new IntVec3Json(2, 4, 2),
        };
        Assert.IsTrue(WorldOccupancy.TryGetObjectBox(obj, null, out var min, out var max));
        // XZ 中央・Y 最下部: x[2±0.25] y[3..3.5] z[4±0.5]
        Assert.AreEqual(new Vector3(1.75f, 3.0f, 3.5f), min);
        Assert.AreEqual(new Vector3(2.25f, 3.5f, 4.5f), max);
    }

    [Test]
    public void TryGetObjectBox_SentinelSize_UsesDefault()
    {
        var obj = new WorldObjectInstance { position = new IntVec3Json(0, 0, 0), size = new IntVec3Json(0, 0, 0) };
        var def = new IntVec3Json(4, 4, 4); // 1m 立方
        Assert.IsTrue(WorldOccupancy.TryGetObjectBox(obj, def, out var min, out var max));
        Assert.AreEqual(new Vector3(-0.5f, 0f, -0.5f), min);
        Assert.AreEqual(new Vector3(0.5f, 1.0f, 0.5f), max);
    }

    [Test]
    public void TryGetObjectBox_ExplicitSizeOverridesDefault()
    {
        var obj = new WorldObjectInstance { position = new IntVec3Json(0, 0, 0), size = new IntVec3Json(2, 2, 2) };
        Assert.IsTrue(WorldOccupancy.TryGetObjectBox(obj, new IntVec3Json(40, 40, 40), out _, out var max));
        Assert.AreEqual(new Vector3(0.25f, 0.5f, 0.25f), max, "明示サイズがデフォルトより優先");
    }

    [Test]
    public void TryGetObjectBox_Decoration_NoCollider()
    {
        var obj = new WorldObjectInstance { position = new IntVec3Json(0, 0, 0), size = new IntVec3Json(0, 0, 0) };
        Assert.IsFalse(WorldOccupancy.TryGetObjectBox(obj, null, out _, out _), "サイズ・デフォルトとも 0 → 装飾");
        Assert.IsFalse(WorldOccupancy.TryGetObjectBox(obj, new IntVec3Json(0, 0, 0), out _, out _));
    }

    [Test]
    public void TryGetObjectBox_Null_False()
    {
        Assert.IsFalse(WorldOccupancy.TryGetObjectBox(null, new IntVec3Json(4, 4, 4), out _, out _));
    }

    [Test]
    public void TryGetObjectBox_RotationIgnored()
    {
        // コライダーは回転に追従しない（3.3）→ AABB は rotationY に依らず同じ
        var a = new WorldObjectInstance { position = new IntVec3Json(2, 0, 4), size = new IntVec3Json(2, 6, 2), rotationY = 0 };
        var b = new WorldObjectInstance { position = new IntVec3Json(2, 0, 4), size = new IntVec3Json(2, 6, 2), rotationY = 2 };
        WorldOccupancy.TryGetObjectBox(a, null, out var amin, out var amax);
        WorldOccupancy.TryGetObjectBox(b, null, out var bmin, out var bmax);
        Assert.AreEqual(amin, bmin);
        Assert.AreEqual(amax, bmax);
    }

    // ── TerrainOccupancyQuery ───────────────────────────────────────────────────

    private static TerrainVoxelStore StoreWith(int x, int y, int z)
    {
        var store = new TerrainVoxelStore();
        store.SetVoxel(x, y, z, TerrainVoxel.Encode(TerrainShape.Cube, 0));
        return store;
    }

    [Test]
    public void Terrain_AabbInsideSolidVoxel_Overlaps()
    {
        // ボクセル (10,0,10) は world [5.0,5.5)×[0,0.5)×[5.0,5.5)
        var q = new TerrainOccupancyQuery(StoreWith(10, 0, 10), Vector3.zero);
        Assert.IsTrue(q.OverlapsSolid(new Vector3(5.4f, 0f, 5.0f), new Vector3(5.9f, 0.4f, 5.5f)));
    }

    [Test]
    public void Terrain_AabbTouchingFace_NoOverlap()
    {
        // ボクセル (10,0,10) の +X 面に接するだけ（半開区間）
        var q = new TerrainOccupancyQuery(StoreWith(10, 0, 10), Vector3.zero);
        Assert.IsFalse(q.OverlapsSolid(new Vector3(5.5f, 0f, 5.0f), new Vector3(6.0f, 0.5f, 5.5f)));
    }

    [Test]
    public void Terrain_AabbInEmptyRegion_NoOverlap()
    {
        var q = new TerrainOccupancyQuery(StoreWith(10, 0, 10), Vector3.zero);
        Assert.IsFalse(q.OverlapsSolid(new Vector3(0f, 0f, 0f), new Vector3(0.5f, 0.5f, 0.5f)));
    }

    [Test]
    public void Terrain_WorldOriginOffsetRespected()
    {
        // 中央ボクセル (31,15,31) を原点中心に整列させる worldOrigin
        var origin = new Vector3(-15.75f, -7.75f, -15.75f);
        var q = new TerrainOccupancyQuery(StoreWith(31, 15, 31), origin);
        // 原点まわりの小さな AABB は中央ボクセル（world [-0.25,0.25)³）に重なる
        Assert.IsTrue(q.OverlapsSolid(new Vector3(-0.1f, -0.1f, -0.1f), new Vector3(0.1f, 0.1f, 0.1f)));
    }

    [Test]
    public void Terrain_AabbOutOfRange_NoOverlapNoThrow()
    {
        var q = new TerrainOccupancyQuery(StoreWith(0, 0, 0), Vector3.zero);
        Assert.DoesNotThrow(() => q.OverlapsSolid(new Vector3(-100f, -100f, -100f), new Vector3(-90f, -90f, -90f)));
        Assert.IsFalse(q.OverlapsSolid(new Vector3(1000f, 1000f, 1000f), new Vector3(1001f, 1001f, 1001f)));
    }

    // ── WorldObjectOccupancyQuery ───────────────────────────────────────────────

    [Test]
    public void ObjectOccupancy_FromBoxes_OverlapDetected()
    {
        var q = new WorldObjectOccupancyQuery(new[] { (new Vector3(0f, 0f, 0f), new Vector3(1f, 1f, 1f)) });
        Assert.IsTrue(q.OverlapsSolid(new Vector3(0.5f, 0.5f, 0.5f), new Vector3(1.5f, 1.5f, 1.5f)));
        Assert.IsFalse(q.OverlapsSolid(new Vector3(1f, 0f, 0f), new Vector3(2f, 1f, 1f)), "面接触は重ならない");
    }

    [Test]
    public void ObjectOccupancy_FromDefinition_ExcludesDecoration()
    {
        var def = new WorldDefinitionJson
        {
            objects = new[]
            {
                new WorldObjectInstance { objectTypeId = "box", position = new IntVec3Json(0, 0, 0), size = new IntVec3Json(2, 2, 2) },
                new WorldObjectInstance { objectTypeId = "deco", position = new IntVec3Json(10, 0, 0), size = new IntVec3Json(0, 0, 0) },
            },
        };
        // deco は default も装飾（0）→ コライダーなし
        var q = WorldObjectOccupancyQuery.FromDefinition(
            def, id => id == "deco" ? new IntVec3Json(0, 0, 0) : null);

        // box (0,0,0)・サイズ0.5m立方 → world x[-0.25,0.25] と重なる
        Assert.IsTrue(q.OverlapsSolid(new Vector3(0f, 0f, 0f), new Vector3(0.2f, 0.2f, 0.2f)));
        // deco の位置 (10 グリッド = 5m) には何もない
        Assert.IsFalse(q.OverlapsSolid(new Vector3(4.9f, 0f, -0.1f), new Vector3(5.1f, 0.5f, 0.1f)));
    }

    [Test]
    public void ObjectOccupancy_NullBoxes_NeverOverlaps()
    {
        var q = new WorldObjectOccupancyQuery(null);
        Assert.IsFalse(q.OverlapsSolid(Vector3.zero, Vector3.one));
    }

    // ── CompositeOccupancyQuery ─────────────────────────────────────────────────

    private sealed class Stub : IWorldOccupancyQuery
    {
        public bool Result;
        public bool OverlapsSolid(Vector3 min, Vector3 max) => Result;
    }

    [Test]
    public void Composite_OrSemantics()
    {
        var a = new Stub { Result = false };
        var b = new Stub { Result = true };
        Assert.IsTrue(new CompositeOccupancyQuery(a, b).OverlapsSolid(Vector3.zero, Vector3.one));
        Assert.IsFalse(new CompositeOccupancyQuery(a, new Stub { Result = false }).OverlapsSolid(Vector3.zero, Vector3.one));
    }

    [Test]
    public void Composite_NullEntriesSkipped()
    {
        var q = new CompositeOccupancyQuery(null, new Stub { Result = true });
        Assert.IsTrue(q.OverlapsSolid(Vector3.zero, Vector3.one));
        Assert.DoesNotThrow(() => new CompositeOccupancyQuery((IWorldOccupancyQuery[])null).OverlapsSolid(Vector3.zero, Vector3.one));
    }

    // ── SpecialObjectOverlap との統合 ───────────────────────────────────────────

    [Test]
    public void Integration_SpawnInTerrain_FlaggedViaQuery()
    {
        // スポーンを地形ボクセルにめり込ませる
        var origin = Vector3.zero;
        var store = StoreWith(10, 0, 10); // world [5.0,5.5)×[0,0.5)×[5.0,5.5)
        var terrain = new TerrainOccupancyQuery(store, origin);

        // グリッド (10,0,10) のスポーン → m (5,0,5)・XZ中央/Y最下部 1×1.5×1 = x[4.5,5.5] y[0,1.5] z[4.5,5.5]
        var so = new SpecialObjectsData { spawn = new SpawnPointData { isSet = true, position = new IntVec3Json(10, 0, 10) } };
        var def = new WorldDefinitionJson { specialObjects = so };

        var overlapping = SpecialObjectOverlap.FindOverlapping(def, terrain);
        Assert.AreEqual(1, overlapping.Count);
        Assert.AreEqual(SpecialObjectOverlap.Kind.Spawn, overlapping[0].Kind);
    }

    [Test]
    public void Integration_SpawnOnTopOfTerrain_NotFlagged()
    {
        // 地形の上面ちょうどに立つスポーンは重複しない（半開区間）
        var store = StoreWith(10, 0, 10); // 上面 y=0.5
        var terrain = new TerrainOccupancyQuery(store, Vector3.zero);
        // スポーン Y 最下部 = グリッド y=1 → 0.5m（地形上面と一致）
        var so = new SpecialObjectsData { spawn = new SpawnPointData { isSet = true, position = new IntVec3Json(10, 1, 10) } };
        var def = new WorldDefinitionJson { specialObjects = so };
        Assert.AreEqual(0, SpecialObjectOverlap.FindOverlapping(def, terrain).Count);
    }
}
