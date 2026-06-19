using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// アイコン付きの選択肢を持つ、再利用可能なカスタムドロップダウン（UI Toolkit の DropdownField の代替）。
///
/// 操作:
/// - タップするとフィールドの下にメニューが開き、項目をタップして選べる（メニュー外タップで閉じる）。
/// - 押したまま下にスライドし、項目の上で指を離すとその項目が選ばれる（押下→ドラッグ→離す の一連操作）。
///
/// 各選択肢は任意のアイコン（USS クラスで背景画像を指定）とティント色を持てる。閉じた状態の
/// フィールドには現在の選択肢のアイコン + ラベルを表示する。汎用コンポーネントとして
/// 話者選択以外でも使えるよう、表示は <see cref="Option"/> のリストで与える。
/// </summary>
public class UiSelectField : VisualElement
{
    public readonly struct Option
    {
        public readonly string Label;
        public readonly string IconClass; // 背景画像を割り当てる USS クラス（null = アイコンなし）
        public readonly Color? IconTint;  // アイコンのティント（null = USS 既定）

        public Option(string label, string iconClass = null, Color? iconTint = null)
        {
            Label = label;
            IconClass = iconClass;
            IconTint = iconTint;
        }
    }

    /// <summary>選択が変わったとき（新しいインデックス）。</summary>
    public event Action<int> SelectionChanged;

    private const float DragThreshold = 6f;

    private readonly VisualElement _icon;
    private readonly Label _label;
    private readonly List<Option> _options = new();
    private int _index = -1;

    private VisualElement _menu;
    private VisualElement _scrim;
    private readonly List<VisualElement> _menuItems = new();
    private int _pointerId = -1;
    private Vector2 _downPos;
    private bool _dragMoved;
    private int _hot = -1;

    private ScrollView _closeOnScroll;
    private Vector2 _lastScroll;
    private IVisualElementScheduledItem _scrollWatch;

    public int Index => _index;

    public UiSelectField()
    {
        AddToClassList("ui-select");

        _icon = new VisualElement { pickingMode = PickingMode.Ignore };
        _icon.AddToClassList("ui-select-icon");
        Add(_icon);

        _label = new Label { pickingMode = PickingMode.Ignore };
        _label.AddToClassList("ui-select-label");
        Add(_label);

        var chevron = new VisualElement { pickingMode = PickingMode.Ignore };
        chevron.AddToClassList("ui-select-chevron");
        Add(chevron);

        RegisterCallback<PointerDownEvent>(OnDown);
        RegisterCallback<PointerMoveEvent>(OnMove);
        RegisterCallback<PointerUpEvent>(OnUp);
        RegisterCallback<DetachFromPanelEvent>(_ => CloseMenu());
    }

    /// <summary>選択肢と初期選択インデックスを設定する。</summary>
    public void SetOptions(IReadOnlyList<Option> options, int index)
    {
        _options.Clear();
        if (options != null)
            _options.AddRange(options);
        _index = Clamp(index);
        UpdateField();
    }

    /// <summary>選択を変更する（notify = true で <see cref="SelectionChanged"/> を発火）。</summary>
    public void SetIndex(int index, bool notify = false)
    {
        int v = Clamp(index);
        if (v == _index)
            return;
        _index = v;
        UpdateField();
        if (notify)
            SelectionChanged?.Invoke(_index);
    }

    private int Clamp(int i) => _options.Count == 0 ? -1 : Mathf.Clamp(i, 0, _options.Count - 1);

    private void UpdateField()
    {
        if (_index >= 0 && _index < _options.Count)
            ApplyOption(_icon, _label, _options[_index]);
        else
        {
            ResetIcon(_icon);
            _icon.style.display = DisplayStyle.None;
            _label.text = "";
        }
    }

    private static void ResetIcon(VisualElement icon)
    {
        icon.ClearClassList();
        icon.AddToClassList("ui-select-icon");
        icon.style.unityBackgroundImageTintColor = StyleKeyword.Null;
    }

    private static void ApplyOption(VisualElement icon, Label label, in Option o)
    {
        ResetIcon(icon);
        if (!string.IsNullOrEmpty(o.IconClass))
        {
            icon.AddToClassList(o.IconClass);
            icon.style.display = DisplayStyle.Flex;
            if (o.IconTint.HasValue)
                icon.style.unityBackgroundImageTintColor = o.IconTint.Value;
        }
        else
        {
            icon.style.display = DisplayStyle.None;
        }
        label.text = o.Label ?? "";
    }

    // ── ポインター処理（押下でメニューを開き、ドラッグ→離すで即選択）─────────────────

    private void OnDown(PointerDownEvent e)
    {
        if (_options.Count == 0)
            return;
        if (_menu != null) // 開いていれば閉じる（トグル）
        {
            CloseMenu();
            return;
        }
        _pointerId = e.pointerId;
        _downPos = e.position;
        _dragMoved = false;
        OpenMenu();
        this.CapturePointer(e.pointerId);
        e.StopPropagation();
    }

    private void OnMove(PointerMoveEvent e)
    {
        if (_pointerId != e.pointerId || _menu == null)
            return;
        if (!_dragMoved && ((Vector2)e.position - _downPos).magnitude > DragThreshold)
            _dragMoved = true;
        SetHot(HitTest(e.position));
        e.StopPropagation();
    }

