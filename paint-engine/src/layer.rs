/// レイヤー種別
#[repr(u8)]
#[derive(Clone, Copy, PartialEq, Eq, Debug)]
pub enum LayerType {
    Normal = 0,
    ColorAdjustment = 1,
    Base = 2,
}

/// レイヤーグループ（複数通常レイヤーをまとめて表示管理する）
#[derive(Clone)]
pub struct LayerGroup {
    pub id: u32,
    pub name: String,
    pub visible: bool,
    pub expanded: bool,
}

impl LayerGroup {
    pub fn new(id: u32, name: impl Into<String>) -> Self {
        Self { id, name: name.into(), visible: true, expanded: true }
    }
}

/// 1枚のレイヤー（ピクセルデータ + メタデータ）
#[derive(Clone)]
pub struct Layer {
    pub id: u32,
    pub layer_type: LayerType,
    /// RGBA 各チャンネル 8bit、行優先。ColorAdjustment は空。
    pub pixels: Vec<u8>,
    pub width: u32,
    pub height: u32,
    pub opacity: f32,
    pub visible: bool,
    pub locked: bool,
    pub mask_below: bool,
    /// このレイヤーが属するグループ ID（None = グループなし）
    pub group_id: Option<u32>,
    // ColorAdjustment パラメータ（他種別では未使用）
    pub brightness: f32, // -1.0 〜 1.0
    pub saturation: f32, // -1.0 〜 1.0
    pub contrast: f32,   // -1.0 〜 1.0
    pub hue_shift: f32,  // -180 〜 180 (度)
}

impl Layer {
    pub fn new_normal(id: u32, width: u32, height: u32) -> Self {
        Self {
            id,
            layer_type: LayerType::Normal,
            pixels: vec![0u8; (width * height * 4) as usize],
            width,
            height,
            opacity: 1.0,
            visible: true,
            locked: false,
            mask_below: false,
            group_id: None,
            brightness: 0.0,
            saturation: 0.0,
            contrast: 0.0,
            hue_shift: 0.0,
        }
    }

    /// ベースレイヤー: デフォルト全面不透明
    pub fn new_base(id: u32, width: u32, height: u32) -> Self {
        let pixel_count = (width * height) as usize;
        let mut pixels = vec![255u8; pixel_count * 4];
        // R=G=B=0 (黒), A=255
        for i in 0..pixel_count {
            pixels[i * 4] = 0;
            pixels[i * 4 + 1] = 0;
            pixels[i * 4 + 2] = 0;
            pixels[i * 4 + 3] = 255;
        }
        Self {
            id,
            layer_type: LayerType::Base,
            pixels,
            width,
            height,
            opacity: 1.0,
            visible: true,
            locked: false,
            mask_below: false,
            group_id: None,
            brightness: 0.0,
            saturation: 0.0,
            contrast: 0.0,
            hue_shift: 0.0,
        }
    }

    pub fn new_color_adjustment(id: u32) -> Self {
        Self {
            id,
            layer_type: LayerType::ColorAdjustment,
            pixels: Vec::new(),
            width: 0,
            height: 0,
            opacity: 1.0,
            visible: true,
            locked: false,
            mask_below: false,
            group_id: None,
            brightness: 0.0,
            saturation: 0.0,
            contrast: 0.0,
            hue_shift: 0.0,
        }
    }

    pub fn get_pixel(&self, x: i32, y: i32) -> [u8; 4] {
        if x < 0 || y < 0 || x >= self.width as i32 || y >= self.height as i32 {
            return [0, 0, 0, 0];
        }
        let i = ((y as u32 * self.width + x as u32) * 4) as usize;
        [self.pixels[i], self.pixels[i + 1], self.pixels[i + 2], self.pixels[i + 3]]
    }

    pub fn set_pixel(&mut self, x: i32, y: i32, rgba: [u8; 4]) {
        if x < 0 || y < 0 || x >= self.width as i32 || y >= self.height as i32 {
            return;
        }
        let i = ((y as u32 * self.width + x as u32) * 4) as usize;
        self.pixels[i] = rgba[0];
        self.pixels[i + 1] = rgba[1];
        self.pixels[i + 2] = rgba[2];
        self.pixels[i + 3] = rgba[3];
    }
}
