using NUnit.Framework;
using UnityEngine;

public class CameraPhotoModeLogicTests
{
    private CameraPhotoModeLogic _logic;

    [SetUp]
    public void SetUp()
    {
        _logic = new CameraPhotoModeLogic();
    }

    [Test]
    public void InitialState_ZeroOffsets()
    {
        Assert.AreEqual(0f, _logic.ZoomOffset);
        Assert.AreEqual(Vector2.zero, _logic.SlideOffset);
    }

    [Test]
    public void Pinch_Outward_IncreasesZoom()
    {
        _logic.BeginTwoFingers(new Vector2(0, 500), new Vector2(100, 500));
        _logic.UpdateTwoFingers(new Vector2(-50, 500), new Vector2(200, 500), 1000f); // 250 → 100 apart, diff +150 → zoom+
        Assert.Greater(_logic.ZoomOffset, 0f);
    }

    [Test]
    public void Pinch_Inward_DecreasesZoom()
    {
        _logic.BeginTwoFingers(new Vector2(-100, 500), new Vector2(100, 500)); // 200 apart
        _logic.UpdateTwoFingers(new Vector2(-10, 500), new Vector2(10, 500), 1000f);  // 20 apart → much smaller
        Assert.Less(_logic.ZoomOffset, 0f);
    }

    [Test]
    public void Zoom_ClampsAtMax()
    {
        _logic.BeginTwoFingers(new Vector2(0, 500), new Vector2(10, 500));
        // Very large outward pinch
        _logic.UpdateTwoFingers(new Vector2(-10000, 500), new Vector2(10000, 500), 1000f);
        Assert.LessOrEqual(_logic.ZoomOffset, CameraPhotoModeLogic.MaxZoom);
    }

    [Test]
    public void Zoom_ClampsAtMin()
    {
        _logic.BeginTwoFingers(new Vector2(-10000, 500), new Vector2(10000, 500));
        _logic.UpdateTwoFingers(new Vector2(0, 500), new Vector2(10, 500), 1000f);
        Assert.GreaterOrEqual(_logic.ZoomOffset, CameraPhotoModeLogic.MinZoom);
    }

    [Test]
    public void SlideRight_NegativesXOffset()
    {
        _logic.BeginTwoFingers(new Vector2(100, 800), new Vector2(200, 800));
        // Mid point moves right
        _logic.UpdateTwoFingers(new Vector2(200, 800), new Vector2(300, 800), 1000f);
        // SlideOffset.x should be negative (camera moves left so world moves right)
        Assert.Less(_logic.SlideOffset.x, 0f);
    }

    [Test]
    public void SlideUp_PositiveYOffset()
    {
        _logic.BeginTwoFingers(new Vector2(100, 500), new Vector2(200, 500));
        // Mid point moves up (higher y in screen space)
        _logic.UpdateTwoFingers(new Vector2(100, 600), new Vector2(200, 600), 1000f);
        Assert.Greater(_logic.SlideOffset.y, 0f);
    }

    [Test]
    public void Reset_ClearsAllOffsets()
    {
        _logic.BeginTwoFingers(new Vector2(0, 500), new Vector2(100, 500));
        _logic.UpdateTwoFingers(new Vector2(-50, 500), new Vector2(200, 500), 1000f);
        _logic.Reset();

        Assert.AreEqual(0f, _logic.ZoomOffset);
        Assert.AreEqual(Vector2.zero, _logic.SlideOffset);
    }

    [Test]
    public void EndThenBegin_ResetsTracking()
    {
        _logic.BeginTwoFingers(new Vector2(0, 500), new Vector2(100, 500));
        _logic.EndTwoFingers();
        // After end, BeginTwoFingers again should not jump
        _logic.BeginTwoFingers(new Vector2(0, 500), new Vector2(100, 500));
        _logic.UpdateTwoFingers(new Vector2(0, 500), new Vector2(101, 500), 1000f); // tiny change
        Assert.That(Mathf.Abs(_logic.ZoomOffset), Is.LessThan(0.1f));
    }
}
