use crate::compositor;
use crate::layer::{Layer, LayerGroup, LayerType};
use crate::tools;
use crate::undo::{PixelSnapshot, UndoStack};

/// 最近傍補間で RGBA ピクセル列をリサイズする。
fn scale_nearest(src: &[u8], sw: u32, sh: u32, dw: u32, dh: u32) -> Vec<u8> {
    let mut dst = vec![0u8; (dw * dh * 4) as usize];
    for dy in 0..dh {
        for dx in 0..dw {
            let sx = (dx * sw / dw).min(sw.saturating_sub(1));
            let sy = (dy * sh / dh).min(sh.saturating_sub(1));
            let si = ((sy * sw + sx) * 4) as usize;
            let di = ((dy * dw + dx) * 4) as usize;
            dst[di..di + 4].copy_from_slice(&src[si..si + 4]);
        }
    }
    dst
}

pub const MAX_NORMAL_LAYERS: usize = 16;

pub struct Canvas {
    pub width: u32,
    pub height: u32,
    /// 表示順: インデックス 0 が最下段。通常レイヤー + 色調補正レイヤーを混在して管理。
    pub layers: Vec<Layer>,
    pub base_layer: Layer,
    pub undo: UndoStack,
    /// グループ一覧
    pub groups: Vec<LayerGroup>,
    /// 範囲選択マスク: 0=選択外, 255=選択内。空ベクター=全域選択（選択なし状態）。
    pub selection: Vec<u8>,
    /// UV オーバーレイ RGBA データ（width*height*4）。None = 未設定。
    pub uv_overlay: Option<Vec<u8>>,
    /// UV オーバーレイの表示フラグ。
    pub uv_overlay_visible: bool,
    next_id: u32,
}

impl Canvas {
    pub fn new(width: u32, height: u32) -> Self {
        let base = Layer::new_base(0, width, height);
        let mut canvas = Self {
            width,
            height,
            layers: Vec::new(),
            base_layer: base,
            undo: UndoStack::new(),
            groups: Vec::new(),
            selection: Vec::new(),
            uv_overlay: None,
            uv_overlay_visible: true,
            next_id: 1,
        };
        canvas.add_normal_layer(); // デフォルトレイヤー
        canvas
    }

    fn alloc_id(&mut self) -> u32 {
        let id = self.next_id;
        self.next_id += 1;
        id
    }

    fn normal_layer_count(&self) -> usize {
        self.layers
            .iter()
            .filter(|l| l.layer_type == LayerType::Normal)
            .count()
    }

    // ---- レイヤー操作 ----

    /// 通常レイヤーを追加。16枚上限を超えた場合は 0 を返す。
    pub fn add_normal_layer(&mut self) -> u32 {
        if self.normal_layer_count() >= MAX_NORMAL_LAYERS {
            return 0;
        }
        let id = self.alloc_id();
        self.layers.push(Layer::new_normal(id, self.width, self.height));
        id
    }

    /// 色調補正レイヤーを追加。既存の場合は 0 を返す。
    pub fn add_color_adjustment_layer(&mut self) -> u32 {
        if self
            .layers
            .iter()
            .any(|l| l.layer_type == LayerType::ColorAdjustment)
        {
            return 0;
        }
        let id = self.alloc_id();
        self.layers.push(Layer::new_color_adjustment(id));
        id
    }

    pub fn remove_layer(&mut self, layer_id: u32) -> bool {
        if let Some(idx) = self.layers.iter().position(|l| l.id == layer_id) {
            self.layers.remove(idx);
            true
        } else {
            false
        }
    }

    pub fn duplicate_layer(&mut self, layer_id: u32) -> u32 {
        let idx = match self.layers.iter().position(|l| l.id == layer_id) {
            Some(i) => i,
            None => return 0,
        };
        if self.layers[idx].layer_type == LayerType::Normal
            && self.normal_layer_count() >= MAX_NORMAL_LAYERS
        {
            return 0;
        }
        let mut dup = self.layers[idx].clone();
        let new_id = self.alloc_id();
        dup.id = new_id;
        self.layers.insert(idx + 1, dup);
        new_id
    }

    pub fn move_layer(&mut self, layer_id: u32, new_index: usize) -> bool {
        let idx = match self.layers.iter().position(|l| l.id == layer_id) {
            Some(i) => i,
            None => return false,
        };
        let max = self.layers.len() - 1;
        let dst = new_index.min(max);
        if idx == dst {
            return true;
        }
        let layer = self.layers.remove(idx);
        self.layers.insert(dst, layer);
        true
    }

