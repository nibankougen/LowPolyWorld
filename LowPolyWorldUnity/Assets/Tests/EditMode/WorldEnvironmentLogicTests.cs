using NUnit.Framework;
using UnityEngine;

public class WorldEnvironmentLogicTests
{
    // ── ParseHexColor ─────────────────────────────────────────────────────────

    [Test]
    public void ParseHexColor_White_ReturnsWhite()
    {
        var color = WorldEnvironmentLogic.ParseHexColor("#FFFFFF");
        Assert.AreEqual(Color.white, color);
    }

    [Test]
    public void ParseHexColor_Black_ReturnsBlack()
    {
        var color = WorldEnvironmentLogic.ParseHexColor("#000000");
        Assert.AreEqual(Color.black, color);
    }

    [Test]
    public void ParseHexColor_Red_ReturnsRed()
    {
        var color = WorldEnvironmentLogic.ParseHexColor("#FF0000");
        Assert.AreEqual(1f, color.r, 0.01f);
        Assert.AreEqual(0f, color.g, 0.01f);
        Assert.AreEqual(0f, color.b, 0.01f);
    }

    [Test]
    public void ParseHexColor_NullOrEmpty_ReturnsWhite()
    {
        Assert.AreEqual(Color.white, WorldEnvironmentLogic.ParseHexColor(null));
        Assert.AreEqual(Color.white, WorldEnvironmentLogic.ParseHexColor(""));
        Assert.AreEqual(Color.white, WorldEnvironmentLogic.ParseHexColor("   "));
    }

    [Test]
    public void ParseHexColor_Invalid_ReturnsWhite()
    {
        Assert.AreEqual(Color.white, WorldEnvironmentLogic.ParseHexColor("not-a-color"));
        Assert.AreEqual(Color.white, WorldEnvironmentLogic.ParseHexColor("#GGGGGG"));
    }

    [Test]
    public void ParseHexColor_WithoutHash_ReturnsWhite()
    {
        // ColorUtility requires '#' prefix
        var result = WorldEnvironmentLogic.ParseHexColor("FFFFFF");
        Assert.AreEqual(Color.white, result);
    }

    // ── IsValidAmbientColor ───────────────────────────────────────────────────

    [Test]
    public void IsValidAmbientColor_White_ReturnsTrue()
    {
        Assert.IsTrue(WorldEnvironmentLogic.IsValidAmbientColor(Color.white));
    }

    [Test]
    public void IsValidAmbientColor_Black_ReturnsFalse()
    {
        Assert.IsFalse(WorldEnvironmentLogic.IsValidAmbientColor(Color.black));
    }

    [Test]
    public void IsValidAmbientColor_AtThreshold_ReturnsTrue()
    {
        // V = 0.25 exactly should pass
        var color = Color.HSVToRGB(0f, 0f, WorldEnvironmentLogic.MinAmbientBrightness);
        Assert.IsTrue(WorldEnvironmentLogic.IsValidAmbientColor(color));
    }

    [Test]
    public void IsValidAmbientColor_JustBelowThreshold_ReturnsFalse()
    {
        var color = Color.HSVToRGB(0f, 0f, WorldEnvironmentLogic.MinAmbientBrightness - 0.01f);
        Assert.IsFalse(WorldEnvironmentLogic.IsValidAmbientColor(color));
    }

    // ── ClampAmbientColor ─────────────────────────────────────────────────────

    [Test]
    public void ClampAmbientColor_AboveThreshold_Unchanged()
    {
        var input = Color.HSVToRGB(0.5f, 0.8f, 0.8f);
        var result = WorldEnvironmentLogic.ClampAmbientColor(input);
        Color.RGBToHSV(result, out _, out _, out float v);
        Assert.AreEqual(0.8f, v, 0.01f);
    }

    [Test]
    public void ClampAmbientColor_Black_ClampsToMinBrightness()
    {
        var result = WorldEnvironmentLogic.ClampAmbientColor(Color.black);
        Color.RGBToHSV(result, out _, out _, out float v);
        Assert.AreEqual(WorldEnvironmentLogic.MinAmbientBrightness, v, 0.01f);
    }

    [Test]
    public void ClampAmbientColor_PreservesHueAndSaturation()
    {
        var input = Color.HSVToRGB(0.3f, 0.7f, 0.1f); // very dark green
        var result = WorldEnvironmentLogic.ClampAmbientColor(input);
        Color.RGBToHSV(result, out float h, out float s, out _);
        Assert.AreEqual(0.3f, h, 0.01f, "色相が保持される");
        Assert.AreEqual(0.7f, s, 0.01f, "彩度が保持される");
    }

    // ── IsValidFog ────────────────────────────────────────────────────────────

    [Test]
    public void IsValidFog_EndGreaterThanStart_ReturnsTrue()
    {
        var fog = new FogData { startDistance = 10f, endDistance = 50f };
        Assert.IsTrue(WorldEnvironmentLogic.IsValidFog(fog));
    }

    [Test]
    public void IsValidFog_EndEqualToStart_ReturnsFalse()
    {
        var fog = new FogData { startDistance = 10f, endDistance = 10f };
        Assert.IsFalse(WorldEnvironmentLogic.IsValidFog(fog));
    }

    [Test]
    public void IsValidFog_EndLessThanStart_ReturnsFalse()
    {
        var fog = new FogData { startDistance = 50f, endDistance = 10f };
        Assert.IsFalse(WorldEnvironmentLogic.IsValidFog(fog));
    }

    [Test]
    public void IsValidFog_Null_ReturnsFalse()
    {
        Assert.IsFalse(WorldEnvironmentLogic.IsValidFog(null));
    }

    // ── ClampFog ──────────────────────────────────────────────────────────────

    [Test]
    public void ClampFog_ValidFog_Unchanged()
    {
        var fog = new FogData { startDistance = 10f, endDistance = 50f };
        var result = WorldEnvironmentLogic.ClampFog(fog);
        Assert.AreEqual(10f, result.startDistance, 0.001f);
        Assert.AreEqual(50f, result.endDistance, 0.001f);
    }

    [Test]
    public void ClampFog_EqualDistances_AddsHalfMeter()
    {
        var fog = new FogData { startDistance = 10f, endDistance = 10f };
        var result = WorldEnvironmentLogic.ClampFog(fog);
        Assert.Greater(result.endDistance, result.startDistance);
    }

    [Test]
    public void ClampFog_Null_ReturnsDefault()
    {
        var result = WorldEnvironmentLogic.ClampFog(null);
        Assert.IsNotNull(result);
    }

    // ── NormalizeIntensity ────────────────────────────────────────────────────

    [TestCase(0, 0f)]
    [TestCase(50, 0.5f)]
    [TestCase(100, 1f)]
    [TestCase(-1, 0f)]
    [TestCase(101, 1f)]
    public void NormalizeIntensity_ClampsTo0_1(int intensity, float expected)
    {
        Assert.AreEqual(expected, WorldEnvironmentLogic.NormalizeIntensity(intensity), 0.001f);
    }
}
