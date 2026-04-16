pub const MAX_UNDO_STEPS: usize = 50;

/// 1ステップ分のレイヤーピクセルスナップショット
pub struct PixelSnapshot {
    pub layer_id: u32,
    pub pixels: Vec<u8>,
}

pub struct UndoStack {
    undo: Vec<PixelSnapshot>,
    redo: Vec<PixelSnapshot>,
}

impl UndoStack {
    pub fn new() -> Self {
        Self {
            undo: Vec::new(),
            redo: Vec::new(),
        }
    }

    /// 通常操作の前に呼ぶ。redo をクリアし undo に積む。
    pub fn record(&mut self, snapshot: PixelSnapshot) {
        self.redo.clear();
        if self.undo.len() >= MAX_UNDO_STEPS {
            self.undo.remove(0);
        }
        self.undo.push(snapshot);
    }

    /// undo 操作: undo スタックから pop し、current を redo に積む。
    pub fn pop_undo(&mut self, current: PixelSnapshot) -> Option<PixelSnapshot> {
        let prev = self.undo.pop()?;
        self.redo.push(current);
        Some(prev)
    }

    /// redo 操作: redo スタックから pop し、current を undo に積む（redo をクリアしない）。
    pub fn pop_redo(&mut self, current: PixelSnapshot) -> Option<PixelSnapshot> {
        let next = self.redo.pop()?;
        if self.undo.len() >= MAX_UNDO_STEPS {
            self.undo.remove(0);
        }
        self.undo.push(current);
        Some(next)
    }

    pub fn can_undo(&self) -> bool {
        !self.undo.is_empty()
    }

    pub fn can_redo(&self) -> bool {
        !self.redo.is_empty()
    }

    pub fn peek_undo_id(&self) -> Option<u32> {
        self.undo.last().map(|s| s.layer_id)
    }

    pub fn peek_redo_id(&self) -> Option<u32> {
        self.redo.last().map(|s| s.layer_id)
    }

    /// Undo/Redo 履歴を全クリアする（保存後にメモリ解放用途で使用）。
    pub fn clear(&mut self) {
        self.undo.clear();
        self.redo.clear();
    }
}
