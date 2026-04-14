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
    private AvatarPaintSession _session;
    private Action _onHistoryChanged;

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

    // ---- ストローク追跡 ----

    private bool _isStroking;
    private (int x, int y) _strokeStart;
    private uint _activeLayerId;

    // ---- テクスチャ ----

    private Texture2D _compositeTexture;

    // ---- 初期化（AvatarEditController から呼び出す） ----

    /// <summary>
    /// テクスチャタブの VisualElement とセッションを受け取って初期化する。
    /// </summary>
    public void Initialize(VisualElement contentTexture, AvatarPaintSession session, Action onHistoryChanged)
    {
        _contentTexture = contentTexture;
        _session = session;
        _onHistoryChanged = onHistoryChanged;

        _brush = new BrushSettingsLogic();
        _colorPicker = new ColorPickerLogic();
        _colorPicker.SetFromRgba(255, 0, 0, 255);

        _canvasLogic = new PaintCanvasLogic(AvatarPaintSession.CanvasWidth, AvatarPaintSession.CanvasHeight);

        _compositeTexture = new Texture2D(
            (int)AvatarPaintSession.CanvasWidth,
            (int)AvatarPaintSession.CanvasHeight,
            TextureFormat.RGBA32,
            false
        );
        _compositeTexture.filterMode = FilterMode.Point;

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
        BindToolButton(PaintTool.Brush,  "btn-tool-brush");
        BindToolButton(PaintTool.Eraser, "btn-tool-eraser");
        BindToolButton(PaintTool.Fill,   "btn-tool-fill");
        BindToolButton(PaintTool.Rect,   "btn-tool-rect");
        BindToolButton(PaintTool.Circle, "btn-tool-circle");
        BindToolButton(PaintTool.Line,   "btn-tool-line");

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

        // 通常レイヤーを上から（スタックの末尾から）表示
        var layers = _session.LayerStack.Layers;
        for (int i = layers.Count - 1; i >= 0; i--)
            _layerList.Add(BuildLayerItem(layers[i]));

        // 色調補正レイヤー
        if (_session.LayerStack.HasColorAdjustment)
            _layerList.Add(BuildLayerItem(_session.LayerStack.ColorAdjustmentLayer));
    }

    private VisualElement BuildLayerItem(PaintLayer layer)
    {
        var item = new VisualElement();
        item.AddToClassList("layer-item");
        if (layer.Id == _activeLayerId)
            item.AddToClassList("layer-item--selected");

        var thumb = new VisualElement();
        thumb.AddToClassList("layer-thumb");

        var nameLabel = new Label(layer.Name);
        nameLabel.AddToClassList("layer-name");

        var visBtn = new Button();
        visBtn.AddToClassList("layer-visible-btn");
        visBtn.text = layer.Visible ? "●" : "○";
        if (!layer.Visible)
            visBtn.AddToClassList("layer-visible-btn--hidden");

        uint layerId = layer.Id;
        visBtn.RegisterCallback<ClickEvent>(_ =>
        {
            _session?.SetLayerVisible(layerId, !layer.Visible);
            RefreshLayerPanel();
            RefreshComposite();
        });

        item.Add(thumb);
        item.Add(nameLabel);
        item.Add(visBtn);

        item.RegisterCallback<ClickEvent>(_ =>
        {
            _activeLayerId = layerId;
            RefreshLayerPanel();
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
        // TODO: PNG バイト列をサーバーへアップロード（Phase 9 API 実装後）
        var png = _session.CompositePng();
        if (png != null)
            Debug.Log($"[TexturePaintController] 保存 PNG size={png.Length} bytes");
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

    // ---- Unity ライフサイクル ----

    private void OnDestroy()
    {
        if (_compositeTexture != null)
        {
            Destroy(_compositeTexture);
            _compositeTexture = null;
        }
        _session?.Dispose();
    }
}