    private void OnUp(PointerUpEvent e)
    {
        if (_pointerId != e.pointerId)
            return;
        this.ReleasePointer(e.pointerId);
        _pointerId = -1;
        int over = HitTest(e.position);
        if (_dragMoved)
        {
            if (over >= 0)
                Choose(over); // ドラッグして項目の上で離した → 即選択して閉じる
            else
                CloseMenu();   // 項目外で離した → 閉じる
        }
        else
        {
            SetHot(-1); // 単なるタップ: メニューは開いたまま（項目タップで選ぶ）
        }
        e.StopPropagation();
    }

    private int HitTest(Vector2 pos)
    {
        for (int i = 0; i < _menuItems.Count; i++)
            if (_menuItems[i].worldBound.Contains(pos))
                return i;
        return -1;
    }

    private void SetHot(int i)
    {
        if (i == _hot)
            return;
        if (_hot >= 0 && _hot < _menuItems.Count)
            _menuItems[_hot].RemoveFromClassList("ui-select-option--hot");
        _hot = i;
        if (_hot >= 0 && _hot < _menuItems.Count)
            _menuItems[_hot].AddToClassList("ui-select-option--hot");
    }

    private void Choose(int i)
    {
        CloseMenu();
        if (i < 0 || i >= _options.Count || i == _index)
            return;
        _index = i;
        UpdateField();
        SelectionChanged?.Invoke(_index);
    }

    // ── メニューの開閉（クリップされない panel root に浮かせる）────────────────────

    private void OpenMenu()
    {
        var root = panel?.visualTree;
        if (root == null)
            return;

        _scrim = new VisualElement();
        _scrim.AddToClassList("ui-popup-scrim");
        _scrim.RegisterCallback<PointerDownEvent>(ev =>
        {
            CloseMenu();
            ev.StopPropagation();
        });
        InheritStyleSheets(_scrim);
        root.Add(_scrim);

        var sv = new ScrollView(ScrollViewMode.Vertical)
        {
            horizontalScrollerVisibility = ScrollerVisibility.Hidden,
            verticalScrollerVisibility = ScrollerVisibility.Auto,
        };
        sv.AddToClassList("ui-select-menu");
        // panel root は WorldEditor.uss のサブツリー外なので、フィールドの祖先が持つ
        // スタイルシートを引き継がないと .ui-select-menu / アイコン等が解決されない。
        InheritStyleSheets(sv);
        _menu = sv;
        _menuItems.Clear();
        _hot = -1;

        for (int i = 0; i < _options.Count; i++)
        {
            int idx = i;
            var item = new Button(() => Choose(idx)) { text = "" };
            item.AddToClassList("ui-select-option");
            var ic = new VisualElement { pickingMode = PickingMode.Ignore };
            var lb = new Label { pickingMode = PickingMode.Ignore };
            lb.AddToClassList("ui-select-option-label");
            ApplyOption(ic, lb, _options[i]);
            item.Add(ic);
            item.Add(lb);
            if (i == _index)
                item.AddToClassList("ui-select-option--selected");
            _menuItems.Add(item);
            sv.Add(item);
        }
        root.Add(sv);

        // サイズ確定後に位置決め（worldBound が必要なので一旦隠す）。
        sv.style.visibility = Visibility.Hidden;
        sv.RegisterCallback<GeometryChangedEvent>(OnMenuGeometry);

        _closeOnScroll = GetFirstAncestorOfType<ScrollView>();
        if (_closeOnScroll != null)
        {
            _lastScroll = _closeOnScroll.scrollOffset;
            _scrollWatch = schedule.Execute(WatchScroll).Every(16);
        }
    }

    private void OnMenuGeometry(GeometryChangedEvent _)
    {
        if (_menu == null)
            return;
        _menu.UnregisterCallback<GeometryChangedEvent>(OnMenuGeometry);
        var root = panel?.visualTree;
        if (root == null)
            return;

        Rect r = root.worldBound;
        Rect f = worldBound;
        float left = f.xMin - r.xMin;
        float top = f.yMax - r.yMin + 2f; // フィールドの下に生やす
        _menu.style.left = left;
        _menu.style.top = top;
        _menu.style.width = f.width;
        float maxH = r.height - top - 6f;
        if (maxH > 0f)
            _menu.style.maxHeight = maxH; // 画面下に収める（多すぎる項目はメニュー内スクロール）
        _menu.style.visibility = Visibility.Visible;
    }

    // フィールドの祖先が持つ全スタイルシートを target に引き継ぐ（panel root に浮かせても
    // USS が効くように）。重複追加は Contains で防ぐ。
    private void InheritStyleSheets(VisualElement target)
    {
        for (VisualElement a = this; a != null; a = a.parent)
            for (int s = 0; s < a.styleSheets.count; s++)
            {
                var sheet = a.styleSheets[s];
                if (sheet != null && !target.styleSheets.Contains(sheet))
                    target.styleSheets.Add(sheet);
            }
    }

    private void WatchScroll()
    {
        if (_closeOnScroll == null)
            return;
        if ((_closeOnScroll.scrollOffset - _lastScroll).sqrMagnitude > 0.01f)
            CloseMenu();
    }

    private void CloseMenu()
    {
        _scrollWatch?.Pause();
        _scrollWatch = null;
        _closeOnScroll = null;
        if (_pointerId != -1)
        {
            this.ReleasePointer(_pointerId);
            _pointerId = -1;
        }
        _hot = -1;
        _menuItems.Clear();
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
}
