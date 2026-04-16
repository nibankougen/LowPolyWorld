using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// テクスチャペイントタブの MonoBehaviour。
/// UI Toolkit の VisualElement イベントを受け取り、AvatarPaintSession / BrushSettingsLogic /
/// ColorPickerLogic / PaintCanvasLogic を協調させてペイント操作を実現する。
/// </summary>
public class TexturePaintController : MonoBehaviour
{
    // ---- 外部参照（AvatarEditController から設定） ----

    private VisualElement _contentTexture;
    private IPaintSession _session;
    private Action _onHistoryChanged;

    /// <summary>コンポジット更新のたびに呼ばれるコールバック（3D プレビュー反映用）。</summary>
    private Action<Texture2D> _onPreviewUpdated;

    /// <summary>保存時に呼ばれるコールバック（Atlas 反映用）。引数は RGBA byte[]。</summary>
    private Action<byte[]> _onSaveRgba;

    // ---- ロジック ----

    private BrushSettingsLogic _brush;
    private ColorPickerLogic _colorPicker;
    private PaintCanvasLogic _canvasLogic;

    // ---- UI 要素 ----

    private VisualElement _paintCanvas;
    private VisualElement _paintCanvasContainer;
    private Slider _sliderBrushSize;
    private Label _labelBrushSize;
    private Label _labelToolHint;
    private Toggle _toggleAntialias;
    private Button _btnColorSwatch;
    private Button _btnSave;

    // ツールボタン
    private readonly Dictionary<PaintTool, Button> _toolButtons = new();

    // レイヤーパネル
    private VisualElement _layerPanel;
    private VisualElement _layerList;
    private Button _btnLayerPanel;
    private Button _btnAddLayer;
    private Button _btnAddColorAdj;
    private Button _btnCloseLayerPanel;

    // カラーピッカーパネル
    private VisualElement _colorPickerPanel;
    private VisualElement _colorHueRing;
    private VisualElement _colorSvSquare;
    private VisualElement _colorCursor;
    private Slider _sliderAlpha;
    private IntegerField _inputR, _inputG, _inputB, _inputA;
    private VisualElement _colorHistory;
    private Button _btnCloseColorPicker;

    // その他メニューパネル
    private VisualElement _otherMenuPanel;
    private Button _btnOtherMenu;
    private Button _btnCloseOtherMenu;

    // 選択範囲オーバーレイ
    private VisualElement _selectionOverlay;
    private Texture2D _selectionTexture;

    // ---- レイヤーパネル展開状態 ----

    private uint _expandedLayerId;

    // ---- レイヤードラッグ並び替え ----

    private bool _isDraggingLayer;
    private uint _draggingLayerId;
    private int _draggingOriginalIndex;
    private VisualElement _dragGhost;
    private int _dropTargetIndex;

    // ---- ストローク追跡 ----

    private bool _isStroking;
    private (int x, int y) _strokeStart;
    private uint _activeLayerId;

    // ---- 変形ツール追跡 ----

    private bool _isTransforming;
    private (int x, int y) _transformStart;

    // ---- テクスチャ ----

    private Texture2D _compositeTexture;

    /// <summary>レイヤー ID → サムネイル Texture2D キャッシュ。RefreshLayerPanel で作成・更新、OnDestroy で解放。</summary>
    private readonly Dictionary<uint, Texture2D> _layerThumbnails = new();

    // ---- 初期化（AvatarEditController から呼び出す） ----

    /// <summary>
    /// テクスチャタブの VisualElement とセッションを受け取って初期化する。
    /// </summary>
    /// <param name="onPreviewUpdated">コンポジット更新時に Texture2D を受け取るコールバック（3D プレビュー反映用・任意）。</param>
    /// <param name="onSaveRgba">保存時に RGBA byte[] を受け取るコールバック（Atlas 反映用・任意）。</param>
    public void Initialize(
        VisualElement contentTexture,
        IPaintSession session,
        Action onHistoryChanged,
        Action<Texture2D> onPreviewUpdated = null,
        Action<byte[]> onSaveRgba = null)
    {
        _contentTexture = contentTexture;
        _session = session;
        _onHistoryChanged = onHistoryChanged;
        _onPreviewUpdated = onPreviewUpdated;
        _onSaveRgba = onSaveRgba;

        _brush = new BrushSettingsLogic();
        _colorPicker = new ColorPickerLogic();
        _colorPicker.SetFromRgba(255, 0, 0, 255);

        _canvasLogic = new PaintCanvasLogic(session.CanvasWidth, session.CanvasHeight);

        _compositeTexture = new Texture2D(
            (int)session.CanvasWidth,
            (int)session.CanvasHeight,
            TextureFormat.RGBA32,
            false
        );
        _compositeTexture.filterMode = FilterMode.Point;

        _selectionTexture = new Texture2D(
            (int)session.CanvasWidth,
            (int)session.CanvasHeight,
            TextureFormat.RGBA32,
            false
        );
        _selectionTexture.filterMode = FilterMode.Point;

        BindElements();
        RefreshComposite();
        RefreshLayerPanel();
        UpdateToolUI();
    }

    public void OnTabEnter() { }

    public void OnTabExit() => _session?.ClearHistory();

    // ---- 要素バインド ----

