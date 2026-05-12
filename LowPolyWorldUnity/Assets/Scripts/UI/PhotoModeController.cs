using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.UIElements;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

/// <summary>
/// 撮影モード全体を管理する MonoBehaviour。
/// WorldHUDController の GameObject にアタッチし、カメラボタンから呼ばれる。
/// 仕様: screens-and-modes.md セクション 2.7
/// </summary>
public class PhotoModeController : MonoBehaviour
{
    [Header("Assets")]
    [SerializeField] private VisualTreeAsset _photoModeHUDAsset;
    [SerializeField] private StampSpriteEntry[] _stampSprites = Array.Empty<StampSpriteEntry>();

    [System.Serializable]
    public struct StampSpriteEntry
    {
        public string stampId;
        public Sprite sprite;
    }

    [Header("Scene References")]
    [SerializeField] private CameraFollowController _cameraFollow;
    [SerializeField] private PlayerController _playerController;

    // ── UI 参照 ───────────────────────────────────────────────────────────
    private VisualElement _photoRoot;
    private VisualElement _stampOverlay;
    private VisualElement _stampMenu;
    private VisualElement _stampTrash;
    private VisualElement _colorPickerBar;
    private VisualElement _eyedropperOverlay;
    private VisualElement _eyedropperCursor;
    private VisualElement _eyedropperColorCircle;
    private VisualElement _photoPreviewModal;
    private VisualElement _previewImage;
    private Button _btnExit;
    private Button _btnShutter;
    private Button _btnStampOpen;
    private Button _btnThumbnail;
    private Button _btnPreviewClose;
    private ScrollView _stampGrid;

    // HUD 上の通常ボタン群（撮影モード中に隠す）
    private VisualElement _hudTopRight;
    private VisualElement _hudBottom;

    // ── ロジック ─────────────────────────────────────────────────────────
    private readonly PhotoModeLogic _photoLogic = new();
    private readonly StampOverlayLogic _stampLogic = new();
    private readonly CameraPhotoModeLogic _cameraLogic = new();

    // スタンプ VisualElement ↔ StampData の双方向マップ
    private readonly Dictionary<StampData, VisualElement> _stampViews = new();
    private readonly Dictionary<VisualElement, StampData> _stampDataMap = new();

    // 選択中の色変えスタンプ（カラーパレット操作対象）
    private StampData _activeColorStamp;
    private readonly Dictionary<StampData, StampColorPickerLogic> _colorPickers = new();
    private readonly Dictionary<StampData, Color> _stampColors = new();

    // テキストスタンプの編集ロジック
    private readonly Dictionary<StampData, TextStampLogic> _textLogics = new();

    // ドラッグ追跡
    private bool _draggingStampFromMenu;
    private StampDefinition _dragSourceDef;
    private VisualElement _dragGhost;
    private VisualElement _dragSourceItem; // メニュー内の元アイテム

    // 配置済みスタンプのドラッグ
    private StampData _draggingPlacedStamp;
    private Vector2 _dragOffset; // タッチ → スタンプ中心のオフセット

    // 最後のスタンプメニュータブ
    private int _lastStampTabIndex;

    // 撮影サムネイル
    private Texture2D _lastPhoto;

    // 2 本指追跡
    private bool _twoFingerActive;

    // スポイトサンプリング（毎フレーム全画面 readback を避けるためスロットル）
    private Texture2D _eyedropTex;
    private float _lastSampleTime = -1f;
    private const float SampleInterval = 0.05f; // 50ms ≒ 20Hz

    // パレット色
    private static readonly Color[] PaletteColors =
    {
        Color.white, Color.black, Color.red,
        new Color(1f, 0.5f, 0f), Color.yellow, Color.green,
        new Color(0f, 0.5f, 0f), Color.cyan, Color.blue,
        new Color(0.5f, 0f, 1f), Color.magenta, Color.gray,
    };

    private readonly Dictionary<string, Sprite> _stampSpriteMap = new();

    // ── 初期化 ───────────────────────────────────────────────────────────

