// undo の記録は canvas.rs 側で行う。tools.rs はピクセル操作のみ担当する。

use crate::layer::Layer;
use std::collections::VecDeque;

// ---- 内部: ピクセルブレンド ----

fn blend_pixel(layer: &mut Layer, x: i32, y: i32, src: [u8; 4]) {
    if x < 0 || y < 0 || x >= layer.width as i32 || y >= layer.height as i32 {
        return;
    }
    let dst = layer.get_pixel(x, y);
    let sa = src[3] as f32 / 255.0;
    if sa <= 0.0 {
        return;
    }
    let da = dst[3] as f32 / 255.0;
    let out_a = sa + da * (1.0 - sa);
    if out_a <= 0.0 {
        layer.set_pixel(x, y, [0, 0, 0, 0]);
        return;
    }
    let r = blend_ch(src[0], dst[0], sa, da, out_a);
    let g = blend_ch(src[1], dst[1], sa, da, out_a);
    let b = blend_ch(src[2], dst[2], sa, da, out_a);
    layer.set_pixel(x, y, [r, g, b, (out_a * 255.0).round() as u8]);
}

fn blend_ch(sc: u8, dc: u8, sa: f32, da: f32, out_a: f32) -> u8 {
    let sc = sc as f32 / 255.0;
    let dc = dc as f32 / 255.0;
    ((sc * sa + dc * da * (1.0 - sa)) / out_a * 255.0)
        .round()
        .clamp(0.0, 255.0) as u8
}

// ---- ブラシ ----

pub fn brush_circle(
    layer: &mut Layer,
    cx: i32,
    cy: i32,
    radius: u32,
    color: [u8; 4],
    antialiased: bool,
) {
    let r = radius as i32;
    for dy in -r..=r {
        for dx in -r..=r {
            let d2 = (dx * dx + dy * dy) as f32;
            if d2 > (r * r) as f32 {
                continue;
            }
            let x = cx + dx;
            let y = cy + dy;
            if antialiased {
                let dist = d2.sqrt();
                let edge = (r as f32) - 1.0;
                let alpha = if dist <= edge {
                    1.0f32
                } else {
                    (1.0 - (dist - edge)).max(0.0)
                };
                let a = (color[3] as f32 * alpha).round() as u8;
                blend_pixel(layer, x, y, [color[0], color[1], color[2], a]);
            } else {
                blend_pixel(layer, x, y, color);
            }
        }
    }
}

// ---- 消しゴム ----

pub fn eraser_circle(layer: &mut Layer, cx: i32, cy: i32, radius: u32) {
    let r = radius as i32;
    for dy in -r..=r {
        for dx in -r..=r {
            if dx * dx + dy * dy > r * r {
                continue;
            }
            layer.set_pixel(cx + dx, cy + dy, [0, 0, 0, 0]);
        }
    }
}

// ---- 塗りつぶし ----

pub fn flood_fill(
    layer: &mut Layer,
    start_x: i32,
    start_y: i32,
    fill_color: [u8; 4],
    tolerance: u8,
) {
    if start_x < 0
        || start_y < 0
        || start_x >= layer.width as i32
        || start_y >= layer.height as i32
    {
        return;
    }
    let target = layer.get_pixel(start_x, start_y);
    if colors_match(target, fill_color, tolerance) {
        return;
    }
    let w = layer.width as i32;
    let h = layer.height as i32;
    let mut visited = vec![false; (w * h) as usize];
    let mut queue = VecDeque::new();
    queue.push_back((start_x, start_y));
    visited[(start_y * w + start_x) as usize] = true;
    while let Some((x, y)) = queue.pop_front() {
        if !colors_match(layer.get_pixel(x, y), target, tolerance) {
            continue;
        }
        layer.set_pixel(x, y, fill_color);
        for (nx, ny) in [(x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)] {
            if nx >= 0 && ny >= 0 && nx < w && ny < h {
                let idx = (ny * w + nx) as usize;
                if !visited[idx] {
                    visited[idx] = true;
                    queue.push_back((nx, ny));
                }
            }
        }
    }
}

fn colors_match(a: [u8; 4], b: [u8; 4], tolerance: u8) -> bool {
    let t = tolerance as i32;
    (a[0] as i32 - b[0] as i32).abs() <= t
        && (a[1] as i32 - b[1] as i32).abs() <= t
        && (a[2] as i32 - b[2] as i32).abs() <= t
        && (a[3] as i32 - b[3] as i32).abs() <= t
}

// ---- 四角形 ----

pub fn draw_rect(
    layer: &mut Layer,
    x1: i32,
    y1: i32,
    x2: i32,
    y2: i32,
    color: [u8; 4],
    filled: bool,
) {
    let (xl, xr) = (x1.min(x2), x1.max(x2));
    let (yt, yb) = (y1.min(y2), y1.max(y2));
    if filled {
        for y in yt..=yb {
            for x in xl..=xr {
                blend_pixel(layer, x, y, color);
            }
        }
    } else {
        for x in xl..=xr {
            blend_pixel(layer, x, yt, color);
            blend_pixel(layer, x, yb, color);
        }
        for y in (yt + 1)..yb {
            blend_pixel(layer, xl, y, color);
            blend_pixel(layer, xr, y, color);
        }
    }
}

// ---- 円 ----

pub fn draw_circle_shape(
    layer: &mut Layer,
    x1: i32,
    y1: i32,
    x2: i32,
    y2: i32,
    color: [u8; 4],
    filled: bool,
) {
    let cx = (x1 + x2) / 2;
    let cy = (y1 + y2) / 2;
    let rx = ((x2 - x1).abs() as f32 / 2.0).max(1.0);
    let ry = ((y2 - y1).abs() as f32 / 2.0).max(1.0);
    let (xl, xr) = (x1.min(x2), x1.max(x2));
    let (yt, yb) = (y1.min(y2), y1.max(y2));
    for y in yt..=yb {
        for x in xl..=xr {
            let nx = (x - cx) as f32 / rx;
            let ny = (y - cy) as f32 / ry;
            let d = nx * nx + ny * ny;
            if filled {
                if d <= 1.0 {
                    blend_pixel(layer, x, y, color);
                }
            } else {
                let inner = (1.0 - 1.5 / rx.min(ry)).max(0.0).powi(2);
                if d <= 1.0 && d >= inner {
                    blend_pixel(layer, x, y, color);
                }
            }
        }
    }
}

// ---- 直線（Bresenham）----

pub fn draw_line(
    layer: &mut Layer,
    x1: i32,
    y1: i32,
    x2: i32,
    y2: i32,
    color: [u8; 4],
) {
    let dx = (x2 - x1).abs();
    let dy = -(y2 - y1).abs();
    let sx = if x1 < x2 { 1i32 } else { -1 };
    let sy = if y1 < y2 { 1i32 } else { -1 };
    let mut err = dx + dy;
    let (mut x, mut y) = (x1, y1);
    loop {
        blend_pixel(layer, x, y, color);
        if x == x2 && y == y2 {
            break;
        }
        let e2 = 2 * err;
        if e2 >= dy {
            err += dy;
            x += sx;
        }
        if e2 <= dx {
            err += dx;
            y += sy;
        }
    }
}