    /// 指定レイヤーを直下の通常レイヤーに結合する。成功時は下のレイヤーの id を返す。
    pub fn merge_layer_down(&mut self, layer_id: u32) -> u32 {
        let idx = match self.layers.iter().position(|l| l.id == layer_id) {
            Some(i) => i,
            None => return 0,
        };
        if idx == 0 {
            return 0;
        }
        if self.layers[idx].layer_type != LayerType::Normal
            || self.layers[idx - 1].layer_type != LayerType::Normal
        {
            return 0;
        }
        let upper_pixels = self.layers[idx].pixels.clone();
        let upper_opacity = self.layers[idx].opacity;
        let lower_id = self.layers[idx - 1].id;
        compositor::blend_layer_onto(
            &mut self.layers[idx - 1].pixels,
            &upper_pixels,
            upper_opacity,
        );
        self.layers.remove(idx);
        lower_id
    }

    pub fn set_layer_opacity(&mut self, layer_id: u32, opacity: f32) {
        if let Some(l) = self.find_layer_mut(layer_id) {
            l.opacity = opacity.clamp(0.0, 1.0);
        }
    }

    pub fn set_layer_visible(&mut self, layer_id: u32, visible: bool) {
        if let Some(l) = self.find_layer_mut(layer_id) {
            l.visible = visible;
        }
    }

    pub fn set_layer_locked(&mut self, layer_id: u32, locked: bool) {
        if let Some(l) = self.find_layer_mut(layer_id) {
            l.locked = locked;
        }
    }

    pub fn set_layer_mask_below(&mut self, layer_id: u32, mask_below: bool) {
        if let Some(l) = self.find_layer_mut(layer_id) {
            l.mask_below = mask_below;
        }
    }

    pub fn set_layer_pixels(&mut self, layer_id: u32, data: &[u8]) -> bool {
        if let Some(l) = self.find_layer_mut(layer_id) {
            if data.len() == l.pixels.len() {
                l.pixels.copy_from_slice(data);
                return true;
            }
        }
        false
    }

    pub fn get_layer_pixels(&self, layer_id: u32) -> Option<&[u8]> {
        self.find_layer(layer_id).map(|l| l.pixels.as_slice())
    }

    pub fn set_color_adjustment(
        &mut self,
        layer_id: u32,
        brightness: f32,
        saturation: f32,
        contrast: f32,
        hue_shift: f32,
    ) {
        if let Some(l) = self.find_layer_mut(layer_id) {
            if l.layer_type != LayerType::ColorAdjustment {
                return;
            }
            l.brightness = brightness;
            l.saturation = saturation;
            l.contrast = contrast;
            l.hue_shift = hue_shift;
        }
    }

    // ---- 合成 ----

    /// レイヤーのみを合成した RGBA バイト列（UV オーバーレイを含まない）。
    fn composite_layers_rgba(&self) -> Vec<u8> {
        let normal: Vec<&Layer> = self
            .layers
            .iter()
            .filter(|l| {
                if l.layer_type != LayerType::Normal {
                    return false;
                }
                // グループに所属している場合、グループが非表示なら除外
                if let Some(gid) = l.group_id {
                    if let Some(g) = self.groups.iter().find(|g| g.id == gid) {
                        if !g.visible {
                            return false;
                        }
                    }
                }
                true
            })
            .collect();
        let color_adj = self
            .layers
            .iter()
            .find(|l| l.layer_type == LayerType::ColorAdjustment);
        compositor::composite(self.width, self.height, &normal, &self.base_layer, color_adj)
    }

    /// プレビュー用 RGBA バイト列（width*height*4）を返す。UV オーバーレイを含む。
    pub fn composite_rgba(&self) -> Vec<u8> {
        let mut result = self.composite_layers_rgba();

        // UV オーバーレイを最上段に合成（プレビュー専用・保存時は除外）
        if let Some(ref uv) = self.uv_overlay {
            if self.uv_overlay_visible {
                compositor::blend_over(&mut result, uv, 1.0);
            }
        }

        result
    }

    /// UV オーバーレイ RGBA データを設定する（width*height*4 バイト）。
    pub fn set_uv_overlay(&mut self, data: &[u8]) {
        let expected = (self.width * self.height * 4) as usize;
        if data.len() == expected {
            self.uv_overlay = Some(data.to_vec());
            self.uv_overlay_visible = true;
        }
    }

