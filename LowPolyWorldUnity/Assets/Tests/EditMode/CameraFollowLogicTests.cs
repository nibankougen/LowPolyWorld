using NUnit.Framework;
using UnityEngine;

public class CameraFollowLogicTests
{
    // ---- 初期状態 ----

    [Test]
    public void InitialPitch_IsClampedToValidRange()
    {
        var logic = new CameraFollowLogic(initialPitch: 15f);
        Assert.GreaterOrEqual(logic.Pitch, CameraFollowLogic.MinPitch);
        Assert.LessOrEqual(logic.Pitch, CameraFollowLogic.MaxPitch);
    }

    // ---- オフセット計算 ----

    [Test]
    public void GetCameraPosition_NoRotation_IsBehindAndAboveTarget()
    {
        // Yaw=0, Pitch=0 のとき: カメラは Z 軸負方向（後ろ）+ 高さオフセット分上
        var logic = new CameraFollowLogic(distance: 5f, heightOffset: 1.5f, initialPitch: 0f);
        var target = Vector3.zero;

        var camPos = logic.GetCameraPosition(target);

        Assert.AreEqual(0f, camPos.x, 0.001f, "X should be 0");
        Assert.AreEqual(1.5f, camPos.y, 0.001f, "Y should equal heightOffset");
        Assert.AreEqual(-5f, camPos.z, 0.001f, "Z should be -distance");
    }

    [Test]
    public void GetCameraPosition_MovesWithTarget()
    {
        var logic = new CameraFollowLogic(distance: 5f, heightOffset: 1.5f, initialPitch: 0f);
        var offset = logic.GetCameraPosition(Vector3.zero);

        var shifted = logic.GetCameraPosition(new Vector3(10f, 0f, 0f));

        Assert.AreEqual(offset.x + 10f, shifted.x, 0.001f);
        Assert.AreEqual(offset.y, shifted.y, 0.001f);
        Assert.AreEqual(offset.z, shifted.z, 0.001f);
    }

    // ---- 仰角クランプ ----

    [Test]
    public void ApplyLookDelta_PitchClampedAtMin()
    {
        var logic = new CameraFollowLogic(initialPitch: 0f);

        // 大きな正の deltaPitch → pitch が下がる方向（MinPitch 方向）
        logic.ApplyLookDelta(0f, 9999f, sensitivity: 1f);

        Assert.AreEqual(CameraFollowLogic.MinPitch, logic.Pitch, 0.001f);
    }

    [Test]
    public void ApplyLookDelta_PitchClampedAtMax()
    {
        var logic = new CameraFollowLogic(initialPitch: 0f);

        // 大きな負の deltaPitch → pitch が上がる方向（MaxPitch 方向）
        logic.ApplyLookDelta(0f, -9999f, sensitivity: 1f);

        Assert.AreEqual(CameraFollowLogic.MaxPitch, logic.Pitch, 0.001f);
    }

    [Test]
    public void ApplyLookDelta_InitialPitchAboveMax_IsClamped()
    {
        var logic = new CameraFollowLogic(initialPitch: 999f);
        Assert.LessOrEqual(logic.Pitch, CameraFollowLogic.MaxPitch);
    }

    [Test]
    public void ApplyLookDelta_InitialPitchBelowMin_IsClamped()
    {
        var logic = new CameraFollowLogic(initialPitch: -999f);
        Assert.GreaterOrEqual(logic.Pitch, CameraFollowLogic.MinPitch);
    }

    // ---- Yaw 回転 ----

    [Test]
    public void ApplyLookDelta_YawChanges()
    {
        var logic = new CameraFollowLogic();
        float before = logic.Yaw;

        logic.ApplyLookDelta(90f, 0f);

        Assert.AreEqual(before + 90f, logic.Yaw, 0.001f);
    }

    [Test]
    public void GetCameraRotation_ReflectsYawAndPitch()
    {
        var logic = new CameraFollowLogic(initialPitch: 0f);
        logic.ApplyLookDelta(45f, 0f);

        var rotation = logic.GetCameraRotation();
        var expected = Quaternion.Euler(0f, 45f, 0f);

        Assert.AreEqual(expected.eulerAngles.y, rotation.eulerAngles.y, 0.01f);
    }
}
