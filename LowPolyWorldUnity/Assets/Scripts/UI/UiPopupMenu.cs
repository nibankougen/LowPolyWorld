using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// アンカー要素からはみ出して開く、縦並びの四角いポップアップメニュー
/// （会話エディタのセリフ行の「追加」「削除」メニューなど）。
///
/// - 指定したレイヤー（クリップされない絶対配置の最上位＝オーバーレイ root）に
///   絶対配置でメニューを描画するため、カードや ScrollView の枠からはみ出して表示される。
/// - 画面下半分のアンカーからはアンカーの上側へ、上半分のアンカーからは下側へ開く。
/// - メニュー外のタップ・項目の選択・（指定時）スクロールで閉じる。
/// </summary>
public class UiPopupMenu
{
    public readonly struct Item
    {
        public readonly string Label;
        public readonly string IconClass; // null 可（アイコンなし）
        public readonly Action OnClick;
        public readonly string RowClass; // 行に付ける追加クラス（赤い危険操作など・null 可）

        public Item(string label, Action onClick, string iconClass = null, string rowClass = null)
        {
            Label = label;
            OnClick = onClick;
            IconClass = iconClass;
            RowClass = rowClass;
        }
    }

    private readonly VisualElement _layer; // クリップされない絶対配置の親（オーバーレイ root）
    private VisualElement _menu;
    private VisualElement _scrim; // 外側タップで閉じる透明オーバーレイ
    private VisualElement _anchor;
    private ScrollView _closeOnScroll;
    private IVisualElementScheduledItem _scrollWatch;
    private Vector2 _lastScrollOffset;

    public UiPopupMenu(VisualElement layer)
    {
        _layer = layer;
    }

    public bool IsOpen => _menu != null;

    /// <summary>
    /// anchor の近くにメニューを開く。closeOnScroll を渡すと、そのスクロールで自動的に閉じる。
    /// </summary>
    public void Open(VisualElement anchor, IReadOnlyList<Item> items, ScrollView closeOnScroll = null)
    {
        Close();
        if (_layer == null || anchor == null || items == null || items.Count == 0)
            return;

        _anchor = anchor;

        // 背後の全面スクリム（メニュー外タップで閉じる・透明）
        _scrim = new VisualElement();
        _scrim.AddToClassList("ui-popup-scrim");
        _scrim.RegisterCallback<PointerDownEvent>(e =>
        {
            Close();
            e.StopPropagation();
        });
        _layer.Add(_scrim);

        _menu = new VisualElement();
        _menu.AddToClassList("ui-popup-menu");
        foreach (var item in items)
        {
            Action onClick = item.OnClick;
            var row = new Button(() =>
            {
                Close();
                onClick?.Invoke();
            }) { text = "" };
            row.AddToClassList("ui-popup-item");
            if (!string.IsNullOrEmpty(item.RowClass))
                row.AddToClassList(item.RowClass);
            if (!string.IsNullOrEmpty(item.IconClass))
            {
                var ic = new VisualElement { pickingMode = PickingMode.Ignore };
                ic.AddToClassList("ui-popup-item-icon");
                ic.AddToClassList(item.IconClass);
                row.Add(ic);
            }
            var label = new Label(item.Label) { pickingMode = PickingMode.Ignore };
            label.AddToClassList("ui-popup-item-label");
            row.Add(label);
            _menu.Add(row);
        }
        _layer.Add(_menu);

        // サイズが確定してから位置決めする（worldBound が必要なため一旦隠す）。
        _menu.style.visibility = Visibility.Hidden;
        _menu.RegisterCallback<GeometryChangedEvent>(OnMenuGeometry);

        if (closeOnScroll != null)
        {
            _closeOnScroll = closeOnScroll;
            _lastScrollOffset = closeOnScroll.scrollOffset;
            _scrollWatch = _layer.schedule.Execute(WatchScroll).Every(16);
        }
    }

    public void Close()
    {
        _scrollWatch?.Pause();
        _scrollWatch = null;
        _closeOnScroll = null;
        _anchor = null;
        if (_scrim != null)
        {
            _scrim.RemoveFromHierarchy();
            _scrim = null;
        }
        if (_menu != null)
        {
            _menu.RemoveFromHierarchy();
            _menu = null;
        }
    }

    private void OnMenuGeometry(GeometryChangedEvent _)
    {
        if (_menu == null)
            return;
        _menu.UnregisterCallback<GeometryChangedEvent>(OnMenuGeometry);
        PositionMenu();
        _menu.style.visibility = Visibility.Visible;
    }

    private void PositionMenu()
    {
        if (_menu == null || _anchor == null)
            return;

        Rect layer = _layer.worldBound;
        Rect anchor = _anchor.worldBound;
        Rect menu = _menu.worldBound; // サイズ確定済み
        const float gap = 4f;
        const float pad = 4f;

        // 横: アンカー左端に合わせる。右端からはみ出すならクランプ。
        float left = anchor.xMin - layer.xMin;
        float maxLeft = Mathf.Max(pad, layer.width - menu.width - pad);
        left = Mathf.Clamp(left, pad, maxLeft);

        // 縦: アンカーが画面下半分なら上へ、上半分なら下へ開く。
        bool openUp = anchor.center.y > layer.center.y;
        float top = openUp
            ? (anchor.yMin - layer.yMin) - menu.height - gap
            : (anchor.yMax - layer.yMin) + gap;
        top = Mathf.Clamp(top, pad, Mathf.Max(pad, layer.height - menu.height - pad));

        _menu.style.left = left;
        _menu.style.top = top;
    }

    private void WatchScroll()
    {
        if (_closeOnScroll == null)
            return;
        if ((_closeOnScroll.scrollOffset - _lastScrollOffset).sqrMagnitude > 0.01f)
            Close();
    }
}
