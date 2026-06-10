using NUnit.Framework;

public class TerrainVoxelStoreTests
{
    private TerrainVoxelStore _store;

    [SetUp]
    public void SetUp()
    {
        _store = new TerrainVoxelStore();
    }

    [Test]
    public void GetVoxel_UnsetCell_ReturnsEmpty()
    {
        Assert.AreEqual(TerrainVoxel.Empty, _store.GetVoxel(0, 0, 0));
        Assert.AreEqual(TerrainVoxel.Empty, _store.GetVoxel(62, 30, 62), "最大座標も範囲内");
    }

    [Test]
    public void SetGetVoxel_AcrossChunkBoundaries()
    {
        byte a = TerrainVoxel.Encode(TerrainShape.Cube, 1);
        byte b = TerrainVoxel.Encode(TerrainShape.RampW, 2);

        _store.SetVoxel(15, 0, 0, a); // チャンク (0,0,0) の端
        _store.SetVoxel(16, 0, 0, b); // チャンク (1,0,0) の先頭

        Assert.AreEqual(a, _store.GetVoxel(15, 0, 0));
        Assert.AreEqual(b, _store.GetVoxel(16, 0, 0));
        Assert.AreEqual(2, _store.NonEmptyChunkCount, "2 チャンクにまたがる");
    }

    [Test]
    public void SetVoxel_EmptyOnUnsetCell_DoesNotCreateChunk()
    {
        _store.SetVoxel(5, 5, 5, TerrainVoxel.Empty);
        Assert.AreEqual(0, _store.NonEmptyChunkCount);
        Assert.IsNull(_store.GetChunk(0, 0, 0), "空書き込みでチャンクは生成しない");
    }

    [Test]
    public void SetVoxel_OutOfBounds_Throws()
    {
        Assert.Throws<System.ArgumentOutOfRangeException>(
            () => _store.SetVoxel(63, 0, 0, TerrainVoxel.Empty));
        Assert.Throws<System.ArgumentOutOfRangeException>(
            () => _store.SetVoxel(0, 31, 0, TerrainVoxel.Empty));
        Assert.Throws<System.ArgumentOutOfRangeException>(
            () => _store.SetVoxel(0, 0, -1, TerrainVoxel.Empty));
    }

    [Test]
    public void InBounds_MatchesWorldSize()
    {
        Assert.IsTrue(TerrainVoxelStore.InBounds(0, 0, 0));
        Assert.IsTrue(TerrainVoxelStore.InBounds(62, 30, 62));
        Assert.IsFalse(TerrainVoxelStore.InBounds(63, 0, 0));
        Assert.IsFalse(TerrainVoxelStore.InBounds(0, 31, 0));
        Assert.IsFalse(TerrainVoxelStore.InBounds(-1, 0, 0));
    }

    [Test]
    public void EnumerateNonEmptyChunks_SkipsEmptiedChunks()
    {
        byte v = TerrainVoxel.Encode(TerrainShape.Cube, 0);
        _store.SetVoxel(0, 0, 0, v);
        _store.SetVoxel(32, 0, 0, v);
        _store.SetVoxel(32, 0, 0, TerrainVoxel.Empty); // チャンク (2,0,0) を空に戻す

        int count = 0;
        foreach (var (cx, cy, cz, chunk) in _store.EnumerateNonEmptyChunks())
        {
            count++;
            Assert.AreEqual((0, 0, 0), (cx, cy, cz));
            Assert.IsFalse(chunk.IsEmpty);
        }
        Assert.AreEqual(1, count, "空に戻ったチャンクは列挙されない");
    }

    [Test]
    public void TryAddChunk_DuplicateCoords_Fails()
    {
        var chunk = new TerrainChunk();
        Assert.IsTrue(_store.TryAddChunk(1, 1, 1, chunk));
        Assert.IsFalse(_store.TryAddChunk(1, 1, 1, new TerrainChunk()), "重複は拒否");
        Assert.IsFalse(_store.TryAddChunk(0, 0, 0, null));
    }
}
