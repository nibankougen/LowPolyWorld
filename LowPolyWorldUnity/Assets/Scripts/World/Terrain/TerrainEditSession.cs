using System;

/// <summary>
/// 地形タブのポインタ操作（タップ / スライド）を編集モードごとの TerrainEditLogic 呼び出しに
/// 変換するロジック（screens-and-modes.md 11.7.2）。純粋 C#・Unity 非依存。
///
/// - Brush / Eraser: Down とセルが変わるたびに適用
/// - Shape（四角形）: Down〜Drag で範囲をプレビューし Up で一括配置
/// - TypeChange: 触れたセルごとにサイクル変更。1 つも変更できなければ Up でフラッシュ
/// - RangeSelect（四角形）: スライドで範囲追加・選択範囲外のタップで解除
/// - RangeSelect（塗りつぶし）: タップ地点から同種連結を選択追加
/// - Move: ドラッグ中は移動先プレビューのみ表示し、指を離した Up で合計差分を一度だけ移動する
///   （ドラッグの経路上を都度上書き・削除して地形が壊れるのを防ぐ）
/// </summary>
public class TerrainEditSession
{
    /// <summary>操作の結果（呼び出し側がメッシュ再構築・オーバーレイ更新・フラッシュ表示を行う）。</summary>
    public readonly struct EditResult
    {
        public readonly bool TerrainChanged;
        public readonly bool SelectionChanged;
        public readonly string FlashMessage; // null = なし

        public EditResult(bool terrainChanged, bool selectionChanged, string flashMessage = null)
        {
            TerrainChanged = terrainChanged;
            SelectionChanged = selectionChanged;
            FlashMessage = flashMessage;
        }

        public static readonly EditResult None = new EditResult(false, false);
    }

    private const string TypeChangeFlash = "角になる地形をタッチしてください";

    private readonly TerrainEditLogic _edit;

    private bool _dragging;
    private bool _movedAcrossCells; // Down 後に別セルへスライドしたか
    private int _startX, _startZ;
    private int _lastX, _lastZ;
    private bool _anyTypeChanged;

    public TerrainEditSession(TerrainEditLogic edit)
    {
        _edit = edit ?? throw new ArgumentNullException(nameof(edit));
    }

    public TerrainEditMode Mode { get; set; } = TerrainEditMode.Brush;

    /// <summary>範囲選択の選択方法（false = 四角形 / true = 塗りつぶし）。</summary>
    public bool FloodSelect { get; set; }

    /// <summary>ブラシ・図形で配置する地形のパレットインデックス。</summary>
    public int SelectedPalette { get; set; }

    public bool IsDragging => _dragging;

    /// <summary>
    /// 追加系のモードか（ブラシ・図形）。隣接セルも上面で反応させてよいかの判定に使う。
    /// </summary>
    public bool IsAdditiveMode => Mode == TerrainEditMode.Brush || Mode == TerrainEditMode.Shape;

    /// <summary>
    /// 図形・範囲選択（四角形）のドラッグ中プレビュー矩形。プレビュー表示が不要なら false。
    /// </summary>
    public bool TryGetDragRect(out int x0, out int z0, out int x1, out int z1)
    {
        x0 = Math.Min(_startX, _lastX);
        x1 = Math.Max(_startX, _lastX);
        z0 = Math.Min(_startZ, _lastZ);
        z1 = Math.Max(_startZ, _lastZ);
        return _dragging
            && (Mode == TerrainEditMode.Shape || (Mode == TerrainEditMode.RangeSelect && !FloodSelect));
    }

    /// <summary>
    /// 移動モードのドラッグ中の「移動先プレビュー」用オフセット（Down からの合計差分）。
    /// 実際の移動は OnPointerUp で確定する。プレビュー不要なら false。
    /// </summary>
    public bool TryGetMovePreview(out int dx, out int dz)
    {
        dx = _lastX - _startX;
        dz = _lastZ - _startZ;
        return _dragging && Mode == TerrainEditMode.Move && (dx != 0 || dz != 0);
    }

    public EditResult OnPointerDown(int x, int z)
    {
        _dragging = true;
        _movedAcrossCells = false;
        _startX = _lastX = x;
        _startZ = _lastZ = z;
        _anyTypeChanged = false;

        switch (Mode)
        {
            case TerrainEditMode.Brush:
                return new EditResult(_edit.PaintCell(x, z, SelectedPalette), false);
            case TerrainEditMode.Eraser:
                return new EditResult(_edit.EraseCell(x, z), false);
            case TerrainEditMode.TypeChange:
                _anyTypeChanged = _edit.CycleType(x, z) == TerrainEditLogic.TypeChangeResult.Changed;
                return new EditResult(_anyTypeChanged, false);
            default:
                return EditResult.None; // Shape / RangeSelect / Move は Drag・Up で処理
        }
    }

    public EditResult OnPointerDrag(int x, int z)
    {
        if (!_dragging || (x == _lastX && z == _lastZ))
            return EditResult.None;

        _lastX = x;
        _lastZ = z;
        _movedAcrossCells = true;

        switch (Mode)
        {
            case TerrainEditMode.Brush:
                return new EditResult(_edit.PaintCell(x, z, SelectedPalette), false);
            case TerrainEditMode.Eraser:
                return new EditResult(_edit.EraseCell(x, z), false);
            case TerrainEditMode.TypeChange:
                bool changed = _edit.CycleType(x, z) == TerrainEditLogic.TypeChangeResult.Changed;
                _anyTypeChanged |= changed;
                return new EditResult(changed, false);
            default:
                // Shape / RangeSelect / Move はプレビューのみ（確定は OnPointerUp）
                return EditResult.None;
        }
    }

    public EditResult OnPointerUp()
    {
        if (!_dragging)
            return EditResult.None;
        _dragging = false;

        switch (Mode)
        {
            case TerrainEditMode.Shape:
                int filled = _edit.FillRect(_startX, _startZ, _lastX, _lastZ, SelectedPalette);
                return new EditResult(filled > 0, false);

            case TerrainEditMode.TypeChange:
                // タッチした範囲に変更できるブロックが 1 つもない場合のフラッシュ
                return _anyTypeChanged ? EditResult.None : new EditResult(false, false, TypeChangeFlash);

            case TerrainEditMode.RangeSelect:
                return FinishRangeSelect();

            case TerrainEditMode.Move:
            {
                // 指を離したタイミングで Down からの合計差分を一度だけ移動して確定する
                int dx = _lastX - _startX;
                int dz = _lastZ - _startZ;
                bool moved = (dx != 0 || dz != 0) && _edit.Move(dx, dz);
                return new EditResult(moved, moved);
            }

            default:
                return EditResult.None;
        }
    }

    private EditResult FinishRangeSelect()
    {
        if (FloodSelect)
        {
            // 塗りつぶし: タッチ地点から同種連結を選択に追加。地形がない場所のタップは選択解除
            if (_edit.SelectFlood(_startX, _startZ, addToSelection: true))
                return new EditResult(false, true);
            return ClearIfTappedOutside();
        }

        if (_movedAcrossCells)
        {
            // スライド → 範囲を選択に追加（複数範囲）
            _edit.SelectRect(_startX, _startZ, _lastX, _lastZ, addToSelection: true);
            return new EditResult(false, true);
        }
        return ClearIfTappedOutside();
    }

    private EditResult ClearIfTappedOutside()
    {
        if (_edit.HasSelection && !_edit.IsSelected(_startX, _startZ))
        {
            _edit.ClearSelection();
            return new EditResult(false, true);
        }
        return EditResult.None;
    }
}
