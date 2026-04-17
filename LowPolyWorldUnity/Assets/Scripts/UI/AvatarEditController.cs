using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// アバター編集画面の MonoBehaviour。
/// 3D プレビュー、タブ切り替え、Undo/Redo ボタン、パネル最小化、
/// アクセサリ選択状態・D&D を管理する。
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class AvatarEditController : MonoBehaviour
{
    [SerializeField] private Camera _previewCamera;
    [SerializeField] private RenderTexture _previewRenderTexture;

    /// <summary>ペイント結果をリアルタイム反映するプレビューアバターの Renderer。</summary>
    [SerializeField] private Renderer _previewAvatarRenderer;

    /// <summary>このアバターに対応する AtlasManager キャラクタースロット番号（-1 = 未割り当て）。</summary>
    [SerializeField] private int _atlasCharacterSlot = -1;

    /// <summary>UV オーバーレイ生成元のアバターメッシュ（省略可・未設定時は UV オーバーレイなし）。</summary>
    [SerializeField] private SkinnedMeshRenderer[] _uvSourceRenderers;

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

    // アクセサリサブタブコンテンツ
    private VisualElement _subtabContentActive;
    private VisualElement _subtabContentSaved;
    private VisualElement _subtabContentShop;
    private VisualElement _subtabContentPreset;

    // アクセサリ選択
    private VisualElement _accessorySlots;
    private VisualElement _accessorySelectionDetail;
    private DropdownField _dropdownAccessoryBone;
    private FloatField _fieldOffsetX;
    private FloatField _fieldOffsetY;
    private FloatField _fieldOffsetZ;
    private FloatField _fieldRotX;
    private FloatField _fieldRotY;
    private FloatField _fieldRotZ;
    private FloatField _fieldScale;
    private Button _btnRemoveAccessory;
    private readonly List<VisualElement> _slotCards = new();
    private readonly AccessorySelectionLogic _accessoryLogic = new();
    private bool _transformFieldsLocked;

    // D&D
    private VisualElement _dndDropZone;
    private VisualElement _dndDragGhost;
    private Label _dndGhostLabel;
    private string _draggedFileId;
    private bool _isDragging;

    // Undo/Redo
    private Button _btnUndo;
    private Button _btnRedo;
    private readonly UndoRedoLogic _undoRedoAccessory = new();
    private UndoRedoLogic _currentUndoRedo;

    // テクスチャペイント
    private TexturePaintController _texturePaint;
    private IPaintSession _paintSession;

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

        _subtabContentActive = _root.Q<VisualElement>("subtab-content-active");
        _subtabContentSaved = _root.Q<VisualElement>("subtab-content-saved");
        _subtabContentShop = _root.Q<VisualElement>("subtab-content-shop");
        _subtabContentPreset = _root.Q<VisualElement>("subtab-content-preset");

        _subtabActive?.RegisterCallback<ClickEvent>(_ => SelectSubtab(_subtabActive, _subtabContentActive));
        _subtabSaved?.RegisterCallback<ClickEvent>(_ => SelectSubtab(_subtabSaved, _subtabContentSaved));
        _subtabShop?.RegisterCallback<ClickEvent>(_ => SelectSubtab(_subtabShop, _subtabContentShop));
        _subtabPreset?.RegisterCallback<ClickEvent>(_ => SelectSubtab(_subtabPreset, _subtabContentPreset));

        // アクセサリ選択 UI
        _accessorySlots = _root.Q<VisualElement>("accessory-slots");
        _accessorySelectionDetail = _root.Q<VisualElement>("accessory-selection-detail");
        _dropdownAccessoryBone = _root.Q<DropdownField>("dropdown-accessory-bone");
        _fieldOffsetX = _root.Q<FloatField>("field-offset-x");
        _fieldOffsetY = _root.Q<FloatField>("field-offset-y");
        _fieldOffsetZ = _root.Q<FloatField>("field-offset-z");
        _fieldRotX = _root.Q<FloatField>("field-rot-x");
        _fieldRotY = _root.Q<FloatField>("field-rot-y");
        _fieldRotZ = _root.Q<FloatField>("field-rot-z");
        _fieldScale = _root.Q<FloatField>("field-scale");
        _btnRemoveAccessory = _root.Q<Button>("btn-remove-accessory");

        if (_dropdownAccessoryBone != null)
        {
            var choices = new List<string>();
            foreach (var bone in AccessoryAttacher.AttachBones)
                choices.Add(bone.ToString());
            _dropdownAccessoryBone.choices = choices;
            _dropdownAccessoryBone.RegisterValueChangedCallback(e =>
            {
                if (_transformFieldsLocked) return;
                if (Enum.TryParse<HumanBodyBones>(e.newValue, out var bone))
                    _accessoryLogic.ChangeSelectedBone(bone);
            });
        }

        RegisterTransformField(_fieldOffsetX, v => ApplyTransformField());
        RegisterTransformField(_fieldOffsetY, v => ApplyTransformField());
        RegisterTransformField(_fieldOffsetZ, v => ApplyTransformField());
        RegisterTransformField(_fieldRotX, v => ApplyTransformField());
        RegisterTransformField(_fieldRotY, v => ApplyTransformField());
        RegisterTransformField(_fieldRotZ, v => ApplyTransformField());
        RegisterTransformField(_fieldScale, v => ApplyTransformField());

        _btnRemoveAccessory?.RegisterCallback<ClickEvent>(_ => _accessoryLogic.RemoveSelected());

        _accessoryLogic.OnSelectionChanged += OnAccessorySelectionChanged;
        _accessoryLogic.OnSlotChanged += OnAccessorySlotChanged;

        // D&D
        _dndDropZone = _root.Q<VisualElement>("dnd-drop-zone");
        _dndDragGhost = _root.Q<VisualElement>("dnd-drag-ghost");
        _dndGhostLabel = _root.Q<Label>("dnd-ghost-label");

        _root.RegisterCallback<PointerMoveEvent>(OnRootPointerMove);
        _root.RegisterCallback<PointerUpEvent>(OnRootPointerUp);

        // Undo/Redo
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

        // テクスチャペイントセッション初期化
        if (_paintSession == null)
        {
            _paintSession = new AvatarPaintSession();

            if (_uvSourceRenderers is { Length: > 0 })
            {
                var meshes = new UnityEngine.Mesh[_uvSourceRenderers.Length];
                for (int i = 0; i < _uvSourceRenderers.Length; i++)
                    meshes[i] = _uvSourceRenderers[i] != null ? _uvSourceRenderers[i].sharedMesh : null;
                var uvRgba = UvOverlayBaker.Bake(meshes, (int)AvatarPaintSession.CanvasWidth, (int)AvatarPaintSession.CanvasHeight);
                if (uvRgba != null)
                    _paintSession.SetUvOverlay(uvRgba);
            }
        }
        if (_texturePaint == null)
            _texturePaint = gameObject.AddComponent<TexturePaintController>();
        _texturePaint.Initialize(
            _contentTexture,
            _paintSession,
            UpdateUndoRedoButtons,
            onPreviewUpdated: OnPreviewTextureUpdated,
            onSaveRgba: OnSaveRgbaToAtlas);

        BuildAccessorySlotCards();

        // デフォルト: テクスチャタブ
        SelectTextureTab();
        SelectSubtab(_subtabActive, _subtabContentActive);
    }

    private void OnDisable()
    {
        _undoRedoAccessory.OnHistoryChanged -= UpdateUndoRedoButtons;
        _accessoryLogic.OnSelectionChanged -= OnAccessorySelectionChanged;
        _accessoryLogic.OnSlotChanged -= OnAccessorySlotChanged;
        _root?.UnregisterCallback<PointerMoveEvent>(OnRootPointerMove);
        _root?.UnregisterCallback<PointerUpEvent>(OnRootPointerUp);
        _paintSession?.Dispose();
        _paintSession = null;
    }

    private void LateUpdate()
    {
        if (_previewCamera == null) return;
        var targetPos = Vector3.up * 1f;
        _previewCamera.transform.position = _previewCamera3D.GetCameraPosition(targetPos);
        _previewCamera.transform.rotation = _previewCamera3D.GetCameraRotation();
    }

    // ---- UV オーバーレイ動的配線 ----

    /// <summary>
    /// VRM ロード後に呼び出してアバターメッシュから UV オーバーレイを再ベイクする。
    /// Phase 5A アバターロード実装時に AvatarManager 側から呼ぶ。
    /// </summary>
    public void SetUvSourceRenderers(SkinnedMeshRenderer[] renderers)
    {
        _uvSourceRenderers = renderers;
        if (_paintSession == null) return;

        if (renderers is { Length: > 0 })
        {
            var meshes = new UnityEngine.Mesh[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
                meshes[i] = renderers[i] != null ? renderers[i].sharedMesh : null;
            var uvRgba = UvOverlayBaker.Bake(meshes, (int)AvatarPaintSession.CanvasWidth, (int)AvatarPaintSession.CanvasHeight);
            if (uvRgba != null)
                _paintSession.SetUvOverlay(uvRgba);
        }
    }

    // ---- タブ ----

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

    private void SelectSubtab(Button subtab, VisualElement content)
    {
        _activeSubtab?.RemoveFromClassList("edit-subtab--active");
        _activeSubtab = subtab;
        _activeSubtab?.AddToClassList("edit-subtab--active");

        _subtabContentActive?.AddToClassList("subtab-content--hidden");
        _subtabContentSaved?.AddToClassList("subtab-content--hidden");
        _subtabContentShop?.AddToClassList("subtab-content--hidden");
        _subtabContentPreset?.AddToClassList("subtab-content--hidden");
        content?.RemoveFromClassList("subtab-content--hidden");

        // 保存・編集 / デフォルトタブはドラッグソースとしてアイテムを有効化
        bool isDndSource = (subtab == _subtabSaved || subtab == _subtabPreset);
        SetDndSourceEnabled(content, isDndSource);
    }

    private void ToggleMinimize()
    {
        _isMinimized = !_isMinimized;
        if (_tabContent != null)
            _tabContent.style.display = _isMinimized ? DisplayStyle.None : DisplayStyle.Flex;
        if (_btnMinimize != null)
            _btnMinimize.text = _isMinimized ? "△" : "▽";
    }

    // ---- アクセサリスロット UI ----

    private void BuildAccessorySlotCards()
    {
        if (_accessorySlots == null) return;
        _accessorySlots.Clear();
        _slotCards.Clear();

        for (int i = 0; i < AccessorySelectionLogic.MaxSlots; i++)
        {
            int capturedIndex = i;
            var card = BuildSlotCard(capturedIndex);
            _accessorySlots.Add(card);
            _slotCards.Add(card);
        }
    }

    private VisualElement BuildSlotCard(int index)
    {
        var card = new VisualElement();
        card.AddToClassList("accessory-slot-card");
        card.AddToClassList("accessory-slot-card--empty");

        var thumb = new VisualElement();
        thumb.AddToClassList("accessory-slot-thumb");
        thumb.name = $"slot-thumb-{index}";
        card.Add(thumb);

        var emptyIcon = new Label("+");
        emptyIcon.AddToClassList("accessory-slot-empty-icon");
        emptyIcon.name = $"slot-empty-icon-{index}";
        card.Add(emptyIcon);

        var check = new Label("✓");
        check.AddToClassList("accessory-slot-check");
        check.name = $"slot-check-{index}";
        card.Add(check);

        card.RegisterCallback<ClickEvent>(_ => _accessoryLogic.Select(index));

        RefreshSlotCard(index);
        return card;
    }

    private void RefreshSlotCard(int index)
    {
        if (index < 0 || index >= _slotCards.Count) return;
        var card = _slotCards[index];
        var slot = _accessoryLogic.Slots[index];

        bool occupied = slot.IsOccupied;
        bool selected = _accessoryLogic.SelectedIndex == index;

        if (occupied)
        {
            card.RemoveFromClassList("accessory-slot-card--empty");
            card.Q($"slot-empty-icon-{index}")?.AddToClassList("accessory-slot-empty-icon--hidden");
        }
        else
        {
            card.AddToClassList("accessory-slot-card--empty");
            card.Q($"slot-empty-icon-{index}")?.RemoveFromClassList("accessory-slot-empty-icon--hidden");
        }

        if (selected && occupied)
        {
            card.AddToClassList("accessory-slot-card--selected");
            card.Q($"slot-check-{index}")?.AddToClassList("accessory-slot-check--visible");
        }
        else
        {
            card.RemoveFromClassList("accessory-slot-card--selected");
            card.Q($"slot-check-{index}")?.RemoveFromClassList("accessory-slot-check--visible");
        }
    }

    // ---- アクセサリ選択イベント ----

    private void OnAccessorySelectionChanged(int selectedIndex)
    {
        for (int i = 0; i < _slotCards.Count; i++)
            RefreshSlotCard(i);

        bool showDetail = _accessoryLogic.HasSelection;
        if (_accessorySelectionDetail != null)
        {
            if (showDetail)
                _accessorySelectionDetail.RemoveFromClassList("accessory-detail--hidden");
            else
                _accessorySelectionDetail.AddToClassList("accessory-detail--hidden");
        }

        if (showDetail)
            PopulateTransformFields(_accessoryLogic.SelectedSlot);
    }

    private void OnAccessorySlotChanged(int slotIndex)
    {
        RefreshSlotCard(slotIndex);
    }

    private void PopulateTransformFields(AccessorySlotData slot)
    {
        if (slot == null) return;
        _transformFieldsLocked = true;

        if (_dropdownAccessoryBone != null)
            _dropdownAccessoryBone.value = slot.Bone.ToString();

        if (_fieldOffsetX != null) _fieldOffsetX.value = slot.LocalPosition.x;
        if (_fieldOffsetY != null) _fieldOffsetY.value = slot.LocalPosition.y;
        if (_fieldOffsetZ != null) _fieldOffsetZ.value = slot.LocalPosition.z;

        var euler = slot.LocalRotation.eulerAngles;
        if (_fieldRotX != null) _fieldRotX.value = euler.x;
        if (_fieldRotY != null) _fieldRotY.value = euler.y;
        if (_fieldRotZ != null) _fieldRotZ.value = euler.z;

        if (_fieldScale != null) _fieldScale.value = slot.LocalScale.x;

        _transformFieldsLocked = false;
    }

    private void ApplyTransformField()
    {
        if (_transformFieldsLocked || !_accessoryLogic.HasSelection) return;

        var pos = new Vector3(
            _fieldOffsetX?.value ?? 0f,
            _fieldOffsetY?.value ?? 0f,
            _fieldOffsetZ?.value ?? 0f);
        var rot = Quaternion.Euler(
            _fieldRotX?.value ?? 0f,
            _fieldRotY?.value ?? 0f,
            _fieldRotZ?.value ?? 0f);
        float s = _fieldScale?.value ?? 1f;
        var scale = new Vector3(s, s, s);

        _accessoryLogic.SetSelectedTransform(pos, rot, scale);
    }

    private static void RegisterTransformField(FloatField field, Action<float> onChange)
    {
        field?.RegisterValueChangedCallback(e => onChange(e.newValue));
    }

    // ---- D&D ----

    private void SetDndSourceEnabled(VisualElement container, bool enabled)
    {
        if (container == null) return;
        // 動的生成アイテムの PointerDown を有効化/無効化するため
        // BuildAccessoryGridItem 時に D&D ハンドラを登録済みのものを呼び出す
        // （現時点ではグリッドアイテムは動的に生成する想定）
    }

    /// <summary>
    /// 保存・編集 / デフォルトグリッドにアイテムを追加する（Phase 5 以降でサーバーデータ連携）。
    /// </summary>
    public void AddAccessoryGridItem(VisualElement container, string fileId, string displayName)
    {
        if (container == null) return;

        var item = new VisualElement();
        item.AddToClassList("accessory-grid-item");

        var thumb = new VisualElement();
        thumb.AddToClassList("accessory-grid-item-thumb");
        item.Add(thumb);

        var label = new Label(displayName);
        label.AddToClassList("accessory-grid-item-label");
        item.Add(label);

        // D&D 開始
        item.RegisterCallback<PointerDownEvent>(e =>
        {
            if (e.button != 0) return;
            BeginDrag(fileId, displayName, e.position);
            item.CapturePointer(e.pointerId);
            e.StopPropagation();
        });

        container.Add(item);
    }

    private void BeginDrag(string fileId, string displayName, Vector2 screenPos)
    {
        if (!_accessoryLogic.CanAddAccessory) return;

        _isDragging = true;
        _draggedFileId = fileId;

        if (_dndDragGhost != null)
        {
            _dndDragGhost.RemoveFromClassList("dnd-drag-ghost--hidden");
            MoveDragGhost(screenPos);
        }
        if (_dndGhostLabel != null)
            _dndGhostLabel.text = displayName;

        _dndDropZone?.RemoveFromClassList("dnd-drop-zone--hidden");
    }

    private void EndDrag(Vector2 screenPos)
    {
        if (!_isDragging) return;
        _isDragging = false;

        _dndDragGhost?.AddToClassList("dnd-drag-ghost--hidden");
        _dndDropZone?.AddToClassList("dnd-drop-zone--hidden");

        // ドロップゾーン（プレビューエリア）上かチェック
        var previewArea = _root.Q<VisualElement>("preview-area");
        if (previewArea != null)
        {
            var localPos = previewArea.WorldToLocal(screenPos);
            if (previewArea.ContainsPoint(localPos))
            {
                DropAccessoryOnPreview();
            }
        }

        _draggedFileId = null;
    }

    private void DropAccessoryOnPreview()
    {
        if (string.IsNullOrEmpty(_draggedFileId)) return;
        if (!_accessoryLogic.CanAddAccessory)
        {
            Debug.Log("[AvatarEdit] アクセサリスロットが満杯です。");
            return;
        }

        _accessoryLogic.TryAddAccessory(_draggedFileId, HumanBodyBones.Head, out int slot);
        // 新たに追加したスロットを選択
        if (slot >= 0)
            _accessoryLogic.Select(slot);

        // TODO: Phase 5 以降で実際の GLB ロード・アタッチ処理を呼ぶ
        Debug.Log($"[AvatarEdit] アクセサリ追加: fileId={_draggedFileId} slot={slot}");
    }

    private void MoveDragGhost(Vector2 screenPos)
    {
        if (_dndDragGhost == null) return;
        // ゴーストの親要素を基準にした座標変換（absolute 配置の基準要素が親になるため）
        var parent = _dndDragGhost.parent ?? _root;
        var localPos = parent.WorldToLocal(screenPos);
        _dndDragGhost.style.left = localPos.x - 32;
        _dndDragGhost.style.top = localPos.y - 32;
    }

    private void OnRootPointerMove(PointerMoveEvent e)
    {
        if (_isDragging)
            MoveDragGhost(e.position);
    }

    private void OnRootPointerUp(PointerUpEvent e)
    {
        if (_isDragging)
            EndDrag(e.position);
    }

    // ---- ペイントコールバック ----

    /// <summary>コンポジット更新時にプレビューアバターのマテリアルテクスチャを差し替える。</summary>
    private void OnPreviewTextureUpdated(Texture2D composite)
    {
        if (_previewAvatarRenderer == null) return;
        _previewAvatarRenderer.material.mainTexture = composite;
    }

    /// <summary>保存時に Atlas のキャラクタースロットへ書き込む。</summary>
    private void OnSaveRgbaToAtlas(byte[] rgba)
    {
        if (_atlasCharacterSlot < 0 || AtlasManager.Instance == null) return;
        var tex = new Texture2D(
            (int)(_paintSession?.CanvasWidth ?? AvatarPaintSession.CanvasWidth),
            (int)(_paintSession?.CanvasHeight ?? AvatarPaintSession.CanvasHeight),
            TextureFormat.RGBA32,
            false);
        tex.SetPixelData(rgba, 0);
        tex.Apply();
        AtlasManager.Instance.WriteCharacterTexture(_atlasCharacterSlot, tex);
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
