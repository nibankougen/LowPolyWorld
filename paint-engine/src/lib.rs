// paint-engine: 2D texture paint engine for avatar/accessory/world-object diffuse editing.
// Exposed as a native library (cdylib/staticlib) called from Unity via P/Invoke.

mod canvas;
mod compositor;
mod layer;
mod tools;
mod undo;

use canvas::Canvas;

// ============================================================
// キャンバス管理
// ============================================================

/// 新しいキャンバスを作成する。返されたポインタは pe_canvas_destroy で解放すること。
#[no_mangle]
pub extern "C" fn pe_canvas_create(width: u32, height: u32) -> *mut Canvas {
    Box::into_raw(Box::new(Canvas::new(width, height)))
}

/// キャンバスを破棄する。
#[no_mangle]
pub extern "C" fn pe_canvas_destroy(ptr: *mut Canvas) {
    if ptr.is_null() {
        return;
    }
    unsafe { drop(Box::from_raw(ptr)) };
}

// ============================================================
// レイヤー操作
// ============================================================

/// 通常レイヤーを追加する。成功時はレイヤー ID（>0）を返す。上限超過時は 0。
#[no_mangle]
pub extern "C" fn pe_layer_add(ptr: *mut Canvas) -> u32 {
    canvas_ref_mut(ptr, |c| c.add_normal_layer())
}

/// 色調補正レイヤーを追加する。既存の場合は 0。
#[no_mangle]
pub extern "C" fn pe_layer_add_color_adjustment(ptr: *mut Canvas) -> u32 {
    canvas_ref_mut(ptr, |c| c.add_color_adjustment_layer())
}

/// レイヤーを削除する。
#[no_mangle]
pub extern "C" fn pe_layer_remove(ptr: *mut Canvas, layer_id: u32) -> bool {
    canvas_ref_mut(ptr, |c| c.remove_layer(layer_id))
}

/// レイヤーを複製する。成功時は新レイヤーの ID を返す。
#[no_mangle]
pub extern "C" fn pe_layer_duplicate(ptr: *mut Canvas, layer_id: u32) -> u32 {
    canvas_ref_mut(ptr, |c| c.duplicate_layer(layer_id))
}

/// レイヤーを指定インデックスへ移動する（0 = 最下段）。
#[no_mangle]
pub extern "C" fn pe_layer_move(ptr: *mut Canvas, layer_id: u32, new_index: u32) -> bool {
    canvas_ref_mut(ptr, |c| c.move_layer(layer_id, new_index as usize))
}

/// 指定レイヤーを直下の通常レイヤーへ結合する。成功時は下レイヤーの ID を返す。
#[no_mangle]
pub extern "C" fn pe_layer_merge_down(ptr: *mut Canvas, layer_id: u32) -> u32 {
    canvas_ref_mut(ptr, |c| c.merge_layer_down(layer_id))
}

#[no_mangle]
pub extern "C" fn pe_layer_set_opacity(ptr: *mut Canvas, layer_id: u32, opacity: f32) {
    canvas_ref_mut(ptr, |c| c.set_layer_opacity(layer_id, opacity));
}

#[no_mangle]
pub extern "C" fn pe_layer_set_visible(ptr: *mut Canvas, layer_id: u32, visible: bool) {
    canvas_ref_mut(ptr, |c| c.set_layer_visible(layer_id, visible));
}

#[no_mangle]
pub extern "C" fn pe_layer_set_locked(ptr: *mut Canvas, layer_id: u32, locked: bool) {
    canvas_ref_mut(ptr, |c| c.set_layer_locked(layer_id, locked));
}

#[no_mangle]
pub extern "C" fn pe_layer_set_mask_below(ptr: *mut Canvas, layer_id: u32, mask_below: bool) {
    canvas_ref_mut(ptr, |c| c.set_layer_mask_below(layer_id, mask_below));
}

/// レイヤーピクセルを RGBA バイト列で設定する（width*height*4 バイト）。
#[no_mangle]
pub extern "C" fn pe_layer_set_pixels(
    ptr: *mut Canvas,
    layer_id: u32,
    data: *const u8,
    data_len: u32,
) -> bool {
    if data.is_null() {
        return false;
    }
    canvas_ref_mut(ptr, |c| {
        let slice = unsafe { std::slice::from_raw_parts(data, data_len as usize) };
        c.set_layer_pixels(layer_id, slice)
    })
}