    /// UV オーバーレイの表示/非表示を切り替える。
    pub fn set_uv_overlay_visible(&mut self, visible: bool) {
        self.uv_overlay_visible = visible;
    }

    /// 保存用 RGBA（透明ピクセル正規化済み）。UV オーバーレイは含まない。
    pub fn composite_for_save_rgba(&self) -> Vec<u8> {
        let mut pixels = self.composite_layers_rgba();
        compositor::process_for_save(&mut pixels);
        pixels
    }

    /// 保存用 PNG バイト列。
    pub fn composite_for_save_png(&self) -> Vec<u8> {
        let pixels = self.composite_for_save_rgba();
        compositor::encode_png(&pixels, self.width, self.height)
    }

    // ---- ペイントツール ----

    pub fn brush(
        &mut self,
        layer_id: u32,
        cx: i32,
        cy: i32,
        radius: u32,
        color: [u8; 4],
        antialiased: bool,
    ) {
        let idx = match self.layers.iter().position(|l| l.id == layer_id) {
            Some(i) => i,
            None => return,
        };
        if self.layers[idx].locked || self.layers[idx].layer_type != LayerType::Normal {
            return;
        }
        self.record_snapshot(idx);
        if self.selection.is_empty() {
            tools::brush_circle(&mut self.layers[idx], cx, cy, radius, color, antialiased);
        } else {
            let before = self.layers[idx].pixels.clone();
            tools::brush_circle(&mut self.layers[idx], cx, cy, radius, color, antialiased);
            self.apply_selection_mask(idx, &before);
        }
    }

    pub fn eraser(&mut self, layer_id: u32, cx: i32, cy: i32, radius: u32) {
        let idx = match self.layers.iter().position(|l| l.id == layer_id) {
            Some(i) => i,
            None => return,
        };
        if self.layers[idx].locked {
            return;
        }
        self.record_snapshot(idx);
        if self.selection.is_empty() {
            tools::eraser_circle(&mut self.layers[idx], cx, cy, radius);
        } else {
            let before = self.layers[idx].pixels.clone();
            tools::eraser_circle(&mut self.layers[idx], cx, cy, radius);
            self.apply_selection_mask(idx, &before);
        }
    }

    pub fn flood_fill(
        &mut self,
        layer_id: u32,
        x: i32,
        y: i32,
        color: [u8; 4],
        tolerance: u8,
    ) {
        let idx = match self.layers.iter().position(|l| l.id == layer_id) {
            Some(i) => i,
            None => return,
        };
        if self.layers[idx].locked || self.layers[idx].layer_type != LayerType::Normal {
            return;
        }
        // 塗りつぶし開始点が選択外なら何もしない
        if !self.is_selected(x, y) {
            return;
        }
        self.record_snapshot(idx);
        if self.selection.is_empty() {
            tools::flood_fill(&mut self.layers[idx], x, y, color, tolerance);
        } else {
            let before = self.layers[idx].pixels.clone();
            tools::flood_fill(&mut self.layers[idx], x, y, color, tolerance);
            self.apply_selection_mask(idx, &before);
        }
    }

    pub fn draw_rect(
        &mut self,
        layer_id: u32,
        x1: i32,
        y1: i32,
        x2: i32,
        y2: i32,
        color: [u8; 4],
        filled: bool,
    ) {
        let idx = match self.layers.iter().position(|l| l.id == layer_id) {
            Some(i) => i,
            None => return,
        };
        if self.layers[idx].locked || self.layers[idx].layer_type != LayerType::Normal {
            return;
        }
        self.record_snapshot(idx);
        if self.selection.is_empty() {
            tools::draw_rect(&mut self.layers[idx], x1, y1, x2, y2, color, filled);
        } else {
            let before = self.layers[idx].pixels.clone();
            tools::draw_rect(&mut self.layers[idx], x1, y1, x2, y2, color, filled);
            self.apply_selection_mask(idx, &before);
        }
    }

    pub fn draw_circle(
        &mut self,
        layer_id: u32,
        x1: i32,
        y1: i32,
        x2: i32,
        y2: i32,
        color: [u8; 4],
        filled: bool,
    ) {
        let idx = match self.layers.iter().position(|l| l.id == layer_id) {
            Some(i) => i,
            None => return,
        };
        if self.layers[idx].locked || self.layers[idx].layer_type != LayerType::Normal {
            return;
        }
        self.record_snapshot(idx);
        if self.selection.is_empty() {
            tools::draw_circle_shape(&mut self.layers[idx], x1, y1, x2, y2, color, filled);
        } else {
            let before = self.layers[idx].pixels.clone();
            tools::draw_circle_shape(&mut self.layers[idx], x1, y1, x2, y2, color, filled);
            self.apply_selection_mask(idx, &before);
        }
    }

