using System.Collections.Generic;

public enum PaintLayerType
{
    Normal = 0,
    ColorAdjustment = 1,
}

/// <summary>
/// レイヤーのメタデータ（表示順・可視性・不透明度等）を保持する純粋 C# クラス。
/// ピクセルデータは Rust 側の Canvas が管理する。
/// </summary>
public class PaintLayer
{
    public uint Id { get; internal set; }
    public PaintLayerType Type { get; internal set; }
    public string Name { get; set; }
    public bool Visible { get; set; } = true;
    public bool Locked { get; set; }
    public float Opacity { get; set; } = 1f;
    public bool MaskBelow { get; set; }

    // ColorAdjustment パラメータ（Type == ColorAdjustment のときのみ有効）
    public float Brightness { get; set; }
    public float Saturation { get; set; }
    public float Contrast { get; set; }
    public float HueShift { get; set; }

    // グループ用（将来拡張）
    public List<PaintLayer> Children { get; } = new();
}
