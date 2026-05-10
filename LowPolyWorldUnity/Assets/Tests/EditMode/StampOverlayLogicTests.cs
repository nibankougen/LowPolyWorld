using NUnit.Framework;
using UnityEngine;

public class StampOverlayLogicTests
{
    // ── 初期状態 ──────────────────────────────────────────────────────────────

    [Test]
    public void InitialState_Stamps_IsEmpty()
    {
        var logic = new StampOverlayLogic();
        Assert.AreEqual(0, logic.Stamps.Count);
    }

    // ── スタンプ追加 ──────────────────────────────────────────────────────────

    [Test]
    public void AddStamp_AddsToList()
    {
        var logic = new StampOverlayLogic();
        logic.AddStamp("stamp_a", Vector2.zero);
        Assert.AreEqual(1, logic.Stamps.Count);
    }

    [Test]
    public void AddStamp_ReturnsStampWithCorrectId()
    {
        var logic = new StampOverlayLogic();
        var stamp = logic.AddStamp("stamp_a", Vector2.zero);
        Assert.AreEqual("stamp_a", stamp.StampId);
    }

    [Test]
    public void AddStamp_InitialRotation_IsZero()
    {
        var logic = new StampOverlayLogic();
        var stamp = logic.AddStamp("stamp_a", Vector2.zero);
        Assert.AreEqual(0f, stamp.Rotation);
    }

    [Test]
    public void AddStamp_InitialScale_IsOne()
    {
        var logic = new StampOverlayLogic();
        var stamp = logic.AddStamp("stamp_a", Vector2.zero);
        Assert.AreEqual(1f, stamp.Scale);
    }

    [Test]
    public void AddStamp_SetsPosition()
    {
        var logic = new StampOverlayLogic();
        var pos = new Vector2(0.5f, 0.3f);
        var stamp = logic.AddStamp("stamp_a", pos);
        Assert.AreEqual(pos, stamp.Position);
    }

    [Test]
    public void AddStamp_MultipleStamps_AllPresent()
    {
        var logic = new StampOverlayLogic();
        logic.AddStamp("stamp_a", Vector2.zero);
        logic.AddStamp("stamp_b", Vector2.one);
        logic.AddStamp("stamp_c", new Vector2(0.5f, 0.5f));
        Assert.AreEqual(3, logic.Stamps.Count);
    }

    // ── スタンプ削除 ──────────────────────────────────────────────────────────

    [Test]
    public void RemoveStamp_RemovesFromList()
    {
        var logic = new StampOverlayLogic();
        var stamp = logic.AddStamp("stamp_a", Vector2.zero);
        logic.RemoveStamp(stamp);
        Assert.AreEqual(0, logic.Stamps.Count);
    }

    [Test]
    public void RemoveStamp_ReturnsTrue_WhenFound()
    {
        var logic = new StampOverlayLogic();
        var stamp = logic.AddStamp("stamp_a", Vector2.zero);
        var result = logic.RemoveStamp(stamp);
        Assert.IsTrue(result);
    }

    [Test]
    public void RemoveStamp_ReturnsFalse_WhenNotFound()
    {
        var logic = new StampOverlayLogic();
        var other = new StampOverlayLogic().AddStamp("stamp_x", Vector2.zero);
        var result = logic.RemoveStamp(other);
        Assert.IsFalse(result);
    }

    [Test]
    public void RemoveStamp_RemovesOnlyTargetStamp()
    {
        var logic = new StampOverlayLogic();
        var a = logic.AddStamp("stamp_a", Vector2.zero);
        logic.AddStamp("stamp_b", Vector2.one);
        logic.RemoveStamp(a);
        Assert.AreEqual(1, logic.Stamps.Count);
        Assert.AreEqual("stamp_b", logic.Stamps[0].StampId);
    }

    // ── 移動 ─────────────────────────────────────────────────────────────────

    [Test]
    public void MoveStamp_UpdatesPosition()
    {
        var logic = new StampOverlayLogic();
        var stamp = logic.AddStamp("stamp_a", Vector2.zero);
        var newPos = new Vector2(0.8f, 0.2f);
        logic.MoveStamp(stamp, newPos);
        Assert.AreEqual(newPos, stamp.Position);
    }

    // ── 回転 ─────────────────────────────────────────────────────────────────

    [Test]
    public void RotateStamp_UpdatesRotation()
    {
        var logic = new StampOverlayLogic();
        var stamp = logic.AddStamp("stamp_a", Vector2.zero);
        logic.RotateStamp(stamp, 45f);
        Assert.AreEqual(45f, stamp.Rotation);
    }

    [Test]
    public void RotateStamp_NegativeAngle_IsAccepted()
    {
        var logic = new StampOverlayLogic();
        var stamp = logic.AddStamp("stamp_a", Vector2.zero);
        logic.RotateStamp(stamp, -90f);
        Assert.AreEqual(-90f, stamp.Rotation);
    }

    // ── スケール ──────────────────────────────────────────────────────────────

    [Test]
    public void ScaleStamp_UpdatesScale()
    {
        var logic = new StampOverlayLogic();
        var stamp = logic.AddStamp("stamp_a", Vector2.zero);
        logic.ScaleStamp(stamp, 2.5f);
        Assert.AreEqual(2.5f, stamp.Scale);
    }

    [Test]
    public void ScaleStamp_Zero_ClampsToMinimum()
    {
        var logic = new StampOverlayLogic();
        var stamp = logic.AddStamp("stamp_a", Vector2.zero);
        logic.ScaleStamp(stamp, 0f);
        Assert.AreEqual(0.1f, stamp.Scale, 0.0001f);
    }

    [Test]
    public void ScaleStamp_Negative_ClampsToMinimum()
    {
        var logic = new StampOverlayLogic();
        var stamp = logic.AddStamp("stamp_a", Vector2.zero);
        logic.ScaleStamp(stamp, -1f);
        Assert.AreEqual(0.1f, stamp.Scale, 0.0001f);
    }

    // ── クリア（ルーム退室時リセット）────────────────────────────────────────

    [Test]
    public void Clear_RemovesAllStamps()
    {
        var logic = new StampOverlayLogic();
        logic.AddStamp("stamp_a", Vector2.zero);
        logic.AddStamp("stamp_b", Vector2.one);
        logic.Clear();
        Assert.AreEqual(0, logic.Stamps.Count);
    }

    // ── セッション保持 ────────────────────────────────────────────────────────

    [Test]
    public void SessionPersistence_StampsRemainAfterPhotoModeExit()
    {
        var logic = new StampOverlayLogic();
        logic.AddStamp("stamp_a", new Vector2(0.5f, 0.5f));

        // 撮影モード終了・再入をシミュレート（Clear は呼ばない）
        Assert.AreEqual(1, logic.Stamps.Count);
        Assert.AreEqual("stamp_a", logic.Stamps[0].StampId);
    }
}
