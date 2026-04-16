using System;

/// <summary>ペイントツールの種別。</summary>
public enum PaintTool
{
    Brush = 0,
    Eraser = 1,
    Fill = 2,
    Rect = 3,
    Circle = 4,
    Line = 5,
    SelectRect = 6,
    SelectEllipse = 7,
    Transform = 8,
}

/// <summary>
/// ブラシ設定（ツール種別・サイズ・オプション）を管理する純粋 C# クラス。
/// </summary>
public class BrushSettingsLogic
{
    public const int MinBrushSize = 1;
    public const int MaxBrushSize = 255;

    private PaintTool _tool = PaintTool.Brush;
    private int _brushSize = 16;
    private bool _antialiased;
    private bool _filled = true;

    /// <summary>現在のツールが変化したときに発火する。</summary>
    public event Action OnSettingsChanged;

    public PaintTool Tool
    {
        get => _tool;
        set { _tool = value; OnSettingsChanged?.Invoke(); }
    }

    public int BrushSize
    {
        get => _brushSize;
        set
        {
            _brushSize = Math.Clamp(value, MinBrushSize, MaxBrushSize);
            OnSettingsChanged?.Invoke();
        }
    }

    public bool Antialiased
    {
        get => _antialiased;
        set { _antialiased = value; OnSettingsChanged?.Invoke(); }
    }

    /// <summary>図形ツール（Rect / Circle）の塗りつぶし有無。</summary>
    public bool Filled
    {
        get => _filled;
        set { _filled = value; OnSettingsChanged?.Invoke(); }
    }

    /// <summary>現在のツールのヒント文字列を返す。</summary>
    public string GetToolHint()
    {
        return _tool switch
        {
            PaintTool.Brush         => "ブラシ",
            PaintTool.Eraser        => "消しゴム",
            PaintTool.Fill          => "塗りつぶし",
            PaintTool.Rect          => "四角形",
            PaintTool.Circle        => "円",
            PaintTool.Line          => "直線",
            PaintTool.SelectRect    => "矩形選択",
            PaintTool.SelectEllipse => "楕円選択",
            PaintTool.Transform     => "移動・変形",
            _                       => string.Empty,
        };
    }
}
