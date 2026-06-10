using NUnit.Framework;

public class TerrainChunkTests
{
    [Test]
    public void Index_FollowsXThenZThenYOrder()
    {
        // 格納順: X → Z → Y（仕様 15.13。X が最内ループ）
        Assert.AreEqual(0, TerrainChunk.Index(0, 0, 0));
        Assert.AreEqual(1, TerrainChunk.Index(1, 0, 0));
        Assert.AreEqual(16, TerrainChunk.Index(0, 0, 1));
        Assert.AreEqual(256, TerrainChunk.Index(0, 1, 0));
        Assert.AreEqual(TerrainChunk.VoxelCount - 1, TerrainChunk.Index(15, 15, 15));
    }

    [Test]
    public void SetGetVoxel_RoundTrips()
    {
        var chunk = new TerrainChunk();
        byte voxel = TerrainVoxel.Encode(TerrainShape.Cube, 3);

        chunk.SetVoxel(5, 7, 9, voxel);

        Assert.AreEqual(voxel, chunk.GetVoxel(5, 7, 9));
        Assert.AreEqual(TerrainVoxel.Empty, chunk.GetVoxel(5, 7, 10), "他のセルは empty のまま");
    }

    [Test]
    public void IsEmpty_TracksNonEmptyCount()
    {
        var chunk = new TerrainChunk();
        Assert.IsTrue(chunk.IsEmpty);

        chunk.SetVoxel(0, 0, 0, TerrainVoxel.Encode(TerrainShape.Cube, 0));
        Assert.IsFalse(chunk.IsEmpty);

        chunk.SetVoxel(0, 0, 0, TerrainVoxel.Empty);
        Assert.IsTrue(chunk.IsEmpty, "全ボクセルを消すと空チャンクに戻る");
    }

    [Test]
    public void Index_OutOfRange_Throws()
    {
        Assert.Throws<System.ArgumentOutOfRangeException>(() => TerrainChunk.Index(16, 0, 0));
        Assert.Throws<System.ArgumentOutOfRangeException>(() => TerrainChunk.Index(0, -1, 0));
    }

    [Test]
    public void ToBytes_FromBytes_RoundTrips()
    {
        var chunk = new TerrainChunk();
        chunk.SetVoxel(1, 2, 3, TerrainVoxel.Encode(TerrainShape.RampN, 5));
        chunk.SetVoxel(15, 15, 15, TerrainVoxel.Encode(TerrainShape.DiagNE, 15));

        var restored = TerrainChunk.FromBytes(chunk.ToBytes());

        Assert.IsNotNull(restored);
        Assert.AreEqual(chunk.GetVoxel(1, 2, 3), restored.GetVoxel(1, 2, 3));
        Assert.AreEqual(chunk.GetVoxel(15, 15, 15), restored.GetVoxel(15, 15, 15));
        Assert.IsFalse(restored.IsEmpty);
    }

    [Test]
    public void FromBytes_InvalidInput_ReturnsNull()
    {
        Assert.IsNull(TerrainChunk.FromBytes(null));
        Assert.IsNull(TerrainChunk.FromBytes(new byte[100]), "長さ不一致");

        var bad = new byte[TerrainChunk.VoxelCount];
        bad[0] = 0xA0; // shape 10 は未定義
        Assert.IsNull(TerrainChunk.FromBytes(bad), "不正なボクセルバイト");
    }
}
