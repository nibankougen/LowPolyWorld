using System;
using UnityEngine;

/// <summary>
/// アバターテクスチャのペイントセッション。
/// Rust ネイティブキャンバスのライフサイクルと、レイヤー操作・ペイント操作を統括する純粋 C# クラス。
/// </summary>
public class AvatarPaintSession : IDisposable
{
    public const uint CanvasWidth = 256;
    public const uint CanvasHeight = 256;

    private IntPtr _canvas;
    private bool _disposed;

    public LayerStack LayerStack { get; } = new();

    /// <summary>レイヤー追加・削除などの構造変更操作用 Undo 履歴（ピクセル操作は Rust 側が管理）。</summary>
    public PaintCommandHistory StructureHistory { get; } = new();

    public bool IsReady => _canvas != IntPtr.Zero && !_disposed;

    /// <summary>ピクセル操作の Undo が可能か（Rust エンジンが管理）。</summary>
    public bool CanUndo => IsReady && PaintEngineWrapper.pe_can_undo(_canvas);

    /// <summary>ピクセル操作の Redo が可能か（Rust エンジンが管理）。</summary>
    public bool CanRedo => IsReady && PaintEngineWrapper.pe_can_redo(_canvas);

    public AvatarPaintSession()
    {
        _canvas = PaintEngineWrapper.pe_canvas_create(CanvasWidth, CanvasHeight);
        if (_canvas == IntPtr.Zero)
            throw new InvalidOperationException("ネイティブキャンバスの生成に失敗しました。");

        // デフォルト: 通常レイヤーを 1 枚追加
        var defaultLayer = LayerStack.AddNormalLayer("Layer 1");
        PaintEngineWrapper.pe_layer_add(_canvas);
    }

    // ---- レイヤー追加 ----

    /// <summary>通常レイヤーを追加する。上限超過時は null を返す。</summary>
    public PaintLayer AddNormalLayer(string name = null)
    {
        if (!IsReady)
            return null;

        var layer = LayerStack.AddNormalLayer(name);
        if (layer == null)
            return null;

        uint nativeId = PaintEngineWrapper.pe_layer_add(_canvas);
        if (nativeId != layer.Id)
        {
            // Rust 側に作られたレイヤーを削除してからロールバック
            PaintEngineWrapper.pe_layer_remove(_canvas, nativeId);
            LayerStack.RemoveLayer(layer.Id);
            return null;
        }
        return layer;
    }

    /// <summary>色調補正レイヤーを追加する。既に存在する場合は null を返す。</summary>
    public PaintLayer AddColorAdjustmentLayer()
    {
        if (!IsReady)
            return null;

        var layer = LayerStack.AddColorAdjustmentLayer();
        if (layer == null)
            return null;

        uint nativeId = PaintEngineWrapper.pe_layer_add_color_adjustment(_canvas);
        if (nativeId != layer.Id)
        {
            PaintEngineWrapper.pe_layer_remove(_canvas, nativeId);
            LayerStack.RemoveLayer(layer.Id);
            return null;
        }
        return layer;
    }

    // ---- レイヤー操作 ----

    public bool RemoveLayer(uint layerId)
    {
        if (!IsReady)
            return false;
        bool ok = PaintEngineWrapper.pe_layer_remove(_canvas, layerId);
        if (ok)
            LayerStack.RemoveLayer(layerId);
        return ok;
    }

    public PaintLayer DuplicateLayer(uint layerId)
    {
        if (!IsReady)
            return null;
        uint nativeId = PaintEngineWrapper.pe_layer_duplicate(_canvas, layerId);
        if (nativeId == 0)
            return null;
        var dup = LayerStack.DuplicateLayer(layerId);
        if (dup == null || dup.Id != nativeId)
        {
            // ID ミスマッチ: Rust 側のレイヤーを削除してロールバック
            if (dup != null)
                LayerStack.RemoveLayer(dup.Id);
            PaintEngineWrapper.pe_layer_remove(_canvas, nativeId);
            return null;
        }
        return dup;
    }

    public bool MoveLayer(uint layerId, int newIndex)
    {
        if (!IsReady)
            return false;
        bool ok = PaintEngineWrapper.pe_layer_move(_canvas, layerId, (uint)newIndex);
        if (ok)
            LayerStack.MoveLayer(layerId, newIndex);
        return ok;
    }

    public PaintLayer MergeLayerDown(uint layerId)
    {
        if (!IsReady)
            return null;
        uint merged = PaintEngineWrapper.pe_layer_merge_down(_canvas, layerId);
        if (merged == 0)
            return null;
        return LayerStack.MergeLayerDown(layerId);
    }

