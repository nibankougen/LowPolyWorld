using System;
using System.Collections.Generic;

/// <summary>
/// テクスチャペイント操作の Undo/Redo 履歴を管理する純粋 C# クラス。
/// テクスチャタブを離れると履歴をリセットする。
/// </summary>
public class PaintCommandHistory
{
    /// <summary>最大 Undo ステップ数。</summary>
    public const int MaxSteps = 50;

    private readonly LinkedList<(Action Undo, Action Redo)> _undoStack = new();
    private readonly Stack<(Action Undo, Action Redo)> _redoStack = new();

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;
    public int UndoCount => _undoStack.Count;
    public int RedoCount => _redoStack.Count;

    /// <summary>
    /// 操作を記録する。Redo スタックはクリアされる。
    /// 上限超過時は最古エントリを破棄する。
    /// </summary>
    /// <param name="undo">Undo 実行時のアクション。</param>
    /// <param name="redo">Redo 実行時のアクション。</param>
    public void Record(Action undo, Action redo)
    {
        _redoStack.Clear();
        if (_undoStack.Count >= MaxSteps)
            _undoStack.RemoveFirst();
        _undoStack.AddLast((undo, redo));
    }

    /// <summary>Undo を実行する。成功時は true。</summary>
    public bool Undo()
    {
        if (_undoStack.Count == 0)
            return false;

        var entry = _undoStack.Last!.Value;
        _undoStack.RemoveLast();
        _redoStack.Push(entry);
        entry.Undo();
        return true;
    }

    /// <summary>Redo を実行する。成功時は true。</summary>
    public bool Redo()
    {
        if (_redoStack.Count == 0)
            return false;

        var entry = _redoStack.Pop();
        _undoStack.AddLast(entry);
        entry.Redo();
        return true;
    }

    /// <summary>テクスチャタブを離れた際に履歴をリセットする。</summary>
    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
    }
}
