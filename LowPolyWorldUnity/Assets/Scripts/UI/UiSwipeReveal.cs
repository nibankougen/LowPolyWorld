using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 行要素を左にスワイプして、背後の操作（丸い削除ボタンなど）を見せる挙動。
///
/// - 前景（<paramref name="foreground"/>＝不透明な行カード）を水平方向にドラッグして開閉する。
/// - 同時に開けるのは 1 つだけ（別の行を触ると前の行は閉じる）。
/// - 外側のタップ・スクロールなど「他の部分をいじったら閉じる」は、呼び出し側が
///   <see cref="CloseCurrent"/> を呼ぶ（開いている行の内側かどうかは <see cref="ContainsTarget"/> で判定）。
/// </summary>
public class UiSwipeReveal
{
    private const float Threshold = 8f;     // スワイプ開始とみなす水平移動量(px)
    // ヒステリシス: 閉→開は深く引いたときだけ開き、開→閉は少し戻すだけで閉じる（戻しやすく）。
    private const float OpenThreshold = 0.4f;   // 閉じた状態から: reveal 幅のこの割合以上引いたら開く
    private const float CloseThreshold = 0.75f; // 開いた状態から: この割合を下回るまで戻したら閉じる

    private static UiSwipeReveal _current; // 今開いている（or 操作中の）スワイプ

    private readonly VisualElement _fg;    // スライドする前景（不透明な行カード）
    private readonly float _revealWidth;

    private int _pointerId = -1;
    private float _startX, _startY, _baseX, _curX;
    private bool _swiping, _capturing, _open;

    public UiSwipeReveal(VisualElement foreground, float revealWidth)
    {
        _fg = foreground;
        _revealWidth = revealWidth;
        _fg.RegisterCallback<PointerDownEvent>(OnDown);
        _fg.RegisterCallback<PointerMoveEvent>(OnMove);
        _fg.RegisterCallback<PointerUpEvent>(OnUp);
    }

    /// <summary>今開いている（または操作中の）スワイプ。無ければ null。</summary>
    public static UiSwipeReveal Current => _current;

    /// <summary>今開いているスワイプを閉じる。</summary>
    public static void CloseCurrent() => _current?.SetOpen(false);

    /// <summary>el がこのスワイプ行（前景の親）の内側にあるか。</summary>
    public bool ContainsTarget(VisualElement el)
    {
        var row = _fg.parent;
        for (var p = el; p != null; p = p.parent)
            if (p == row)
                return true;
        return false;
    }

    private void OnDown(PointerDownEvent e)
    {
        // 別の行が開いていたら閉じる。
        if (_current != null && _current != this)
            CloseCurrent();
        _pointerId = e.pointerId;
        _startX = e.position.x;
        _startY = e.position.y;
        _baseX = _open ? -_revealWidth : 0f;
        _curX = _baseX;
        _swiping = false;
        _capturing = false;
    }

    private void OnMove(PointerMoveEvent e)
    {
        if (e.pointerId != _pointerId)
            return;
        float dx = e.position.x - _startX;
        float dy = e.position.y - _startY;
        if (!_swiping)
        {
            // 水平が縦より優勢で閾値を超えたらスワイプ開始（縦スクロール／縦 D&D とすみ分け）。
            if (Mathf.Abs(dx) < Threshold || Mathf.Abs(dx) <= Mathf.Abs(dy))
                return;
            _swiping = true;
            _current = this;
            _fg.CapturePointer(e.pointerId);
            _capturing = true;
        }
        _curX = Mathf.Clamp(_baseX + dx, -_revealWidth, 0f);
        _fg.style.translate = new Translate(_curX, 0f, 0f);
        e.StopPropagation();
    }

    private void OnUp(PointerUpEvent e)
    {
        if (e.pointerId != _pointerId)
            return;
        if (_capturing)
            _fg.ReleasePointer(e.pointerId);
        _pointerId = -1;
        _capturing = false;
        if (_swiping)
        {
            // 現在の引き具合（0=閉 / 1=開）。開始時の状態でしきい値を変える（ヒステリシス）。
            float revealed = _revealWidth > 0f ? -_curX / _revealWidth : 0f;
            bool startedOpen = _baseX <= -_revealWidth * 0.5f;
            SetOpen(revealed >= (startedOpen ? CloseThreshold : OpenThreshold));
            e.StopPropagation();
        }
    }

    private void SetOpen(bool open)
    {
        _open = open;
        _curX = open ? -_revealWidth : 0f;
        _fg.style.translate = new Translate(_curX, 0f, 0f);
        if (open)
            _current = this;
        else if (_current == this)
            _current = null;
    }
}
