using System.Linq;
using NUnit.Framework;

public class TerrainEditLogicTests
{
    private TerrainVoxelStore _store;
    private TerrainEditLogic _logic;

    [SetUp]
    public void SetUp()
    {
        _store = new TerrainVoxelStore();
        _logic = new TerrainEditLogic(_store);
        _logic.SetHeight(5);
    }

    // ── 高さ・基本 ────────────────────────────────────────────────────────────

    [Test]
    public void SetHeight_ClampsToWorldRange()
    {
        _logic.SetHeight(-3);
        Assert.AreEqual(0, _logic.CurrentHeight);
        _logic.SetHeight(99);
        Assert.AreEqual(TerrainVoxelStore.SizeY - 1, _logic.CurrentHeight);
    }

    [Test]
    public void Constructor_NullStore_Throws()
    {
        Assert.Throws<System.ArgumentNullException>(() => new TerrainEditLogic(null));
    }

    // ── ブラシ / 消しゴム / 図形 ──────────────────────────────────────────────

    [Test]
    public void PaintCell_PlacesCubeWithPalette()
    {
        Assert.IsTrue(_logic.PaintCell(10, 12, 3));
        byte v = _store.GetVoxel(10, 5, 12);
        Assert.AreEqual(TerrainShape.Cube, TerrainVoxel.GetShape(v));
        Assert.AreEqual(3, TerrainVoxel.GetPaletteIndex(v));
    }

    [Test]
    public void PaintCell_OverwritesExistingTerrain()
    {
        _store.SetVoxel(10, 5, 12, TerrainVoxel.Encode(TerrainShape.RampN, 1));
        _logic.PaintCell(10, 12, 4);
        byte v = _store.GetVoxel(10, 5, 12);
        Assert.AreEqual(TerrainShape.Cube, TerrainVoxel.GetShape(v), "配置済みは上書き");
        Assert.AreEqual(4, TerrainVoxel.GetPaletteIndex(v));
    }

    [Test]
    public void PaintCell_OutOfBounds_ReturnsFalse()
    {
        Assert.IsFalse(_logic.PaintCell(-1, 0, 0));
        Assert.IsFalse(_logic.PaintCell(63, 0, 0));
    }

    [Test]
    public void EraseCell_RemovesTerrain()
    {
        _logic.PaintCell(10, 12, 0);
        Assert.IsTrue(_logic.EraseCell(10, 12));
        Assert.AreEqual(TerrainVoxel.Empty, _store.GetVoxel(10, 5, 12));
        Assert.IsFalse(_logic.EraseCell(10, 12), "空セルは false");
    }

    [Test]
    public void FillRect_FillsNormalizedRange()
    {
        int count = _logic.FillRect(12, 14, 10, 12, 2); // 逆順座標も正規化
        Assert.AreEqual(9, count);
        for (int z = 12; z <= 14; z++)
            for (int x = 10; x <= 12; x++)
                Assert.IsFalse(TerrainVoxel.IsEmpty(_store.GetVoxel(x, 5, z)));
    }

    [Test]
    public void FillRect_ClampsToWorldBounds()
    {
        int count = _logic.FillRect(-5, -5, 1, 1, 0);
        Assert.AreEqual(4, count, "範囲外セルは配置しない");
    }

    // ── 範囲選択 ──────────────────────────────────────────────────────────────

    [Test]
    public void SelectRect_RestrictsEditing()
    {
        _logic.SelectRect(10, 10, 12, 12);
        Assert.IsTrue(_logic.PaintCell(11, 11, 0), "選択範囲内は編集可");
        Assert.IsFalse(_logic.PaintCell(20, 20, 0), "選択範囲外は編集不可");
        Assert.AreEqual(9, _logic.Selection.Count);

        _logic.ClearSelection();
        Assert.IsTrue(_logic.PaintCell(20, 20, 0), "選択解除後は編集可");
    }

