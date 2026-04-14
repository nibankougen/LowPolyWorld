using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// カラーピッカーの色状態と履歴を管理する純粋 C# クラス。
/// 内部状態は HSV + Alpha で保持し、RGB バイト値に変換して提供する。
/// </summary>
public class ColorPickerLogic
{
    public const int HistoryCapacity = 16;

    // HSV + Alpha (0-1)
    private float _h;
    private float _s;
    private float _v;
    private float _a = 1f;

    private readonly List<(byte r, byte g, byte b, byte a)> _history = new();

    /// <summary>現在の色が変化したときに発火する。</summary>
    public event Action OnColorChanged;

    // ---- 現在色プロパティ ----

    public float Hue
    {
        get => _h;
        set { _h = Mathf.Clamp01(value); OnColorChanged?.Invoke(); }
    }

    public float Saturation
    {
        get => _s;
        set { _s = Mathf.Clamp01(value); OnColorChanged?.Invoke(); }
    }

    public float Value
    {
        get => _v;
        set { _v = Mathf.Clamp01(value); OnColorChanged?.Invoke(); }
    }

    public float Alpha
    {
        get => _a;
        set { _a = Mathf.Clamp01(value); OnColorChanged?.Invoke(); }
    }

    /// <summary>現在色を RGBA バイト（0-255）で返す。</summary>
    public (byte r, byte g, byte b, byte a) GetRgba()
    {
        Color c = Color.HSVToRGB(_h, _s, _v);
        return (
            (byte)(c.r * 255f),
            (byte)(c.g * 255f),
            (byte)(c.b * 255f),
            (byte)(_a * 255f)
        );
    }

    /// <summary>現在色を Unity Color（0-1）で返す（alpha 込み）。</summary>
    public Color GetColor()
    {
        Color c = Color.HSVToRGB(_h, _s, _v);
        c.a = _a;
        return c;
    }

    // ---- RGB 入力 ----

    /// <summary>RGBA バイト値（0-255）から HSV + Alpha を設定する。</summary>
    public void SetFromRgba(byte r, byte g, byte b, byte a)
    {
        Color c = new Color(r / 255f, g / 255f, b / 255f);
        Color.RGBToHSV(c, out _h, out _s, out _v);
        _a = a / 255f;
        OnColorChanged?.Invoke();
    }

    // ---- SV 四角形操作 ----

    /// <summary>
    /// 明度彩度四角形上の UV 座標（左上=0,0 / 右下=1,1）から S・V を更新する。
    /// U → Saturation、V（縦）を逆転 → Value
    /// </summary>
    public void SetSaturationValue(float u, float v)
    {
        _s = Mathf.Clamp01(u);
        _v = Mathf.Clamp01(1f - v);
        OnColorChanged?.Invoke();
    }

    /// <summary>
    /// 明度彩度四角形の UV 座標を返す（現在の S・V から逆算）。
    /// </summary>
    public (float u, float v) GetSvUV() => (_s, 1f - _v);

    // ---- 色履歴 ----

    /// <summary>現在色を履歴に追加する（同色が先頭にある場合はスキップ）。</summary>
    public void PushCurrentToHistory()
    {
        var current = GetRgba();
        if (_history.Count > 0 && _history[_history.Count - 1] == current)
            return;
        if (_history.Count >= HistoryCapacity)
            _history.RemoveAt(0);
        _history.Add(current);
    }

    /// <summary>履歴の読み取り専用リスト（古い順）。</summary>
    public IReadOnlyList<(byte r, byte g, byte b, byte a)> History => _history;

    /// <summary>履歴の色を現在色として適用する。</summary>
    public void ApplyHistory(int index)
    {
        if (index < 0 || index >= _history.Count)
            return;
        var (r, g, b, a) = _history[index];
        SetFromRgba(r, g, b, a);
    }
}
