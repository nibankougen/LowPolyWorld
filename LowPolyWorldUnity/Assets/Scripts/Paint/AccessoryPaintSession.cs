using System;
using UnityEngine;

/// <summary>
/// アクセサリテクスチャのペイントセッション（64×64）。
/// AvatarPaintSession と同じ構造で、キャンバスサイズのみ異なる。
/// </summary>
public class AccessoryPaintSession : IPaintSession
{
    public const uint CanvasWidth = 64;
    public const uint CanvasHeight = 64;

    uint IPaintSession.CanvasWidth => CanvasWidth;
    uint IPaintSession.CanvasHeight => CanvasHeight;

    private IntPtr _canvas;
    private bool _disposed;
    private bool _hasUvOverlay;
    private bool _uvOverlayVisible;

    public LayerStack LayerStack { get; } = new();
    public PaintCommandHistory StructureHistory { get; } = new();

    public bool IsReady => _canvas != IntPtr.Zero && !_disposed;
    public bool CanUndo => IsReady && PaintEngineWrapper.pe_can_undo(_canvas);
    public bool CanRedo => IsReady && PaintEngineWrapper.pe_can_redo(_canvas);

    public AccessoryPaintSession()
    {
        _canvas = PaintEngineWrapper.pe_canvas_create(CanvasWidth, CanvasHeight);
        if (_canvas == IntPtr.Zero)
            throw new InvalidOperationException("ネイティブキャンバスの生成に失敗しました。");

        var defaultLayer = LayerStack.AddNormalLayer("Layer 1");
        PaintEngineWrapper.pe_layer_add(_canvas);
    }

    // ---- UV オーバーレイ ----

    public bool HasUvOverlay => _hasUvOverlay;
    public bool UvOverlayVisible => _uvOverlayVisible;

    public void SetUvOverlay(byte[] rgba)
    {
        if (!IsReady || rgba == null)
            return;
        PaintEngineWrapper.SetUvOverlay(_canvas, rgba);
        _hasUvOverlay = true;
        _uvOverlayVisible = true;
    }

    public void SetUvOverlayVisible(bool visible)
    {
        if (!IsReady)
            return;
        PaintEngineWrapper.pe_uv_overlay_set_visible(_canvas, visible);
        _uvOverlayVisible = visible;
    }

    // ---- レイヤー追加 ----

    public PaintLayer AddNormalLayer(string name = null)
    {
        if (!IsReady) return null;
        var layer = LayerStack.AddNormalLayer(name);
        if (layer == null) return null;
        uint nativeId = PaintEngineWrapper.pe_layer_add(_canvas);
        if (nativeId != layer.Id)
        {
            PaintEngineWrapper.pe_layer_remove(_canvas, nativeId);
            LayerStack.RemoveLayer(layer.Id);
            return null;
        }
        return layer;
    }

    public PaintLayer AddColorAdjustmentLayer()
    {
        if (!IsReady) return null;
        var layer = LayerStack.AddColorAdjustmentLayer();
        if (layer == null) return null;
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
        if (!IsReady) return false;
        bool ok = PaintEngineWrapper.pe_layer_remove(_canvas, layerId);
        if (ok) LayerStack.RemoveLayer(layerId);
        return ok;
    }

    public bool MoveLayer(uint layerId, int newIndex)
    {
        if (!IsReady) return false;
        bool ok = PaintEngineWrapper.pe_layer_move(_canvas, layerId, (uint)newIndex);
        if (ok) LayerStack.MoveLayer(layerId, newIndex);
        return ok;
    }

    public void SetLayerOpacity(uint layerId, float opacity)
    {
        if (!IsReady) return;
        var layer = LayerStack.FindLayer(layerId);
        if (layer == null) return;
        PaintEngineWrapper.pe_layer_set_opacity(_canvas, layerId, opacity);
        layer.Opacity = opacity;
    }

    public void SetLayerVisible(uint layerId, bool visible)
    {
        if (!IsReady) return;
        var layer = LayerStack.FindLayer(layerId);
        if (layer == null) return;
        PaintEngineWrapper.pe_layer_set_visible(_canvas, layerId, visible);
        layer.Visible = visible;
    }

    public void SetLayerLocked(uint layerId, bool locked)
    {
        if (!IsReady) return;
        var layer = LayerStack.FindLayer(layerId);
        if (layer == null) return;
        PaintEngineWrapper.pe_layer_set_locked(_canvas, layerId, locked);
        layer.Locked = locked;
    }

    public void SetLayerMaskBelow(uint layerId, bool maskBelow)
    {
        if (!IsReady) return;
        var layer = LayerStack.FindLayer(layerId);
        if (layer == null) return;
        PaintEngineWrapper.pe_layer_set_mask_below(_canvas, layerId, maskBelow);
        layer.MaskBelow = maskBelow;
    }

    // ---- ペイント操作 ----

    public void Brush(uint layerId, int cx, int cy, uint radius, byte r, byte g, byte b, byte a, bool antialiased)
    {
        if (!IsReady || IsLayerLocked(layerId)) return;
        PaintEngineWrapper.pe_brush(_canvas, layerId, cx, cy, radius, r, g, b, a, antialiased);
    }

    public void Eraser(uint layerId, int cx, int cy, uint radius)
    {
        if (!IsReady || IsLayerLocked(layerId)) return;
        PaintEngineWrapper.pe_eraser(_canvas, layerId, cx, cy, radius);
    }