    [Test]
    public void SelectRect_AddToSelection_AccumulatesRanges()
    {
        _logic.SelectRect(0, 0, 1, 1);
        _logic.SelectRect(10, 10, 11, 11, addToSelection: true);
        Assert.AreEqual(8, _logic.Selection.Count, "複数範囲の選択");

        _logic.SelectRect(20, 20, 20, 20); // add なしは置き換え
        Assert.AreEqual(1, _logic.Selection.Count);
    }

    [Test]
    public void SelectFlood_ExpandsOverSameKind()
    {
        // 同一パレットの連結領域（形状混在）+ 別パレットの隣接 + 斜め隣接
        _store.SetVoxel(10, 5, 10, TerrainVoxel.Encode(TerrainShape.Cube, 1));
        _store.SetVoxel(11, 5, 10, TerrainVoxel.Encode(TerrainShape.RampN, 1));
        _store.SetVoxel(11, 5, 11, TerrainVoxel.Encode(TerrainShape.DiagNW, 1));
        _store.SetVoxel(12, 5, 10, TerrainVoxel.Encode(TerrainShape.Cube, 2)); // 別種
        _store.SetVoxel(9, 5, 9, TerrainVoxel.Encode(TerrainShape.Cube, 1));   // 斜め隣接

        Assert.IsTrue(_logic.SelectFlood(10, 10));
        CollectionAssert.AreEquivalent(
            new[] { (10, 10), (11, 10), (11, 11) },
            _logic.Selection,
            "同種（ramp/diag 含む）のみ・斜め隣接は接続しない");
    }

    [Test]
    public void SelectFlood_EmptySeed_ReturnsFalse()
    {
        Assert.IsFalse(_logic.SelectFlood(10, 10));
        Assert.IsFalse(_logic.HasSelection);
    }

    // ── タイプ変更 ────────────────────────────────────────────────────────────

    [Test]
    public void CycleType_IsolatedCube_CyclesThroughAllShapesAndBack()
    {
        _logic.PaintCell(10, 10, 0);
        var expected = new[]
        {
            TerrainShape.RampN, TerrainShape.RampE, TerrainShape.RampS, TerrainShape.RampW,
            TerrainShape.DiagNW, TerrainShape.DiagNE, TerrainShape.DiagSE, TerrainShape.DiagSW,
            TerrainShape.Cube,
        };
        foreach (var shape in expected)
        {
            Assert.AreEqual(TerrainEditLogic.TypeChangeResult.Changed, _logic.CycleType(10, 10));
            Assert.AreEqual(shape, TerrainVoxel.GetShape(_store.GetVoxel(10, 5, 10)), $"次は {shape}");
        }
    }

    [Test]
    public void CycleType_RampRequiresOpenLowSideAndEmptyAbove()
    {
        // 北・東・西を埋める → 空いている側面は南のみ → ramp は RampN（低い側 = South）のみ
        _logic.PaintCell(10, 10, 0);
        _logic.PaintCell(10, 11, 0); // North
        _logic.PaintCell(11, 10, 0); // East
        _logic.PaintCell(9, 10, 0);  // West
        Assert.AreEqual(TerrainEditLogic.TypeChangeResult.Changed, _logic.CycleType(10, 10));
        Assert.AreEqual(TerrainShape.RampN, TerrainVoxel.GetShape(_store.GetVoxel(10, 5, 10)));
    }

    [Test]
    public void CycleType_AboveOccupied_SkipsRampsToDiag()
    {
        _logic.PaintCell(10, 10, 0);
        _store.SetVoxel(10, 6, 10, TerrainVoxel.Encode(TerrainShape.Cube, 0)); // 真上を塞ぐ
        Assert.AreEqual(TerrainEditLogic.TypeChangeResult.Changed, _logic.CycleType(10, 10));
        Assert.AreEqual(
            TerrainShape.DiagNW,
            TerrainVoxel.GetShape(_store.GetVoxel(10, 5, 10)),
            "真上が塞がっていると ramp は不可・diag は可");
    }

