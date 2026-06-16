using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

/// <summary>地形タブの編集モード（screens-and-modes.md 11.7.2 編集バー）。</summary>
public enum TerrainEditMode
{
    Brush,
    Eraser,
    Shape,
    TypeChange,
    RangeSelect,
    Move,
}

/// <summary>
/// 地形タブの UI 制御（screens-and-modes.md 11.7.2）。WorldEditorController から生成される。
/// 編集バー・高さバー・上方非表示トグル・地形サブタブ・地形一覧・フラッシュメッセージを担当し、
/// 実際の編集処理（TerrainEditLogic / TerrainRenderer / カメラ・タッチ入力）への接続は
/// イベント経由で上位レイヤー（ワールドエディタシーン統合時）が行う。
/// </summary>
public class TerrainTabController
{
    /// <summary>地形一覧の表示項目。</summary>
    public readonly struct TerrainListItem
    {
        public readonly string Name;
        public readonly bool HasTransparency; // 透明ピクセルを含む地形（! 警告）

        public TerrainListItem(string name, bool hasTransparency)
        {
            Name = name;
            HasTransparency = hasTransparency;
        }
    }

    public event Action<TerrainEditMode> ModeChanged;
    public event Action<bool> FloodSelectChanged;  // 範囲選択方法（false = 四角形 / true = 塗りつぶし）
    public event Action<int> HeightChanged;
    public event Action<bool> HideAboveChanged;
    public event Action CopyClicked;
    public event Action PasteClicked;
    public event Action DeselectClicked;
    public event Action<int> SubTabChanged;        // 0 = 利用中 / 1 = 保存・編集 / 2 = 所有
    public event Action<int> TerrainSelected;      // 一覧のインデックス

    public TerrainEditMode Mode { get; private set; } = TerrainEditMode.Brush;
    public bool FloodSelect { get; private set; }
    public int Height { get; private set; }
    public bool HideAbove { get; private set; }
    public int SelectedTerrainIndex { get; private set; } = -1;

    private readonly Button[] _modeButtons;
    private readonly Button[] _subTabButtons;
    private readonly Button _btnCopy;
    private readonly Button _btnPaste;
    private readonly Button _btnDeselect;
    private readonly Button _btnHideAbove;
    private readonly Label _heightLabel;
    private readonly Label _selectMethodLabel;
    private readonly Label _flashLabel;
    private readonly VisualElement _viewOverlay;
    private readonly VisualElement _terrainList;
    private IVisualElementScheduledItem _flashHide;

    public TerrainTabController(VisualElement root)
    {
        if (root == null)
            throw new ArgumentNullException(nameof(root));

        _modeButtons = new[]
        {
            root.Q<Button>("terrain-mode-brush"),
            root.Q<Button>("terrain-mode-eraser"),
            root.Q<Button>("terrain-mode-shape"),
            root.Q<Button>("terrain-mode-type"),
            root.Q<Button>("terrain-mode-select"),
            root.Q<Button>("terrain-mode-move"),
        };
        for (int i = 0; i < _modeButtons.Length; i++)
        {
            var mode = (TerrainEditMode)i;
            _modeButtons[i].clicked += () => OnModeButton(mode);
        }

        _btnCopy = root.Q<Button>("terrain-btn-copy");
        _btnPaste = root.Q<Button>("terrain-btn-paste");
        _btnDeselect = root.Q<Button>("terrain-btn-deselect");
        _btnCopy.clicked += () => CopyClicked?.Invoke();
        _btnPaste.clicked += () => PasteClicked?.Invoke();
        _btnDeselect.clicked += () => DeselectClicked?.Invoke();

        _viewOverlay = root.Q("terrain-view-overlay");
        _btnHideAbove = root.Q<Button>("terrain-btn-hide-above");
        _btnHideAbove.clicked += ToggleHideAbove;
        _heightLabel = root.Q<Label>("terrain-height-label");
        // 単発（タップ）はここで処理。0.5 秒以上の長押し連続移動は TerrainEditSceneController が
        // Pointer.current のポーリングで行う（UI Toolkit の Button キャプチャ挙動で
        // schedule + ポインタイベント方式が不安定なため）。
        root.Q<Button>("terrain-height-up").clicked += () => SetHeight(Height + 1);
        root.Q<Button>("terrain-height-down").clicked += () => SetHeight(Height - 1);

        _selectMethodLabel = root.Q<Label>("terrain-select-method");
        _flashLabel = root.Q<Label>("terrain-flash");

        _subTabButtons = new[]
        {
            root.Q<Button>("terrain-subtab-used"),
            root.Q<Button>("terrain-subtab-saved"),
            root.Q<Button>("terrain-subtab-owned"),
        };
        for (int i = 0; i < _subTabButtons.Length; i++)
        {
            int index = i;
            _subTabButtons[i].clicked += () => SwitchSubTab(index);
        }

        _terrainList = root.Q("terrain-list");
        RefreshHeightLabel();
    }

