using NUnit.Framework;
using UnityEngine;

public class TerrainTextureLayoutTests
{
    private const float Cell = 1f / 8f;
    private const float Delta = 1e-6f;

    [Test]
    public void GetVariantCount_RandomTexture()
    {
        Assert.AreEqual(8, TerrainTextureLayout.GetVariantCount(TerrainFaceRegion.Top, false));
        Assert.AreEqual(8, TerrainTextureLayout.GetVariantCount(TerrainFaceRegion.Side, false));
        Assert.AreEqual(8, TerrainTextureLayout.GetVariantCount(TerrainFaceRegion.SideTopBottom, false));
        Assert.AreEqual(4, TerrainTextureLayout.GetVariantCount(TerrainFaceRegion.RampSide, false));
        Assert.AreEqual(4, TerrainTextureLayout.GetVariantCount(TerrainFaceRegion.RampSideBottom, false));
        Assert.AreEqual(4, TerrainTextureLayout.GetVariantCount(TerrainFaceRegion.Bottom, false));
    }

    [Test]
    public void GetVariantCount_FixedTexture_AlwaysOne()
    {
        foreach (TerrainFaceRegion region in System.Enum.GetValues(typeof(TerrainFaceRegion)))
            Assert.AreEqual(1, TerrainTextureLayout.GetVariantCount(region, true));
    }

    [Test]
    public void GetRegionRect_RandomTexture_RowsMatchSpec()
    {
        // 行 7（最上行）: 上面
        AssertRect(TerrainTextureLayout.GetRegionRect(TerrainFaceRegion.Top, 0, false), 0, 7);
        // 行 6: 上面中間
        AssertRect(TerrainTextureLayout.GetRegionRect(TerrainFaceRegion.TopMiddle, 1, false), 1, 6);
        // 行 5: 側面上端 / 行 4: 側面 / 行 3: 側面下端 / 行 2: 側面上端下端
        AssertRect(TerrainTextureLayout.GetRegionRect(TerrainFaceRegion.SideTop, 7, false), 7, 5);
        AssertRect(TerrainTextureLayout.GetRegionRect(TerrainFaceRegion.Side, 0, false), 0, 4);
        AssertRect(TerrainTextureLayout.GetRegionRect(TerrainFaceRegion.SideBottom, 0, false), 0, 3);
        AssertRect(TerrainTextureLayout.GetRegionRect(TerrainFaceRegion.SideTopBottom, 0, false), 0, 2);
        // 行 1: 坂側面下端 0〜3 / 坂側面 4〜7
        AssertRect(TerrainTextureLayout.GetRegionRect(TerrainFaceRegion.RampSideBottom, 2, false), 2, 1);
        AssertRect(TerrainTextureLayout.GetRegionRect(TerrainFaceRegion.RampSide, 0, false), 4, 1);
        AssertRect(TerrainTextureLayout.GetRegionRect(TerrainFaceRegion.RampSide, 3, false), 7, 1);
        // 行 0（最下行）: 下面 0〜3
        AssertRect(TerrainTextureLayout.GetRegionRect(TerrainFaceRegion.Bottom, 3, false), 3, 0);
    }

    [Test]
    public void GetRegionRect_FixedTexture_FullWidthRows()
    {
        Rect top = TerrainTextureLayout.GetRegionRect(TerrainFaceRegion.Top, 0, true);
        Assert.AreEqual(0f, top.x, Delta);
        Assert.AreEqual(7 * Cell, top.y, Delta);
        Assert.AreEqual(1f, top.width, Delta);
        Assert.AreEqual(Cell, top.height, Delta);

        // 固定テクスチャに坂側面下端はなく、坂側面（行 1）を使用する
        Rect rampSideBottom = TerrainTextureLayout.GetRegionRect(TerrainFaceRegion.RampSideBottom, 0, true);
        Rect rampSide = TerrainTextureLayout.GetRegionRect(TerrainFaceRegion.RampSide, 0, true);
        Assert.AreEqual(rampSide, rampSideBottom);
        Assert.AreEqual(1 * Cell, rampSide.y, Delta);

        Assert.AreEqual(0f, TerrainTextureLayout.GetRegionRect(TerrainFaceRegion.Bottom, 0, true).y, Delta);
    }

    [Test]
    public void GetRegionRect_VariantOutOfRange_Throws()
    {
        Assert.Throws<System.ArgumentOutOfRangeException>(
            () => TerrainTextureLayout.GetRegionRect(TerrainFaceRegion.Top, 8, false));
        Assert.Throws<System.ArgumentOutOfRangeException>(
            () => TerrainTextureLayout.GetRegionRect(TerrainFaceRegion.Bottom, 4, false));
        Assert.Throws<System.ArgumentOutOfRangeException>(
            () => TerrainTextureLayout.GetRegionRect(TerrainFaceRegion.Top, 1, true));
        Assert.Throws<System.ArgumentOutOfRangeException>(
            () => TerrainTextureLayout.GetRegionRect(TerrainFaceRegion.Top, -1, false));
    }

    private static void AssertRect(Rect rect, int col, int row)
    {
        Assert.AreEqual(col * Cell, rect.x, Delta, "col");
        Assert.AreEqual(row * Cell, rect.y, Delta, "row");
        Assert.AreEqual(Cell, rect.width, Delta, "width");
        Assert.AreEqual(Cell, rect.height, Delta, "height");
    }
}