/// 指定レイヤーの RGBA ピクセルデータを返す（width*height*4 バイト）。
/// 呼び出し元は pe_free_bytes で解放すること。
/// レイヤーが存在しない・ピクセルデータなし（色調補正レイヤー等）の場合は null / out_len=0。
#[no_mangle]
pub extern "C" fn pe_layer_get_pixels(
    ptr: *const Canvas,
    layer_id: u32,
    out_len: *mut u32,
) -> *mut u8 {
    canvas_ref(ptr, |c| match c.get_layer_pixels(layer_id) {
        Some(pixels) if !pixels.is_empty() => {
            if !out_len.is_null() {
                unsafe { *out_len = pixels.len() as u32 };
            }
            let mut v = pixels.to_vec().into_boxed_slice();
            let p = v.as_mut_ptr();
            std::mem::forget(v);
            p
        }
        _ => {
            if !out_len.is_null() {
                unsafe { *out_len = 0 };
            }
            std::ptr::null_mut()
        }
    })
    .unwrap_or(std::ptr::null_mut())
}

/// 色調補正レイヤーのパラメータを設定する。
#[no_mangle]
pub extern "C" fn pe_color_adj_set(
    ptr: *mut Canvas,
    layer_id: u32,
    brightness: f32,
    saturation: f32,
    contrast: f32,
    hue_shift: f32,
) {
    canvas_ref_mut(ptr, |c| {
        c.set_color_adjustment(layer_id, brightness, saturation, contrast, hue_shift)
    });
}

// ============================================================
// ペイントツール
// ============================================================

#[no_mangle]
pub extern "C" fn pe_brush(
    ptr: *mut Canvas,
    layer_id: u32,
    cx: i32,
    cy: i32,
    radius: u32,
    r: u8,
    g: u8,
    b: u8,
    a: u8,
    antialiased: bool,
) {
    canvas_ref_mut(ptr, |c| c.brush(layer_id, cx, cy, radius, [r, g, b, a], antialiased));
}

#[no_mangle]
pub extern "C" fn pe_eraser(ptr: *mut Canvas, layer_id: u32, cx: i32, cy: i32, radius: u32) {
    canvas_ref_mut(ptr, |c| c.eraser(layer_id, cx, cy, radius));
}

#[no_mangle]
pub extern "C" fn pe_flood_fill(
    ptr: *mut Canvas,
    layer_id: u32,
    x: i32,
    y: i32,
    r: u8,
    g: u8,
    b: u8,
    a: u8,
    tolerance: u8,
) {
    canvas_ref_mut(ptr, |c| c.flood_fill(layer_id, x, y, [r, g, b, a], tolerance));
}

#[no_mangle]
pub extern "C" fn pe_draw_rect(
    ptr: *mut Canvas,
    layer_id: u32,
    x1: i32,
    y1: i32,
    x2: i32,
    y2: i32,
    r: u8,
    g: u8,
    b: u8,
    a: u8,
    filled: bool,
) {
    canvas_ref_mut(ptr, |c| c.draw_rect(layer_id, x1, y1, x2, y2, [r, g, b, a], filled));
}

#[no_mangle]
pub extern "C" fn pe_draw_circle(
    ptr: *mut Canvas,
    layer_id: u32,
    x1: i32,
    y1: i32,
    x2: i32,
    y2: i32,
    r: u8,
    g: u8,
    b: u8,
    a: u8,
    filled: bool,
) {
    canvas_ref_mut(ptr, |c| c.draw_circle(layer_id, x1, y1, x2, y2, [r, g, b, a], filled));
}

#[no_mangle]
pub extern "C" fn pe_draw_line(
    ptr: *mut Canvas,
    layer_id: u32,
    x1: i32,
    y1: i32,
    x2: i32,
    y2: i32,
    r: u8,
    g: u8,
    b: u8,
    a: u8,
) {
    canvas_ref_mut(ptr, |c| c.draw_line(layer_id, x1, y1, x2, y2, [r, g, b, a]));
}

#[no_mangle]
pub extern "C" fn pe_base_brush(ptr: *mut Canvas, cx: i32, cy: i32, radius: u32) {
    canvas_ref_mut(ptr, |c| c.base_brush(cx, cy, radius));
}

#[no_mangle]
pub extern "C" fn pe_base_eraser(ptr: *mut Canvas, cx: i32, cy: i32, radius: u32) {
    canvas_ref_mut(ptr, |c| c.base_eraser(cx, cy, radius));
}

// ============================================================
// 合成・出力
// ============================================================

