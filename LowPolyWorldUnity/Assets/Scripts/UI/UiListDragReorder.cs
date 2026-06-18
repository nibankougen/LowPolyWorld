using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// フラットなリスト（各行が <c>container</c> の直接の子）に対して、ドラッグハンドルでの
/// 並べ替え D&D を提供する再利用ヘルパー。ギミックタブのツリー D&D（GimmickTabController）を
/// 平坦リスト向けに一般化したもの。USS クラス（gimmick-drag-handle / gimmick-drop-line /
/// gimmick-drag-ghost / gimmick-row-dragging）は WorldEditor.uss を共有する。
///
/// 使い方: リスト再構築のたびに <see cref="Reset"/> → 各行で <see cref="CreateHandle"/> を呼んで
/// 返ったハンドルを行に差し込む（登録順 = 表示順）。ドロップ時に
/// <c>onReorder(fromIndex, toIndex)</c> が呼ばれる（toIndex = 取り除いた後の挿入先インデックス）。
/// onReorder 側でデータを動かし UI を再構築する。
/// </summary>
public class UiListDragReorder
{
    private const float DragThreshold = 6f;
    private const float AutoScrollMargin = 52f;
    private const float AutoScrollMaxSpeed = 16f;

    private readonly VisualElement _container;
    private readonly ScrollView _scroll;
    private readonly Action<int, int> _onReorder;

    private readonly List<VisualElement> _rows = new();
    private readonly List<string> _labels = new();

    private VisualElement _dragRow;
    private int _dragPointerId = -1;
    private float _dragStartY;
    private float _lastPointerY;
    private bool _dragging;
    private VisualElement _dragHandle;
    private VisualElement _dropLine;
    private VisualElement _ghost;
    private IVisualElementScheduledItem _autoScroll;

    /// <param name="container">行を直接の子に持つリスト要素。</param>
    /// <param name="scroll">スクロール対象の ScrollView（端でのオートスクロール用・無ければ null）。</param>
    /// <param name="onReorder">ドロップ確定時のコールバック（from→to の並べ替え）。</param>
    public UiListDragReorder(VisualElement container, ScrollView scroll, Action<int, int> onReorder)
    {
        _container = container;
        _scroll = scroll;
        _onReorder = onReorder;
    }

    /// <summary>
    /// リスト要素から自動でヘルパーを作る。listElement が ScrollView なら行の親 = その
    /// contentContainer・スクロール対象 = その ScrollView。素の要素なら親 = 自身・スクロールは
    /// 祖先の ScrollView を使う。
    /// </summary>
    public static UiListDragReorder For(VisualElement listElement, Action<int, int> onReorder)
    {
        VisualElement container = listElement;
        var scroll = listElement as ScrollView;
        if (scroll != null)
            container = scroll.contentContainer;
        else
            scroll = listElement.GetFirstAncestorOfType<ScrollView>();
        return new UiListDragReorder(container, scroll, onReorder);
    }

    /// <summary>リスト再構築の先頭で呼ぶ（登録済み行をクリア）。</summary>
    public void Reset()
    {
        _rows.Clear();
        _labels.Clear();
    }

    /// <summary>row を登録し、その行に差し込むドラッグハンドルを返す。ghostLabel はドラッグ中の追従表示。</summary>
    public VisualElement CreateHandle(VisualElement row, string ghostLabel = "")
    {
        _rows.Add(row);
        _labels.Add(ghostLabel ?? "");

        var h = new VisualElement { tooltip = "ドラッグして並べ替え" };
        h.AddToClassList("gimmick-drag-handle");
        h.RegisterCallback<PointerDownEvent>(e => OnDown(e, row, h));
        h.RegisterCallback<PointerMoveEvent>(OnMove);
        h.RegisterCallback<PointerUpEvent>(OnUp);
        return h;
    }

    // ── ポインター処理 ──────────────────────────────────────────────────────────

    private void OnDown(PointerDownEvent e, VisualElement row, VisualElement handle)
    {
        _dragRow = row;
        _dragPointerId = e.pointerId;
        _dragStartY = e.position.y;
        _lastPointerY = e.position.y;
        _dragging = false;
        _dragHandle = handle;
        handle.CapturePointer(e.pointerId);
        e.StopPropagation();
    }

    private void OnMove(PointerMoveEvent e)
    {
        if (_dragRow == null || e.pointerId != _dragPointerId)
            return;
        _lastPointerY = e.position.y;
        if (!_dragging)
        {
            if (Mathf.Abs(e.position.y - _dragStartY) < DragThreshold)
                return;
            _dragging = true;
            BeginVisual();
            StartAutoScroll();
        }
        UpdateLine(e.position.y);
        UpdateGhost(e.position.y);
        e.StopPropagation();
    }

