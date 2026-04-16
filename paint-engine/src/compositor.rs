use crate::layer::Layer;

/// 通常レイヤー群・色調補正レイヤー・ベースレイヤーを合成して RGBA バイト列を返す。
pub fn composite(
    width: u32,
    height: u32,
    layers: &[&Layer],
    base_layer: &Layer,
    color_adj: Option<&Layer>,
) -> Vec<u8> {
    let n = (width * height) as usize;
    let mut result = vec![0u8; n * 4];

    // 通常レイヤーを下から上へ合成
    for layer in layers {
        if !layer.visible {
            continue;
        }
        if layer.mask_below {
            // クリッピングマスク: 下の合成アルファでこのレイヤーをクリップ
            let clipped = clip_by_accumulated_alpha(&layer.pixels, &result);
            blend_over(&mut result, &clipped, layer.opacity);
        } else {
            blend_over(&mut result, &layer.pixels, layer.opacity);
        }
    }

    // 色調補正レイヤーを適用
    if let Some(adj) = color_adj {
        if adj.visible {
            apply_color_adjustment(
                &mut result,
                adj.brightness,
                adj.saturation,
                adj.contrast,
                adj.hue_shift,
            );
        }
    }

    // ベースレイヤーマスクを適用
    apply_base_mask(&mut result, &base_layer.pixels);

    result
}

/// 透明ピクセル正規化（保存時）:
/// α < 128 → α=0, RGB=(0,0,0)  /  α ≥ 128 → α=255, RGB 保持
pub fn process_for_save(pixels: &mut [u8]) {
    let n = pixels.len() / 4;
    for i in 0..n {
        let b = i * 4;
        let a = pixels[b + 3];
        if a < 128 {
            pixels[b] = 0;
            pixels[b + 1] = 0;
            pixels[b + 2] = 0;
            pixels[b + 3] = 0;
        } else {
            pixels[b + 3] = 255;
        }
    }
}

/// mask_below レイヤー用: src の各ピクセルのアルファを accumulated のアルファで乗算して返す。
fn clip_by_accumulated_alpha(src: &[u8], accumulated: &[u8]) -> Vec<u8> {
    let mut clipped = src.to_vec();
    let n = clipped.len() / 4;
    for i in 0..n {
        let b = i * 4;
        let mask_a = accumulated[b + 3] as f32 / 255.0;
        clipped[b + 3] = ((clipped[b + 3] as f32 / 255.0 * mask_a) * 255.0).round() as u8;
    }
    clipped
}

/// src を dst に Porter-Duff "over" でブレンドする。
pub fn blend_over(dst: &mut [u8], src: &[u8], src_opacity: f32) {
    let n = dst.len() / 4;
    for i in 0..n {
        let b = i * 4;
        let sa = (src[b + 3] as f32 / 255.0) * src_opacity.clamp(0.0, 1.0);
        if sa <= 0.0 {
            continue;
        }
        let da = dst[b + 3] as f32 / 255.0;
        let out_a = sa + da * (1.0 - sa);
        if out_a <= 0.0 {
            continue;
        }
        for c in 0..3 {
            let sc = src[b + c] as f32 / 255.0;
            let dc = dst[b + c] as f32 / 255.0;
            let oc = (sc * sa + dc * da * (1.0 - sa)) / out_a;
            dst[b + c] = (oc * 255.0).round().clamp(0.0, 255.0) as u8;
        }
        dst[b + 3] = (out_a * 255.0).round().clamp(0.0, 255.0) as u8;
    }
}

/// src レイヤーの内容を dst レイヤーピクセルに over ブレンドする（レイヤー結合用）。
pub fn blend_layer_onto(dst: &mut [u8], src: &[u8], src_opacity: f32) {
    blend_over(dst, src, src_opacity);
}

fn apply_base_mask(dst: &mut [u8], base: &[u8]) {
    let n = dst.len() / 4;
    for i in 0..n {
        let b = i * 4;
        if base[b + 3] == 0 {
            dst[b] = 0;
            dst[b + 1] = 0;
            dst[b + 2] = 0;
            dst[b + 3] = 0;
        }
    }
}

fn apply_color_adjustment(
    pixels: &mut [u8],
    brightness: f32,
    saturation: f32,
    contrast: f32,
    hue_shift: f32,
) {
    let n = pixels.len() / 4;
    for i in 0..n {
        let b = i * 4;
        if pixels[b + 3] == 0 {
            continue;
        }
        let r = pixels[b] as f32 / 255.0;
        let g = pixels[b + 1] as f32 / 255.0;
        let bl = pixels[b + 2] as f32 / 255.0;

        let (h, s, v) = rgb_to_hsv(r, g, bl);
        let h = (h + hue_shift / 360.0).rem_euclid(1.0);
        let s = (s + saturation).clamp(0.0, 1.0);
        let v = (v + brightness).clamp(0.0, 1.0);
        let (mut nr, mut ng, mut nb) = hsv_to_rgb(h, s, v);

        // コントラスト: c*(x - 0.5) + 0.5
        let cf = 1.0 + contrast;
        nr = ((nr - 0.5) * cf + 0.5).clamp(0.0, 1.0);
        ng = ((ng - 0.5) * cf + 0.5).clamp(0.0, 1.0);
        nb = ((nb - 0.5) * cf + 0.5).clamp(0.0, 1.0);

        pixels[b] = (nr * 255.0).round() as u8;
        pixels[b + 1] = (ng * 255.0).round() as u8;
        pixels[b + 2] = (nb * 255.0).round() as u8;
    }
}

fn rgb_to_hsv(r: f32, g: f32, b: f32) -> (f32, f32, f32) {
    let max = r.max(g).max(b);
    let min = r.min(g).min(b);
    let delta = max - min;
    let v = max;
    let s = if max > 0.0 { delta / max } else { 0.0 };
    let h = if delta < 1e-6 {
        0.0
    } else if (max - r).abs() < 1e-6 {
        ((g - b) / delta).rem_euclid(6.0) / 6.0
    } else if (max - g).abs() < 1e-6 {
        ((b - r) / delta + 2.0) / 6.0
    } else {
        ((r - g) / delta + 4.0) / 6.0
    };
    (h, s, v)
}

fn hsv_to_rgb(h: f32, s: f32, v: f32) -> (f32, f32, f32) {
    if s <= 0.0 {
        return (v, v, v);
    }
    let h6 = h * 6.0;
    let i = h6.floor() as u32 % 6;
    let f = h6 - h6.floor();
    let p = v * (1.0 - s);
    let q = v * (1.0 - s * f);
    let t = v * (1.0 - s * (1.0 - f));
    match i {
        0 => (v, t, p),
        1 => (q, v, p),
        2 => (p, v, t),
        3 => (p, q, v),
        4 => (t, p, v),
        _ => (v, p, q),
    }
}

/// RGBA バイト列を PNG に変換する。
pub fn encode_png(pixels: &[u8], width: u32, height: u32) -> Vec<u8> {
    use image::{ImageBuffer, Rgba};
    let img: ImageBuffer<Rgba<u8>, Vec<u8>> =
        ImageBuffer::from_raw(width, height, pixels.to_vec())
            .expect("encode_png: invalid dimensions");
    let mut buf = Vec::new();
    img.write_to(
        &mut std::io::Cursor::new(&mut buf),
        image::ImageFormat::Png,
    )
    .expect("encode_png: PNG encoding failed");
    buf
}
