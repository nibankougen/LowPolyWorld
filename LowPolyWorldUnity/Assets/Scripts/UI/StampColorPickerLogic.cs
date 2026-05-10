using System;
using UnityEngine;

/// <summary>
/// 撮影モード・スタンプの色選択ロジック（純粋 C#）。
/// パレット色選択とスポイトモード（0.4 秒確定タイマー）を管理する。
/// 仕様: screens-and-modes.md セクション 2.7.3
/// </summary>
public class StampColorPickerLogic
{
    public const float EyedropperConfirmDelay = 0.4f;

    /// <summary>現在の確定済み選択色。</summary>
    public Color SelectedColor { get; private set; }

    /// <summary>スポイトで現在サンプリング中の色。</summary>
    public Color SampledColor { get; private set; }

    /// <summary>スポイトモードが有効か。</summary>
    public bool IsEyedropperActive { get; private set; }

    /// <summary>スポイト確定タイマーが進行中か（指を離してから 0.4 秒待機中）。</summary>
    public bool IsPendingConfirm => IsEyedropperActive && _confirmTimer >= 0f;

    private float _confirmTimer = -1f; // -1 = カウントしていない

    /// <summary>スポイト色確定時に発火する。引数は確定された色。</summary>
    public event Action<Color> OnColorConfirmed;

    public StampColorPickerLogic(Color initialColor)
    {
        SelectedColor = initialColor;
        SampledColor = initialColor;
    }

    /// <summary>パレットから色を選択する（スポイトモード中は無効）。</summary>
    public bool SelectPaletteColor(Color color)
    {
        if (IsEyedropperActive) return false;
        SelectedColor = color;
        return true;
    }

    /// <summary>スポイトモードに移行する。</summary>
    public void EnterEyedropper()
    {
        IsEyedropperActive = true;
        _confirmTimer = -1f;
    }

    /// <summary>スポイトモードを強制終了する（キャンセル等）。</summary>
    public void ExitEyedropper()
    {
        IsEyedropperActive = false;
        _confirmTimer = -1f;
    }

    /// <summary>
    /// スポイトで色をサンプリングする（タップ中・指が画面に触れている間）。
    /// 確定タイマーをリセットする（仕様: 再タップで再スポイト開始）。
    /// </summary>
    public void SampleColor(Color color)
    {
        if (!IsEyedropperActive) return;
        SampledColor = color;
        _confirmTimer = -1f; // タイマーリセット
    }

    /// <summary>
    /// スポイトサンプリング中に指を離した → 確定タイマーを開始する。
    /// </summary>
    public void OnFingerReleased()
    {
        if (!IsEyedropperActive) return;
        _confirmTimer = 0f;
    }

    /// <summary>
    /// フレーム更新。確定タイマーが EyedropperConfirmDelay に達したら色確定・スポイト終了。
    /// </summary>
    public void Tick(float deltaTime)
    {
        if (!IsEyedropperActive || _confirmTimer < 0f) return;

        _confirmTimer += deltaTime;
        if (_confirmTimer >= EyedropperConfirmDelay)
        {
            SelectedColor = SampledColor;
            IsEyedropperActive = false;
            _confirmTimer = -1f;
            OnColorConfirmed?.Invoke(SelectedColor);
        }
    }
}
