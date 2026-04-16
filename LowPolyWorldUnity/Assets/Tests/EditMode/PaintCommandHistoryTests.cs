using NUnit.Framework;
using System.Collections.Generic;

public class PaintCommandHistoryTests
{
    private PaintCommandHistory _history;
    private List<string> _log;

    [SetUp]
    public void SetUp()
    {
        _history = new PaintCommandHistory();
        _log = new List<string>();
    }

    // ---- 基本動作 ----

    [Test]
    public void Initial_CanUndoAndCanRedo_AreFalse()
    {
        Assert.IsFalse(_history.CanUndo);
        Assert.IsFalse(_history.CanRedo);
    }

    [Test]
    public void Record_OneStep_CanUndoIsTrue()
    {
        _history.Record(() => { }, () => { });

        Assert.IsTrue(_history.CanUndo);
        Assert.AreEqual(1, _history.UndoCount);
    }

    [Test]
    public void Undo_NoHistory_ReturnsFalse()
    {
        bool result = _history.Undo();

        Assert.IsFalse(result);
    }

    [Test]
    public void Redo_NoHistory_ReturnsFalse()
    {
        bool result = _history.Redo();

        Assert.IsFalse(result);
    }

    [Test]
    public void Undo_AfterRecord_InvokesUndoAction()
    {
        _history.Record(() => _log.Add("undo"), () => _log.Add("redo"));

        _history.Undo();

        CollectionAssert.AreEqual(new[] { "undo" }, _log);
    }

    [Test]
    public void Undo_AfterRecord_MovesToRedoStack()
    {
        _history.Record(() => { }, () => { });

        _history.Undo();

        Assert.IsFalse(_history.CanUndo);
        Assert.IsTrue(_history.CanRedo);
    }

    [Test]
    public void Redo_AfterUndo_InvokesRedoAction()
    {
        _history.Record(() => _log.Add("undo"), () => _log.Add("redo"));
        _history.Undo();

        _history.Redo();

        CollectionAssert.AreEqual(new[] { "undo", "redo" }, _log);
    }

    [Test]
    public void Redo_AfterUndo_MovesBackToUndoStack()
    {
        _history.Record(() => { }, () => { });
        _history.Undo();

        _history.Redo();

        Assert.IsTrue(_history.CanUndo);
        Assert.IsFalse(_history.CanRedo);
    }

    // ---- Redo スタッククリア ----

    [Test]
    public void Record_AfterUndo_ClearsRedoStack()
    {
        _history.Record(() => { }, () => { });
        _history.Undo();

        _history.Record(() => { }, () => { });

        Assert.IsFalse(_history.CanRedo);
    }

    // ---- 複数ステップ ----

    [Test]
    public void MultipleUndos_InvokedInReverseOrder()
    {
        _history.Record(() => _log.Add("undo1"), () => { });
        _history.Record(() => _log.Add("undo2"), () => { });
        _history.Record(() => _log.Add("undo3"), () => { });

        _history.Undo();
        _history.Undo();
        _history.Undo();

        CollectionAssert.AreEqual(new[] { "undo3", "undo2", "undo1" }, _log);
    }

    [Test]
    public void MultipleRedos_InvokedInOriginalOrder()
    {
        _history.Record(() => { }, () => _log.Add("redo1"));
        _history.Record(() => { }, () => _log.Add("redo2"));
        _history.Undo();
        _history.Undo();

        _history.Redo();
        _history.Redo();

        CollectionAssert.AreEqual(new[] { "redo1", "redo2" }, _log);
    }

    // ---- 上限（MaxSteps = 50）----

    [Test]
    public void Record_AtMaxSteps_DoesNotExceedLimit()
    {
        for (int i = 0; i < PaintCommandHistory.MaxSteps; i++)
            _history.Record(() => { }, () => { });

        Assert.AreEqual(PaintCommandHistory.MaxSteps, _history.UndoCount);
    }

    [Test]
    public void Record_OverMaxSteps_DropsOldestEntry()
    {
        int undoCallCount = 0;

        // 1件目: カウンター付き
        _history.Record(() => undoCallCount++, () => { });

        // 残り MaxSteps 件を追加して押し出す
        for (int i = 0; i < PaintCommandHistory.MaxSteps; i++)
            _history.Record(() => { }, () => { });

        // MaxSteps 件分 Undo → 1件目は押し出されているので呼ばれない
        for (int i = 0; i < PaintCommandHistory.MaxSteps; i++)
            _history.Undo();

        Assert.AreEqual(0, undoCallCount);
        Assert.AreEqual(PaintCommandHistory.MaxSteps, _history.RedoCount);
    }

    [Test]
    public void Record_ExactlyMaxStepsPlusOne_UndoCountStaysAtMax()
    {
        for (int i = 0; i < PaintCommandHistory.MaxSteps + 1; i++)
            _history.Record(() => { }, () => { });

        Assert.AreEqual(PaintCommandHistory.MaxSteps, _history.UndoCount);
    }

    // ---- Undo → Redo → Undo ラウンドトリップ ----

    [Test]
    public void UndoRedoUndo_RoundTrip_InvokesActionsInCorrectOrder()
    {
        _history.Record(() => _log.Add("undo1"), () => _log.Add("redo1"));
        _history.Record(() => _log.Add("undo2"), () => _log.Add("redo2"));

        _history.Undo();   // undo2
        _history.Redo();   // redo2
        _history.Undo();   // undo2 again

        CollectionAssert.AreEqual(new[] { "undo2", "redo2", "undo2" }, _log);
        Assert.AreEqual(1, _history.UndoCount);  // undo1 だけ残る
        Assert.AreEqual(1, _history.RedoCount);  // redo2 が積まれている
    }

    [Test]
    public void Record_AfterPartialUndoRedo_ClearsRedoAndAppendsCorrectly()
    {
        _history.Record(() => _log.Add("undo1"), () => _log.Add("redo1"));
        _history.Record(() => _log.Add("undo2"), () => _log.Add("redo2"));
        _history.Undo();  // undo2 → redo: [(,redo2)]
        _history.Undo();  // undo1 → redo: [(,redo2),(,redo1)]

        // 新規 Record → Redo スタックがクリアされる
        _history.Record(() => _log.Add("undo3"), () => _log.Add("redo3"));

        Assert.IsFalse(_history.CanRedo);
        Assert.AreEqual(1, _history.UndoCount);  // undo3 のみ
    }

    // ---- Clear ----

    [Test]
    public void Clear_AfterRecords_ResetsAllStacks()
    {
        _history.Record(() => { }, () => { });
        _history.Record(() => { }, () => { });
        _history.Undo();

        _history.Clear();

        Assert.IsFalse(_history.CanUndo);
        Assert.IsFalse(_history.CanRedo);
        Assert.AreEqual(0, _history.UndoCount);
        Assert.AreEqual(0, _history.RedoCount);
    }
}