    pub fn draw_line(
        &mut self,
        layer_id: u32,
        x1: i32,
        y1: i32,
        x2: i32,
        y2: i32,
        color: [u8; 4],
    ) {
        let idx = match self.layers.iter().position(|l| l.id == layer_id) {
            Some(i) => i,
            None => return,
        };
        if self.layers[idx].locked || self.layers[idx].layer_type != LayerType::Normal {
            return;
        }
        self.record_snapshot(idx);
        if self.selection.is_empty() {
            tools::draw_line(&mut self.layers[idx], x1, y1, x2, y2, color);
        } else {
            let before = self.layers[idx].pixels.clone();
            tools::draw_line(&mut self.layers[idx], x1, y1, x2, y2, color);
            self.apply_selection_mask(idx, &before);
        }
    }

    // ---- ベースレイヤー操作 ----

    pub fn base_brush(&mut self, cx: i32, cy: i32, radius: u32) {
        let r = radius as i32;
        for dy in -r..=r {
            for dx in -r..=r {
                if dx * dx + dy * dy > r * r {
                    continue;
                }
                self.base_layer
                    .set_pixel(cx + dx, cy + dy, [0, 0, 0, 255]);
            }
        }
    }

    pub fn base_eraser(&mut self, cx: i32, cy: i32, radius: u32) {
        let r = radius as i32;
        for dy in -r..=r {
            for dx in -r..=r {
                if dx * dx + dy * dy > r * r {
                    continue;
                }
                self.base_layer
                    .set_pixel(cx + dx, cy + dy, [0, 0, 0, 0]);
            }
        }
    }

    // ---- グループ管理 ----

    /// グループを追加する。ID を返す。
    pub fn add_group(&mut self, name: &str) -> u32 {
        let id = self.alloc_id();
        self.groups.push(LayerGroup::new(id, name));
        id
    }

    pub fn remove_group(&mut self, group_id: u32) -> bool {
        if let Some(idx) = self.groups.iter().position(|g| g.id == group_id) {
            // グループ解除
            for l in self.layers.iter_mut() {
                if l.group_id == Some(group_id) {
                    l.group_id = None;
                }
            }
            self.groups.remove(idx);
            true
        } else {
            false
        }
    }

    pub fn set_group_visible(&mut self, group_id: u32, visible: bool) -> bool {
        if let Some(g) = self.groups.iter_mut().find(|g| g.id == group_id) {
            g.visible = visible;
            true
        } else {
            false
        }
    }

    pub fn set_group_expanded(&mut self, group_id: u32, expanded: bool) -> bool {
        if let Some(g) = self.groups.iter_mut().find(|g| g.id == group_id) {
            g.expanded = expanded;
            true
        } else {
            false
        }
    }

    pub fn set_layer_group(&mut self, layer_id: u32, group_id: Option<u32>) -> bool {
        if let Some(l) = self.find_layer_mut(layer_id) {
            l.group_id = group_id;
            true
        } else {
            false
        }
    }

    // ---- 範囲選択 ----

    /// 範囲選択が有効かどうか（空 = 全域が選択対象）
    pub fn has_selection(&self) -> bool {
        !self.selection.is_empty()
    }

    pub fn selection_set_rect(&mut self, x1: i32, y1: i32, x2: i32, y2: i32) {
        let w = self.width as usize;
        let h = self.height as usize;
        self.selection = vec![0u8; w * h];
        let rx1 = x1.max(0) as usize;
        let ry1 = y1.max(0) as usize;
        let rx2 = (x2 as usize).min(w.saturating_sub(1));
        let ry2 = (y2 as usize).min(h.saturating_sub(1));
        for y in ry1..=ry2 {
            for x in rx1..=rx2 {
                self.selection[y * w + x] = 255;
            }
        }
    }

