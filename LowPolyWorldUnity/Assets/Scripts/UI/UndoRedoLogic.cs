using System;
using System.Collections.Generic;

/// <summary>
/// 編集画面共通の Undo/Redo スタック管理（純粋 C#）。
/// タブごとにインスタンスを作成して使用する。
/// </summary>
public class UndoRedoLogic
{
    public const int PaintUndoMaxSteps = 50;

    private readonly int _maxSteps;

    // (undoAction, redoAction) のペアを保持
    private readonly LinkedList<(Action undo, Action redo)> _undoStack = new();
    private readonly Stack<(Action undo, Action redo)> _redoStack = new();

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    public event Action OnHistoryChanged;

    public UndoRedoLogic(int maxSteps = PaintUndoMaxSteps)
    {
        _maxSteps = maxSteps;
    }

    /// <summary>
    /// 操作を記録する。undoAction で元に戻し、redoAction で再適用する。
    /// </summary>
    public void Record(Action undoAction, Action redoAction)
    {
        _redoStack.Clear();
        _undoStack.AddLast((undoAction, redoAction));

        while (_undoStack.Count > _maxSteps)
            _undoStack.RemoveFirst();

        OnHistoryChanged?.Invoke();
    }

    /// <summary>Undo を実行する。</summary>
    public void Undo()
    {
        if (!CanUndo) return;

        var entry = _undoStack.Last.Value;
        _undoStack.RemoveLast();
        _redoStack.Push(entry);
        entry.undo?.Invoke();

        OnHistoryChanged?.Invoke();
    }

    /// <summary>Redo を実行する。</summary>
    public void Redo()
    {
        if (!CanRedo) return;

        var entry = _redoStack.Pop();
        _undoStack.AddLast(entry);
        entry.redo?.Invoke();

        OnHistoryChanged?.Invoke();
    }

    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        OnHistoryChanged?.Invoke();
    }
}