    private void BindElements()
    {
        _paintCanvas = _contentTexture.Q<VisualElement>("paint-canvas");
        _paintCanvasContainer = _contentTexture.Q<VisualElement>("paint-canvas-container");
        _sliderBrushSize = _contentTexture.Q<Slider>("slider-brush-size");
        _labelBrushSize = _contentTexture.Q<Label>("label-brush-size");
        _labelToolHint = _contentTexture.Q<Label>("label-tool-hint");
        _toggleAntialias = _contentTexture.Q<Toggle>("toggle-antialias");
        _btnColorSwatch = _contentTexture.Q<Button>("btn-color-swatch");
        _btnSave = _contentTexture.Q<Button>("btn-save");

        // ツールボタン
        BindToolButton(PaintTool.Brush,         "btn-tool-brush");
        BindToolButton(PaintTool.Eraser,        "btn-tool-eraser");
        BindToolButton(PaintTool.Fill,          "btn-tool-fill");
        BindToolButton(PaintTool.Rect,          "btn-tool-rect");
        BindToolButton(PaintTool.Circle,        "btn-tool-circle");
        BindToolButton(PaintTool.Line,          "btn-tool-line");
        BindToolButton(PaintTool.SelectRect,    "btn-tool-select-rect");
        BindToolButton(PaintTool.SelectEllipse, "btn-tool-select-ellipse");
        BindToolButton(PaintTool.Transform,     "btn-tool-transform");

        // レイヤーパネル
        _layerPanel = _contentTexture.Q<VisualElement>("layer-panel");
        _layerList = _contentTexture.Q<VisualElement>("layer-list");
        _btnLayerPanel = _contentTexture.Q<Button>("btn-layer-panel");
        _btnAddLayer = _contentTexture.Q<Button>("btn-add-layer");
        _btnAddColorAdj = _contentTexture.Q<Button>("btn-add-color-adj");
        _btnCloseLayerPanel = _contentTexture.Q<Button>("btn-close-layer-panel");

        // カラーピッカーパネル
        _colorPickerPanel = _contentTexture.Q<VisualElement>("color-picker-panel");
        _colorHueRing = _contentTexture.Q<VisualElement>("color-hue-ring");
        _colorSvSquare = _contentTexture.Q<VisualElement>("color-sv-square");
        _colorCursor = _contentTexture.Q<VisualElement>("color-cursor");
        _sliderAlpha = _contentTexture.Q<Slider>("slider-alpha");
        _inputR = _contentTexture.Q<IntegerField>("input-r");
        _inputG = _contentTexture.Q<IntegerField>("input-g");
        _inputB = _contentTexture.Q<IntegerField>("input-b");
        _inputA = _contentTexture.Q<IntegerField>("input-a");
        _colorHistory = _contentTexture.Q<VisualElement>("color-history");
        _btnCloseColorPicker = _contentTexture.Q<Button>("btn-close-color-picker");

        // イベント登録
        _sliderBrushSize?.RegisterValueChangedCallback(e =>
        {
            _brush.BrushSize = Mathf.RoundToInt(e.newValue);
            if (_labelBrushSize != null)
                _labelBrushSize.text = _brush.BrushSize.ToString();
        });

        _toggleAntialias?.RegisterValueChangedCallback(e => _brush.Antialiased = e.newValue);

        _btnColorSwatch?.RegisterCallback<ClickEvent>(_ => TogglePanel(_colorPickerPanel));
        _btnLayerPanel?.RegisterCallback<ClickEvent>(_ => TogglePanel(_layerPanel));
        _btnCloseLayerPanel?.RegisterCallback<ClickEvent>(_ => ClosePanel(_layerPanel));
        _btnCloseColorPicker?.RegisterCallback<ClickEvent>(_ => ClosePanel(_colorPickerPanel));

        _btnAddLayer?.RegisterCallback<ClickEvent>(_ => OnAddLayer());
        _btnAddColorAdj?.RegisterCallback<ClickEvent>(_ => OnAddColorAdjLayer());
        _btnSave?.RegisterCallback<ClickEvent>(_ => OnSave());

        // その他メニューパネル
        _otherMenuPanel = _contentTexture.Q<VisualElement>("other-menu-panel");
        _btnOtherMenu = _contentTexture.Q<Button>("btn-other-menu");
        _btnCloseOtherMenu = _contentTexture.Q<Button>("btn-close-other-menu");
        _btnOtherMenu?.RegisterCallback<ClickEvent>(_ => TogglePanel(_otherMenuPanel));
        _btnCloseOtherMenu?.RegisterCallback<ClickEvent>(_ => ClosePanel(_otherMenuPanel));
        _contentTexture.Q<Button>("btn-export-png")?.RegisterCallback<ClickEvent>(_ => OnExportPng());
        _contentTexture.Q<Button>("btn-texture-resize")?.RegisterCallback<ClickEvent>(_ => OnTextureResize());
        _contentTexture.Q<Button>("btn-import-layer")?.RegisterCallback<ClickEvent>(_ => OnImportLayerPng());
        _contentTexture.Q<Button>("btn-texture-switch")?.RegisterCallback<ClickEvent>(_ =>
            Debug.Log("[TexturePaintController] 簡単テクスチャ切り替え (Phase 4 保留: docs/phase4-deferred.md)"));

        // 選択範囲オーバーレイ
        _selectionOverlay = _contentTexture.Q<VisualElement>("selection-overlay");

        // カラーピッカー RGBA 入力
        _inputR?.RegisterValueChangedCallback(e => OnRgbaFieldChanged());
        _inputG?.RegisterValueChangedCallback(e => OnRgbaFieldChanged());
        _inputB?.RegisterValueChangedCallback(e => OnRgbaFieldChanged());
        _inputA?.RegisterValueChangedCallback(e => OnRgbaFieldChanged());
        _sliderAlpha?.RegisterValueChangedCallback(e =>
        {
            _colorPicker.Alpha = e.newValue / 255f;
            SyncColorPickerUI(syncFields: true);
        });

        // SV 四角形クリック
        _colorSvSquare?.RegisterCallback<PointerDownEvent>(OnSvSquarePointer);
        _colorSvSquare?.RegisterCallback<PointerMoveEvent>(OnSvSquarePointer);

        // 色相リングクリック
        _colorHueRing?.RegisterCallback<PointerDownEvent>(OnHueRingPointer);
        _colorHueRing?.RegisterCallback<PointerMoveEvent>(OnHueRingPointer);

        // キャンバスポインターイベント
        _paintCanvas?.RegisterCallback<PointerDownEvent>(OnCanvasPointerDown);
        _paintCanvas?.RegisterCallback<PointerMoveEvent>(OnCanvasPointerMove);
        _paintCanvas?.RegisterCallback<PointerUpEvent>(OnCanvasPointerUp);
        _paintCanvas?.RegisterCallback<PointerLeaveEvent>(OnCanvasPointerLeave);

        // ホイールズーム
        _paintCanvasContainer?.RegisterCallback<WheelEvent>(OnCanvasWheel);

        // 色変化コールバック
        _colorPicker.OnColorChanged += () => SyncColorPickerUI(syncFields: true);

        // コンテナサイズ確定後に fit
        _paintCanvasContainer?.RegisterCallback<GeometryChangedEvent>(e =>
        {
            _canvasLogic.FitToContainer(e.newRect.size);
            ApplyCanvasTransform();
        });

        // アクティブレイヤー初期値
        var firstLayer = _session?.LayerStack.Layers.Count > 0
            ? _session.LayerStack.Layers[0]
            : null;
        _activeLayerId = firstLayer?.Id ?? 0;
    }

