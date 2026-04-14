using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// レイヤースタックのメタデータを管理する純粋 C# クラス。
/// ピクセル描画は Rust エンジンが担う。
/// - 通常レイヤー最大 16 枚
/// - 色調補正レイヤー 1 枚（通常レイヤーの 16 枚制限とは独立カウント）
/// </summary>
public class LayerStack
{
    public const int MaxNormalLayers = 16;

    private readonly List<PaintLayer> _layers = new();
    private PaintLayer _colorAdjustment;
    private uint _nextId = 1;

    /// <summary>通常レイヤー数（色調補正レイヤーを除く）。</summary>
    public int NormalLayerCount { get; private set; }

    /// <summary>色調補正レイヤーが追加されているか。</summary>
    public bool HasColorAdjustment => _colorAdjustment != null;

    /// <summary>色調補正レイヤー（null の場合は未追加）。</summary>
    public PaintLayer ColorAdjustmentLayer => _colorAdjustment;

    /// <summary>レイヤー一覧（インデックス 0 = 最下段）。</summary>
    public IReadOnlyList<PaintLayer> Layers => _layers;

    // ---- 追加 ----

    /// <summary>
    /// 通常レイヤーを最上段に追加する。
    /// 上限（16枚）超過時は null を返す。
    /// </summary>
    public PaintLayer AddNormalLayer(string name = null)
    {
        if (NormalLayerCount >= MaxNormalLayers)
            return null;

        var layer = new PaintLayer
        {
            Id = _nextId++,
            Type = PaintLayerType.Normal,
            Name = name ?? $"Layer {NormalLayerCount + 1}",
        };
        _layers.Add(layer);
        NormalLayerCount++;
        return layer;
    }

    /// <summary>
    /// 色調補正レイヤーを追加する。
    /// 既に存在する場合は null を返す。
    /// </summary>
    public PaintLayer AddColorAdjustmentLayer()
    {
        if (_colorAdjustment != null)
            return null;

        _colorAdjustment = new PaintLayer
        {
            Id = _nextId++,
            Type = PaintLayerType.ColorAdjustment,
            Name = "Color Adjustment",
        };
        return _colorAdjustment;
    }

    // ---- 削除 ----

    /// <summary>指定 ID のレイヤーを削除する。</summary>
    public bool RemoveLayer(uint id)
    {
        if (_colorAdjustment?.Id == id)
        {
            _colorAdjustment = null;
            return true;
        }
        var idx = _layers.FindIndex(l => l.Id == id);
        if (idx < 0)
            return false;
        _layers.RemoveAt(idx);
        NormalLayerCount--;
        return true;
    }

    // ---- 複製 ----

    /// <summary>
    /// 指定 ID の通常レイヤーを複製して直上に挿入する。
    /// 上限超過または色調補正レイヤー指定時は null。
    /// </summary>
    public PaintLayer DuplicateLayer(uint id)
    {
        var src = FindNormalLayer(id);
        if (src == null)
            return null;
        if (NormalLayerCount >= MaxNormalLayers)
            return null;

        var dup = new PaintLayer
        {
            Id = _nextId++,
            Type = PaintLayerType.Normal,
            Name = src.Name + " Copy",
            Visible = src.Visible,
            Locked = src.Locked,
            Opacity = src.Opacity,
            MaskBelow = src.MaskBelow,
        };
        int srcIdx = _layers.IndexOf(src);
        _layers.Insert(srcIdx + 1, dup);
        NormalLayerCount++;
        return dup;
    }

    // ---- 並び替え ----

    /// <summary>指定 ID のレイヤーを newIndex へ移動する（0 = 最下段）。</summary>
    public bool MoveLayer(uint id, int newIndex)
    {
        var idx = _layers.FindIndex(l => l.Id == id);
        if (idx < 0 || newIndex < 0 || newIndex >= _layers.Count)
            return false;
        if (idx == newIndex)
            return true;
        var layer = _layers[idx];
        _layers.RemoveAt(idx);
        _layers.Insert(newIndex, layer);
        return true;
    }

    // ---- 結合 ----

    /// <summary>
    /// 指定 ID の通常レイヤーを直下の通常レイヤーに結合する（メタデータのみ）。
    /// 結合後は上のレイヤーが削除され、下のレイヤーが残る。
    /// 成功時は下のレイヤーを返す。失敗時は null。
    /// </summary>
    public PaintLayer MergeLayerDown(uint id)
    {
        var idx = _layers.FindIndex(l => l.Id == id);
        if (idx <= 0)
            return null;
        if (_layers[idx].Type != PaintLayerType.Normal)
            return null;
        if (_layers[idx - 1].Type != PaintLayerType.Normal)
            return null;

        _layers.RemoveAt(idx);
        NormalLayerCount--;
        return _layers[idx - 1];
    }

    // ---- 検索 ----

    public PaintLayer FindLayer(uint id)
    {
        if (_colorAdjustment?.Id == id)
            return _colorAdjustment;
        return _layers.FirstOrDefault(l => l.Id == id);
    }

    private PaintLayer FindNormalLayer(uint id) =>
        _layers.FirstOrDefault(l => l.Id == id && l.Type == PaintLayerType.Normal);
}
