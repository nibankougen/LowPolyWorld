using NUnit.Framework;

public class TransparentPixelProcessorTests
{
    // ---- α < 128 → 完全透明 ----

    [Test]
    public void Process_AlphaZero_SetsAllChannelsToZero()
    {
        var pixels = new byte[] { 200, 100, 50, 0 };

        TransparentPixelProcessor.Process(pixels);

        Assert.AreEqual(0, pixels[0]);
        Assert.AreEqual(0, pixels[1]);
        Assert.AreEqual(0, pixels[2]);
        Assert.AreEqual(0, pixels[3]);
    }

    [Test]
    public void Process_Alpha127_SetsAllChannelsToZero()
    {
        var pixels = new byte[] { 255, 255, 255, 127 };

        TransparentPixelProcessor.Process(pixels);

        Assert.AreEqual(0, pixels[0]);
        Assert.AreEqual(0, pixels[1]);
        Assert.AreEqual(0, pixels[2]);
        Assert.AreEqual(0, pixels[3]);
    }

    [Test]
    public void Process_Alpha1_SetsAllChannelsToZero()
    {
        var pixels = new byte[] { 128, 64, 32, 1 };

        TransparentPixelProcessor.Process(pixels);

        Assert.AreEqual(0, pixels[0]);
        Assert.AreEqual(0, pixels[1]);
        Assert.AreEqual(0, pixels[2]);
        Assert.AreEqual(0, pixels[3]);
    }

    // ---- α >= 128 → 完全不透明（RGB 保持）----

    [Test]
    public void Process_Alpha128_SetsAlphaTo255AndPreservesRgb()
    {
        var pixels = new byte[] { 100, 150, 200, 128 };

        TransparentPixelProcessor.Process(pixels);

        Assert.AreEqual(100, pixels[0]);
        Assert.AreEqual(150, pixels[1]);
        Assert.AreEqual(200, pixels[2]);
        Assert.AreEqual(255, pixels[3]);
    }

    [Test]
    public void Process_Alpha255_SetsAlphaTo255AndPreservesRgb()
    {
        var pixels = new byte[] { 10, 20, 30, 255 };

        TransparentPixelProcessor.Process(pixels);

        Assert.AreEqual(10, pixels[0]);
        Assert.AreEqual(20, pixels[1]);
        Assert.AreEqual(30, pixels[2]);
        Assert.AreEqual(255, pixels[3]);
    }

    [Test]
    public void Process_Alpha200_PreservesRgb()
    {
        var pixels = new byte[] { 77, 88, 99, 200 };

        TransparentPixelProcessor.Process(pixels);

        Assert.AreEqual(77, pixels[0]);
        Assert.AreEqual(88, pixels[1]);
        Assert.AreEqual(99, pixels[2]);
        Assert.AreEqual(255, pixels[3]);
    }

    // ---- 境界値: α = 127 / 128 ----

    [Test]
    public void Process_Boundary_Alpha127IsTransparent_Alpha128IsOpaque()
    {
        // pixel0: α=127 → transparent / pixel1: α=128 → opaque
        var pixels = new byte[]
        {
            255, 0, 0, 127,
            0, 255, 0, 128,
        };

        TransparentPixelProcessor.Process(pixels);

        // pixel0
        Assert.AreEqual(0, pixels[0]);
        Assert.AreEqual(0, pixels[1]);
        Assert.AreEqual(0, pixels[2]);
        Assert.AreEqual(0, pixels[3]);

        // pixel1
        Assert.AreEqual(0, pixels[4]);
        Assert.AreEqual(255, pixels[5]);
        Assert.AreEqual(0, pixels[6]);
        Assert.AreEqual(255, pixels[7]);
    }

    // ---- 複数ピクセル ----

    [Test]
    public void Process_MultiplePixels_ProcessesAll()
    {
        var pixels = new byte[]
        {
            255, 0,   0,   0,    // α=0   → transparent
            0,   255, 0,   255,  // α=255 → opaque, preserve RGB
            0,   0,   255, 64,   // α=64  → transparent
            128, 64,  32,  200,  // α=200 → opaque, preserve RGB
        };

        TransparentPixelProcessor.Process(pixels);

        // px0
        Assert.AreEqual(0, pixels[3]);
        Assert.AreEqual(0, pixels[0]);

        // px1
        Assert.AreEqual(255, pixels[7]);
        Assert.AreEqual(0, pixels[4]);
        Assert.AreEqual(255, pixels[5]);
        Assert.AreEqual(0, pixels[6]);

        // px2
        Assert.AreEqual(0, pixels[11]);
        Assert.AreEqual(0, pixels[8]);

        // px3
        Assert.AreEqual(255, pixels[15]);
        Assert.AreEqual(128, pixels[12]);
        Assert.AreEqual(64, pixels[13]);
        Assert.AreEqual(32, pixels[14]);
    }

    // ---- エッジケース ----

    [Test]
    public void Process_NullArray_DoesNotThrow()
    {
        Assert.DoesNotThrow(() => TransparentPixelProcessor.Process(null));
    }

    [Test]
    public void Process_EmptyArray_DoesNotThrow()
    {
        Assert.DoesNotThrow(() => TransparentPixelProcessor.Process(new byte[0]));
    }

    [Test]
    public void Process_IsInPlace_ModifiesOriginalArray()
    {
        var pixels = new byte[] { 100, 100, 100, 50 };

        TransparentPixelProcessor.Process(pixels);

        // インプレース処理であること（同じ配列が変更される）
        Assert.AreEqual(0, pixels[3]);
    }
}
