/// <summary>文字入れスタンプの編集状態。</summary>
public enum TextStampEditState
{
    NotEdited,
    Editing,
    Completed,
}

/// <summary>
/// 文字入れスタンプのテキスト編集状態管理（純粋 C#）。
/// 状態遷移: 未編集 → 編集中 → 完了 → 再編集（完了 → 編集中）。
/// 仕様: screens-and-modes.md セクション 2.7.3
/// </summary>
public class TextStampLogic
{
    public TextStampEditState EditState { get; private set; } = TextStampEditState.NotEdited;
    public string Text { get; private set; } = string.Empty;

    /// <summary>
    /// テキスト編集を開始する（配置直後 または スタンプ再タップ）。
    /// 既に編集中の場合は false を返す。
    /// </summary>
    public bool BeginEditing()
    {
        if (EditState == TextStampEditState.Editing) return false;
        EditState = TextStampEditState.Editing;
        return true;
    }

    /// <summary>
    /// テキストを更新する。編集中でない場合は false を返す。
    /// </summary>
    public bool UpdateText(string text)
    {
        if (EditState != TextStampEditState.Editing) return false;
        Text = text ?? string.Empty;
        return true;
    }

    /// <summary>
    /// 編集を完了する（スタンプ外タップ等）。編集中でない場合は false を返す。
    /// </summary>
    public bool CompleteEditing()
    {
        if (EditState != TextStampEditState.Editing) return false;
        EditState = TextStampEditState.Completed;
        return true;
    }
}