    private void BindToolButton(PaintTool tool, string name)
    {
        var btn = _contentTexture.Q<Button>(name);
        if (btn == null)
            return;
        _toolButtons[tool] = btn;
        btn.RegisterCallback<ClickEvent>(_ => SelectTool(tool));
    }

    // ---- ツール選択 ----

    private void SelectTool(PaintTool tool)
    {
        // 選択ツール以外に切り替えたとき選択範囲をクリア
        bool wasSelectTool = _brush.Tool == PaintTool.SelectRect || _brush.Tool == PaintTool.SelectEllipse;
        bool isSelectTool = tool == PaintTool.SelectRect || tool == PaintTool.SelectEllipse;
        if (wasSelectTool && !isSelectTool)
        {
            _session?.SelectionClear();
            RefreshSelectionOverlay();
        }
        _brush.Tool = tool;
        UpdateToolUI();
    }

    private void UpdateToolUI()
    {
        foreach (var (t, btn) in _toolButtons)
        {
            if (t == _brush.Tool)
                btn.AddToClassList("tool-btn--active");
            else
                btn.RemoveFromClassList("tool-btn--active");
        }
        if (_labelToolHint != null)
            _labelToolHint.text = _brush.GetToolHint();
    }

    // ---- キャンバスイベント ----

    private void OnCanvasPointerDown(PointerDownEvent e)
    {
        if (_session == null || _activeLayerId == 0)
            return;
        _isStroking = true;
        e.target.CapturePointer(e.pointerId);
        var (cx, cy) = _canvasLogic.ScreenToCanvas(e.localPosition);
        _strokeStart = (cx, cy);
        ApplyTool(cx, cy, isFinalPoint: false);
    }

    private void OnCanvasPointerMove(PointerMoveEvent e)
    {
        if (!_isStroking)
            return;
        var (cx, cy) = _canvasLogic.ScreenToCanvas(e.localPosition);
        // ストローク中ドラッグはブラシ・消しゴムのみ逐次描画
        if (_brush.Tool == PaintTool.Brush || _brush.Tool == PaintTool.Eraser)
            ApplyTool(cx, cy, isFinalPoint: false);
    }

    private void OnCanvasPointerUp(PointerUpEvent e)
    {
        if (!_isStroking)
            return;
        _isStroking = false;
        e.target.ReleasePointer(e.pointerId);
        var (cx, cy) = _canvasLogic.ScreenToCanvas(e.localPosition);
        ApplyTool(cx, cy, isFinalPoint: true);
        RefreshActiveLayerThumbnail();
        _onHistoryChanged?.Invoke();
    }

    private void OnCanvasPointerLeave(PointerLeaveEvent e)
    {
        if (_isStroking)
        {
            _isStroking = false;
            _onHistoryChanged?.Invoke();
        }
    }

    private void OnCanvasWheel(WheelEvent e)
    {
        _canvasLogic.ZoomByWheel(-e.delta.y, e.localMousePosition);
        ApplyCanvasTransform();
        e.StopPropagation();
    }