    public void FloodFill(uint layerId, int x, int y, byte r, byte g, byte b, byte a, byte tolerance)
    {
        if (!IsReady || IsLayerLocked(layerId)) return;
        PaintEngineWrapper.pe_flood_fill(_canvas, layerId, x, y, r, g, b, a, tolerance);
    }

    public void DrawRect(uint layerId, int x1, int y1, int x2, int y2, byte r, byte g, byte b, byte a, bool filled)
    {
        if (!IsReady || IsLayerLocked(layerId)) return;
        PaintEngineWrapper.pe_draw_rect(_canvas, layerId, x1, y1, x2, y2, r, g, b, a, filled);
    }

    public void DrawCircle(uint layerId, int x1, int y1, int x2, int y2, byte r, byte g, byte b, byte a, bool filled)
    {
        if (!IsReady || IsLayerLocked(layerId)) return;
        PaintEngineWrapper.pe_draw_circle(_canvas, layerId, x1, y1, x2, y2, r, g, b, a, filled);
    }

    public void DrawLine(uint layerId, int x1, int y1, int x2, int y2, byte r, byte g, byte b, byte a)
    {
        if (!IsReady || IsLayerLocked(layerId)) return;
        PaintEngineWrapper.pe_draw_line(_canvas, layerId, x1, y1, x2, y2, r, g, b, a);
    }

    private bool IsLayerLocked(uint layerId) => LayerStack.FindLayer(layerId)?.Locked == true;

    // ---- レイヤー変形 ----

    public void TranslateLayer(uint layerId, int dx, int dy)
    {
        if (!IsReady || IsLayerLocked(layerId)) return;
        PaintEngineWrapper.pe_layer_translate(_canvas, layerId, dx, dy);
    }

    public void ScaleLayerNn(uint layerId, float scaleX, float scaleY)
    {
        if (!IsReady || IsLayerLocked(layerId)) return;
        PaintEngineWrapper.pe_layer_scale_nn(_canvas, layerId, scaleX, scaleY);
    }

    // ---- 範囲選択 ----

    public void SelectionSetRect(int x1, int y1, int x2, int y2)
    {
        if (!IsReady) return;
        PaintEngineWrapper.pe_selection_set_rect(_canvas, x1, y1, x2, y2);
    }

    public void SelectionSetEllipse(int x1, int y1, int x2, int y2)
    {
        if (!IsReady) return;
        PaintEngineWrapper.pe_selection_set_ellipse(_canvas, x1, y1, x2, y2);
    }

    public void SelectionClear()
    {
        if (!IsReady) return;
        PaintEngineWrapper.pe_selection_clear(_canvas);
    }

    public void SelectionInvert()
    {
        if (!IsReady) return;
        PaintEngineWrapper.pe_selection_invert(_canvas);
    }

    public bool SelectionHas => IsReady && PaintEngineWrapper.pe_selection_has(_canvas);

    // ---- グループ管理 ----

    public uint AddGroup(string name)
    {
        if (!IsReady) return 0;
        return PaintEngineWrapper.pe_group_add(_canvas, name ?? "Group");
    }

    public bool RemoveGroup(uint groupId)
    {
        if (!IsReady) return false;
        return PaintEngineWrapper.pe_group_remove(_canvas, groupId);
    }

    public bool SetGroupVisible(uint groupId, bool visible)
    {
        if (!IsReady) return false;
        return PaintEngineWrapper.pe_group_set_visible(_canvas, groupId, visible);
    }

    public bool SetLayerGroup(uint layerId, uint groupId)
    {
        if (!IsReady) return false;
        return PaintEngineWrapper.pe_layer_set_group(_canvas, layerId, groupId);
    }

    // ---- ピクセル読み出し ----

    public byte[] GetLayerPixels(uint layerId)
    {
        if (!IsReady) return null;
        return PaintEngineWrapper.GetLayerPixels(_canvas, layerId);
    }

    // ---- 合成出力 ----

    public byte[] CompositeRgba()
    {
        if (!IsReady) return null;
        return PaintEngineWrapper.CompositeRgba(_canvas, CanvasWidth, CanvasHeight);
    }

    public byte[] CompositePng()
    {
        if (!IsReady) return null;
        return PaintEngineWrapper.CompositePng(_canvas);
    }

    // ---- Undo / Redo ----

    public bool Undo() => IsReady && PaintEngineWrapper.pe_undo(_canvas);
    public bool Redo() => IsReady && PaintEngineWrapper.pe_redo(_canvas);
    public void ClearHistory() => StructureHistory.Clear();

    // ---- 保存後クリーンアップ ----

    public void CleanupUndo()
    {
        if (!IsReady) return;
        PaintEngineWrapper.pe_canvas_cleanup(_canvas);
    }

    // ---- キャンバスリサイズ ----

    public void ResizeCanvas(uint newWidth, uint newHeight)
    {
        if (!IsReady) return;
        PaintEngineWrapper.pe_canvas_resize(_canvas, newWidth, newHeight);
    }

    // ---- レイヤー取り込み ----

    public bool ImportLayerPng(uint layerId, byte[] pngData)
    {
        if (!IsReady) return false;
        return PaintEngineWrapper.ImportLayerPng(_canvas, layerId, pngData);
    }

    // ---- IDisposable ----

    public void Dispose()
    {
        if (_disposed) return;
        if (_canvas != IntPtr.Zero)
        {
            PaintEngineWrapper.pe_canvas_destroy(_canvas);
            _canvas = IntPtr.Zero;
        }
        _disposed = true;
    }
}
