/// <summary>撮影モードの状態。</summary>
public enum PhotoModeState
{
    Normal,
    Photo,
}

/// <summary>
/// 撮影モードの状態遷移ロジック（純粋 C#）。
/// 入場時にプレイヤー操作を無効化し、退場時にカメラ位置を復元する指示を
/// イベント経由で MonoBehaviour へ伝える。
/// 仕様: screens-and-modes.md セクション 2.7
/// </summary>
public class PhotoModeLogic
{
    public PhotoModeState State { get; private set; } = PhotoModeState.Normal;
    public bool IsPhotoMode => State == PhotoModeState.Photo;

    /// <summary>
    /// 撮影モードに入る。既にPhoto状態の場合は false を返す。
    /// </summary>
    public bool Enter()
    {
        if (State == PhotoModeState.Photo) return false;
        State = PhotoModeState.Photo;
        return true;
    }

    /// <summary>
    /// 撮影モードを終了する。Normal状態の場合は false を返す。
    /// </summary>
    public bool Exit()
    {
        if (State == PhotoModeState.Normal) return false;
        State = PhotoModeState.Normal;
        return true;
    }
}
