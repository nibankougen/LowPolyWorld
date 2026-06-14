using NUnit.Framework;
using UnityEngine;

public class SpecialObjectOverlapTests
{
    private sealed class StubOccupancy : IWorldOccupancyQuery
    {
        public bool Result;
        public int Calls;
        public bool OverlapsSolid(Vector3 min, Vector3 max)
        {
            Calls++;
            return Result;
        }
    }

    private static WorldDefinitionJson Def(SpecialObjectsData so) =>
        new() { worldName = "テスト", specialObjects = so };

    private static SpawnPointData Spawn(int x, int y, int z) =>
        new() { isSet = true, position = new IntVec3Json(x, y, z) };

    // ── BoxFromGrid ───────────────────────────────────────────────────────────

    [Test]
    public void BoxFromGrid_BottomCenterAnchor_1x1_5x1()
    {
        SpecialObjectOverlap.BoxFromGrid(new IntVec3Json(4, 6, 8), out var min, out var max);
        // グリッド (4,6,8) → m (2.0, 3.0, 4.0)。XZ 中央・Y 最下部・1×1.5×1
        Assert.AreEqual(new Vector3(1.5f, 3.0f, 3.5f), min);
        Assert.AreEqual(new Vector3(2.5f, 4.5f, 4.5f), max);
    }

    // ── Collect ───────────────────────────────────────────────────────────────

    [Test]
    public void Collect_SpawnNotSet_Excluded()
    {
        var so = new SpecialObjectsData { spawn = new SpawnPointData { isSet = false } };
        Assert.AreEqual(0, SpecialObjectOverlap.Collect(Def(so)).Count);
    }

    [Test]
    public void Collect_PortalContributesEntryAndExitBoxes()
    {
        var so = new SpecialObjectsData
        {
            portals = new[]
            {
                new PortalInstance
                {
                    entryId = "e1", exitId = "x1",
                    entryPosition = new IntVec3Json(0, 0, 0),
                    exitPosition = new IntVec3Json(20, 0, 0),
                },
            },
        };
        var boxes = SpecialObjectOverlap.Collect(Def(so));
        Assert.AreEqual(2, boxes.Count, "入口・出口の 2 コライダー");
        Assert.AreEqual(SpecialObjectOverlap.Kind.PortalEntry, boxes[0].Kind);
        Assert.AreEqual("e1", boxes[0].Id);
        Assert.AreEqual(SpecialObjectOverlap.Kind.PortalExit, boxes[1].Kind);
        Assert.AreEqual("x1", boxes[1].Id);
    }

    [Test]
    public void Collect_NullSpecialObjects_Empty()
    {
        Assert.AreEqual(0, SpecialObjectOverlap.Collect(new WorldDefinitionJson()).Count);
        Assert.AreEqual(0, SpecialObjectOverlap.Collect(null).Count);
    }

    // ── 特殊オブジェクト同士の重複 ────────────────────────────────────────────

    [Test]
    public void SamePosition_BothFlagged()
    {
        var so = new SpecialObjectsData
        {
            spawn = Spawn(0, 0, 0),
            worldPortals = new[] { new WorldPortalInstance { instanceId = "w1", position = new IntVec3Json(0, 0, 0) } },
        };
        var overlapping = SpecialObjectOverlap.FindOverlapping(Def(so));
        Assert.AreEqual(2, overlapping.Count, "同一位置のスポーンとワールドポータルは重複");
        Assert.IsTrue(SpecialObjectOverlap.HasOverlap(Def(so)));
    }

    [Test]
    public void OneGridApart_Overlaps()
    {
        // 1 グリッド = 0.5m 差 → 1×1 フットプリントは重なる
        var so = new SpecialObjectsData
        {
            spawn = Spawn(0, 0, 0),
            worldPortals = new[] { new WorldPortalInstance { instanceId = "w1", position = new IntVec3Json(1, 0, 0) } },
        };
        Assert.AreEqual(2, SpecialObjectOverlap.FindOverlapping(Def(so)).Count);
    }

    [Test]
    public void TwoGridApart_Touching_NoOverlap()
    {
        // 2 グリッド = 1m 差 → 面で接するだけ（重ならない）
        var so = new SpecialObjectsData
        {
            spawn = Spawn(0, 0, 0),
            worldPortals = new[] { new WorldPortalInstance { instanceId = "w1", position = new IntVec3Json(2, 0, 0) } },
        };
        Assert.AreEqual(0, SpecialObjectOverlap.FindOverlapping(Def(so)).Count);
        Assert.IsFalse(SpecialObjectOverlap.HasOverlap(Def(so)));
    }

    [Test]
    public void StackedExactlyOnTop_NoOverlap()
    {
        // Y 3 グリッド = 1.5m 上（高さちょうど）→ 接するだけ
        var so = new SpecialObjectsData
        {
            spawn = Spawn(0, 0, 0),
            worldPortals = new[] { new WorldPortalInstance { instanceId = "w1", position = new IntVec3Json(0, 3, 0) } },
        };
        Assert.AreEqual(0, SpecialObjectOverlap.FindOverlapping(Def(so)).Count);
    }

    // ── 地形/オブジェクトとの重複（IWorldOccupancyQuery） ──────────────────────

    [Test]
    public void OccupancyQuery_FlagsSpawn_WhenSolid()
    {
        var so = new SpecialObjectsData { spawn = Spawn(5, 0, 5) };
        var occ = new StubOccupancy { Result = true };
        var overlapping = SpecialObjectOverlap.FindOverlapping(Def(so), occ);

        Assert.AreEqual(1, overlapping.Count);
        Assert.AreEqual(SpecialObjectOverlap.Kind.Spawn, overlapping[0].Kind);
        Assert.AreEqual(1, occ.Calls, "重複していない 1 個だけ占有クエリで判定");
    }

    [Test]
    public void OccupancyQuery_Null_OnlyChecksSpecialVsSpecial()
    {
        var so = new SpecialObjectsData { spawn = Spawn(5, 0, 5) };
        Assert.AreEqual(0, SpecialObjectOverlap.FindOverlapping(Def(so), null).Count);
    }

    [Test]
    public void OccupancyQuery_NotCalledForAlreadyFlaggedBoxes()
    {
        // 既に特殊同士で重複している箱には占有クエリを呼ばない（呼び出し最小化）
        var so = new SpecialObjectsData
        {
            spawn = Spawn(0, 0, 0),
            worldPortals = new[] { new WorldPortalInstance { instanceId = "w1", position = new IntVec3Json(0, 0, 0) } },
        };
        var occ = new StubOccupancy { Result = false };
        var overlapping = SpecialObjectOverlap.FindOverlapping(Def(so), occ);
        Assert.AreEqual(2, overlapping.Count);
        Assert.AreEqual(0, occ.Calls, "両方とも特殊同士で重複済み → 占有クエリ呼び出しなし");
    }
}
