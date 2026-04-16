using NUnit.Framework;
using UnityEngine;

/// <summary>
/// UvOverlayBaker のユニットテスト。
/// BresenhamLine は純粋ロジックとして EditMode で完結するため、
/// Mesh.AcquireReadOnlyMeshData を使う Bake テストも含む。
/// </summary>
public class UvOverlayBakerTests
{
    // ---- BresenhamLine ----

    [Test]
    public void BresenhamLine_Horizontal_DrawsAllPixels()
    {
        var pixels = new Color32[8 * 1];
        UvOverlayBaker.BresenhamLine(pixels, 8, 1, 0, 0, 7, 0);

        for (int x = 0; x <= 7; x++)
            Assert.AreNotEqual(0, pixels[x].a, $"pixel [{x}] should be drawn");
    }

    [Test]
    public void BresenhamLine_Vertical_DrawsAllPixels()
    {
        var pixels = new Color32[1 * 8];
        UvOverlayBaker.BresenhamLine(pixels, 1, 8, 0, 0, 0, 7);

        for (int y = 0; y <= 7; y++)
            Assert.AreNotEqual(0, pixels[y].a, $"pixel at y={y} should be drawn");
    }

    [Test]
    public void BresenhamLine_Diagonal_DrawsDiagonalPixels()
    {
        var pixels = new Color32[8 * 8];
        UvOverlayBaker.BresenhamLine(pixels, 8, 8, 0, 0, 7, 7);

        for (int i = 0; i <= 7; i++)
            Assert.AreNotEqual(0, pixels[i * 8 + i].a, $"diagonal pixel [{i},{i}] should be drawn");
    }

    [Test]
    public void BresenhamLine_SinglePoint_DrawsOnePixel()
    {
        var pixels = new Color32[4 * 4];
        UvOverlayBaker.BresenhamLine(pixels, 4, 4, 2, 2, 2, 2);

        Assert.AreNotEqual(0, pixels[2 * 4 + 2].a, "single point should be drawn");
        int drawn = 0;
        foreach (var p in pixels)
            if (p.a != 0) drawn++;
        Assert.AreEqual(1, drawn, "only one pixel should be drawn");
    }

    [Test]
    public void BresenhamLine_OutOfBounds_DoesNotThrow()
    {
        var pixels = new Color32[4 * 4];
        Assert.DoesNotThrow(() =>
            UvOverlayBaker.BresenhamLine(pixels, 4, 4, -1, -1, 10, 10));
    }

    // ---- Bake ----

    [Test]
    public void Bake_NullInput_ReturnsNull()
    {
        Assert.IsNull(UvOverlayBaker.Bake(null, 8, 8));
    }

    [Test]
    public void Bake_EmptyArray_ReturnsNull()
    {
        Assert.IsNull(UvOverlayBaker.Bake(new Mesh[0], 8, 8));
    }

    [Test]
    public void Bake_SingleTriangle_ReturnsRgbaWithDrawnPixels()
    {
        var mesh = new Mesh();
        mesh.vertices = new[]
        {
            new Vector3(0, 0, 0),
            new Vector3(1, 0, 0),
            new Vector3(0, 1, 0),
        };
        mesh.uv = new[]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0f, 1f),
        };
        mesh.triangles = new[] { 0, 1, 2 };

        var result = UvOverlayBaker.Bake(new[] { mesh }, 8, 8);

        Assert.IsNotNull(result);
        Assert.AreEqual(8 * 8 * 4, result.Length, "結果は width*height*4 バイト");

        // UV (0,0)-(1,0) の水平エッジはピクセル (0,0)-(7,0) に描画されるはず
        bool anyDrawn = false;
        for (int i = 0; i < result.Length; i += 4)
            if (result[i + 3] > 0) { anyDrawn = true; break; }
        Assert.IsTrue(anyDrawn, "少なくとも 1 ピクセルが描画されているはず");

        Object.DestroyImmediate(mesh);
    }

    [Test]
    public void Bake_AllNullMeshes_ReturnsNull()
    {
        var result = UvOverlayBaker.Bake(new Mesh[] { null, null }, 8, 8);
        Assert.IsNull(result);
    }
}