/// プレビュー用 RGBA バイト列（width*height*4）を返す。
/// 呼び出し元は pe_free_bytes で解放すること。
#[no_mangle]
pub extern "C" fn pe_composite_rgba(ptr: *const Canvas, out_len: *mut u32) -> *mut u8 {
    canvas_ref(ptr, |c| {
        let bytes = c.composite_rgba();
        if !out_len.is_null() {
            unsafe { *out_len = bytes.len() as u32 };
        }
        let mut v = bytes.into_boxed_slice();
        let p = v.as_mut_ptr();
        std::mem::forget(v);
        p
    })
    .unwrap_or(std::ptr::null_mut())
}

/// 保存用 PNG バイト列を返す（透明ピクセル正規化済み）。
/// 呼び出し元は pe_free_bytes で解放すること。
#[no_mangle]
pub extern "C" fn pe_composite_png(ptr: *const Canvas, out_len: *mut u32) -> *mut u8 {
    canvas_ref(ptr, |c| {
        let bytes = c.composite_for_save_png();
        if !out_len.is_null() {
            unsafe { *out_len = bytes.len() as u32 };
        }
        let mut v = bytes.into_boxed_slice();
        let p = v.as_mut_ptr();
        std::mem::forget(v);
        p
    })
    .unwrap_or(std::ptr::null_mut())
}

// ============================================================
// グループ管理
// ============================================================

/// グループを追加する。ID を返す。
#[no_mangle]
pub extern "C" fn pe_group_add(ptr: *mut Canvas, name: *const std::os::raw::c_char) -> u32 {
    if name.is_null() {
        return canvas_ref_mut(ptr, |c| c.add_group("Group"));
    }
    let s = unsafe { std::ffi::CStr::from_ptr(name).to_str().unwrap_or("Group") };
    canvas_ref_mut(ptr, |c| c.add_group(s))
}

#[no_mangle]
pub extern "C" fn pe_group_remove(ptr: *mut Canvas, group_id: u32) -> bool {
    canvas_ref_mut(ptr, |c| c.remove_group(group_id))
}

#[no_mangle]
pub extern "C" fn pe_group_set_visible(ptr: *mut Canvas, group_id: u32, visible: bool) -> bool {
    canvas_ref_mut(ptr, |c| c.set_group_visible(group_id, visible))
}

#[no_mangle]
pub extern "C" fn pe_group_set_expanded(ptr: *mut Canvas, group_id: u32, expanded: bool) -> bool {
    canvas_ref_mut(ptr, |c| c.set_group_expanded(group_id, expanded))
}

/// レイヤーをグループに追加する。group_id=0 でグループから外す。
#[no_mangle]
pub extern "C" fn pe_layer_set_group(ptr: *mut Canvas, layer_id: u32, group_id: u32) -> bool {
    let gid = if group_id == 0 { None } else { Some(group_id) };
    canvas_ref_mut(ptr, |c| c.set_layer_group(layer_id, gid))
}

// ============================================================
// 範囲選択
// ============================================================

/// 矩形選択を設定する。
#[no_mangle]
pub extern "C" fn pe_selection_set_rect(
    ptr: *mut Canvas,
    x1: i32,
    y1: i32,
    x2: i32,
    y2: i32,
) {
    canvas_ref_mut(ptr, |c| c.selection_set_rect(x1, y1, x2, y2));
}

/// 楕円選択を設定する。
#[no_mangle]
pub extern "C" fn pe_selection_set_ellipse(
    ptr: *mut Canvas,
    x1: i32,
    y1: i32,
    x2: i32,
    y2: i32,
) {
    canvas_ref_mut(ptr, |c| c.selection_set_ellipse(x1, y1, x2, y2));
}

/// 選択を解除する（全域選択に戻す）。
#[no_mangle]
pub extern "C" fn pe_selection_clear(ptr: *mut Canvas) {
    canvas_ref_mut(ptr, |c| c.selection_clear());
}

/// 選択を反転する。
#[no_mangle]
pub extern "C" fn pe_selection_invert(ptr: *mut Canvas) {
    canvas_ref_mut(ptr, |c| c.selection_invert());
}

/// 現在選択が有効かどうか（1=選択あり, 0=全域選択）。
#[no_mangle]
pub extern "C" fn pe_selection_has(ptr: *const Canvas) -> bool {
    canvas_ref(ptr, |c| c.has_selection()).unwrap_or(false)
}

// ============================================================
// レイヤー変形
// ============================================================

/// レイヤーを (dx, dy) 平行移動する。
#[no_mangle]
pub extern "C" fn pe_layer_translate(ptr: *mut Canvas, layer_id: u32, dx: i32, dy: i32) {
    canvas_ref_mut(ptr, |c| c.translate_layer(layer_id, dx, dy));
}

