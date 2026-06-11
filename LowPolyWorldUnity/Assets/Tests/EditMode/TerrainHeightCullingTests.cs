using NUnit.Framework;
using UnityEngine;

public class TerrainHeightCullingTests
{
    private TerrainVoxelStore _store;

    [SetUp]
    public void SetUp()
    {
        _store = new TerrainVoxelStore();
    }

    [Test]
    public void ComputeThreshold_NoTerrainAbove_ReturnsNoCulling()
    {
        Set(5, 2, 5, TerrainShape.Cube); // 足元の地形はヒットしない
        int threshold = Compute(new Vector3(2.6f, 1.5f, 2.7f)); // グリッド (5, 3, 5)

        Assert.AreEqual(TerrainHeightCulling.NoCulling, threshold, "屋外では何も非表示にしない");
    }

    [Test]
    public void ComputeThreshold_ReturnsFirstBlockAbovePlayer()
    {
        Set(5, 8, 5, TerrainShape.Cube);  // 最初の天井
        Set(5, 12, 5, TerrainShape.Cube); // さらに上（無視される）
        int threshold = Compute(new Vector3(2.6f, 1.2f, 2.7f)); // グリッド (5, 2, 5)

        Assert.AreEqual(8, threshold);
    }

    [Test]
    public void ComputeThreshold_BlockAtPlayerCellOrBelow_Ignored()
    {
        Set(5, 1, 5, TerrainShape.Cube);
        Set(5, 2, 5, TerrainShape.Cube); // プレイヤーと同じセル
        int threshold = Compute(new Vector3(2.6f, 1.2f, 2.7f)); // グリッド (5, 2, 5)

        Assert.AreEqual(TerrainHeightCulling.NoCulling, threshold, "走査はプレイヤーセルの 1 つ上から");
    }

    [Test]
    public void ComputeThreshold_RampCountsAsTerrain()
    {
        Set(5, 6, 5, TerrainShape.RampN);
        Assert.AreEqual(6, Compute(new Vector3(2.6f, 0.3f, 2.7f)));
    }

    [Test]
    public void ComputeThreshold_PlayerBelowWorldBottom_ScansFromZero()
    {
        Set(5, 0, 5, TerrainShape.Cube);
        Assert.AreEqual(0, Compute(new Vector3(2.6f, -0.4f, 2.7f)));
    }

    [Test]
    public void ComputeThreshold_OutOfBoundsXZ_ReturnsNoCulling()
    {
        Assert.AreEqual(TerrainHeightCulling.NoCulling, Compute(new Vector3(-0.3f, 1f, 2.7f)));
        Assert.AreEqual(TerrainHeightCulling.NoCulling, Compute(new Vector3(2.6f, 1f, 99f)));
    }

    [Test]
    public void ComputeThreshold_NullSampler_Throws()
    {
        Assert.Throws<System.ArgumentNullException>(
            () => TerrainHeightCulling.ComputeThreshold(null, Vector3.zero));
    }

    [Test]
    public void IsGridYHidden_ThresholdBoundary()
    {
        Assert.IsFalse(TerrainHeightCulling.IsGridYHidden(3, 4));
        Assert.IsTrue(TerrainHeightCulling.IsGridYHidden(4, 4), "閾値以上を非表示");
        Assert.IsTrue(TerrainHeightCulling.IsGridYHidden(10, 4));
        Assert.IsFalse(TerrainHeightCulling.IsGridYHidden(10, TerrainHeightCulling.NoCulling));
    }

    [Test]
    public void IsWorldYHidden_UsesThresholdBlockBottom()
    {
        // 閾値 4 → 非表示は y ≥ 2.0m（閾値ブロックの下端）
        Assert.IsFalse(TerrainHeightCulling.IsWorldYHidden(1.9f, 4), "同じ階の他プレイヤーは表示");
        Assert.IsTrue(TerrainHeightCulling.IsWorldYHidden(2.0f, 4));
        Assert.IsTrue(TerrainHeightCulling.IsWorldYHidden(2.5f, 4), "天井ブロックの上に立つプレイヤーは非表示");
        Assert.IsFalse(TerrainHeightCulling.IsWorldYHidden(99f, TerrainHeightCulling.NoCulling));
    }

    private int Compute(Vector3 playerPosition) =>
        TerrainHeightCulling.ComputeThreshold(new TerrainStoreSampler(_store), playerPosition);

    private void Set(int x, int y, int z, TerrainShape shape, int palette = 0) =>
        _store.SetVoxel(x, y, z, TerrainVoxel.Encode(shape, palette));
}
