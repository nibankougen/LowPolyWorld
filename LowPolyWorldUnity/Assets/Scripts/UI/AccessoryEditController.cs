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

    private UIDocument _document;
    private VisualElement _root;

    private Button _tabTexture;
    private Button _tabPlacement;
    private VisualElement _contentTexture;
    private VisualElement _contentPlacement;
    private Button _activeTab;

    private Button _btnUndo;
    private Button _btnRedo;
    private readonly UndoRedoLogic _undoRedoTexture = new();
    private readonly UndoRedoLogic _undoRedoPlacement = new();
    private UndoRedoLogic _currentUndoRedo;

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

        _tabTexture?.RegisterCallback<ClickEvent>(_ => SelectTab(_tabTexture, _contentTexture, _undoRedoTexture));
        _tabPlacement?.RegisterCallback<ClickEvent>(_ => SelectTab(_tabPlacement, _contentPlacement, _undoRedoPlacement));

        _btnUndo = _root.Q<Button>("btn-undo");
        _btnRedo = _root.Q<Button>("btn-redo");
        _btnUndo?.RegisterCallback<ClickEvent>(_ => _currentUndoRedo?.Undo());
        _btnRedo?.RegisterCallback<ClickEvent>(_ => _currentUndoRedo?.Redo());

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

        _undoRedoTexture.OnHistoryChanged += UpdateUndoRedoButtons;
        _undoRedoPlacement.OnHistoryChanged += UpdateUndoRedoButtons;

        SelectTab(_tabTexture, _contentTexture, _undoRedoTexture);
    }

    private void OnDisable()
    {
        _undoRedoTexture.OnHistoryChanged -= UpdateUndoRedoButtons;
        _undoRedoPlacement.OnHistoryChanged -= UpdateUndoRedoButtons;
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
