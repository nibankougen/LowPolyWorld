using NUnit.Framework;

public class TerrainAoTests
{
    private const float Delta = 1e-5f;

    [Test]
    public void Brightness_NoDarkness_ReturnsBase()
    {
        Assert.AreEqual(TerrainAo.BaseBrightness, TerrainAo.Brightness(0f), Delta);
    }

    [Test]
    public void Brightness_LinearMapping()
    {
        Assert.AreEqual(0.75f, TerrainAo.Brightness(1f), Delta);
        Assert.AreEqual(0.5f, TerrainAo.Brightness(2f), Delta);
        Assert.AreEqual(0.875f, TerrainAo.Brightness(0.5f), Delta);
    }

    [Test]
    public void Brightness_ClampedToMinBrightness()
    {
        Assert.AreEqual(TerrainAo.MinBrightness, TerrainAo.Brightness(3f), Delta, "部屋の隅でも真っ黒にならない");
        Assert.AreEqual(TerrainAo.MinBrightness, TerrainAo.Brightness(4f), Delta);
        Assert.AreEqual(TerrainAo.MinBrightness, TerrainAo.Brightness(100f), Delta);
        Assert.Less(TerrainAo.MinBrightness, TerrainAo.Brightness(2f), "直線壁の足元 (darkness 2) は下限より明るい");
    }
}
