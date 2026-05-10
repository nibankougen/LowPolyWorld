using NUnit.Framework;
using UnityEngine;

public class StampColorPickerLogicTests
{
    private static readonly Color Red = Color.red;
    private static readonly Color Blue = Color.blue;
    private static readonly Color Green = Color.green;

    // ── パレット色選択 ────────────────────────────────────────────────────────

    [Test]
    public void SelectPaletteColor_UpdatesSelectedColor()
    {
        var logic = new StampColorPickerLogic(Red);
        logic.SelectPaletteColor(Blue);
        Assert.AreEqual(Blue, logic.SelectedColor);
    }

    [Test]
    public void SelectPaletteColor_DuringEyedropper_ReturnsFalse()
    {
        var logic = new StampColorPickerLogic(Red);
        logic.EnterEyedropper();
        var result = logic.SelectPaletteColor(Blue);
        Assert.IsFalse(result);
        Assert.AreEqual(Red, logic.SelectedColor); // 変化しない
    }

    // ── スポイトモード移行 ────────────────────────────────────────────────────

    [Test]
    public void EnterEyedropper_SetsIsActive()
    {
        var logic = new StampColorPickerLogic(Red);
        logic.EnterEyedropper();
        Assert.IsTrue(logic.IsEyedropperActive);
    }

    [Test]
    public void ExitEyedropper_ClearsIsActive()
    {
        var logic = new StampColorPickerLogic(Red);
        logic.EnterEyedropper();
        logic.ExitEyedropper();
        Assert.IsFalse(logic.IsEyedropperActive);
    }

    [Test]
    public void ExitEyedropper_ClearsPendingConfirm()
    {
        var logic = new StampColorPickerLogic(Red);
        logic.EnterEyedropper();
        logic.SampleColor(Blue);
        logic.OnFingerReleased();
        logic.ExitEyedropper();
        Assert.IsFalse(logic.IsPendingConfirm);
    }

    // ── スポイト色サンプリング ────────────────────────────────────────────────

    [Test]
    public void SampleColor_UpdatesSampledColor()
    {
        var logic = new StampColorPickerLogic(Red);
        logic.EnterEyedropper();
        logic.SampleColor(Blue);
        Assert.AreEqual(Blue, logic.SampledColor);
    }

    [Test]
    public void SampleColor_ResetsConfirmTimer()
    {
        var logic = new StampColorPickerLogic(Red);
        logic.EnterEyedropper();
        logic.SampleColor(Blue);
        logic.OnFingerReleased(); // タイマー開始

        logic.SampleColor(Green); // 再タップ → タイマーリセット

        Assert.IsFalse(logic.IsPendingConfirm);
    }

    // ── スポイト確定タイマー（0.4 秒）─────────────────────────────────────────

    [Test]
    public void OnFingerReleased_StartsPendingConfirm()
    {
        var logic = new StampColorPickerLogic(Red);
        logic.EnterEyedropper();
        logic.SampleColor(Blue);
        logic.OnFingerReleased();
        Assert.IsTrue(logic.IsPendingConfirm);
    }

    [Test]
    public void Tick_BeforeDelay_DoesNotConfirmColor()
    {
        var logic = new StampColorPickerLogic(Red);
        logic.EnterEyedropper();
        logic.SampleColor(Blue);
        logic.OnFingerReleased();

        logic.Tick(StampColorPickerLogic.EyedropperConfirmDelay - 0.01f);

        Assert.IsTrue(logic.IsEyedropperActive);
        Assert.AreEqual(Red, logic.SelectedColor); // まだ確定していない
    }

    [Test]
    public void Tick_AfterDelay_ConfirmsColor()
    {
        var logic = new StampColorPickerLogic(Red);
        logic.EnterEyedropper();
        logic.SampleColor(Blue);
        logic.OnFingerReleased();

        logic.Tick(StampColorPickerLogic.EyedropperConfirmDelay);

        Assert.IsFalse(logic.IsEyedropperActive);
        Assert.AreEqual(Blue, logic.SelectedColor);
    }

    [Test]
    public void Tick_AfterDelay_FiresOnColorConfirmed()
    {
        var logic = new StampColorPickerLogic(Red);
        Color confirmed = Color.clear;
        logic.OnColorConfirmed += c => confirmed = c;

        logic.EnterEyedropper();
        logic.SampleColor(Blue);
        logic.OnFingerReleased();
        logic.Tick(StampColorPickerLogic.EyedropperConfirmDelay);

        Assert.AreEqual(Blue, confirmed);
    }

    // ── 再タップで再スポイト開始 ──────────────────────────────────────────────

    [Test]
    public void RetapWithin04s_ResetsTimerAndAllowsNewSample()
    {
        var logic = new StampColorPickerLogic(Red);
        logic.EnterEyedropper();
        logic.SampleColor(Blue);
        logic.OnFingerReleased(); // タイマー開始

        logic.Tick(0.2f); // 0.4 秒経過前

        // 再タップ → タイマーリセット・新しい色をサンプル
        logic.SampleColor(Green);
        logic.OnFingerReleased(); // 新タイマー開始

        logic.Tick(StampColorPickerLogic.EyedropperConfirmDelay);

        Assert.AreEqual(Green, logic.SelectedColor); // 最後にサンプルした色が確定
    }

    [Test]
    public void Tick_WithoutFingerRelease_DoesNotConfirm()
    {
        var logic = new StampColorPickerLogic(Red);
        logic.EnterEyedropper();
        logic.SampleColor(Blue);
        // OnFingerReleased を呼ばない

        logic.Tick(StampColorPickerLogic.EyedropperConfirmDelay * 2f);

        Assert.IsTrue(logic.IsEyedropperActive);
        Assert.AreEqual(Red, logic.SelectedColor);
    }
}
