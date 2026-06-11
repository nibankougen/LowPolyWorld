using NUnit.Framework;

public class TerrainTextureHashTests
{
    [Test]
    public void SelectIndex_Deterministic()
    {
        int a = TerrainTextureHash.SelectIndex(10, 20, 30, 1, 8);
        int b = TerrainTextureHash.SelectIndex(10, 20, 30, 1, 8);
        Assert.AreEqual(a, b);
    }

    [Test]
    public void SelectIndex_WithinRange()
    {
        foreach (int count in new[] { 1, 4, 8 })
        {
            for (int x = 0; x < 8; x++)
            for (int y = 0; y < 4; y++)
            for (int z = 0; z < 8; z++)
            for (int dir = 1; dir <= 8; dir++)
            {
                int index = TerrainTextureHash.SelectIndex(x, y, z, dir, count);
                Assert.GreaterOrEqual(index, 0);
                Assert.Less(index, count);
            }
        }
    }

    [Test]
    public void SelectIndex_CountOne_AlwaysZero()
    {
        Assert.AreEqual(0, TerrainTextureHash.SelectIndex(5, 5, 5, 1, 1));
        Assert.AreEqual(0, TerrainTextureHash.SelectIndex(62, 30, 62, 8, 1));
    }

    [Test]
    public void SelectIndex_ProducesVariation()
    {
        // 近傍座標・方向の組み合わせで全バリアントが一度は選ばれる程度に分散すること
        var seen = new bool[8];
        for (int x = 0; x < 16; x++)
        for (int z = 0; z < 16; z++)
            seen[TerrainTextureHash.SelectIndex(x, 0, z, 1, 8)] = true;
        CollectionAssert.DoesNotContain(seen, false);
    }

    [Test]
    public void SelectIndex_NonPowerOfTwoCount_Throws()
    {
        Assert.Throws<System.ArgumentException>(() => TerrainTextureHash.SelectIndex(0, 0, 0, 1, 3));
        Assert.Throws<System.ArgumentException>(() => TerrainTextureHash.SelectIndex(0, 0, 0, 1, 0));
        Assert.Throws<System.ArgumentException>(() => TerrainTextureHash.SelectIndex(0, 0, 0, 1, -4));
    }
}