    // ---- ツール適用 ----

    private void ApplyTool(int cx, int cy, bool isFinalPoint)
    {
        if (_session == null || _activeLayerId == 0)
            return;

        var (r, g, b, a) = _colorPicker.GetRgba();
        uint radius = (uint)(_brush.BrushSize / 2);

        switch (_brush.Tool)
        {
            case PaintTool.Brush:
                _session.Brush(_activeLayerId, cx, cy, radius, r, g, b, a, _brush.Antialiased);
                RefreshComposite();
                break;

            case PaintTool.Eraser:
                _session.Eraser(_activeLayerId, cx, cy, radius);
                RefreshComposite();
                break;

            case PaintTool.Fill:
                if (isFinalPoint)
                {
                    _session.FloodFill(_activeLayerId, cx, cy, r, g, b, a, 0);
                    RefreshComposite();
                }
                break;

            case PaintTool.Rect:
                if (isFinalPoint)
                {
                    _session.DrawRect(_activeLayerId,
                        _strokeStart.x, _strokeStart.y, cx, cy,
                        r, g, b, a, _brush.Filled);
                    RefreshComposite();
                }
                break;

            case PaintTool.Circle:
                if (isFinalPoint)
                {
                    _session.DrawCircle(_activeLayerId,
                        _strokeStart.x, _strokeStart.y, cx, cy,
                        r, g, b, a, _brush.Filled);
                    RefreshComposite();
                }
                break;

            case PaintTool.Line:
                if (isFinalPoint)
                {
                    _session.DrawLine(_activeLayerId,
                        _strokeStart.x, _strokeStart.y, cx, cy,
                        r, g, b, a);
                    RefreshComposite();
                }
                break;

            case PaintTool.SelectRect:
                if (isFinalPoint)
                {
                    _session.SelectionSetRect(_strokeStart.x, _strokeStart.y, cx, cy);
                    RefreshSelectionOverlay();
                }
                break;

            case PaintTool.SelectEllipse:
                if (isFinalPoint)
                {
                    _session.SelectionSetEllipse(_strokeStart.x, _strokeStart.y, cx, cy);
                    RefreshSelectionOverlay();
                }
                break;

            case PaintTool.Transform:
                if (isFinalPoint && _activeLayerId != 0)
                {
                    int dx = cx - _strokeStart.x;
                    int dy = cy - _strokeStart.y;
                    if (dx != 0 || dy != 0)
                    {
                        _session.TranslateLayer(_activeLayerId, dx, dy);
                        RefreshComposite();
                        RefreshActiveLayerThumbnail();
                    }
                }
                break;
        }
    }

    // ---- キャンバス表示更新 ----

    private void RefreshComposite()
    {
        if (_session == null || _paintCanvas == null || _compositeTexture == null)
            return;

        byte[] rgba = _session.CompositeRgba();
        if (rgba == null)
            return;

        _compositeTexture.SetPixelData(rgba, 0);
        _compositeTexture.Apply();
        _paintCanvas.style.backgroundImage = Background.FromTexture2D(_compositeTexture);
        _onPreviewUpdated?.Invoke(_compositeTexture);
    }

    private void ApplyCanvasTransform()
    {
        if (_paintCanvas == null)
            return;
        var rect = _canvasLogic.GetCanvasRect();
        _paintCanvas.style.position = Position.Absolute;
        _paintCanvas.style.left = rect.x;
        _paintCanvas.style.top = rect.y;
        _paintCanvas.style.width = rect.width;
        _paintCanvas.style.height = rect.height;
    }

    // ---- レイヤーパネル ----

    private void OnAddLayer()
    {
        _session?.AddNormalLayer();
        RefreshLayerPanel();
        _onHistoryChanged?.Invoke();
    }

    private void OnAddColorAdjLayer()
    {
        _session?.AddColorAdjustmentLayer();
        RefreshLayerPanel();
        _onHistoryChanged?.Invoke();
    }

    private void RefreshLayerPanel()
    {
        if (_layerList == null || _session == null)
            return;

        _layerList.Clear();

        // UV オーバーレイエントリ（最上段・編集不可）
        if (_session.HasUvOverlay)
            _layerList.Add(BuildUvOverlayItem());

        // 色調補正レイヤー
        if (_session.LayerStack.HasColorAdjustment)
            _layerList.Add(BuildLayerItem(_session.LayerStack.ColorAdjustmentLayer));

        // 通常レイヤーを上から（スタックの末尾から）表示
        var layers = _session.LayerStack.Layers;
        for (int i = layers.Count - 1; i >= 0; i--)
            _layerList.Add(BuildLayerItem(layers[i]));

        // ベースレイヤー（最下段・読み取り専用）
        _layerList.Add(BuildBaseLayerItem());

        // 削除済みレイヤーのサムネイルを解放
        var currentIds = new HashSet<uint>();
        foreach (var l in _session.LayerStack.Layers)
            currentIds.Add(l.Id);
        if (_session.LayerStack.HasColorAdjustment)
            currentIds.Add(_session.LayerStack.ColorAdjustmentLayer.Id);

        var toRemove = new List<uint>();
        foreach (var id in _layerThumbnails.Keys)
            if (!currentIds.Contains(id))
                toRemove.Add(id);
        foreach (var id in toRemove)
        {
            if (_layerThumbnails[id] != null)
                Destroy(_layerThumbnails[id]);
            _layerThumbnails.Remove(id);
        }
    }

