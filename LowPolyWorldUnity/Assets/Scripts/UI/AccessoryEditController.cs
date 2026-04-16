using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// アクセサリ編集画面の MonoBehaviour。
/// テクスチャタブ（Phase 4）・配置タブ（ボーン選択・ギズモ）を管理する。
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class AccessoryEditController : MonoBehaviour
{
    [SerializeField] private Camera _previewCamera;
    [SerializeField] private RenderTexture _previewRenderTexture;
    [SerializeField] private TexturePaintController _texturePaintController;

    /// <summary>ペイント結果をリアルタイム反映するプレビューアクセサリの Renderer。</summary>
    [SerializeField] private Renderer _previewAccessoryRenderer;

    /// <summary>このアクセサリに対応する AtlasManager アクセサリスロット番号（-1 = 未割り当て）。</summary>
    [SerializeField] private int _atlasAccessorySlot = -1;

    private UIDocument _document;
    private VisualElement _root;

    private Button _tabTexture;
    private Button _tabPlacement;
    private VisualElement _contentTexture;
    private VisualElement _contentPlacement;
    private Button _activeTab;

    private Button _btnUndo;
    private Button _btnRedo;
    private readonly UndoRedoLogic _undoRedoPlacement = new();
    private UndoRedoLogic _currentUndoRedo;

    private AccessoryPaintSession _paintSession;
    private bool _paintSessionInitialized;

    private Button _btnMinimize;
    private VisualElement _tabContent;
    private bool _isMinimized;

    private DropdownField _dropdownBone;

    private readonly EditPreviewCameraLogic _previewCamera3D = new(initialDistance: 1.5f);

    public event Action OnBackRequested;
    public event Action<HumanBodyBones> OnBoneChanged;

    private void Awake()
    {
        _document = GetComponent<UIDocument>();
    }

    private void OnEnable()
    {
        _root = _document.rootVisualElement;

        _root.Q<Button>("btn-back")?.RegisterCallback<ClickEvent>(_ => OnBackRequested?.Invoke());

        _tabTexture = _root.Q<Button>("tab-texture");
        _tabPlacement = _root.Q<Button>("tab-placement");
        _contentTexture = _root.Q<VisualElement>("content-texture");
        _contentPlacement = _root.Q<VisualElement>("content-placement");

        _tabTexture?.RegisterCallback<ClickEvent>(_ => SelectTab(_tabTexture, _contentTexture, null));
        _tabPlacement?.RegisterCallback<ClickEvent>(_ => SelectTab(_tabPlacement, _contentPlacement, _undoRedoPlacement));

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

        var previewRender = _root.Q<VisualElement>("preview-render");
        if (previewRender != null && _previewRenderTexture != null)
            previewRender.style.backgroundImage = Background.FromRenderTexture(_previewRenderTexture);

        // ボーン選択ドロップダウン
        _dropdownBone = _root.Q<DropdownField>("dropdown-bone");
        if (_dropdownBone != null)
        {
            var choices = new List<string>();
            foreach (var bone in AccessoryAttacher.AttachBones)
                choices.Add(bone.ToString());
            _dropdownBone.choices = choices;
            _dropdownBone.value = choices.Count > 0 ? choices[0] : "";
            _dropdownBone.RegisterValueChangedCallback(e =>
            {
                if (Enum.TryParse<HumanBodyBones>(e.newValue, out var bone))
                    OnBoneChanged?.Invoke(bone);
            });
        }

        _root.Q<Button>("btn-switch-avatar")?.RegisterCallback<ClickEvent>(_ => Debug.Log("[AccessoryEdit] Switch avatar"));

        _undoRedoPlacement.OnHistoryChanged += UpdateUndoRedoButtons;

        SelectTab(_tabTexture, _contentTexture, null);
    }

    private void OnDisable()
    {
        _undoRedoPlacement.OnHistoryChanged -= UpdateUndoRedoButtons;
        _paintSession?.Dispose();
        _paintSession = null;
        _paintSessionInitialized = false;
    }

    private void LateUpdate()
    {
        if (_previewCamera == null) return;
        var targetPos = Vector3.up * 1f;
        _previewCamera.transform.position = _previewCamera3D.GetCameraPosition(targetPos);
        _previewCamera.transform.rotation = _previewCamera3D.GetCameraRotation();
    }

    private void SelectTab(Button tab, VisualElement content, UndoRedoLogic undoRedo)
    {
        _activeTab?.RemoveFromClassList("edit-tab--active");
        _contentTexture?.AddToClassList("edit-tab-content--hidden");
        _contentPlacement?.AddToClassList("edit-tab-content--hidden");

        _activeTab = tab;
        _activeTab?.AddToClassList("edit-tab--active");
        content?.RemoveFromClassList("edit-tab-content--hidden");

        _currentUndoRedo = undoRedo;
        UpdateUndoRedoButtons();

        // テクスチャタブ選択時に TexturePaintController を初期化（遅延初期化）
        if (tab == _tabTexture && !_paintSessionInitialized && _texturePaintController != null && _contentTexture != null)
        {
            try
            {
                _paintSession = new AccessoryPaintSession();
                _texturePaintController.Initialize(
                    _contentTexture,
                    _paintSession,
                    UpdateUndoRedoButtons,
                    onPreviewUpdated: OnPreviewTextureUpdated,
                    onSaveRgba: OnSaveRgbaToAtlas);
                _paintSessionInitialized = true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[AccessoryEditController] ペイントセッション初期化失敗: {e.Message}");
            }
        }
    }

    private void ToggleMinimize()
    {
        _isMinimized = !_isMinimized;
        if (_tabContent != null)
            _tabContent.style.display = _isMinimized ? DisplayStyle.None : DisplayStyle.Flex;
        if (_btnMinimize != null)
            _btnMinimize.text = _isMinimized ? "△" : "▽";
    }

    // ---- ペイントコールバック ----

    /// <summary>コンポジット更新時にプレビューアクセサリのマテリアルテクスチャを差し替える。</summary>
    private void OnPreviewTextureUpdated(Texture2D composite)
    {
        if (_previewAccessoryRenderer == null) return;
        _previewAccessoryRenderer.material.mainTexture = composite;
    }

    /// <summary>保存時に Atlas のアクセサリスロットへ書き込む。</summary>
    private void OnSaveRgbaToAtlas(byte[] rgba)
    {
        if (_atlasAccessorySlot < 0 || AtlasManager.Instance == null) return;
        var tex = new Texture2D(
            (int)AccessoryPaintSession.CanvasWidth,
            (int)AccessoryPaintSession.CanvasHeight,
            TextureFormat.RGBA32,
            false);
        tex.SetPixelData(rgba, 0);
        tex.Apply();
        AtlasManager.Instance.WriteAccessoryTexture(_atlasAccessorySlot, tex);
        AtlasManager.Instance.ScheduleAtlasUpdate();
        Destroy(tex);
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
