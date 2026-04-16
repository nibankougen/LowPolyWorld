using System;
using System.Runtime.InteropServices;

/// <summary>
/// Rust paint-engine ネイティブライブラリへの P/Invoke ラッパー。
/// iOS は "__Internal" (staticlib)、それ以外は "paint_engine" (cdylib) を使用。
/// </summary>
public static class PaintEngineWrapper
{
#if UNITY_IOS && !UNITY_EDITOR
    private const string Lib = "__Internal";
#else
    private const string Lib = "paint_engine";
#endif

    // ---- キャンバス管理 ----

    [DllImport(Lib)] public static extern IntPtr pe_canvas_create(uint width, uint height);
    [DllImport(Lib)] public static extern void pe_canvas_destroy(IntPtr canvas);

    // ---- レイヤー操作 ----

    [DllImport(Lib)] public static extern uint pe_layer_add(IntPtr canvas);
    [DllImport(Lib)] public static extern uint pe_layer_add_color_adjustment(IntPtr canvas);
    [DllImport(Lib)] [return: MarshalAs(UnmanagedType.I1)] public static extern bool pe_layer_remove(IntPtr canvas, uint layerId);
    [DllImport(Lib)] public static extern uint pe_layer_duplicate(IntPtr canvas, uint layerId);
    [DllImport(Lib)] [return: MarshalAs(UnmanagedType.I1)] public static extern bool pe_layer_move(IntPtr canvas, uint layerId, uint newIndex);
    [DllImport(Lib)] public static extern uint pe_layer_merge_down(IntPtr canvas, uint layerId);
    [DllImport(Lib)] public static extern void pe_layer_set_opacity(IntPtr canvas, uint layerId, float opacity);
    [DllImport(Lib)] public static extern void pe_layer_set_visible(IntPtr canvas, uint layerId, [MarshalAs(UnmanagedType.I1)] bool visible);
    [DllImport(Lib)] public static extern void pe_layer_set_locked(IntPtr canvas, uint layerId, [MarshalAs(UnmanagedType.I1)] bool locked);
    [DllImport(Lib)] public static extern void pe_layer_set_mask_below(IntPtr canvas, uint layerId, [MarshalAs(UnmanagedType.I1)] bool maskBelow);
    [DllImport(Lib)] [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool pe_layer_set_pixels(IntPtr canvas, uint layerId, IntPtr data, uint dataLen);

    [DllImport(Lib)] public static extern IntPtr pe_layer_get_pixels(IntPtr canvas, uint layerId, out uint outLen);

    [DllImport(Lib)] public static extern void pe_layer_translate(IntPtr canvas, uint layerId, int dx, int dy);
    [DllImport(Lib)] public static extern void pe_layer_scale_nn(IntPtr canvas, uint layerId, float scaleX, float scaleY);

    // ---- 色調補正 ----

    [DllImport(Lib)]
    public static extern void pe_color_adj_set(IntPtr canvas, uint layerId, float brightness, float saturation, float contrast, float hueShift);

    // ---- ペイントツール ----

    [DllImport(Lib)]
    public static extern void pe_brush(IntPtr canvas, uint layerId, int cx, int cy, uint radius, byte r, byte g, byte b, byte a, [MarshalAs(UnmanagedType.I1)] bool antialiased);

    [DllImport(Lib)]
    public static extern void pe_eraser(IntPtr canvas, uint layerId, int cx, int cy, uint radius);

    [DllImport(Lib)]
    public static extern void pe_flood_fill(IntPtr canvas, uint layerId, int x, int y, byte r, byte g, byte b, byte a, byte tolerance);

    [DllImport(Lib)]
    public static extern void pe_draw_rect(IntPtr canvas, uint layerId, int x1, int y1, int x2, int y2, byte r, byte g, byte b, byte a, [MarshalAs(UnmanagedType.I1)] bool filled);

    [DllImport(Lib)]
    public static extern void pe_draw_circle(IntPtr canvas, uint layerId, int x1, int y1, int x2, int y2, byte r, byte g, byte b, byte a, [MarshalAs(UnmanagedType.I1)] bool filled);

    [DllImport(Lib)]
    public static extern void pe_draw_line(IntPtr canvas, uint layerId, int x1, int y1, int x2, int y2, byte r, byte g, byte b, byte a);

    [DllImport(Lib)] public static extern void pe_base_brush(IntPtr canvas, int cx, int cy, uint radius);
    [DllImport(Lib)] public static extern void pe_base_eraser(IntPtr canvas, int cx, int cy, uint radius);

    // ---- 合成出力 ----

    [DllImport(Lib)] public static extern IntPtr pe_composite_rgba(IntPtr canvas, out uint outLen);
    [DllImport(Lib)] public static extern IntPtr pe_composite_png(IntPtr canvas, out uint outLen);
    [DllImport(Lib)] public static extern void pe_free_bytes(IntPtr ptr, uint len);

    // ---- Undo / Redo ----

    [DllImport(Lib)] [return: MarshalAs(UnmanagedType.I1)] public static extern bool pe_undo(IntPtr canvas);
    [DllImport(Lib)] [return: MarshalAs(UnmanagedType.I1)] public static extern bool pe_redo(IntPtr canvas);
    [DllImport(Lib)] [return: MarshalAs(UnmanagedType.I1)] public static extern bool pe_can_undo(IntPtr canvas);
    [DllImport(Lib)] [return: MarshalAs(UnmanagedType.I1)] public static extern bool pe_can_redo(IntPtr canvas);

    // ---- グループ管理 ----

    [DllImport(Lib)] public static extern uint pe_group_add(IntPtr canvas, string name);
    [DllImport(Lib)] [return: MarshalAs(UnmanagedType.I1)] public static extern bool pe_group_remove(IntPtr canvas, uint groupId);
    [DllImport(Lib)] [return: MarshalAs(UnmanagedType.I1)] public static extern bool pe_group_set_visible(IntPtr canvas, uint groupId, [MarshalAs(UnmanagedType.I1)] bool visible);
    [DllImport(Lib)] [return: MarshalAs(UnmanagedType.I1)] public static extern bool pe_group_set_expanded(IntPtr canvas, uint groupId, [MarshalAs(UnmanagedType.I1)] bool expanded);
    [DllImport(Lib)] [return: MarshalAs(UnmanagedType.I1)] public static extern bool pe_layer_set_group(IntPtr canvas, uint layerId, uint groupId);

    // ---- 範囲選択 ----

    [DllImport(Lib)] public static extern void pe_selection_set_rect(IntPtr canvas, int x1, int y1, int x2, int y2);
    [DllImport(Lib)] public static extern void pe_selection_set_ellipse(IntPtr canvas, int x1, int y1, int x2, int y2);
    [DllImport(Lib)] public static extern void pe_selection_clear(IntPtr canvas);
    [DllImport(Lib)] public static extern void pe_selection_invert(IntPtr canvas);
    [DllImport(Lib)] [return: MarshalAs(UnmanagedType.I1)] public static extern bool pe_selection_has(IntPtr canvas);

    // ---- クリーンアップ ----

    [DllImport(Lib)] public static extern void pe_canvas_cleanup(IntPtr canvas);

    // ---- キャンバスリサイズ ----

    [DllImport(Lib)] public static extern void pe_canvas_resize(IntPtr canvas, uint newWidth, uint newHeight);

    // ---- UV オーバーレイ ----

    [DllImport(Lib)]
    public static extern void pe_canvas_set_uv_overlay(IntPtr canvas, IntPtr data, uint dataLen);

    [DllImport(Lib)]
    public static extern void pe_uv_overlay_set_visible(IntPtr canvas, [MarshalAs(UnmanagedType.I1)] bool visible);

    // ---- レイヤー取り込み ----

    [DllImport(Lib)] [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool pe_layer_import_png(IntPtr canvas, uint layerId, IntPtr data, uint dataLen);

    // ---- 高レベルヘルパー ----

    /// <summary>pe_composite_rgba が返した RGBA データを byte[] にコピーして解放する。</summary>
    public static byte[] CompositeRgba(IntPtr canvas, uint width, uint height)
    {
        uint len;
        var ptr = pe_composite_rgba(canvas, out len);
        if (ptr == IntPtr.Zero)
            return null;
        try
        {
            var bytes = new byte[len];
            Marshal.Copy(ptr, bytes, 0, (int)len);
            return bytes;
        }
        finally
        {
            pe_free_bytes(ptr, len);
        }
    }

    /// <summary>pe_composite_png が返した PNG データを byte[] にコピーして解放する。</summary>
    public static byte[] CompositePng(IntPtr canvas)
    {
        uint len;
        var ptr = pe_composite_png(canvas, out len);
        if (ptr == IntPtr.Zero)
            return null;
        try
        {
            var bytes = new byte[len];
            Marshal.Copy(ptr, bytes, 0, (int)len);
            return bytes;
        }
        finally
        {
            pe_free_bytes(ptr, len);
        }
    }

    /// <summary>指定レイヤーの RGBA ピクセルを byte[] として返す。色調補正レイヤーや不明 ID は null。</summary>
    public static byte[] GetLayerPixels(IntPtr canvas, uint layerId)
    {
        uint len;
        var ptr = pe_layer_get_pixels(canvas, layerId, out len);
        if (ptr == IntPtr.Zero || len == 0)
            return null;
        try
        {
            var bytes = new byte[len];
            Marshal.Copy(ptr, bytes, 0, (int)len);
            return bytes;
        }
        finally
        {
            pe_free_bytes(ptr, len);
        }
    }

    /// <summary>PNG バイト列をピン固定して Rust へ渡す。</summary>
    public static bool ImportLayerPng(IntPtr canvas, uint layerId, byte[] pngData)
    {
        if (pngData == null)
            return false;
        var handle = GCHandle.Alloc(pngData, GCHandleType.Pinned);
        try
        {
            return pe_layer_import_png(canvas, layerId, handle.AddrOfPinnedObject(), (uint)pngData.Length);
        }
        finally
        {
            handle.Free();
        }
    }

    /// <summary>UV オーバーレイ RGBA バイト列をピン固定して Rust へ渡す。</summary>
    public static void SetUvOverlay(IntPtr canvas, byte[] rgba)
    {
        if (rgba == null)
            return;
        var handle = GCHandle.Alloc(rgba, GCHandleType.Pinned);
        try
        {
            pe_canvas_set_uv_overlay(canvas, handle.AddrOfPinnedObject(), (uint)rgba.Length);
        }
        finally
        {
            handle.Free();
        }
    }

    /// <summary>RGBA バイト列をピン固定して Rust へ渡す。</summary>
    public static bool SetLayerPixels(IntPtr canvas, uint layerId, byte[] rgba)
    {
        if (rgba == null)
            return false;
        var handle = GCHandle.Alloc(rgba, GCHandleType.Pinned);
        try
        {
            return pe_layer_set_pixels(canvas, layerId, handle.AddrOfPinnedObject(), (uint)rgba.Length);
        }
        finally
        {
            handle.Free();
        }
    }
}
