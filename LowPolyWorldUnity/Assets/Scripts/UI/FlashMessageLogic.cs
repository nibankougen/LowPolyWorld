using System.Collections.Generic;

public enum FlashMessageType
{
    Info,
    Success,
    Warning,
    Error,
}

public class FlashMessageEntry
{
    public string Text { get; }
    public FlashMessageType Type { get; }

    public FlashMessageEntry(string text, FlashMessageType type)
    {
        Text = text;
        Type = type;
    }
}

/// <summary>
/// フラッシュメッセージ（トースト通知）のキュー管理と表示タイマーロジック（純粋 C#）。
/// メッセージを FIFO キューに積み、1件ずつ表示する。
/// 仕様: screens-and-modes.md セクション 1
/// </summary>
public class FlashMessageLogic
{
    private readonly Queue<FlashMessageEntry> _queue = new();
    private FlashMessageEntry _current;
    private float _remainingSeconds;

    public float DisplaySeconds { get; }

    public FlashMessageEntry Current => _current;
    public bool IsVisible => _current != null;

    public FlashMessageLogic(float displaySeconds = 2.5f)
    {
        DisplaySeconds = displaySeconds;
    }

    /// <summary>メッセージをキューに追加する。</summary>
    public void Show(string text, FlashMessageType type = FlashMessageType.Info)
    {
        _queue.Enqueue(new FlashMessageEntry(text, type));
    }

    /// <summary>
    /// 時間を進める。新しいメッセージの表示を開始したとき true を返す。
    /// </summary>
    public bool Tick(float deltaSeconds)
    {
        if (_current != null)
        {
            _remainingSeconds -= deltaSeconds;
            if (_remainingSeconds <= 0f)
                _current = null;
        }

        if (_current == null && _queue.Count > 0)
        {
            _current = _queue.Dequeue();
            _remainingSeconds = DisplaySeconds;
            return true;
        }

        return false;
    }

    /// <summary>キューと現在表示中のメッセージをすべてクリアする。</summary>
    public void Clear()
    {
        _queue.Clear();
        _current = null;
    }
}
