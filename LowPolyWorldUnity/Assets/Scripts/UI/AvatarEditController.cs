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
    private readonly UndoRedoLogic _undoRedoAccessory = new();
    private UndoRedoLogic _currentUndoRedo;

    // テクスチャペイント
    private TexturePaintController _texturePaint;
    private AvatarPaintSession _paintSession;

    // パネル最小化
    private Button _btnMinimize;
    private VisualElement _tabContent;
    private bool _isMinimized;

    // 3D プレビューカメラ
    private readonly EditPreviewCameraLogic _previewCamera3D = new();

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

        _tabTexture?.RegisterCallback<ClickEvent>(_ => SelectTextureTab());
        _tabAccessory?.RegisterCallback<ClickEvent>(_ => SelectAccessoryTab());

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
        _btnUndo?.RegisterCallback<ClickEvent>(_ =>
        {
            if (_paintSession != null && _activeTab == _tabTexture)
                _paintSession.Undo();
            else
                _currentUndoRedo?.Undo();
            UpdateUndoRedoButtons();
        });
        _btnRedo?.RegisterCallback<ClickEvent>(_ =>
        {
            if (_paintSession != null && _activeTab == _tabTexture)
                _paintSession.Redo();
            else
                _currentUndoRedo?.Redo();
            UpdateUndoRedoButtons();
        });

        _btnMinimize = _root.Q<Button>("btn-minimize");
        _tabContent = _root.Q<VisualElement>("tab-content");
        _btnMinimize?.RegisterCallback<ClickEvent>(_ => ToggleMinimize());

        // プレビュー RenderTexture を VisualElement に設定
        var previewRender = _root.Q<VisualElement>("preview-render");
        if (previewRender != null && _previewRenderTexture != null)
            previewRender.style.backgroundImage = Background.FromRenderTexture(_previewRenderTexture);

        _undoRedoAccessory.OnHistoryChanged += UpdateUndoRedoButtons;

        // テクスチャペイントセッション初期化（OnEnable が複数回呼ばれても重複しないよう null チェック）
        if (_paintSession == null)
            _paintSession = new AvatarPaintSession();
        if (_texturePaint == null)
            _texturePaint = gameObject.AddComponent<TexturePaintController>();
        _texturePaint.Initialize(_contentTexture, _paintSession, UpdateUndoRedoButtons);

        // デフォルト: テクスチャタブ
        SelectTextureTab();
        SelectSubtab(_subtabActive);
    }

    private void OnDisable()
    {
        _undoRedoAccessory.OnHistoryChanged -= UpdateUndoRedoButtons;
        _paintSession?.Dispose();
        _paintSession = null;
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

    private void SelectTextureTab()
    {
        _activeTab?.RemoveFromClassList("edit-tab--active");
        _contentTexture?.RemoveFromClassList("edit-tab-content--hidden");
        _contentAccessory?.AddToClassList("edit-tab-content--hidden");
        _activeTab = _tabTexture;
        _activeTab?.AddToClassList("edit-tab--active");
        _currentUndoRedo = null;
        _texturePaint?.OnTabEnter();
        UpdateUndoRedoButtons();
    }

    private void SelectAccessoryTab()
    {
        _texturePaint?.OnTabExit();
        _activeTab?.RemoveFromClassList("edit-tab--active");
        _contentTexture?.AddToClassList("edit-tab-content--hidden");
        _contentAccessory?.RemoveFromClassList("edit-tab-content--hidden");
        _activeTab = _tabAccessory;
        _activeTab?.AddToClassList("edit-tab--active");
        _currentUndoRedo = _undoRedoAccessory;
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
        if (_activeTab == _tabTexture && _paintSession != null)
        {
            if (_btnUndo != null)
                _btnUndo.SetEnabled(_paintSession.CanUndo);
            if (_btnRedo != null)
                _btnRedo.SetEnabled(_paintSession.CanRedo);
        }
        else
        {
            if (_btnUndo != null)
                _btnUndo.SetEnabled(_currentUndoRedo?.CanUndo ?? false);
            if (_btnRedo != null)
                _btnRedo.SetEnabled(_currentUndoRedo?.CanRedo ?? false);
        }
    }
}