    /// <summary>UV オーバーレイの表示切り替え専用アイテムを生成する（編集不可）。</summary>
    private VisualElement BuildUvOverlayItem()
    {
        var item = new VisualElement();
        item.AddToClassList("layer-item");
        item.AddToClassList("layer-item--base");

        var mainRow = new VisualElement();
        mainRow.AddToClassList("layer-item-main-row");

        var thumb = new VisualElement();
        thumb.AddToClassList("layer-thumb");

        var nameLabel = new Label("UV");
        nameLabel.AddToClassList("layer-name");

        var visBtn = new Button();
        visBtn.AddToClassList("layer-visible-btn");
        visBtn.text = _session.UvOverlayVisible ? "●" : "○";
        if (!_session.UvOverlayVisible)
            visBtn.AddToClassList("layer-visible-btn--hidden");
        visBtn.RegisterCallback<ClickEvent>(e =>
        {
            e.StopPropagation();
            _session.SetUvOverlayVisible(!_session.UvOverlayVisible);
            RefreshLayerPanel();
            RefreshComposite();
        });

        mainRow.Add(thumb);
        mainRow.Add(nameLabel);
        mainRow.Add(visBtn);
        item.Add(mainRow);

        return item;
    }

    /// <summary>ベースレイヤーの読み取り専用アイテムを生成する。</summary>
    private VisualElement BuildBaseLayerItem()
    {
        var item = new VisualElement();
        item.AddToClassList("layer-item");
        item.AddToClassList("layer-item--base");

        var mainRow = new VisualElement();
        mainRow.AddToClassList("layer-item-main-row");

        var thumb = new VisualElement();
        thumb.AddToClassList("layer-thumb");

        var nameLabel = new Label("ベース");
        nameLabel.AddToClassList("layer-name");

        var lockIcon = new Label("🔒");
        lockIcon.style.color = new UnityEngine.UIElements.StyleColor(new UnityEngine.Color(0.6f, 0.6f, 0.6f));
        lockIcon.style.fontSize = 11;

        mainRow.Add(thumb);
        mainRow.Add(nameLabel);
        mainRow.Add(lockIcon);
        item.Add(mainRow);

        return item;
    }