    // ── 公開 API ─────────────────────────────────────────────────────────────

    /// <summary>3D ビューオーバーレイ（高さバー・上方非表示）の表示切替。地形タブ選択中のみ表示する。</summary>
    public void SetViewOverlayVisible(bool visible) =>
        _viewOverlay.EnableInClassList("overlay-hidden", !visible);

    /// <summary>選択状態に応じてコピー / ペーストボタンの表示を切り替える。</summary>
    public void SetSelectionState(bool hasSelection, bool hasClipboard)
    {
        _btnCopy.EnableInClassList("overlay-hidden", !hasSelection);
        _btnPaste.EnableInClassList("overlay-hidden", !hasClipboard);
        _btnDeselect.EnableInClassList("overlay-hidden", !hasSelection);
    }

    /// <summary>フラッシュメッセージを表示する（自動で消える）。</summary>
    public void ShowFlash(string message)
    {
        _flashLabel.text = message;
        _flashLabel.EnableInClassList("overlay-hidden", false);
        _flashHide?.Pause();
        _flashHide = _flashLabel.schedule
            .Execute(() => _flashLabel.EnableInClassList("overlay-hidden", true))
            .StartingIn(2000);
    }

    /// <summary>地形一覧を再構築する（サブタブ切替・データ取得時に呼ぶ）。</summary>
    public void SetTerrainList(IReadOnlyList<TerrainListItem> items)
    {
        _terrainList.Clear();
        SelectedTerrainIndex = items.Count > 0 ? 0 : -1;
        for (int i = 0; i < items.Count; i++)
        {
            int index = i;
            var item = items[i];

            var card = new VisualElement();
            card.AddToClassList("terrain-item");
            card.EnableInClassList("terrain-item--selected", index == SelectedTerrainIndex);

            var thumb = new VisualElement();
            thumb.AddToClassList("terrain-item-thumb");
            card.Add(thumb);

            var name = new Label(item.Name);
            name.AddToClassList("terrain-item-name");
            card.Add(name);

            if (item.HasTransparency)
            {
                var warn = new Button(() =>
                    ShowFlash("透明領域を含む地形は内側の地形が描画されない場合があります"))
                {
                    text = "!",
                };
                warn.AddToClassList("terrain-item-warn");
                card.Add(warn);
            }

            card.RegisterCallback<ClickEvent>(_ => SelectTerrain(index));
            _terrainList.Add(card);
        }
    }

    public void SetHeight(int height)
    {
        int clamped = height < 0 ? 0
            : height >= TerrainVoxelStore.SizeY ? TerrainVoxelStore.SizeY - 1
            : height;
        if (clamped == Height)
            return;
        Height = clamped;
        RefreshHeightLabel();
        HeightChanged?.Invoke(Height);
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private void OnModeButton(TerrainEditMode mode)
    {
        if (Mode == TerrainEditMode.RangeSelect && mode == TerrainEditMode.RangeSelect)
        {
            // 範囲選択ボタンの再タップ → 選択方法（四角形 / 塗りつぶし）を切替
            FloodSelect = !FloodSelect;
            RefreshSelectMethodLabel();
            FloodSelectChanged?.Invoke(FloodSelect);
            return;
        }

        Mode = mode;
        for (int i = 0; i < _modeButtons.Length; i++)
            _modeButtons[i].EnableInClassList("terrain-mode-btn--active", i == (int)mode);
        _selectMethodLabel.EnableInClassList("overlay-hidden", mode != TerrainEditMode.RangeSelect);
        if (mode == TerrainEditMode.RangeSelect)
            RefreshSelectMethodLabel();
        ModeChanged?.Invoke(mode);
    }

    private void ToggleHideAbove()
    {
        HideAbove = !HideAbove;
        _btnHideAbove.EnableInClassList("terrain-hide-above--active", HideAbove);
        HideAboveChanged?.Invoke(HideAbove);
    }

    private void SwitchSubTab(int index)
    {
        for (int i = 0; i < _subTabButtons.Length; i++)
            _subTabButtons[i].EnableInClassList("terrain-subtab--active", i == index);
        SubTabChanged?.Invoke(index);
    }

    private void SelectTerrain(int index)
    {
        SelectedTerrainIndex = index;
        for (int i = 0; i < _terrainList.childCount; i++)
            _terrainList[i].EnableInClassList("terrain-item--selected", i == index);
        TerrainSelected?.Invoke(index);
    }

    private void RefreshHeightLabel() => _heightLabel.text = Height.ToString();

    private void RefreshSelectMethodLabel() =>
        _selectMethodLabel.text = FloodSelect
            ? "選択方法: 塗りつぶし範囲（ボタン再タップで切替）"
            : "選択方法: 四角形（ボタン再タップで切替）";
}
