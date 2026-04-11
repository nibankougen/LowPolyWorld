using System;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// アバター編集画面の MonoBehaviour。
/// 3D プレビュー、タブ切り替え、Undo/Redo ボタン、パネル最小化を管理する。
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class AvatarEditController : MonoBehaviour
{
    [SerializeField] private Camera _previewCamera;
    [SerializeField] private RenderTexture _previewRenderTexture;

    private UIDocument _document;
    private VisualElement _root;

    // タブ
    private Button _tabTexture;
    private Button _tabAccessory;
    private VisualElement _contentTexture;
    private VisualElement _contentAccessory;
    private Button _activeTab;

    // アクセサリサブタブ
    private Button _subtabActive;
    private Button _subtabSaved;
    private Button _subtabShop;
    private Button _subtabPreset;
    private Button _activeSubtab;

    // Undo/Redo
    private Button _btnUndo;
    private Button _btnRedo;
    private readonly UndoRedoLogic _undoRedoTexture = new();
    private readonly UndoRedoLogic _undoRedoAccessory = new();
    private UndoRedoLogic _currentUndoRedo;

    // パネル最小化
    private Button _btnMinimize;
    private VisualElement _bottomPanel;
    private VisualElement _tabContent;
    private bool _isMinimized;

    // 3D プレビューカメラ
    private readonly EditPreviewCameraLogic _previewCamera3D = new();
    private Vector2 _lastTouchPos;
    private float _lastPinchDistance;

    public event Action OnBackRequested;

    private void Awake()
    {
        _document = GetComponent<UIDocument>();
    }

    private void OnEnable()
    {
        _root = _document.rootVisualElement;

        _root.Q<Button>("btn-back")?.RegisterCallback<ClickEvent>(_ => OnBackRequested?.Invoke());

        _tabTexture = _root.Q<Button>("tab-texture");
        _tabAccessory = _root.Q<Button>("tab-accessory");
        _contentTexture = _root.Q<VisualElement>("content-texture");
        _contentAccessory = _root.Q<VisualElement>("content-accessory");

        _tabTexture?.RegisterCallback<ClickEvent>(_ => SelectTab(_tabTexture, _contentTexture, _undoRedoTexture));
        _tabAccessory?.RegisterCallback<ClickEvent>(_ => SelectTab(_tabAccessory, _contentAccessory, _undoRedoAccessory));

        _subtabActive = _root.Q<Button>("subtab-active");
        _subtabSaved = _root.Q<Button>("subtab-saved");
        _subtabShop = _root.Q<Button>("subtab-shop");
        _subtabPreset = _root.Q<Button>("subtab-preset");

        _subtabActive?.RegisterCallback<ClickEvent>(_ => SelectSubtab(_subtabActive));
        _subtabSaved?.RegisterCallback<ClickEvent>(_ => SelectSubtab(_subtabSaved));
        _subtabShop?.RegisterCallback<ClickEvent>(_ => SelectSubtab(_subtabShop));
        _subtabPreset?.RegisterCallback<ClickEvent>(_ => SelectSubtab(_subtabPreset));

        _btnUndo = _root.Q<Button>("btn-undo");
        _btnRedo = _root.Q<Button>("btn-redo");
        _btnUndo?.RegisterCallback<ClickEvent>(_ => _currentUndoRedo?.Undo());
        _btnRedo?.RegisterCallback<ClickEvent>(_ => _currentUndoRedo?.Redo());

        _btnMinimize = _root.Q<Button>("btn-minimize");
        _bottomPanel = _root.Q<VisualElement>("bottom-panel");
        _tabContent = _root.Q<VisualElement>("tab-content");
        _btnMinimize?.RegisterCallback<ClickEvent>(_ => ToggleMinimize());

        // プレビュー RenderTexture を VisualElement に設定
        var previewRender = _root.Q<VisualElement>("preview-render");
        if (previewRender != null && _previewRenderTexture != null)
            previewRender.style.backgroundImage = Background.FromRenderTexture(_previewRenderTexture);

        _undoRedoTexture.OnHistoryChanged += UpdateUndoRedoButtons;
        _undoRedoAccessory.OnHistoryChanged += UpdateUndoRedoButtons;

        // デフォルト: テクスチャタブ
        SelectTab(_tabTexture, _contentTexture, _undoRedoTexture);
        SelectSubtab(_subtabActive);
    }

    private void OnDisable()
    {
        _undoRedoTexture.OnHistoryChanged -= UpdateUndoRedoButtons;
        _undoRedoAccessory.OnHistoryChanged -= UpdateUndoRedoButtons;
    }

    private void LateUpdate()
    {
        if (_previewCamera == null) return;
        // TODO: タッチ入力で EditPreviewCameraLogic を更新
        // Phase 2 では PreviewCamera の transform を毎フレーム更新
        var targetPos = Vector3.up * 1f; // アバター腰高さ相当
        _previewCamera.transform.position = _previewCamera3D.GetCameraPosition(targetPos);
        _previewCamera.transform.rotation = _previewCamera3D.GetCameraRotation();
    }

    private void SelectTab(Button tab, VisualElement content, UndoRedoLogic undoRedo)
    {
        _activeTab?.RemoveFromClassList("edit-tab--active");
        _contentTexture?.AddToClassList("edit-tab-content--hidden");
        _contentAccessory?.AddToClassList("edit-tab-content--hidden");

        _activeTab = tab;
        _activeTab?.AddToClassList("edit-tab--active");
        content?.RemoveFromClassList("edit-tab-content--hidden");

        _currentUndoRedo = undoRedo;
        UpdateUndoRedoButtons();
    }

    private void SelectSubtab(Button subtab)
    {
        _activeSubtab?.RemoveFromClassList("edit-subtab--active");
        _activeSubtab = subtab;
        _activeSubtab?.AddToClassList("edit-subtab--active");
    }

    private void ToggleMinimize()
    {
        _isMinimized = !_isMinimized;
        if (_tabContent != null)
            _tabContent.style.display = _isMinimized ? DisplayStyle.None : DisplayStyle.Flex;
        if (_btnMinimize != null)
            _btnMinimize.text = _isMinimized ? "△" : "▽";
    }

    private void UpdateUndoRedoButtons()
    {
        if (_btnUndo != null)
            _btnUndo.SetEnabled(_currentUndoRedo?.CanUndo ?? false);
        if (_btnRedo != null)
            _btnRedo.SetEnabled(_currentUndoRedo?.CanRedo ?? false);
    }
}