    private VisualElement BuildLayerItem(PaintLayer layer)
    {
        uint layerId = layer.Id;

        var item = new VisualElement();
        item.AddToClassList("layer-item");
        if (layerId == _activeLayerId)
            item.AddToClassList("layer-item--selected");

        // ---- 主行 ----
        var mainRow = new VisualElement();
        mainRow.AddToClassList("layer-item-main-row");

        var thumb = new VisualElement();
        thumb.AddToClassList("layer-thumb");
        var thumbTex = GetOrCreateLayerThumbnail(layer);
        if (thumbTex != null)
            thumb.style.backgroundImage = Background.FromTexture2D(thumbTex);

        var nameLabel = new Label(layer.Name);
        nameLabel.AddToClassList("layer-name");
        if (layer.Locked)
            nameLabel.AddToClassList("layer-name--locked");

        var visBtn = new Button();
        visBtn.AddToClassList("layer-visible-btn");
        visBtn.text = layer.Visible ? "●" : "○";
        if (!layer.Visible)
            visBtn.AddToClassList("layer-visible-btn--hidden");
        visBtn.RegisterCallback<ClickEvent>(e =>
        {
            e.StopPropagation();
            _session?.SetLayerVisible(layerId, !layer.Visible);
            RefreshLayerPanel();
            RefreshComposite();
        });

        var expandBtn = new Button();
        expandBtn.AddToClassList("layer-expand-btn");
        expandBtn.text = layerId == _expandedLayerId ? "▾" : "▸";
        expandBtn.RegisterCallback<ClickEvent>(e =>
        {
            e.StopPropagation();
            _expandedLayerId = _expandedLayerId == layerId ? 0 : layerId;
            RefreshLayerPanel();
        });

        mainRow.Add(thumb);
        mainRow.Add(nameLabel);
        mainRow.Add(visBtn);
        mainRow.Add(expandBtn);

        // ---- アクション行（展開時のみ表示） ----
        var actionsRow = new VisualElement();
        actionsRow.AddToClassList("layer-item-actions");
        if (layerId != _expandedLayerId)
            actionsRow.AddToClassList("layer-item-actions--hidden");

        // 不透明度行
        var opacityRow = new VisualElement();
        opacityRow.AddToClassList("layer-opacity-row");

        var opacityLabel = new Label("不透明");
        opacityLabel.AddToClassList("layer-opacity-label");

        var opacitySlider = new Slider(0f, 100f) { value = layer.Opacity * 100f };
        opacitySlider.AddToClassList("layer-opacity-slider");

        var opacityValue = new Label($"{Mathf.RoundToInt(layer.Opacity * 100)}%");
        opacityValue.AddToClassList("layer-opacity-value");

        opacitySlider.RegisterValueChangedCallback(e =>
        {
            _session?.SetLayerOpacity(layerId, e.newValue / 100f);
            opacityValue.text = $"{Mathf.RoundToInt(e.newValue)}%";
            RefreshComposite();
        });

        opacityRow.Add(opacityLabel);
        opacityRow.Add(opacitySlider);
        opacityRow.Add(opacityValue);

        // ボタン行（ロック / マスク / 削除）
        var btnRow = new VisualElement();
        btnRow.AddToClassList("layer-action-btn-row");

        var lockBtn = new Button();
        lockBtn.AddToClassList("layer-action-btn");
        lockBtn.text = "L";
        if (layer.Locked)
            lockBtn.AddToClassList("layer-action-btn--active");
        lockBtn.RegisterCallback<ClickEvent>(e =>
        {
            e.StopPropagation();
            _session?.SetLayerLocked(layerId, !layer.Locked);
            RefreshLayerPanel();
        });

        var maskBtn = new Button();
        maskBtn.AddToClassList("layer-action-btn");
        maskBtn.text = "M";
        if (layer.MaskBelow)
            maskBtn.AddToClassList("layer-action-btn--active");
        maskBtn.RegisterCallback<ClickEvent>(e =>
        {
            e.StopPropagation();
            _session?.SetLayerMaskBelow(layerId, !layer.MaskBelow);
            RefreshLayerPanel();
            RefreshComposite();
        });

        var deleteBtn = new Button();
        deleteBtn.AddToClassList("layer-action-btn");
        deleteBtn.AddToClassList("layer-action-btn--delete");
        deleteBtn.text = "×";
        deleteBtn.RegisterCallback<ClickEvent>(e =>
        {
            e.StopPropagation();
            _session?.RemoveLayer(layerId);
            if (_activeLayerId == layerId)
            {
                var remaining = _session?.LayerStack.Layers;
                _activeLayerId = remaining is { Count: > 0 } ? remaining[^1].Id : 0;
            }
            if (_expandedLayerId == layerId)
                _expandedLayerId = 0;
            RefreshLayerPanel();
            RefreshComposite();
            _onHistoryChanged?.Invoke();
        });

        btnRow.Add(lockBtn);
        btnRow.Add(maskBtn);
        btnRow.Add(deleteBtn);

        actionsRow.Add(opacityRow);
        actionsRow.Add(btnRow);

        item.Add(mainRow);
        item.Add(actionsRow);

        // 主行クリックでアクティブレイヤー変更（ボタン以外）
        item.RegisterCallback<ClickEvent>(e =>
        {
            if (e.target is Button)
                return;
            _activeLayerId = layerId;
            RefreshLayerPanel();
        });

        // ドラッグ並び替え: 主行の長押し/ドラッグで開始
        mainRow.RegisterCallback<PointerDownEvent>(e =>
        {
            if (e.button != 0) return;
            _isDraggingLayer = false;
            _draggingLayerId = layerId;
            var layers = _session?.LayerStack.Layers;
            _draggingOriginalIndex = -1;
            if (layers != null)
                for (int idx = 0; idx < layers.Count; idx++)
                    if (layers[idx].Id == layerId) { _draggingOriginalIndex = idx; break; }
            mainRow.CapturePointer(e.pointerId);
            e.StopPropagation();
        });

        mainRow.RegisterCallback<PointerMoveEvent>(e =>
        {
            if (_draggingLayerId != layerId || !mainRow.HasPointerCapture(e.pointerId)) return;
            if (!_isDraggingLayer && e.deltaPosition.magnitude > 5f)
            {
                _isDraggingLayer = true;
                if (_dragGhost == null)
                {
                    _dragGhost = new VisualElement();
                    _dragGhost.AddToClassList("layer-drag-ghost");
                    _dragGhost.style.width = _layerList.resolvedStyle.width;
                    _dragGhost.style.height = 44;
                    _layerPanel?.Add(_dragGhost);
                }
            }
            if (_isDraggingLayer && _dragGhost != null)
            {
                var worldPos2D = new Vector2(e.position.x, e.position.y);
                Vector2 localPos;
                if (_layerPanel != null)
                    localPos = _layerPanel.WorldToLocal(worldPos2D);
                else
                    localPos = e.localPosition;
                _dragGhost.style.top = localPos.y - 22;
                _dragGhost.style.left = 8;

                // ドロップ先インデックス計算（レイヤーリスト内での位置）
                var listPos = _layerList.WorldToLocal(worldPos2D);
                var layerCount = _session?.LayerStack.Layers.Count ?? 0;
                float itemH = 46f;
                int rawIdx = Mathf.FloorToInt((float)(listPos.y / itemH));
                _dropTargetIndex = Mathf.Clamp(layerCount - 1 - rawIdx, 0, layerCount - 1);
            }
            e.StopPropagation();
        });

        mainRow.RegisterCallback<PointerUpEvent>(e =>
        {
            if (_draggingLayerId != layerId || !mainRow.HasPointerCapture(e.pointerId)) return;
            mainRow.ReleasePointer(e.pointerId);

            if (_isDraggingLayer && _draggingOriginalIndex >= 0 && _dropTargetIndex != _draggingOriginalIndex)
            {
                _session?.MoveLayer(layerId, _dropTargetIndex);
            }

            _isDraggingLayer = false;
            _draggingLayerId = 0;
            if (_dragGhost != null)
            {
                _layerPanel?.Remove(_dragGhost);
                _dragGhost = null;
            }

            if (_draggingOriginalIndex != _dropTargetIndex)
            {
                RefreshLayerPanel();
                RefreshComposite();
                _onHistoryChanged?.Invoke();
            }
            e.StopPropagation();
        });

        return item;
    }