    pub fn selection_set_ellipse(&mut self, x1: i32, y1: i32, x2: i32, y2: i32) {
        let w = self.width as usize;
        let h = self.height as usize;
        self.selection = vec![0u8; w * h];
        let cx = (x1 + x2) as f32 / 2.0;
        let cy = (y1 + y2) as f32 / 2.0;
        let rx = ((x2 - x1).abs() as f32) / 2.0;
        let ry = ((y2 - y1).abs() as f32) / 2.0;
        if rx < 0.5 || ry < 0.5 {
            return;
        }
        for y in 0..h {
            for x in 0..w {
                let dx = (x as f32 - cx) / rx;
                let dy = (y as f32 - cy) / ry;
                if dx * dx + dy * dy <= 1.0 {
                    self.selection[y * w + x] = 255;
                }
            }
        }
    }

    pub fn selection_clear(&mut self) {
        self.selection.clear();
    }

    pub fn selection_invert(&mut self) {
        let w = self.width as usize;
        let h = self.height as usize;
        if self.selection.is_empty() {
            self.selection = vec![255u8; w * h];
        } else {
            for v in self.selection.iter_mut() {
                *v = if *v > 0 { 0 } else { 255 };
            }
        }
    }

    /// ピクセル (x, y) が選択範囲内かどうか。選択なし(空)なら常に true。
    #[inline]
    pub fn is_selected(&self, x: i32, y: i32) -> bool {
        if self.selection.is_empty() {
            return true;
        }
        if x < 0 || y < 0 || x >= self.width as i32 || y >= self.height as i32 {
            return false;
        }
        self.selection[(y as usize) * (self.width as usize) + (x as usize)] > 0
    }

    // ---- レイヤー変形 ----

    /// レイヤーピクセルを (dx, dy) だけ平行移動する（はみ出した部分は破棄、空白は透明で埋める）。
    pub fn translate_layer(&mut self, layer_id: u32, dx: i32, dy: i32) {
        let idx = match self.layers.iter().position(|l| l.id == layer_id) {
            Some(i) => i,
            None => return,
        };
        if self.layers[idx].layer_type != LayerType::Normal {
            return;
        }
        self.record_snapshot(idx);
        let w = self.width as i32;
        let h = self.height as i32;
        let src = self.layers[idx].pixels.clone();
        let dst = &mut self.layers[idx].pixels;
        let stride = (w * 4) as usize;
        dst.iter_mut().for_each(|v| *v = 0);
        for sy in 0..h {
            let ty = sy + dy;
            if ty < 0 || ty >= h {
                continue;
            }
            for sx in 0..w {
                let tx = sx + dx;
                if tx < 0 || tx >= w {
                    continue;
                }
                let si = (sy * w + sx) as usize * 4;
                let ti = (ty * w + tx) as usize * 4;
                dst[ti..ti + 4].copy_from_slice(&src[si..si + 4]);
            }
        }
        let _ = stride;
    }

    /// レイヤーピクセルを最近傍補間でスケーリングする（キャンバス中心を基準）。
    /// scale_x / scale_y は正の浮動小数点数。
    pub fn scale_layer_nn(&mut self, layer_id: u32, scale_x: f32, scale_y: f32) {
        let idx = match self.layers.iter().position(|l| l.id == layer_id) {
            Some(i) => i,
            None => return,
        };
        if self.layers[idx].layer_type != LayerType::Normal {
            return;
        }
        if scale_x <= 0.0 || scale_y <= 0.0 {
            return;
        }
        self.record_snapshot(idx);
        let w = self.width as i32;
        let h = self.height as i32;
        let src = self.layers[idx].pixels.clone();
        let dst = &mut self.layers[idx].pixels;
        dst.iter_mut().for_each(|v| *v = 0);
        let cx = w as f32 / 2.0;
        let cy = h as f32 / 2.0;
        for ty in 0..h {
            for tx in 0..w {
                // 逆変換: dst(tx,ty) <- src(sx,sy)
                let sx = ((tx as f32 - cx) / scale_x + cx).round() as i32;
                let sy = ((ty as f32 - cy) / scale_y + cy).round() as i32;
                if sx < 0 || sy < 0 || sx >= w || sy >= h {
                    continue;
                }
                let si = (sy * w + sx) as usize * 4;
                let ti = (ty * w + tx) as usize * 4;
                dst[ti..ti + 4].copy_from_slice(&src[si..si + 4]);
            }
        }
    }

    // ---- クリーンアップ ----

    /// Undo 履歴をクリアしてメモリを解放する（保存後などに呼び出す）。
    pub fn cleanup_undo(&mut self) {
        self.undo.clear();
    }

    // ---- キャンバスリサイズ ----