    /// <summary>WorldHUDController から呼び出す。</summary>
    public void Initialize(VisualElement hudRoot)
    {
        if (_photoModeHUDAsset == null || _photoRoot != null) return;

        foreach (var entry in _stampSprites)
            if (entry.sprite != null && !string.IsNullOrEmpty(entry.stampId))
                _stampSpriteMap[entry.stampId] = entry.sprite;

        _hudTopRight = hudRoot.Q<VisualElement>("hud-top-right");
        _hudBottom = hudRoot.Q<VisualElement>("hud-bottom");

        _photoRoot = _photoModeHUDAsset.Instantiate();
        _photoRoot.style.position = Position.Absolute;
        _photoRoot.style.top = 0; _photoRoot.style.left = 0;
        _photoRoot.style.right = 0; _photoRoot.style.bottom = 0;
        _photoRoot.style.display = DisplayStyle.None;
        hudRoot.Add(_photoRoot);

        BindUI();
        BuildStampMenu(_lastStampTabIndex);
        BuildColorPicker();
    }

    // ── 撮影モード入退 ───────────────────────────────────────────────────

    public void Enter()
    {
        if (_photoLogic.IsPhotoMode || _photoRoot == null) return;
        _photoLogic.Enter();

        _hudTopRight?.AddToClassList("hud-hidden");
        _hudBottom?.AddToClassList("hud-hidden");
        _photoRoot.style.display = DisplayStyle.Flex;

        if (_playerController != null)
            _playerController.IsPhotoMode = true;
    }

    public void Exit()
    {
        if (!_photoLogic.IsPhotoMode) return;
        _photoLogic.Exit();

        _hudTopRight?.RemoveFromClassList("hud-hidden");
        _hudBottom?.RemoveFromClassList("hud-hidden");
        _photoRoot.style.display = DisplayStyle.None;

        if (_playerController != null)
            _playerController.IsPhotoMode = false;

        _cameraLogic.Reset();
        _cameraFollow?.ClearPhotoOffset();

        CloseStampMenu();
        CloseColorPicker();
        CloseEyedropper();
        HideTrash();
    }

    // ── Unity ライフサイクル ─────────────────────────────────────────────

    private void Update()
    {
        if (!_photoLogic.IsPhotoMode) return;

        HandleTouches();
        ApplyCameraOffset();
        TickColorPickers();
    }

    private void OnDisable()
    {
        if (_photoLogic.IsPhotoMode)
            Exit();
    }

    // ── UI バインド ──────────────────────────────────────────────────────

    private void BindUI()
    {
        _stampOverlay = _photoRoot.Q<VisualElement>("stamp-overlay");
        _stampMenu = _photoRoot.Q<VisualElement>("stamp-menu");
        _stampTrash = _photoRoot.Q<VisualElement>("stamp-trash");
        _colorPickerBar = _photoRoot.Q<VisualElement>("color-picker-bar");
        _eyedropperOverlay = _photoRoot.Q<VisualElement>("eyedropper-overlay");
        _eyedropperCursor = _photoRoot.Q<VisualElement>("eyedropper-cursor");
        _eyedropperColorCircle = _photoRoot.Q<VisualElement>("eyedropper-color-circle");
        _photoPreviewModal = _photoRoot.Q<VisualElement>("photo-preview-modal");
        _previewImage = _photoRoot.Q<VisualElement>("preview-image");
        _stampGrid = _photoRoot.Q<ScrollView>("stamp-grid");
        _btnExit = _photoRoot.Q<Button>("btn-photo-exit");
        _btnShutter = _photoRoot.Q<Button>("btn-shutter");
        _btnStampOpen = _photoRoot.Q<Button>("btn-stamp-open");
        _btnThumbnail = _photoRoot.Q<Button>("btn-thumbnail");
        _btnPreviewClose = _photoRoot.Q<Button>("btn-preview-close");

        _btnExit.clicked += Exit;
        _btnShutter.clicked += OnShutterClicked;
        _btnStampOpen.clicked += ToggleStampMenu;
        _btnThumbnail.clicked += ShowPhotoPreview;
        _btnPreviewClose.clicked += ClosePhotoPreview;

        _photoRoot.RegisterCallback<PointerMoveEvent>(OnRootPointerMove);
        _photoRoot.RegisterCallback<PointerUpEvent>(OnRootPointerUp);

        // スタンプメニュータブ
        for (int i = 0; i < 3; i++)
        {
            var idx = i;
            _photoRoot.Q<Button>($"stamp-tab-{i}")?.RegisterCallback<ClickEvent>(_ => SelectStampTab(idx));
        }
    }

