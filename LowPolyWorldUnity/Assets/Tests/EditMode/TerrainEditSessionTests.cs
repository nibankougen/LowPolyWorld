using NUnit.Framework;

public class TerrainEditSessionTests
{
    private TerrainVoxelStore _store;
    private TerrainEditLogic _edit;
    private TerrainEditSession _session;

    [SetUp]
    public void SetUp()
    {
        _store = new TerrainVoxelStore();
        _edit = new TerrainEditLogic(_store);
        _edit.SetHeight(5);
        _session = new TerrainEditSession(_edit) { SelectedPalette = 2 };
    }

    [Test]
    public void Brush_DownAndDrag_PaintsEachNewCell()
    {
        _session.Mode = TerrainEditMode.Brush;
        var down = _session.OnPointerDown(10, 10);
        Assert.IsTrue(down.TerrainChanged);

        Assert.IsFalse(_session.OnPointerDrag(10, 10).TerrainChanged, "同一セルでは何もしない");
        Assert.IsTrue(_session.OnPointerDrag(11, 10).TerrainChanged);
        _session.OnPointerUp();

        Assert.IsFalse(TerrainVoxel.IsEmpty(_store.GetVoxel(10, 5, 10)));
        Assert.IsFalse(TerrainVoxel.IsEmpty(_store.GetVoxel(11, 5, 10)));
        Assert.AreEqual(2, TerrainVoxel.GetPaletteIndex(_store.GetVoxel(10, 5, 10)), "選択中パレットで配置");
    }

    [Test]
    public void Eraser_RemovesCells()
    {
        _edit.PaintCell(10, 10, 0);
        _session.Mode = TerrainEditMode.Eraser;
        _session.OnPointerDown(10, 10);
        _session.OnPointerUp();
        Assert.IsTrue(TerrainVoxel.IsEmpty(_store.GetVoxel(10, 5, 10)));
    }

    [Test]
    public void Shape_FillsRectOnPointerUp()
    {
        _session.Mode = TerrainEditMode.Shape;
        Assert.IsFalse(_session.OnPointerDown(10, 10).TerrainChanged, "Down では配置しない");
        _session.OnPointerDrag(12, 11);

        Assert.IsTrue(_session.TryGetDragRect(out int x0, out int z0, out int x1, out int z1));
        Assert.AreEqual((10, 10, 12, 11), (x0, z0, x1, z1));

        Assert.IsTrue(_session.OnPointerUp().TerrainChanged);
        Assert.IsFalse(TerrainVoxel.IsEmpty(_store.GetVoxel(12, 5, 11)));
        Assert.IsFalse(_session.TryGetDragRect(out _, out _, out _, out _), "Up 後はプレビューなし");
    }

    [Test]
    public void TypeChange_NothingChangeable_FlashesOnUp()
    {
        _session.Mode = TerrainEditMode.TypeChange;
        _session.OnPointerDown(10, 10); // 空セル
        var up = _session.OnPointerUp();
        Assert.IsNotNull(up.FlashMessage, "変更できるブロックがなければフラッシュ");
    }

    [Test]
    public void TypeChange_ChangedCell_NoFlash()
    {
        _edit.PaintCell(10, 10, 0);
        _session.Mode = TerrainEditMode.TypeChange;
        Assert.IsTrue(_session.OnPointerDown(10, 10).TerrainChanged);
        Assert.IsNull(_session.OnPointerUp().FlashMessage);
        Assert.AreEqual(TerrainShape.RampN, TerrainVoxel.GetShape(_store.GetVoxel(10, 5, 10)));
    }

    [Test]
    public void RangeSelect_DragAddsRect_TapOutsideClears()
    {
        _session.Mode = TerrainEditMode.RangeSelect;

        _session.OnPointerDown(10, 10);
        _session.OnPointerDrag(11, 11);
        Assert.IsTrue(_session.OnPointerUp().SelectionChanged);
        Assert.AreEqual(4, _edit.Selection.Count);

        // 別範囲のスライドで複数範囲
        _session.OnPointerDown(20, 20);
        _session.OnPointerDrag(20, 21);
        _session.OnPointerUp();
        Assert.AreEqual(6, _edit.Selection.Count);

        // 選択範囲内のタップは維持・範囲外のタップで解除
        _session.OnPointerDown(10, 10);
        Assert.IsFalse(_session.OnPointerUp().SelectionChanged);
        Assert.AreEqual(6, _edit.Selection.Count);

        _session.OnPointerDown(30, 30);
        Assert.IsTrue(_session.OnPointerUp().SelectionChanged);
        Assert.IsFalse(_edit.HasSelection);
    }

    [Test]
    public void RangeSelect_Flood_SelectsConnectedCells()
    {
        _edit.PaintCell(10, 10, 1);
        _edit.PaintCell(11, 10, 1);
        _session.Mode = TerrainEditMode.RangeSelect;
        _session.FloodSelect = true;

        _session.OnPointerDown(10, 10);
        Assert.IsTrue(_session.OnPointerUp().SelectionChanged);
        Assert.AreEqual(2, _edit.Selection.Count);
    }

    [Test]
    public void Move_PreviewsDuringDrag_CommitsTotalDeltaOnPointerUp()
    {
        _edit.PaintCell(10, 10, 1);
        _session.Mode = TerrainEditMode.Move;

        _session.OnPointerDown(20, 20);
        // ドラッグ中は地形を変更しない（移動先プレビューのみ）
        Assert.IsFalse(_session.OnPointerDrag(21, 20).TerrainChanged, "ドラッグ中は移動しない");
        Assert.IsFalse(_session.OnPointerDrag(22, 21).TerrainChanged, "ドラッグ中は移動しない");
        Assert.IsFalse(TerrainVoxel.IsEmpty(_store.GetVoxel(10, 5, 10)), "ドラッグ中は元位置のまま");

        // ドラッグ中は Down からの合計差分をプレビューオフセットとして取得できる
        Assert.IsTrue(_session.TryGetMovePreview(out int dx, out int dz));
        Assert.AreEqual((2, 1), (dx, dz));

        // 指を離した Up で合計 (+2, +1) を一度だけ移動して確定する
        Assert.IsTrue(_session.OnPointerUp().TerrainChanged);
        Assert.IsTrue(TerrainVoxel.IsEmpty(_store.GetVoxel(10, 5, 10)));
        Assert.IsFalse(TerrainVoxel.IsEmpty(_store.GetVoxel(12, 5, 11)), "計 (+2, +1) 移動");
        Assert.IsFalse(_session.TryGetMovePreview(out _, out _), "Up 後はプレビューなし");
    }

    [Test]
    public void Constructor_NullLogic_Throws()
    {
        Assert.Throws<System.ArgumentNullException>(() => new TerrainEditSession(null));
    }
}