    private void OnUp(PointerUpEvent e)
    {
        if (_dragRow == null || e.pointerId != _dragPointerId)
            return;
        _dragHandle?.ReleasePointer(e.pointerId);

        bool was = _dragging;
        var row = _dragRow;
        float py = _lastPointerY;

        _dragRow = null;
        _dragPointerId = -1;
        _dragging = false;
        _dragHandle = null;
        StopAutoScroll();
        EndVisual(row);

        if (was)
            ApplyDrop(row, py);
        e.StopPropagation();
    }

    // pointerY より上にある行数 = 挿入ギャップ（0.._rows.Count・ドラッグ中の行も含む）。
    private int ComputeInsertIndex(float pointerY)
    {
        int n = 0;
        foreach (var r in _rows)
            if (r.worldBound.center.y < pointerY)
                n++;
        return n;
    }

    private void ApplyDrop(VisualElement row, float pointerY)
    {
        int from = _rows.IndexOf(row);
        if (from < 0)
            return;
        int rawInsert = ComputeInsertIndex(pointerY);
        int to = rawInsert > from ? rawInsert - 1 : rawInsert; // 取り除いた後の挿入先
        if (to != from)
            _onReorder(from, to);
    }

    // ── 視覚フィードバック ──────────────────────────────────────────────────────

    private void BeginVisual()
    {
        _dragRow.AddToClassList("gimmick-row-dragging");

        if (_dropLine == null)
        {
            _dropLine = new VisualElement { pickingMode = PickingMode.Ignore };
            _dropLine.AddToClassList("gimmick-drop-line");
        }
        _container.Add(_dropLine);

        int idx = _rows.IndexOf(_dragRow);
        string label = idx >= 0 ? _labels[idx] : "";
        if (!string.IsNullOrEmpty(label))
        {
            _ghost = new VisualElement { pickingMode = PickingMode.Ignore };
            _ghost.AddToClassList("gimmick-drag-ghost");
            var lbl = new Label(label) { pickingMode = PickingMode.Ignore };
            lbl.AddToClassList("gimmick-drag-ghost-label");
            _ghost.Add(lbl);
            _container.Add(_ghost);
        }
    }

    private void UpdateLine(float pointerY)
    {
        if (_dropLine == null || _rows.Count == 0)
            return;
        int rawInsert = ComputeInsertIndex(pointerY);
        float top;
        if (rawInsert <= 0)
            top = _rows[0].layout.yMin;
        else if (rawInsert >= _rows.Count)
            top = _rows[_rows.Count - 1].layout.yMax;
        else
            top = (_rows[rawInsert - 1].layout.yMax + _rows[rawInsert].layout.yMin) * 0.5f;
        _dropLine.style.top = top;
    }

    private void UpdateGhost(float pointerY)
    {
        if (_ghost == null)
            return;
        float localY = pointerY - _container.worldBound.yMin;
        float h = _ghost.resolvedStyle.height;
        if (h <= 0f)
            h = 34f;
        _ghost.style.top = localY - h * 0.5f;
    }

    private void EndVisual(VisualElement row)
    {
        row?.RemoveFromClassList("gimmick-row-dragging");
        _dropLine?.RemoveFromHierarchy();
        _dropLine = null;
        _ghost?.RemoveFromHierarchy();
        _ghost = null;
    }

    // ── 端でのオートスクロール ──────────────────────────────────────────────────

    private void StartAutoScroll()
    {
        if (_scroll == null || _autoScroll != null)
            return;
        _autoScroll = _container.schedule.Execute(AutoScrollTick).Every(16);
    }

    private void StopAutoScroll()
    {
        _autoScroll?.Pause();
        _autoScroll = null;
    }

    private void AutoScrollTick()
    {
        if (!_dragging || _scroll == null)
            return;

        var vp = _scroll.contentViewport.worldBound;
        float dy = 0f;
        if (_lastPointerY < vp.yMin + AutoScrollMargin)
        {
            float t = Mathf.Clamp01((vp.yMin + AutoScrollMargin - _lastPointerY) / AutoScrollMargin);
            dy = -AutoScrollMaxSpeed * t;
        }
        else if (_lastPointerY > vp.yMax - AutoScrollMargin)
        {
            float t = Mathf.Clamp01((_lastPointerY - (vp.yMax - AutoScrollMargin)) / AutoScrollMargin);
            dy = AutoScrollMaxSpeed * t;
        }
        if (Mathf.Approximately(dy, 0f))
            return;

        float lo = _scroll.verticalScroller.lowValue;
        float hi = _scroll.verticalScroller.highValue;
        float newY = Mathf.Clamp(_scroll.scrollOffset.y + dy, lo, hi);
        if (Mathf.Approximately(newY, _scroll.scrollOffset.y))
            return;

        _scroll.scrollOffset = new Vector2(_scroll.scrollOffset.x, newY);
        UpdateLine(_lastPointerY);
        UpdateGhost(_lastPointerY);
    }
}