/// レイヤーを最近傍補間でスケーリングする（キャンバス中心基準）。
#[no_mangle]
pub extern "C" fn pe_layer_scale_nn(
    ptr: *mut Canvas,
    layer_id: u32,
    scale_x: f32,
    scale_y: f32,
) {
    canvas_ref_mut(ptr, |c| c.scale_layer_nn(layer_id, scale_x, scale_y));
}

// ============================================================
// クリーンアップ
// ============================================================

/// Undo 履歴をクリアしてメモリを解放する（保存後に呼び出す）。
#[no_mangle]
pub extern "C" fn pe_canvas_cleanup(ptr: *mut Canvas) {
    canvas_ref_mut(ptr, |c| c.cleanup_undo());
}

// ============================================================
// キャンバスリサイズ
// ============================================================

/// キャンバス全体を最近傍補間でリサイズする。Undo 履歴・選択範囲はクリアされる。
#[no_mangle]
pub extern "C" fn pe_canvas_resize(ptr: *mut Canvas, new_width: u32, new_height: u32) {
    canvas_ref_mut(ptr, |c| c.resize(new_width, new_height));
}

// ============================================================
// レイヤー取り込み
// ============================================================

/// PNG バイト列を指定レイヤーに読み込む。サイズ不一致時は最近傍でリサイズ。
/// 成功時 true、色調補正レイヤーや不正データ時は false。
#[no_mangle]
pub extern "C" fn pe_layer_import_png(
    ptr: *mut Canvas,
    layer_id: u32,
    data: *const u8,
    data_len: u32,
) -> bool {
    if data.is_null() {
        return false;
    }
    canvas_ref_mut(ptr, |c| {
        let slice = unsafe { std::slice::from_raw_parts(data, data_len as usize) };
        c.import_layer_png(layer_id, slice)
    })
}

// ============================================================
// UV オーバーレイ
// ============================================================

/// UV オーバーレイ RGBA データを設定する（width*height*4 バイト）。
/// データ長が不一致の場合は無視される。
#[no_mangle]
pub extern "C" fn pe_canvas_set_uv_overlay(ptr: *mut Canvas, data: *const u8, data_len: u32) {
    if data.is_null() {
        return;
    }
    canvas_ref_mut(ptr, |c| {
        let slice = unsafe { std::slice::from_raw_parts(data, data_len as usize) };
        c.set_uv_overlay(slice);
    });
}

/// UV オーバーレイの表示/非表示を設定する。
#[no_mangle]
pub extern "C" fn pe_uv_overlay_set_visible(ptr: *mut Canvas, visible: bool) {
    canvas_ref_mut(ptr, |c| c.set_uv_overlay_visible(visible));
}

/// pe_composite_rgba / pe_composite_png が返したバイト列を解放する。
#[no_mangle]
pub extern "C" fn pe_free_bytes(ptr: *mut u8, len: u32) {
    if ptr.is_null() {
        return;
    }
    unsafe {
        let _ = Vec::from_raw_parts(ptr, len as usize, len as usize);
    }
}

// ============================================================
// Undo / Redo
// ============================================================

#[no_mangle]
pub extern "C" fn pe_undo(ptr: *mut Canvas) -> bool {
    canvas_ref_mut(ptr, |c| c.undo())
}

#[no_mangle]
pub extern "C" fn pe_redo(ptr: *mut Canvas) -> bool {
    canvas_ref_mut(ptr, |c| c.redo())
}

#[no_mangle]
pub extern "C" fn pe_can_undo(ptr: *const Canvas) -> bool {
    canvas_ref(ptr, |c| c.undo.can_undo()).unwrap_or(false)
}

#[no_mangle]
pub extern "C" fn pe_can_redo(ptr: *const Canvas) -> bool {
    canvas_ref(ptr, |c| c.undo.can_redo()).unwrap_or(false)
}

// ============================================================
// 内部ユーティリティ
// ============================================================

fn canvas_ref_mut<T, F: FnOnce(&mut Canvas) -> T>(ptr: *mut Canvas, f: F) -> T
where
    T: Default,
{
    if ptr.is_null() {
        return T::default();
    }
    f(unsafe { &mut *ptr })
}

fn canvas_ref<T, F: FnOnce(&Canvas) -> T>(ptr: *const Canvas, f: F) -> Option<T> {
    if ptr.is_null() {
        return None;
    }
    Some(f(unsafe { &*ptr }))
}