    [Test]
    public void CycleType_FullySurroundedCube_NotChangeable()
    {
        _logic.PaintCell(10, 10, 0);
        _logic.PaintCell(10, 11, 0);
        _logic.PaintCell(10, 9, 0);
        _logic.PaintCell(11, 10, 0);
        _logic.PaintCell(9, 10, 0);
        _store.SetVoxel(10, 6, 10, TerrainVoxel.Encode(TerrainShape.Cube, 0));
        Assert.AreEqual(TerrainEditLogic.TypeChangeResult.NotChangeable, _logic.CycleType(10, 10));
        Assert.AreEqual(TerrainShape.Cube, TerrainVoxel.GetShape(_store.GetVoxel(10, 5, 10)), "変更されない");
    }

    [Test]
    public void CycleType_RampWithNoValidShape_RevertsToCube()
    {
        // ramp を置いた後に周囲を全部埋める → 次のタップで立方体に戻る
        _store.SetVoxel(10, 5, 10, TerrainVoxel.Encode(TerrainShape.RampN, 2));
        _logic.PaintCell(10, 11, 0);
        _logic.PaintCell(10, 9, 0);
        _logic.PaintCell(11, 10, 0);
        _logic.PaintCell(9, 10, 0);
        _store.SetVoxel(10, 6, 10, TerrainVoxel.Encode(TerrainShape.Cube, 0));

        Assert.AreEqual(TerrainEditLogic.TypeChangeResult.Changed, _logic.CycleType(10, 10));
        byte v = _store.GetVoxel(10, 5, 10);
        Assert.AreEqual(TerrainShape.Cube, TerrainVoxel.GetShape(v));
        Assert.AreEqual(2, TerrainVoxel.GetPaletteIndex(v), "パレットは維持");
    }

    [Test]
    public void CycleType_EmptyCell_NotChangeable()
    {
        Assert.AreEqual(TerrainEditLogic.TypeChangeResult.NotChangeable, _logic.CycleType(10, 10));
    }

    [Test]
    public void CycleType_DiagRequiresTwoPerpendicularOpenSides()
    {
        // 南だけ空き（北・東・西埋め・真上も塞ぐ）→ diag は直角 2 面の空きが必要なので不可
        _logic.PaintCell(10, 10, 0);
        _logic.PaintCell(10, 11, 0);
        _logic.PaintCell(11, 10, 0);
        _logic.PaintCell(9, 10, 0);
        _store.SetVoxel(10, 6, 10, TerrainVoxel.Encode(TerrainShape.Cube, 0));
        Assert.AreEqual(TerrainEditLogic.TypeChangeResult.NotChangeable, _logic.CycleType(10, 10));
    }

    // ── 移動 ──────────────────────────────────────────────────────────────────

    [Test]
    public void Move_WholeLayer_WhenNoSelection()
    {
        _logic.PaintCell(10, 10, 1);
        _logic.PaintCell(11, 10, 1);
        Assert.IsTrue(_logic.Move(2, 3));

        Assert.IsTrue(TerrainVoxel.IsEmpty(_store.GetVoxel(10, 5, 10)));
        Assert.IsFalse(TerrainVoxel.IsEmpty(_store.GetVoxel(12, 5, 13)));
        Assert.IsFalse(TerrainVoxel.IsEmpty(_store.GetVoxel(13, 5, 13)));
    }

    [Test]
    public void Move_SelectionOnly_AndUpdatesSelection()
    {
        _logic.PaintCell(10, 10, 1);
        _logic.PaintCell(20, 20, 2); // 選択外
        _logic.SelectRect(10, 10, 10, 10);
        _logic.Move(1, 0);

        Assert.IsFalse(TerrainVoxel.IsEmpty(_store.GetVoxel(11, 5, 10)), "選択範囲内は移動");
        Assert.IsFalse(TerrainVoxel.IsEmpty(_store.GetVoxel(20, 5, 20)), "選択範囲外は動かない");
        CollectionAssert.AreEquivalent(new[] { (11, 10) }, _logic.Selection, "選択範囲も移動");
    }