    // ── スタンプメニュー ─────────────────────────────────────────────────

    private void ToggleStampMenu()
    {
        bool open = _stampMenu.ClassListContains("photo-hidden");
        if (open)
        {
            _stampMenu.RemoveFromClassList("photo-hidden");
            RefreshStampGrid(_lastStampTabIndex);
        }
        else
        {
            CloseStampMenu();
        }
    }

    private void CloseStampMenu() => _stampMenu.AddToClassList("photo-hidden");

    private void SelectStampTab(int idx)
    {
        _lastStampTabIndex = idx;
        for (int i = 0; i < 3; i++)
        {
            var btn = _photoRoot.Q<Button>($"stamp-tab-{i}");
            if (btn == null) continue;
            if (i == idx) btn.AddToClassList("stamp-tab--active");
            else btn.RemoveFromClassList("stamp-tab--active");
        }
        RefreshStampGrid(idx);
    }

    private void BuildStampMenu(int tabIdx)
    {
        SelectStampTab(tabIdx);
    }

    private void RefreshStampGrid(int tabIdx)
    {
        if (_stampGrid == null) return;
        _stampGrid.Clear();

        var cat = tabIdx switch
        {
            1 => StampCategory.Premium,
            2 => StampCategory.Purchased,
            _ => StampCategory.Default,
        };

        bool hasPremium = UserManager.Instance?.Capabilities?.inviteRoomCreate ?? false;

        // 横スクロール用の行コンテナ
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.flexWrap = Wrap.Wrap;

        foreach (var def in StampCatalog.All)
        {
            if (def.Category != cat) continue;
            bool locked = def.Category == StampCategory.Premium && !hasPremium;

            var item = BuildStampMenuItem(def, locked);
            row.Add(item);
        }

        // 購入済みタブは空の場合に案内テキストを表示
        if (cat == StampCategory.Purchased && row.childCount == 0)
        {
            var empty = new Label("購入済みスタンプはありません");
            empty.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.6f));
            empty.style.fontSize = 13;
            empty.style.marginTop = 12;
            empty.style.unityTextAlign = TextAnchor.MiddleCenter;
            row.Add(empty);
        }

        _stampGrid.Add(row);
    }

    // スプライトがある場合は backgroundImage の VisualElement、なければ Label を返す。
    private VisualElement MakeStampIcon(StampDefinition def, string labelClass, string imageClass)
    {
        if (!def.IsText && _stampSpriteMap.TryGetValue(def.Id, out var sprite))
        {
            var img = new VisualElement();
            img.AddToClassList(imageClass);
            img.style.backgroundImage = new StyleBackground(sprite);
            return img;
        }
        var label = new Label(def.IsText ? "A" : def.Label);
        label.AddToClassList(labelClass);
        return label;
    }

    private VisualElement BuildStampMenuItem(StampDefinition def, bool locked)
    {
        var item = new VisualElement();
        item.AddToClassList("stamp-menu-item");

        item.Add(MakeStampIcon(def, "stamp-menu-item__label", "stamp-menu-item__image"));

        if (def.IsColorable)
        {
            var dot = new VisualElement();
            dot.AddToClassList("stamp-menu-item__color-dot");
            dot.style.backgroundColor = new StyleColor(Color.red);
            item.Add(dot);
        }

        if (locked)
        {
            var lockIcon = new Label("🔒");
            lockIcon.AddToClassList("stamp-menu-item__lock");
            item.Add(lockIcon);
            return item; // ロック済みはドラッグ不可
        }

        // ドラッグ操作
        item.RegisterCallback<PointerDownEvent>(e =>
        {
            e.StopPropagation();
            StartDragFromMenu(def, item, e.position);
            item.CapturePointer(e.pointerId);
        });

        return item;
    }

    // ── スタンプドラッグ（メニュー → スクリーン） ────────────────────────

    private void StartDragFromMenu(StampDefinition def, VisualElement srcItem, Vector2 pos)
    {
        _draggingStampFromMenu = true;
        _dragSourceDef = def;
        _dragSourceItem = srcItem;
        srcItem.AddToClassList("stamp-menu-item--dragging");

        // ゴーストを作成して追従させる
        _dragGhost = new VisualElement();
        _dragGhost.AddToClassList("stamp-drag-ghost");
        _dragGhost.Add(MakeStampIcon(def, "stamp-drag-ghost__label", "stamp-drag-ghost__image"));
        _dragGhost.style.position = Position.Absolute;
        _dragGhost.pickingMode = PickingMode.Ignore;
        _photoRoot.Add(_dragGhost);
        UpdateGhostPosition(pos);
    }

    private void UpdateGhostPosition(Vector2 pos)
    {
        if (_dragGhost == null) return;
        _dragGhost.style.left = pos.x - 36f;
        _dragGhost.style.top = pos.y - 36f;
    }

    private void OnRootPointerMove(PointerMoveEvent e)
    {
        if (!_draggingStampFromMenu) return;
        UpdateGhostPosition(e.position);
    }

    private void OnRootPointerUp(PointerUpEvent e)
    {
        if (!_draggingStampFromMenu) return;

        // メニュー領域の上端を取得
        float menuTop = _stampMenu.resolvedStyle.top;
        bool aboveMenu = !_stampMenu.ClassListContains("photo-hidden")
            && e.position.y < menuTop;
        // メニューが閉じている場合も画面上部なら配置
        bool menuHidden = _stampMenu.ClassListContains("photo-hidden");

        if (aboveMenu || menuHidden)
        {
            // スタンプを配置
            var normalizedPos = new Vector2(
                e.position.x / _photoRoot.resolvedStyle.width,
                e.position.y / _photoRoot.resolvedStyle.height);
            PlaceStamp(_dragSourceDef, normalizedPos);
            CloseStampMenu();
        }

        FinishDragFromMenu();
    }

    private void FinishDragFromMenu()
    {
        _draggingStampFromMenu = false;
        _dragSourceItem?.RemoveFromClassList("stamp-menu-item--dragging");
        _dragSourceItem = null;
        _dragSourceDef = null;

        if (_dragGhost != null)
        {
            _photoRoot.Remove(_dragGhost);
            _dragGhost = null;
        }
    }

    // ── スタンプ配置・操作 ────────────────────────────────────────────────

    private void PlaceStamp(StampDefinition def, Vector2 normalizedPos)
    {
        var stamp = _stampLogic.AddStamp(def.Id, normalizedPos);
        var view = CreateStampView(def, stamp);
        PositionStampView(stamp, view);
        _stampOverlay.Add(view);
        _stampViews[stamp] = view;
        _stampDataMap[view] = stamp;

        if (def.IsText)
        {
            var textLogic = new TextStampLogic();
            _textLogics[stamp] = textLogic;
            BeginTextEdit(stamp, view, textLogic);
        }

        if (def.IsColorable)
        {
            _stampColors[stamp] = PaletteColors[0];
            var picker = new StampColorPickerLogic(PaletteColors[0]);
            picker.OnColorConfirmed += color =>
            {
                _stampColors[stamp] = color;
                ApplyColorToStampView(stamp, view);
            };
            _colorPickers[stamp] = picker;
            ShowColorPicker(stamp);
        }
    }

    private VisualElement CreateStampView(StampDefinition def, StampData stamp)
    {
        var container = new VisualElement();
        container.AddToClassList("stamp-placed");

        if (def.IsText)
        {
            var textLabel = new Label("");
            textLabel.AddToClassList("stamp-placed__label");
            container.Add(textLabel);

            switch (def.TextVariant)
            {
                case TextStampVariant.WhiteRound: container.AddToClassList("text-stamp-white"); break;
                case TextStampVariant.BlackRound: container.AddToClassList("text-stamp-black"); break;
                default: container.AddToClassList("text-stamp-clear"); break;
            }
        }
        else
        {
            container.Add(MakeStampIcon(def, "stamp-placed__label", "stamp-placed__image"));
        }

        if (def.IsColorable)
        {
            var dot = new VisualElement();
            dot.AddToClassList("stamp-placed__color-dot");
            dot.style.backgroundColor = new StyleColor(PaletteColors[0]);
            container.Add(dot);
        }

        // タッチイベント: 配置済みスタンプのドラッグ
        container.RegisterCallback<PointerDownEvent>(e =>
        {
            e.StopPropagation();
            _draggingPlacedStamp = stamp;
            var rootPos = _stampOverlay.WorldToLocal(container.LocalToWorld(Vector2.zero));
            _dragOffset = e.localPosition;
            container.CapturePointer(e.pointerId);
            ShowTrash();

            // テキストスタンプ再タップで編集再開
            if (def.IsText && _textLogics.TryGetValue(stamp, out var tl))
                if (tl.EditState == TextStampEditState.Completed)
                    BeginTextEdit(stamp, container, tl);
        });

        container.RegisterCallback<PointerMoveEvent>(e =>
        {
            if (_draggingPlacedStamp != stamp) return;
            var newPos = (Vector2)e.position - _dragOffset;
            container.style.left = newPos.x;
            container.style.top = newPos.y;

            float nx = newPos.x / _stampOverlay.resolvedStyle.width;
            float ny = newPos.y / _stampOverlay.resolvedStyle.height;
            _stampLogic.MoveStamp(stamp, new Vector2(nx, ny));

            // ゴミ箱判定: 画面下部中央エリア
            bool overTrash = IsOverTrash(newPos);
            SetTrashHighlight(overTrash);
        });

        container.RegisterCallback<PointerUpEvent>(e =>
        {
            if (_draggingPlacedStamp != stamp) return;
            _draggingPlacedStamp = null;
            HideTrash();

            var finalPos = new Vector2(container.resolvedStyle.left, container.resolvedStyle.top);
            if (IsOverTrash(finalPos))
                RemovePlacedStamp(stamp, container);
        });

        return container;
    }

    private void PositionStampView(StampData stamp, VisualElement view)
    {
        float w = _stampOverlay.resolvedStyle.width;
        float h = _stampOverlay.resolvedStyle.height;
        view.style.left = stamp.Position.x * w - 40f;
        view.style.top = stamp.Position.y * h - 40f;
    }

    private void RemovePlacedStamp(StampData stamp, VisualElement view)
    {
        _stampLogic.RemoveStamp(stamp);
        _stampOverlay.Remove(view);
        _stampViews.Remove(stamp);
        _stampDataMap.Remove(view);
        _colorPickers.Remove(stamp);
        _stampColors.Remove(stamp);
        _textLogics.Remove(stamp);

        if (_activeColorStamp == stamp)
        {
            _activeColorStamp = null;
            CloseColorPicker();
        }
    }

    // ── テキストスタンプ編集 ─────────────────────────────────────────────

    private void BeginTextEdit(StampData stamp, VisualElement view, TextStampLogic logic)
    {
        logic.BeginEditing();
        var label = view.Q<Label>("stamp-placed__label") ?? view.Q<Label>();
        if (label == null) return;

        // UI Toolkit の TextField を使ってキーボード入力を受け取る
        var tf = view.Q<TextField>();
        if (tf == null)
        {
            tf = new TextField();
            tf.style.position = Position.Absolute;
            tf.style.width = Length.Percent(100);
            tf.style.height = Length.Percent(100);
            tf.style.opacity = 0; // 見た目は Label に反映
            view.Add(tf);

            tf.RegisterCallback<ChangeEvent<string>>(e =>
            {
                logic.UpdateText(e.newValue);
                label.text = e.newValue;
            });
            tf.RegisterCallback<FocusOutEvent>(_ => FinishTextEdit(stamp, view, logic));
        }
        tf.style.display = DisplayStyle.Flex;
        tf.schedule.Execute(() => tf.Focus()).StartingIn(50);

        // テキストスタンプ以外のタップで編集完了
        _photoRoot.RegisterCallback<PointerDownEvent>(FinishTextEditOnOutsideTap);
    }

    private void FinishTextEdit(StampData stamp, VisualElement view, TextStampLogic logic)
    {
        if (logic.EditState != TextStampEditState.Editing) return;
        logic.CompleteEditing();
        var tf = view.Q<TextField>();
        if (tf != null) tf.style.display = DisplayStyle.None;
        _photoRoot.UnregisterCallback<PointerDownEvent>(FinishTextEditOnOutsideTap);
    }

    private void FinishTextEditOnOutsideTap(PointerDownEvent e)
    {
        // 編集中スタンプを全て完了させる
        foreach (var (stamp, logic) in _textLogics)
        {
            if (logic.EditState == TextStampEditState.Editing &&
                _stampViews.TryGetValue(stamp, out var view))
                FinishTextEdit(stamp, view, logic);
        }
    }

    // ── カラーパレット ───────────────────────────────────────────────────

    private void BuildColorPicker()
    {
        var scroll = _photoRoot.Q<ScrollView>("color-scroll");
        if (scroll == null) return;
        scroll.Clear();

        // スポイトボタン
        var eyedropBtn = new Button(() => EnterEyedropperMode());
        eyedropBtn.AddToClassList("color-eyedropper-btn");
        eyedropBtn.text = "🔍";
        scroll.Add(eyedropBtn);

        // カラースウォッチ
        foreach (var col in PaletteColors)
        {
            var c = col;
            var swatch = new VisualElement();
            swatch.AddToClassList("color-swatch");
            swatch.style.backgroundColor = new StyleColor(c);
            swatch.RegisterCallback<ClickEvent>(_ => SelectPaletteColor(c));
            scroll.Add(swatch);
        }
    }

    private void ShowColorPicker(StampData stamp)
    {
        _activeColorStamp = stamp;
        _colorPickerBar.RemoveFromClassList("photo-hidden");
    }

    private void CloseColorPicker()
    {
        _activeColorStamp = null;
        _colorPickerBar.AddToClassList("photo-hidden");
    }

    private void SelectPaletteColor(Color color)
    {
        if (_activeColorStamp == null) return;
        if (!_colorPickers.TryGetValue(_activeColorStamp, out var picker)) return;
        picker.SelectPaletteColor(color);
        _stampColors[_activeColorStamp] = color;
        if (_stampViews.TryGetValue(_activeColorStamp, out var view))
            ApplyColorToStampView(_activeColorStamp, view);
        CloseEyedropper();
    }

    private void ApplyColorToStampView(StampData stamp, VisualElement view)
    {
        if (!_stampColors.TryGetValue(stamp, out var color)) return;
        var label = view.Q<Label>();
        if (label != null) label.style.color = new StyleColor(color);
        var dot = view.Q<VisualElement>("stamp-placed__color-dot");
        if (dot != null) dot.style.backgroundColor = new StyleColor(color);
    }

    private void TickColorPickers()
    {
        foreach (var picker in _colorPickers.Values)
            picker.Tick(Time.deltaTime);
    }

    // ── スポイトモード ───────────────────────────────────────────────────

    private void EnterEyedropperMode()
    {
        if (_activeColorStamp == null) return;
        if (!_colorPickers.TryGetValue(_activeColorStamp, out var picker)) return;
        picker.EnterEyedropper();
        _eyedropperOverlay.RemoveFromClassList("photo-hidden");
    }

    private void CloseEyedropper()
    {
        if (_activeColorStamp != null && _colorPickers.TryGetValue(_activeColorStamp, out var picker))
            picker.ExitEyedropper();
        _eyedropperOverlay.AddToClassList("photo-hidden");
    }

    private void HandleEyedropperTouch()
    {
        if (_eyedropperOverlay.ClassListContains("photo-hidden")) return;
        if (_activeColorStamp == null) return;
        if (!_colorPickers.TryGetValue(_activeColorStamp, out var picker)) return;

        bool anyTouching = false;
        foreach (var t in Touch.activeTouches)
        {
            anyTouching = true;
            Vector2 pos = t.screenPosition;

            // カーソル移動
            _eyedropperCursor.style.left = pos.x;
            _eyedropperCursor.style.top = Screen.height - pos.y;

            // スクリーン色サンプリング（UI 除外は不可のため近似）
            var col = SampleScreenColor(pos);
            picker.SampleColor(col);
            _eyedropperColorCircle.style.backgroundColor = new StyleColor(col);

            if (t.phase == UnityEngine.InputSystem.TouchPhase.Ended ||
                t.phase == UnityEngine.InputSystem.TouchPhase.Canceled)
            {
                picker.OnFingerReleased();
            }
        }

        // エディタ (マウス) フォールバック
        if (!Application.isMobilePlatform && !anyTouching)
        {
            if (Input.GetMouseButton(0))
            {
                var col = SampleScreenColor(Input.mousePosition);
                picker.SampleColor(col);
                _eyedropperColorCircle.style.backgroundColor = new StyleColor(col);
                _eyedropperCursor.style.left = Input.mousePosition.x;
                _eyedropperCursor.style.top = Screen.height - Input.mousePosition.y;
            }
            if (Input.GetMouseButtonUp(0))
                picker.OnFingerReleased();
        }

        if (picker.IsPendingConfirm && !picker.IsEyedropperActive)
        {
            // 色確定済み
            _stampColors[_activeColorStamp] = picker.SelectedColor;
            if (_stampViews.TryGetValue(_activeColorStamp, out var view))
                ApplyColorToStampView(_activeColorStamp, view);
            _eyedropperOverlay.AddToClassList("photo-hidden");
        }
    }

    private Color SampleScreenColor(Vector2 screenPos)
    {
        // 50ms スロットル: 毎フレームの全画面 GPU readback を避ける
        if (Time.realtimeSinceStartup - _lastSampleTime < SampleInterval)
            return _eyedropTex != null ? _eyedropTex.GetPixel(0, 0) : Color.black;

        _lastSampleTime = Time.realtimeSinceStartup;

        var rt = RenderTexture.GetTemporary(Screen.width, Screen.height, 0);
        ScreenCapture.CaptureScreenshotIntoRenderTexture(rt);
        RenderTexture.active = rt;

        if (_eyedropTex == null)
            _eyedropTex = new Texture2D(1, 1, TextureFormat.RGB24, false);

        _eyedropTex.ReadPixels(new Rect((int)screenPos.x, (int)screenPos.y, 1, 1), 0, 0);
        _eyedropTex.Apply();

        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(rt);
        return _eyedropTex.GetPixel(0, 0);
    }

    // ── ゴミ箱 ──────────────────────────────────────────────────────────

    private void ShowTrash() => _stampTrash.RemoveFromClassList("photo-hidden");
    private void HideTrash() => _stampTrash.AddToClassList("photo-hidden");

    private bool IsOverTrash(Vector2 screenPos)
    {
        if (_stampTrash == null) return false;
        var trashBounds = _stampTrash.worldBound;
        return trashBounds.Contains(screenPos);
    }

    private void SetTrashHighlight(bool highlight)
    {
        var icon = _stampTrash.Q<Label>("trash-icon");
        if (icon == null) return;
        icon.style.backgroundColor = new StyleColor(
            highlight ? new Color(0.9f, 0.1f, 0.1f, 0.9f) : new Color(0.78f, 0.2f, 0.2f, 0.75f));
    }

    // ── カメラ制御 (2 本指) ──────────────────────────────────────────────

    private void HandleTouches()
    {
        HandleEyedropperTouch();

        if (!Application.isMobilePlatform) return;

        var activeTouches = Touch.activeTouches;
        if (activeTouches.Count == 2)
        {
            // 上半分 (y > Screen.height * 0.5) のタッチのみカメラ制御に使う
            var t0 = activeTouches[0];
            var t1 = activeTouches[1];
            bool bothUpper = t0.screenPosition.y > Screen.height * 0.5f
                && t1.screenPosition.y > Screen.height * 0.5f;

            if (bothUpper && !_draggingStampFromMenu && _draggingPlacedStamp == null)
            {
                if (!_twoFingerActive)
                {
                    _cameraLogic.BeginTwoFingers(t0.screenPosition, t1.screenPosition);
                    _twoFingerActive = true;
                }
                else
                {
                    _cameraLogic.UpdateTwoFingers(t0.screenPosition, t1.screenPosition, Screen.height);
                }
                return;
            }
        }

        if (_twoFingerActive)
        {
            _cameraLogic.EndTwoFingers();
            _twoFingerActive = false;
        }

        // 1 本指上半分ドラッグ → PlayerController 経由でカメラ回転を 1 フレーム遅延で通す
        // IsPhotoMode=false にすると次フレームの PlayerController.Update() が LookDelta を読み、
        // CameraFollowController.LateUpdate() がカメラを回転させる。
        bool wantCameraRotation = activeTouches.Count == 1
            && activeTouches[0].screenPosition.y > Screen.height * 0.5f
            && activeTouches[0].phase == UnityEngine.InputSystem.TouchPhase.Moved
            && !_draggingStampFromMenu
            && _draggingPlacedStamp == null;

        if (_playerController != null)
            _playerController.IsPhotoMode = !wantCameraRotation;
    }

    private void ApplyCameraOffset()
    {
        _cameraFollow?.SetPhotoOffset(_cameraLogic.ZoomOffset, _cameraLogic.SlideOffset);
    }

    // ── 撮影 ─────────────────────────────────────────────────────────────

    private void OnShutterClicked()
    {
        StartCoroutine(TakePhotoCoroutine());
    }

    private IEnumerator TakePhotoCoroutine()
    {
        // ボタン類を一時的に非表示
        _btnExit.style.display = DisplayStyle.None;
        _btnShutter.style.display = DisplayStyle.None;
        _btnStampOpen.style.display = DisplayStyle.None;
        _btnThumbnail.style.display = DisplayStyle.None;
        _stampMenu.AddToClassList("photo-hidden");
        _colorPickerBar.AddToClassList("photo-hidden");

        yield return new WaitForEndOfFrame();

        var tex = ScreenCapture.CaptureScreenshotAsTexture();

        // ボタン類を復元
        _btnExit.style.display = DisplayStyle.Flex;
        _btnShutter.style.display = DisplayStyle.Flex;
        _btnStampOpen.style.display = DisplayStyle.Flex;

        // カメラロールへ保存
        PhotoSaver.SaveToGallery(tex);

        // サムネイルを表示
        if (_lastPhoto != null)
            Destroy(_lastPhoto);
        _lastPhoto = tex;
        _btnThumbnail.style.backgroundImage = new StyleBackground(tex);
        _btnThumbnail.style.display = DisplayStyle.Flex;
        _btnThumbnail.RemoveFromClassList("photo-hidden");

        FlashMessageController.Current?.Show("写真を保存しました");
    }

    // ── サムネイルプレビュー ─────────────────────────────────────────────

    private void ShowPhotoPreview()
    {
        if (_lastPhoto == null) return;
        _previewImage.style.backgroundImage = new StyleBackground(_lastPhoto);
        _photoPreviewModal.RemoveFromClassList("photo-hidden");
    }

    private void ClosePhotoPreview() => _photoPreviewModal.AddToClassList("photo-hidden");

    // ── 破棄 ─────────────────────────────────────────────────────────────

    private void OnDestroy()
    {
        if (_lastPhoto != null)
            Destroy(_lastPhoto);
        if (_eyedropTex != null)
            Destroy(_eyedropTex);
    }
}
