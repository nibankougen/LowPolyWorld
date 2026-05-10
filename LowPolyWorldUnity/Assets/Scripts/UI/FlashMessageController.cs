using System;
using UnityEngine.UIElements;

/// <summary>
/// フラッシュメッセージ表示を管理する非 MonoBehaviour コントローラー。
/// UXML 側に flash-root / flash-label 要素が必要。
/// UI Toolkit スケジューラーで 100ms ごとに状態を更新する。
/// Current プロパティで現在アクティブなインスタンスへアクセスできる。
/// </summary>
public class FlashMessageController : IDisposable
{
    /// <summary>現在アクティブなインスタンス。存在しない場合 null。</summary>
    public static FlashMessageController Current { get; private set; }

    private readonly VisualElement _root;
    private readonly Label _label;
    private readonly FlashMessageLogic _logic;
    private IVisualElementScheduledItem _schedule;

    private bool _visible;

    public FlashMessageController(VisualElement flashRoot, float displaySeconds = 2.5f)
    {
        _root = flashRoot ?? throw new ArgumentNullException(nameof(flashRoot));
        _label = _root.Q<Label>("flash-label");
        _logic = new FlashMessageLogic(displaySeconds);
        _root.AddToClassList("overlay-hidden");
        _schedule = _root.schedule.Execute(Tick).Every(100);
        Current = this;
    }

    /// <summary>メッセージをキューに追加して表示する。</summary>
    public void Show(string text, FlashMessageType type = FlashMessageType.Info)
    {
        _logic.Show(text, type);
    }

    private void Tick()
    {
        bool newMessage = _logic.Tick(0.1f);
        bool isVisible = _logic.IsVisible;

        if (isVisible && (newMessage || !_visible))
        {
            if (_label != null)
            {
                _label.text = _logic.Current.Text;
                _label.RemoveFromClassList("flash-type-info");
                _label.RemoveFromClassList("flash-type-success");
                _label.RemoveFromClassList("flash-type-warning");
                _label.RemoveFromClassList("flash-type-error");
                _label.AddToClassList($"flash-type-{_logic.Current.Type.ToString().ToLower()}");
            }
            _root.RemoveFromClassList("overlay-hidden");
        }
        else if (!isVisible && _visible)
        {
            _root.AddToClassList("overlay-hidden");
        }

        _visible = isVisible;
    }

    public void Dispose()
    {
        if (Current == this)
            Current = null;
        _schedule?.Pause();
        _schedule = null;
        _logic.Clear();
    }
}
