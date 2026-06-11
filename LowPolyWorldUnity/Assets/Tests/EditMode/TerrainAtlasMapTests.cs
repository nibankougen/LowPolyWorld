using NUnit.Framework;
using UnityEngine;

public class TerrainAtlasMapTests
{
    private const float Delta = 1e-6f;

    [Test]
    public void GetUvRect_ComposesTextureRectAndRegionRect()
    {
        // ランダムテクスチャをアトラス右上 1/4 にパックした想定
        var map = new TerrainAtlasMap(new[]
        {
            new TerrainAtlasMap.Entry(false, new Rect(0.5f, 0.75f, 0.25f, 0.25f)),
        });

        // Top バリアント 0 = テクスチャ内 (0, 7/8, 1/8, 1/8)
        Rect rect = map.GetUvRect(0, TerrainFaceRegion.Top, 0);
        Assert.AreEqual(0.5f, rect.x, Delta);
        Assert.AreEqual(0.75f + 7f / 8f * 0.25f, rect.y, Delta);
        Assert.AreEqual(0.25f / 8f, rect.width, Delta);
        Assert.AreEqual(0.25f / 8f, rect.height, Delta);
    }

    [Test]
    public void GetVariantCount_DependsOnTextureKind()
    {
        var map = new TerrainAtlasMap(new[]
        {
            new TerrainAtlasMap.Entry(false, new Rect(0f, 0f, 0.25f, 0.25f)),
            new TerrainAtlasMap.Entry(true, new Rect(0.25f, 0f, 0.03125f, 0.25f)),
        });

        Assert.AreEqual(8, map.GetVariantCount(0, TerrainFaceRegion.Top), "ランダムテクスチャ");
        Assert.AreEqual(4, map.GetVariantCount(0, TerrainFaceRegion.Bottom));
        Assert.AreEqual(1, map.GetVariantCount(1, TerrainFaceRegion.Top), "固定テクスチャ");
    }

    [Test]
    public void Constructor_Validation()
    {
        Assert.Throws<System.ArgumentNullException>(() => new TerrainAtlasMap(null));

        var tooMany = new TerrainAtlasMap.Entry[17];
        Assert.Throws<System.ArgumentException>(() => new TerrainAtlasMap(tooMany), "パレットは最大 16 種類");
    }

    [Test]
    public void GetUvRect_PaletteIndexOutOfRange_Throws()
    {
        var map = new TerrainAtlasMap(new[]
        {
            new TerrainAtlasMap.Entry(false, new Rect(0f, 0f, 1f, 1f)),
        });
        Assert.Throws<System.ArgumentOutOfRangeException>(() => map.GetUvRect(1, TerrainFaceRegion.Top, 0));
        Assert.Throws<System.ArgumentOutOfRangeException>(() => map.GetVariantCount(-1, TerrainFaceRegion.Top));
    }
}