    /// キャンバス全体を最近傍補間でリサイズする。
    /// Undo 履歴・選択範囲はクリアされる。
    pub fn resize(&mut self, new_width: u32, new_height: u32) {
        if new_width == 0 || new_height == 0 || (new_width == self.width && new_height == self.height) {
            return;
        }
        for layer in &mut self.layers {
            if layer.layer_type != LayerType::Normal {
                continue;
            }
            layer.pixels = scale_nearest(&layer.pixels, layer.width, layer.height, new_width, new_height);
            layer.width = new_width;
            layer.height = new_height;
        }
        self.base_layer.pixels =
            scale_nearest(&self.base_layer.pixels, self.base_layer.width, self.base_layer.height, new_width, new_height);
        self.base_layer.width = new_width;
        self.base_layer.height = new_height;
        self.width = new_width;
        self.height = new_height;
        self.undo.clear();
        self.selection.clear();
    }

    // ---- レイヤー取り込み ----

    /// PNG バイト列を指定レイヤーに読み込む。
    /// キャンバスサイズと異なる場合は最近傍でリサイズする。
    /// 色調補正レイヤーへの書き込みは拒否（false を返す）。
    pub fn import_layer_png(&mut self, layer_id: u32, png_data: &[u8]) -> bool {
        let img = match image::load_from_memory(png_data) {
            Ok(i) => i,
            Err(_) => return false,
        };
        let rgba = img.to_rgba8();
        let pixels = if rgba.width() != self.width || rgba.height() != self.height {
            scale_nearest(rgba.as_raw(), rgba.width(), rgba.height(), self.width, self.height)
        } else {
            rgba.into_raw()
        };
        let idx = match self.layers.iter().position(|l| l.id == layer_id) {
            Some(i) if self.layers[i].layer_type == LayerType::Normal => i,
            _ => return false,
        };
        self.record_snapshot(idx);
        self.layers[idx].pixels = pixels;
        true
    }

    // ---- Undo / Redo ----

    pub fn undo(&mut self) -> bool {
        let layer_id = match self.undo.peek_undo_id() {
            Some(id) => id,
            None => return false,
        };
        if let Some(current_pixels) = self
            .layers
            .iter()
            .find(|l| l.id == layer_id)
            .map(|l| l.pixels.clone())
        {
            let current_snap = PixelSnapshot {
                layer_id,
                pixels: current_pixels,
            };
            if let Some(prev) = self.undo.pop_undo(current_snap) {
                if let Some(l) = self.find_layer_mut(prev.layer_id) {
                    l.pixels = prev.pixels;
                }
                return true;
            }
        }
        false
    }

    pub fn redo(&mut self) -> bool {
        let layer_id = match self.undo.peek_redo_id() {
            Some(id) => id,
            None => return false,
        };
        if let Some(current_pixels) = self
            .layers
            .iter()
            .find(|l| l.id == layer_id)
            .map(|l| l.pixels.clone())
        {
            let current_snap = PixelSnapshot {
                layer_id,
                pixels: current_pixels,
            };
            if let Some(next) = self.undo.pop_redo(current_snap) {
                if let Some(l) = self.find_layer_mut(next.layer_id) {
                    l.pixels = next.pixels;
                }
                return true;
            }
        }
        false
    }

    // ---- 内部ヘルパー ----

    /// ペイント操作前に指定インデックスのレイヤーのスナップショットを undo スタックに記録する。
    fn record_snapshot(&mut self, idx: usize) {
        let snap = PixelSnapshot {
            layer_id: self.layers[idx].id,
            pixels: self.layers[idx].pixels.clone(),
        };
        self.undo.record(snap);
    }

    pub fn find_layer(&self, id: u32) -> Option<&Layer> {
        self.layers.iter().find(|l| l.id == id)
    }

    pub fn find_layer_mut(&mut self, id: u32) -> Option<&mut Layer> {
        self.layers.iter_mut().find(|l| l.id == id)
    }

    /// 選択範囲外のピクセルを before スナップショットの値に戻す。
    fn apply_selection_mask(&mut self, idx: usize, before: &[u8]) {
        let w = self.width as usize;
        let h = self.height as usize;
        let pixels = &mut self.layers[idx].pixels;
        for y in 0..h {
            for x in 0..w {
                if self.selection[y * w + x] == 0 {
                    let i = (y * w + x) * 4;
                    pixels[i..i + 4].copy_from_slice(&before[i..i + 4]);
                }
            }
        }
    }
}
