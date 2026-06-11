using NUnit.Framework;

public class TerrainStoreSamplerTests
{
    [Test]
    public void GetVoxel_InBounds_ReturnsStoreValue()
    {
        var store = new TerrainVoxelStore();
        byte v = TerrainVoxel.Encode(TerrainShape.RampE, 5);
        store.SetVoxel(10, 10, 10, v);

        var sampler = new TerrainStoreSampler(store);
        Assert.AreEqual(v, sampler.GetVoxel(10, 10, 10));
        Assert.AreEqual(TerrainVoxel.Empty, sampler.GetVoxel(0, 0, 0));
    }

    [Test]
    public void GetVoxel_BelowWorldBottom_ReturnsVirtualGround()
    {
        var sampler = new TerrainStoreSampler(new TerrainVoxelStore());
        byte below = sampler.GetVoxel(5, -1, 5);
        Assert.IsFalse(TerrainVoxel.IsEmpty(below), "ワールド下端の下は「地形あり」扱い（15.14）");
        Assert.AreEqual(TerrainShape.Cube, TerrainVoxel.GetShape(below));
    }

    [Test]
    public void GetVoxel_OtherOutOfBounds_ReturnsEmpty()
    {
        var sampler = new TerrainStoreSampler(new TerrainVoxelStore());
        Assert.AreEqual(TerrainVoxel.Empty, sampler.GetVoxel(-1, 0, 0));
        Assert.AreEqual(TerrainVoxel.Empty, sampler.GetVoxel(63, 0, 0));
        Assert.AreEqual(TerrainVoxel.Empty, sampler.GetVoxel(0, 31, 0), "ワールド上端の上は empty");
        Assert.AreEqual(TerrainVoxel.Empty, sampler.GetVoxel(0, 0, 63));
    }

    [Test]
    public void Constructor_NullStore_Throws()
    {
        Assert.Throws<System.ArgumentNullException>(() => new TerrainStoreSampler(null));
    }
}