    public void SetLayerOpacity(uint layerId, float opacity)
    {
        if (!IsReady)
            return;
        var layer = LayerStack.FindLayer(layerId);
        if (layer == null)
            return;
        PaintEngineWrapper.pe_layer_set_opacity(_canvas, layerId, opacity);
        layer.Opacity = opacity;
    }

    public void SetLayerVisible(uint layerId, bool visible)
    {
        if (!IsReady)
            return;
        var layer = LayerStack.FindLayer(layerId);
        if (layer == null)
            return;
        PaintEngineWrapper.pe_layer_set_visible(_canvas, layerId, visible);
        layer.Visible = visible;
    }

    public void SetColorAdj(float brightness, float saturation, float contrast, float hueShift)
    {
        if (!IsReady || !LayerStack.HasColorAdjustment)
            return;
        uint id = LayerStack.ColorAdjustmentLayer.Id;
        PaintEngineWrapper.pe_color_adj_set(_canvas, id, brightness, saturation, contrast, hueShift);
        var layer = LayerStack.ColorAdjustmentLayer;
        layer.Brightness = brightness;
        layer.Saturation = saturation;
        layer.Contrast = contrast;
        layer.HueShift = hueShift;
    }

    // ---- ペイント操作（Undo は Rust エンジンが自動管理） ----

    public void Brush(uint layerId, int cx, int cy, uint radius, byte r, byte g, byte b, byte a, bool antialiased)
    {
        if (!IsReady) return;
        PaintEngineWrapper.pe_brush(_canvas, layerId, cx, cy, radius, r, g, b, a, antialiased);
    }

    public void Eraser(uint layerId, int cx, int cy, uint radius)
    {
        if (!IsReady) return;
        PaintEngineWrapper.pe_eraser(_canvas, layerId, cx, cy, radius);
    }

    public void FloodFill(uint layerId, int x, int y, byte r, byte g, byte b, byte a, byte tolerance)
    {
        if (!IsReady) return;
        PaintEngineWrapper.pe_flood_fill(_canvas, layerId, x, y, r, g, b, a, tolerance);
    }

    public void DrawRect(uint layerId, int x1, int y1, int x2, int y2, byte r, byte g, byte b, byte a, bool filled)
    {
        if (!IsReady) return;
        PaintEngineWrapper.pe_draw_rect(_canvas, layerId, x1, y1, x2, y2, r, g, b, a, filled);
    }

    public void DrawCircle(uint layerId, int x1, int y1, int x2, int y2, byte r, byte g, byte b, byte a, bool filled)
    {
        if (!IsReady) return;
        PaintEngineWrapper.pe_draw_circle(_canvas, layerId, x1, y1, x2, y2, r, g, b, a, filled);
    }

    public void DrawLine(uint layerId, int x1, int y1, int x2, int y2, byte r, byte g, byte b, byte a)
    {
        if (!IsReady) return;
        PaintEngineWrapper.pe_draw_line(_canvas, layerId, x1, y1, x2, y2, r, g, b, a);
    }

    // ---- 合成出力 ----

    /// <summary>全レイヤーを合成した RGBA バイト列を返す（width × height × 4）。</summary>
    public byte[] CompositeRgba()
    {
        if (!IsReady)
            return null;
        return PaintEngineWrapper.CompositeRgba(_canvas, CanvasWidth, CanvasHeight);
    }

    /// <summary>保存用 PNG バイト列を返す（透明ピクセル処理済み）。</summary>
    public byte[] CompositePng()
    {
        if (!IsReady)
            return null;
        return PaintEngineWrapper.CompositePng(_canvas);
    }

    // ---- Undo / Redo（ピクセル操作は Rust エンジンに委譲） ----

    /// <summary>ピクセル操作を 1 ステップ Undo する。成功時 true。</summary>
    public bool Undo() => IsReady && PaintEngineWrapper.pe_undo(_canvas);

    /// <summary>ピクセル操作を 1 ステップ Redo する。成功時 true。</summary>
    public bool Redo() => IsReady && PaintEngineWrapper.pe_redo(_canvas);

    // ---- タブ離脱時リセット ----

    /// <summary>テクスチャタブを離れるときに C# レイヤー構造履歴をリセットする。</summary>
    public void ClearHistory() => StructureHistory.Clear();

    // ---- IDisposable ----

    public void Dispose()
    {
        if (_disposed)
            return;
        if (_canvas != IntPtr.Zero)
        {
            PaintEngineWrapper.pe_canvas_destroy(_canvas);
            _canvas = IntPtr.Zero;
        }
        _disposed = true;
    }

}