    [Test]
    public void Move_OutOfBounds_DeletesBlocks()
    {
        _logic.PaintCell(62, 10, 1);
        _logic.PaintCell(61, 10, 1);
        _logic.Move(1, 0);

        Assert.IsTrue(TerrainVoxel.IsEmpty(_store.GetVoxel(61, 5, 10)));
        Assert.IsFalse(TerrainVoxel.IsEmpty(_store.GetVoxel(62, 5, 10)), "範囲内に残るブロックは移動");
        // x=63 は存在しない（範囲外に出たブロックは削除）
    }

    [Test]
    public void Move_PreservesShapeAndOverwritesDestination()
    {
        _store.SetVoxel(10, 5, 10, TerrainVoxel.Encode(TerrainShape.DiagSE, 3));
        _logic.PaintCell(11, 10, 0); // 移動先の既存ブロック
        _logic.SelectRect(10, 10, 10, 10);
        _logic.Move(1, 0);

        byte v = _store.GetVoxel(11, 5, 10);
        Assert.AreEqual(TerrainShape.DiagSE, TerrainVoxel.GetShape(v), "形状ごと移動・移動先は上書き");
        Assert.AreEqual(3, TerrainVoxel.GetPaletteIndex(v));
    }

    // ── コピー & ペースト ─────────────────────────────────────────────────────

    [Test]
    public void CopyPaste_ToAnotherHeight()
    {
        _logic.PaintCell(10, 10, 2);
        _store.SetVoxel(11, 5, 10, TerrainVoxel.Encode(TerrainShape.RampE, 2));
        _logic.SelectRect(10, 10, 11, 10);
        Assert.IsTrue(_logic.CopySelection());

        _logic.SetHeight(8);
        Assert.IsTrue(_logic.Paste());
        Assert.AreEqual(TerrainShape.Cube, TerrainVoxel.GetShape(_store.GetVoxel(10, 8, 10)));
        Assert.AreEqual(TerrainShape.RampE, TerrainVoxel.GetShape(_store.GetVoxel(11, 8, 10)));
        Assert.IsFalse(TerrainVoxel.IsEmpty(_store.GetVoxel(10, 5, 10)), "コピー元は残る");
    }

    [Test]
    public void CopySelection_NoTerrain_ReturnsFalse()
    {
        _logic.SelectRect(30, 30, 31, 31);
        Assert.IsFalse(_logic.CopySelection());
        Assert.IsFalse(_logic.HasClipboard);
        Assert.IsFalse(_logic.Paste());
    }

    // ── Dirty チャンク ────────────────────────────────────────────────────────

    [Test]
    public void DirtyChunks_TracksEditedChunks()
    {
        _logic.PaintCell(5, 5, 0);   // チャンク (0,0,0)
        _logic.PaintCell(20, 20, 0); // チャンク (1,0,1)
        var dirty = _logic.ConsumeDirtyChunks();

        CollectionAssert.AreEquivalent(new[] { (0, 0, 0), (1, 0, 1) }, dirty);
        Assert.AreEqual(0, _logic.ConsumeDirtyChunks().Count, "取り出し後はクリア");
    }

    [Test]
    public void DirtyChunks_BorderEdit_IncludesNeighborChunks()
    {
        _logic.PaintCell(16, 15, 0); // チャンク (1,0,0) の西端 + 北端（z=15）
        var dirty = _logic.ConsumeDirtyChunks();

        CollectionAssert.AreEquivalent(
            new[] { (1, 0, 0), (0, 0, 0), (1, 0, 1), (0, 0, 1) },
            dirty,
            "境界セルは隣接チャンク（斜め含む）のメッシュにも影響する");
    }

    [Test]
    public void DirtyChunks_NoChangeWrite_NotMarked()
    {
        _logic.PaintCell(5, 5, 0);
        _logic.ConsumeDirtyChunks();
        _logic.PaintCell(5, 5, 0); // 同じ値の上書き
        Assert.AreEqual(0, _logic.ConsumeDirtyChunks().Count, "値が変わらなければ Dirty にしない");
    }
}