    // ---- カラーピッカー ----

    private bool _syncingFields;

    private void OnRgbaFieldChanged()
    {
        if (_syncingFields)
            return;
        int r = Mathf.Clamp(_inputR?.value ?? 255, 0, 255);
        int g = Mathf.Clamp(_inputG?.value ?? 0, 0, 255);
        int b = Mathf.Clamp(_inputB?.value ?? 0, 0, 255);
        int a = Mathf.Clamp(_inputA?.value ?? 255, 0, 255);
        _colorPicker.SetFromRgba((byte)r, (byte)g, (byte)b, (byte)a);
        SyncColorPickerUI(syncFields: false);
    }

    private void SyncColorPickerUI(bool syncFields)
    {
        var color = _colorPicker.GetColor();
        if (_btnColorSwatch != null)
            _btnColorSwatch.style.backgroundColor = new StyleColor(color);

        if (syncFields)
        {
            _syncingFields = true;
            var (r, g, b, a) = _colorPicker.GetRgba();
            if (_inputR != null) _inputR.value = r;
            if (_inputG != null) _inputG.value = g;
            if (_inputB != null) _inputB.value = b;
            if (_inputA != null) _inputA.value = a;
            if (_sliderAlpha != null) _sliderAlpha.value = a;
            _syncingFields = false;
        }

        // SV 四角形のカーソル位置更新
        UpdateColorCursor();
    }

    private void UpdateColorCursor()
    {
        if (_colorSvSquare == null || _colorCursor == null)
            return;
        var (u, v) = _colorPicker.GetSvUV();
        float w = _colorSvSquare.resolvedStyle.width;
        float h = _colorSvSquare.resolvedStyle.height;
        _colorCursor.style.left = u * w - 5f;
        _colorCursor.style.top = v * h - 5f;
    }

    private void OnSvSquarePointer<T>(PointerEventBase<T> e) where T : PointerEventBase<T>, new()
    {
        if (e.isPrimary && (e is PointerDownEvent || (e is PointerMoveEvent && e.pressedButtons != 0)))
        {
            float w = _colorSvSquare.resolvedStyle.width;
            float h = _colorSvSquare.resolvedStyle.height;
            if (w <= 0 || h <= 0) return;
            _colorPicker.SetSaturationValue(e.localPosition.x / w, e.localPosition.y / h);
            SyncColorPickerUI(syncFields: true);
        }
    }

    private void OnHueRingPointer<T>(PointerEventBase<T> e) where T : PointerEventBase<T>, new()
    {
        if (e.isPrimary && (e is PointerDownEvent || (e is PointerMoveEvent && e.pressedButtons != 0)))
        {
            float w = _colorHueRing.resolvedStyle.width;
            float h = _colorHueRing.resolvedStyle.height;
            if (w <= 0 || h <= 0) return;
            float cx = w * 0.5f, cy = h * 0.5f;
            float dx = e.localPosition.x - cx;
            float dy = e.localPosition.y - cy;
            float angle = Mathf.Atan2(dy, dx) / (2f * Mathf.PI);
            _colorPicker.Hue = (angle + 1f) % 1f;
            SyncColorPickerUI(syncFields: true);
        }
    }

    // ---- 保存 ----

    private void OnSave()
    {
        if (_session == null)
            return;
        _colorPicker.PushCurrentToHistory();

        // Atlas 反映（ローカル即時）
        var rgba = _session.CompositeRgba();
        if (rgba != null)
            _onSaveRgba?.Invoke(rgba);

        // TODO: Phase 9 — PNG + レイヤー構造 JSON をサーバーへアップロード
        var png = _session.CompositePng();
        if (png != null)
        {
            _session.CleanupUndo();
            Debug.Log($"[TexturePaintController] 保存 PNG size={png.Length} bytes");
        }
    }

    // ---- PNG 書き出し（その他メニュー） ----

    private void OnExportPng()
    {
        if (_session == null) return;
        var png = _session.CompositePng();
        if (png == null) return;
#if UNITY_EDITOR
        string path = UnityEditor.EditorUtility.SaveFilePanel("PNG 書き出し", "", "texture_export.png", "png");
        if (!string.IsNullOrEmpty(path))
        {
            System.IO.File.WriteAllBytes(path, png);
            Debug.Log($"[TexturePaintController] PNG 書き出し完了: {path}");
        }
#else
        Debug.Log($"[TexturePaintController] PNG 書き出し size={png.Length} bytes (実機: 保存処理は未実装)");
#endif
        ClosePanel(_otherMenuPanel);
    }

    // ---- テクスチャサイズ変更 ----

    internal void OnTextureResize()
    {
        if (_session == null) return;
        ClosePanel(_otherMenuPanel);

#if UNITY_EDITOR
        uint current = _session.CanvasWidth;
        // サイズ候補を現在サイズより小さいもののみ提示
        int choice = UnityEditor.EditorUtility.DisplayDialogComplex(
            "テクスチャサイズ変更",
            $"現在のサイズ: {current}×{current}\n変更後のサイズを選択してください。\nUndo 履歴・選択範囲はリセットされます。",
            current > 64 ? "64×64" : "変更なし",
            "キャンセル",
            current > 128 ? "128×128" : "変更なし");

        uint newSize = choice switch
        {
            0 when current > 64  => 64,
            2 when current > 128 => 128,
            _ => 0,
        };
        if (newSize == 0) return;
        _session.ResizeCanvas(newSize, newSize);
        RecreateCompositeTexture(newSize, newSize);
        _canvasLogic = new PaintCanvasLogic(newSize, newSize);
        RefreshComposite();
        RefreshLayerPanel();
        _onHistoryChanged?.Invoke();
        Debug.Log($"[TexturePaintController] テクスチャリサイズ → {newSize}×{newSize}");
#else
        Debug.Log("[TexturePaintController] テクスチャサイズ変更は現在 Editor のみ対応");
#endif
    }

    private void RecreateCompositeTexture(uint w, uint h)
    {
        if (_compositeTexture != null)
            Destroy(_compositeTexture);
        _compositeTexture = new Texture2D((int)w, (int)h, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point
        };
        if (_selectionTexture != null)
            Destroy(_selectionTexture);
        _selectionTexture = new Texture2D((int)w, (int)h, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point
        };
    }

    // ---- レイヤー取り込み ----

    internal void OnImportLayerPng()
    {
        if (_session == null || _activeLayerId == 0) return;
        ClosePanel(_otherMenuPanel);

#if UNITY_EDITOR
        string path = UnityEditor.EditorUtility.OpenFilePanel("PNG を取り込む", "", "png");
        if (string.IsNullOrEmpty(path)) return;
        byte[] pngData = System.IO.File.ReadAllBytes(path);
        bool ok = _session.ImportLayerPng(_activeLayerId, pngData);
        if (ok)
        {
            RefreshComposite();
            RefreshActiveLayerThumbnail();
            _onHistoryChanged?.Invoke();
            Debug.Log($"[TexturePaintController] PNG 取り込み完了: {path}");
        }
        else
        {
            Debug.LogWarning("[TexturePaintController] PNG 取り込み失敗（色調補正レイヤーへの書き込みは不可）");
        }
#else
        Debug.Log("[TexturePaintController] レイヤー取り込みは現在 Editor のみ対応");
#endif
    }

    // ---- 選択範囲オーバーレイ更新 ----

    private void RefreshSelectionOverlay()
    {
        if (_selectionOverlay == null || _selectionTexture == null || _session == null)
            return;

        if (!_session.SelectionHas)
        {
            _selectionOverlay.AddToClassList("paint-panel--hidden");
            return;
        }

        // キャンバス上の選択マスクを青い半透明オーバーレイとして表示
        // NOTE: Rust の selection は 0/255 のマスク。ここでは青チャンネルで可視化する。
        // 実際の選択マスクピクセルデータは Rust 側に持つため、ここでは composite に基づく
        // ダミービジュアルとして「選択中は右上に小ラベルで通知」する簡略実装とする。
        _selectionOverlay.RemoveFromClassList("paint-panel--hidden");
    }

    // ---- パネル表示切り替え ----

    private void TogglePanel(VisualElement panel)
    {
        if (panel == null) return;
        bool hidden = panel.ClassListContains("paint-panel--hidden");
        if (hidden)
            panel.RemoveFromClassList("paint-panel--hidden");
        else
            panel.AddToClassList("paint-panel--hidden");
    }

    private void ClosePanel(VisualElement panel)
    {
        panel?.AddToClassList("paint-panel--hidden");
    }

    // ---- サムネイル ----

    /// <summary>
    /// 指定レイヤーのサムネイル Texture2D を生成・更新して返す。
    /// 色調補正レイヤーなどピクセルデータがない場合は null。
    /// </summary>
    private Texture2D GetOrCreateLayerThumbnail(PaintLayer layer)
    {
        if (_session == null)
            return null;
        byte[] pixels = _session.GetLayerPixels(layer.Id);
        if (pixels == null)
            return null;

        if (!_layerThumbnails.TryGetValue(layer.Id, out var tex) || tex == null)
        {
            tex = new Texture2D((int)_session.CanvasWidth, (int)_session.CanvasHeight, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            _layerThumbnails[layer.Id] = tex;
        }
        tex.SetPixelData(pixels, 0);
        tex.Apply();
        return tex;
    }

    /// <summary>
    /// アクティブレイヤーのサムネイルテクスチャだけを更新する（ストローク終了時に呼ぶ）。
    /// パネルの再構築は行わず、既存 Texture2D のピクセルを上書きするだけ。
    /// UIToolkit は Texture2D.Apply() 後に自動再描画する。
    /// </summary>
    private void RefreshActiveLayerThumbnail()
    {
        if (_session == null || _activeLayerId == 0)
            return;
        var layer = _session.LayerStack.FindLayer(_activeLayerId);
        if (layer == null)
            return;
        GetOrCreateLayerThumbnail(layer);
    }

    // ---- Unity ライフサイクル ----

    private void OnDestroy()
    {
        if (_compositeTexture != null)
        {
            Destroy(_compositeTexture);
            _compositeTexture = null;
        }
        if (_selectionTexture != null)
        {
            Destroy(_selectionTexture);
            _selectionTexture = null;
        }
        foreach (var tex in _layerThumbnails.Values)
            if (tex != null)
                Destroy(tex);
        _layerThumbnails.Clear();
        _session?.Dispose();
    }
}
