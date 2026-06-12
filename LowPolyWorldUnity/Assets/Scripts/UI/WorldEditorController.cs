using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// ワールドエディタ画面コントローラー（screens-and-modes.md セクション 11.7）。
/// WorldCreationManager と連携して WorldSettingsPanelLogic の変更を UI に反映する。
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class WorldEditorController : MonoBehaviour
{
    private UIDocument _doc;
    private VisualElement _root;

    // ── ヘッダー ──────────────────────────────────────────────────────────────
    private Button _btnBack;
    private TextField _fieldWorldName;
    private Button _btnSave;
    private Button _btnPublish;

    // ── ギズモバー ────────────────────────────────────────────────────────────
    private VisualElement _gizmoBar;
    private Button _btnGizmoMove;
    private Button _btnGizmoScale;
    private Button _btnGizmoRotate;

    // ── 3D ビュー ─────────────────────────────────────────────────────────────
    private VisualElement _costBarFill;
    private Label _costLabel;

    // ── タブバー ──────────────────────────────────────────────────────────────
    private Button _tabTerrain;
    private Button _tabObjects;
    private Button _tabGimmicks;
    private Button _tabSettings;
    private Button _btnMinimize;

    // ── タブパネル ────────────────────────────────────────────────────────────
    private VisualElement _panelTerrain;
    private VisualElement _panelObjects;
    private VisualElement _panelGimmicks;
    private VisualElement _panelSettings;
    private VisualElement _tabContent;

    // ── 地形タブ ──────────────────────────────────────────────────────────────
    private TerrainTabController _terrainTab;

    /// <summary>地形タブ UI（シーン統合時に編集ロジック・レンダラーへ接続する）。</summary>
    public TerrainTabController TerrainTab => _terrainTab;

    // ── 設定タブ: 基本 ────────────────────────────────────────────────────────
    private TextField _settingsWorldName;
    private VisualElement _tagChips;
    private VisualElement _tagInputRow;
    private TextField _fieldTagInput;
    private Button _btnTagAdd;
    private Label _tagError;

    // ── 設定タブ: BGM ─────────────────────────────────────────────────────────
    private Label _bgmCurrentName;
    private Label _bgmCurrentAuthor;
    private VisualElement _bgmTrackList;
    private Slider _sliderBgmVolume;
    private Label _labelBgmVolume;

    // ── 設定タブ: 人数上限 ────────────────────────────────────────────────────
    private Label _labelMaxPlayers;
    private Button _btnPlayersInc;
    private Button _btnPlayersDec;
    private Label _labelPlayersHint;

    // ── 設定タブ: 背景 ────────────────────────────────────────────────────────
    private Button _btnBgSolid;
    private Button _btnBgGradient;
    private Button _btnBgTexture;
    private TextField _fieldBgColor;

    // ── 設定タブ: 環境カラー ──────────────────────────────────────────────────
    private TextField _fieldAmbientColor;

    // ── 設定タブ: フォグ ──────────────────────────────────────────────────────
    private Toggle _toggleFog;
    private VisualElement _fogFields;
    private TextField _fieldFogColor;
    private Slider _sliderFogStart;
    private Label _labelFogStart;
    private Slider _sliderFogEnd;
    private Label _labelFogEnd;

    // ── 設定タブ: スクリーンエフェクト ───────────────────────────────────────
    private Button _btnFxNone;
    private Button _btnFxRain;
    private VisualElement _fxIntensityRow;
    private Slider _sliderFxIntensity;
    private Label _labelFxIntensity;

    // ── 設定タブ: 公開 ────────────────────────────────────────────────────────
    private Toggle _togglePublic;
    private Label _labelVersion;
    private Button _btnPublishSettings;
    private VisualElement _publishErrors;
    private VisualElement _backupSection;

    // ── 状態 ─────────────────────────────────────────────────────────────────
    private WorldSettingsPanelLogic _settingsLogic;
    private WorldPublishValidator _publishValidator;
    private int _textureCost;
    private int _objectCount;
    private bool _hasThumbnail;
    private int _publishedVersion;
    private bool _tabContentVisible = true;
    private string _bgmSoundId = "none";

    // ── BGM トラック（ショップ購入曲は API 取得後に追記） ─────────────────────
    private readonly List<WorldMusicTrack> _availableTracks =
        new(WorldMusicLibrary.BuiltInTracks);

    private void Awake()
    {
        _doc = GetComponent<UIDocument>();
        _publishValidator = new WorldPublishValidator();
    }

    private void OnEnable()
    {
        _root = _doc.rootVisualElement.Q("editor-root");
        BindElements();
        RegisterCallbacks();
        BuildBgmTrackList();
        _terrainTab = new TerrainTabController(_root);
    }

    // ── 公開 API ─────────────────────────────────────────────────────────────

    /// <summary>
    /// ワールド定義を読み込んで UI に反映する。
    /// WorldManageTabController のカードタップ時に呼び出す。
    /// </summary>
    public void LoadWorld(
        WorldDefinitionJson def,
        bool isPremium,
        int publishedVersion = 0,
        bool hasThumbnail = false)
    {
        // WorldCreationManager の settingsLogic と同一インスタンスを共有することで
        // CommitSettingsChanges が正しくエディタの変更を保存できる。
        var mgr = WorldCreationManager.Instance;
        if (mgr != null)
        {
            mgr.LoadWorldDef(def, isPremium);
            _settingsLogic = mgr.SettingsLogic;
        }
        else
        {
            // マネージャーなし（エディタ外テスト等）のフォールバック
            _settingsLogic = new WorldSettingsPanelLogic(isPremium);
            _settingsLogic.LoadFrom(def);
        }

        _publishedVersion = publishedVersion;
        _hasThumbnail = hasThumbnail;

        ApplySettingsToUI();
        UpdateCostDisplay(0, 0);
        UpdatePublishButton();

        // バックアップはプレミアムのみ
        _backupSection?.EnableInClassList("overlay-hidden", !isPremium);
    }

    /// <summary>テクスチャコスト・オブジェクト数を更新してコスト表示を刷新する。</summary>
    public void UpdateCostDisplay(int textureCost, int objectCount)
    {
        _textureCost = textureCost;
        _objectCount = objectCount;
        RefreshCostDisplay();
    }

    // ── 要素バインド ─────────────────────────────────────────────────────────

    private void BindElements()
    {
        _btnBack = _root.Q<Button>("btn-back");
        _fieldWorldName = _root.Q<TextField>("field-world-name");
        _btnSave = _root.Q<Button>("btn-save");
        _btnPublish = _root.Q<Button>("btn-publish");

        _gizmoBar = _root.Q("gizmo-bar");
        _btnGizmoMove = _root.Q<Button>("btn-gizmo-move");
        _btnGizmoScale = _root.Q<Button>("btn-gizmo-scale");
        _btnGizmoRotate = _root.Q<Button>("btn-gizmo-rotate");

        _costBarFill = _root.Q("cost-bar-fill");
        _costLabel = _root.Q<Label>("cost-label");

        _tabTerrain = _root.Q<Button>("tab-terrain");
        _tabObjects = _root.Q<Button>("tab-objects");
        _tabGimmicks = _root.Q<Button>("tab-gimmicks");
        _tabSettings = _root.Q<Button>("tab-settings");
        _btnMinimize = _root.Q<Button>("btn-minimize");
        _tabContent = _root.Q("tab-content");

        _panelTerrain = _root.Q("panel-terrain");
        _panelObjects = _root.Q("panel-objects");
        _panelGimmicks = _root.Q("panel-gimmicks");
        _panelSettings = _root.Q("panel-settings");

        // 設定タブ
        _settingsWorldName = _root.Q<TextField>("settings-world-name");
        _tagChips = _root.Q("tag-chips");
        _tagInputRow = _root.Q("tag-input-row");
        _fieldTagInput = _root.Q<TextField>("field-tag-input");
        _btnTagAdd = _root.Q<Button>("btn-tag-add");
        _tagError = _root.Q<Label>("tag-error");

        _bgmCurrentName = _root.Q<Label>("bgm-current-name");
        _bgmCurrentAuthor = _root.Q<Label>("bgm-current-author");
        _bgmTrackList = _root.Q("bgm-track-list");
        _sliderBgmVolume = _root.Q<Slider>("slider-bgm-volume");
        _labelBgmVolume = _root.Q<Label>("label-bgm-volume");

        _labelMaxPlayers = _root.Q<Label>("label-max-players");
        _btnPlayersInc = _root.Q<Button>("btn-players-inc");
        _btnPlayersDec = _root.Q<Button>("btn-players-dec");
        _labelPlayersHint = _root.Q<Label>("label-players-hint");

        _btnBgSolid = _root.Q<Button>("btn-bg-solid");
        _btnBgGradient = _root.Q<Button>("btn-bg-gradient");
        _btnBgTexture = _root.Q<Button>("btn-bg-texture");
        _fieldBgColor = _root.Q<TextField>("field-bg-color");

        _fieldAmbientColor = _root.Q<TextField>("field-ambient-color");

        _toggleFog = _root.Q<Toggle>("toggle-fog");
        _fogFields = _root.Q("fog-fields");
        _fieldFogColor = _root.Q<TextField>("field-fog-color");
        _sliderFogStart = _root.Q<Slider>("slider-fog-start");
        _labelFogStart = _root.Q<Label>("label-fog-start");
        _sliderFogEnd = _root.Q<Slider>("slider-fog-end");
        _labelFogEnd = _root.Q<Label>("label-fog-end");

        _btnFxNone = _root.Q<Button>("btn-fx-none");
        _btnFxRain = _root.Q<Button>("btn-fx-rain");
        _fxIntensityRow = _root.Q("fx-intensity-row");
        _sliderFxIntensity = _root.Q<Slider>("slider-fx-intensity");
        _labelFxIntensity = _root.Q<Label>("label-fx-intensity");

        _togglePublic = _root.Q<Toggle>("toggle-public");
        _labelVersion = _root.Q<Label>("label-version");
        _btnPublishSettings = _root.Q<Button>("btn-publish-settings");
        _publishErrors = _root.Q("publish-errors");
        _backupSection = _root.Q("backup-section");
    }

    private void RegisterCallbacks()
    {
        _btnBack.clicked += OnBackClicked;
        _btnSave.clicked += OnSaveClicked;
        _btnPublish.clicked += OnPublishClicked;

        _tabTerrain.clicked += () => SwitchTab(0);
        _tabObjects.clicked += () => SwitchTab(1);
        _tabGimmicks.clicked += () => SwitchTab(2);
        _tabSettings.clicked += () => SwitchTab(3);
        _btnMinimize.clicked += ToggleTabMinimize;

        _btnGizmoMove.clicked += () => SetGizmo(0);
        _btnGizmoScale.clicked += () => SetGizmo(1);
        _btnGizmoRotate.clicked += () => SetGizmo(2);

        // 設定タブ
        _fieldWorldName.RegisterValueChangedCallback(e => OnWorldNameChanged(e.newValue));
        _settingsWorldName.RegisterValueChangedCallback(e => OnWorldNameChanged(e.newValue));

        _btnTagAdd.clicked += OnTagAddClicked;
        _fieldTagInput.RegisterCallback<KeyDownEvent>(e =>
        {
            if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                OnTagAddClicked();
        });

        _sliderBgmVolume.RegisterValueChangedCallback(e =>
        {
            _settingsLogic?.SetBgmVolume((int)e.newValue);
            if (_labelBgmVolume != null) _labelBgmVolume.text = $"{(int)e.newValue}%";
        });

        _btnPlayersInc.clicked += () => ChangeMaxPlayers(+1);
        _btnPlayersDec.clicked += () => ChangeMaxPlayers(-1);

        _toggleFog.RegisterValueChangedCallback(e =>
        {
            _fogFields.EnableInClassList("overlay-hidden", !e.newValue);
            if (_settingsLogic != null)
            {
                var fog = _settingsLogic.Fog ?? new FogData();
                fog.enabled = e.newValue;
                _settingsLogic.SetFog(fog);
            }
        });

        _sliderFogStart.RegisterValueChangedCallback(e =>
        {
            if (_labelFogStart != null) _labelFogStart.text = $"{(int)e.newValue}m";
            UpdateFogData();
        });
        _sliderFogEnd.RegisterValueChangedCallback(e =>
        {
            if (_labelFogEnd != null) _labelFogEnd.text = $"{(int)e.newValue}m";
            UpdateFogData();
        });

        _btnFxNone.clicked += () => SetScreenEffect("none");
        _btnFxRain.clicked += () => SetScreenEffect("rain");
        _sliderFxIntensity.RegisterValueChangedCallback(e =>
        {
            if (_labelFxIntensity != null) _labelFxIntensity.text = $"{(int)e.newValue}%";
            UpdateScreenEffect();
        });

        _togglePublic.RegisterValueChangedCallback(e => _settingsLogic?.SetIsPublic(e.newValue));
        _fieldBgColor.RegisterValueChangedCallback(e => UpdateBackground());
        _fieldAmbientColor.RegisterValueChangedCallback(e => _settingsLogic?.SetAmbientColor(e.newValue));

        _btnPublishSettings.clicked += OnPublishClicked;
    }

    // ── タブ切り替え ─────────────────────────────────────────────────────────

    private void SwitchTab(int index)
    {
        var tabs = new[] { _tabTerrain, _tabObjects, _tabGimmicks, _tabSettings };
        var panels = new[] { _panelTerrain, _panelObjects, _panelGimmicks, _panelSettings };

        for (int i = 0; i < tabs.Length; i++)
        {
            bool active = i == index;
            tabs[i].EnableInClassList("bottom-tab--active", active);
            panels[i].EnableInClassList("overlay-hidden", !active);
        }

        // 高さバー・上方非表示は地形タブ選択中のみ 3D ビューに表示
        _terrainTab?.SetViewOverlayVisible(index == 0);

        if (!_tabContentVisible)
        {
            _tabContentVisible = true;
            _tabContent.EnableInClassList("overlay-hidden", false);
            _btnMinimize.text = "▽";
        }
    }

    private void ToggleTabMinimize()
    {
        _tabContentVisible = !_tabContentVisible;
        _tabContent.EnableInClassList("overlay-hidden", !_tabContentVisible);
        _btnMinimize.text = _tabContentVisible ? "▽" : "△";
    }

    // ── ギズモ ────────────────────────────────────────────────────────────────

    private void SetGizmo(int index)
    {
        var btns = new[] { _btnGizmoMove, _btnGizmoScale, _btnGizmoRotate };
        for (int i = 0; i < btns.Length; i++)
            btns[i].EnableInClassList("gizmo-btn--active", i == index);
        // TODO: ギズモシステムに通知
    }

    public void ShowGizmoBar(bool show) =>
        _gizmoBar?.EnableInClassList("overlay-hidden", !show);

    // ── テクスチャコスト表示 ─────────────────────────────────────────────────

    private void RefreshCostDisplay()
    {
        if (_costLabel == null) return;
        _costLabel.text = $"{_textureCost} / {TextureCostCalculator.CostLimit}";

        float ratio = TextureCostCalculator.UsageRatio(_textureCost);
        if (_costBarFill != null)
        {
            _costBarFill.style.width = Length.Percent(ratio * 100f);
            var color = ratio >= 1f
                ? new Color(1f, 0.4f, 0.3f)
                : ratio >= 0.8f
                    ? new Color(1f, 0.7f, 0.2f)
                    : new Color(0.3f, 0.6f, 1f);
            _costBarFill.style.backgroundColor = color;
        }
    }

    // ── 設定→UI 反映 ─────────────────────────────────────────────────────────

    private void ApplySettingsToUI()
    {
        if (_settingsLogic == null) return;

        // ワールド名
        _fieldWorldName?.SetValueWithoutNotify(_settingsLogic.WorldName);
        _settingsWorldName?.SetValueWithoutNotify(_settingsLogic.WorldName);

        // タグ
        RefreshTagChips();

        // BGM
        _bgmSoundId = _settingsLogic.BgmSoundId;
        _sliderBgmVolume?.SetValueWithoutNotify(_settingsLogic.BgmVolume);
        if (_labelBgmVolume != null) _labelBgmVolume.text = $"{_settingsLogic.BgmVolume}%";
        RefreshBgmCurrentDisplay();
        RefreshBgmTrackList();

        // 人数上限
        if (_labelMaxPlayers != null) _labelMaxPlayers.text = _settingsLogic.MaxPlayers.ToString();
        UpdatePlayersHint();

        // 背景
        _fieldBgColor?.SetValueWithoutNotify(
            _settingsLogic.Background?.colors?.Length > 0
                ? _settingsLogic.Background.colors[0]
                : "#111111");

        // 環境カラー
        _fieldAmbientColor?.SetValueWithoutNotify(_settingsLogic.AmbientColor);

        // フォグ
        var fog = _settingsLogic.Fog ?? new FogData();
        _toggleFog?.SetValueWithoutNotify(fog.enabled);
        _fogFields?.EnableInClassList("overlay-hidden", !fog.enabled);
        _fieldFogColor?.SetValueWithoutNotify(fog.color);
        _sliderFogStart?.SetValueWithoutNotify(fog.startDistance);
        if (_labelFogStart != null) _labelFogStart.text = $"{(int)fog.startDistance}m";
        _sliderFogEnd?.SetValueWithoutNotify(fog.endDistance);
        if (_labelFogEnd != null) _labelFogEnd.text = $"{(int)fog.endDistance}m";

        // スクリーンエフェクト
        var fx = _settingsLogic.ScreenEffect ?? new ScreenEffectData();
        bool hasEffect = fx.type != "none";
        _btnFxNone?.EnableInClassList("radio-btn--active", !hasEffect);
        _btnFxRain?.EnableInClassList("radio-btn--active", fx.type == "rain");
        _fxIntensityRow?.EnableInClassList("overlay-hidden", !hasEffect);
        _sliderFxIntensity?.SetValueWithoutNotify(fx.intensity);
        if (_labelFxIntensity != null) _labelFxIntensity.text = $"{fx.intensity}%";

        // 公開状態
        _togglePublic?.SetValueWithoutNotify(_settingsLogic.IsPublic);

        // バージョン
        if (_labelVersion != null) _labelVersion.text = _publishedVersion == 0 ? "未公開" : $"バージョン {_publishedVersion}";
    }

    // ── タグ UI ───────────────────────────────────────────────────────────────

    private void RefreshTagChips()
    {
        _tagChips?.Clear();
        if (_settingsLogic == null) return;

        foreach (var tag in _settingsLogic.Tags.GetTags())
        {
            var chip = BuildTagChip(tag);
            _tagChips?.Add(chip);
        }

        bool full = _settingsLogic.Tags.IsFull;
        _tagInputRow?.EnableInClassList("overlay-hidden", full);
    }

    private VisualElement BuildTagChip(string tag)
    {
        var chip = new VisualElement();
        chip.AddToClassList("settings-tag-chip");

        var label = new Label { text = tag };
        label.AddToClassList("settings-tag-chip__label");

        var removeBtn = new Button(() =>
        {
            _settingsLogic?.Tags.Remove(tag);
            RefreshTagChips();
        });
        removeBtn.text = "×";
        removeBtn.AddToClassList("settings-tag-chip__remove");

        chip.Add(label);
        chip.Add(removeBtn);
        return chip;
    }

    private void OnTagAddClicked()
    {
        var input = _fieldTagInput?.value?.Trim() ?? "";
        if (string.IsNullOrEmpty(input)) return;

        var result = _settingsLogic?.Tags.TryAdd(input) ?? TagAddResult.Empty;

        if (result == TagAddResult.Success)
        {
            _fieldTagInput?.SetValueWithoutNotify("");
            _tagError?.EnableInClassList("overlay-hidden", true);
            RefreshTagChips();
        }
        else
        {
            _tagError.text = result switch
            {
                TagAddResult.TooLong => $"タグは {WorldTagLogic.MaxTagLength} 文字以内で入力してください",
                TagAddResult.LimitReached => $"タグは最大 {WorldTagLogic.MaxTags} 個まで設定できます",
                TagAddResult.AlreadyExists => "同じタグがすでに追加されています",
                _ => "",
            };
            _tagError?.EnableInClassList("overlay-hidden", false);
        }
    }

    // ── BGM UI ────────────────────────────────────────────────────────────────

    private void BuildBgmTrackList()
    {
        _bgmTrackList?.Clear();
        foreach (var track in _availableTracks)
        {
            var item = BuildBgmTrackItem(track);
            _bgmTrackList?.Add(item);
        }
    }

    private VisualElement BuildBgmTrackItem(WorldMusicTrack track)
    {
        var row = new VisualElement();
        row.AddToClassList("bgm-track-item");
        if (track.SoundId == _bgmSoundId)
            row.AddToClassList("bgm-track-item--active");

        // 種別バッジ
        if (track.Kind != TrackKind.None)
        {
            var badge = new Label();
            badge.AddToClassList("bgm-track-item__kind");
            if (track.Kind == TrackKind.Ambient)
            {
                badge.text = "環境音";
                badge.AddToClassList("bgm-kind-ambient");
            }
            else
            {
                badge.text = "BGM";
                badge.AddToClassList("bgm-kind-bgm");
            }
            row.Add(badge);
        }

        var nameLabel = new Label { text = track.DisplayName };
        nameLabel.AddToClassList("bgm-track-item__name");
        row.Add(nameLabel);

        if (!string.IsNullOrEmpty(track.AuthorName))
        {
            var authorLabel = new Label { text = track.AuthorName };
            authorLabel.AddToClassList("bgm-track-item__author");
            row.Add(authorLabel);
        }

        row.RegisterCallback<ClickEvent>(_ => SelectBgmTrack(track.SoundId));
        return row;
    }

    private void SelectBgmTrack(string soundId)
    {
        _bgmSoundId = soundId;
        _settingsLogic?.SetBgmSoundId(soundId);
        RefreshBgmCurrentDisplay();
        RefreshBgmTrackList();
    }

    private void RefreshBgmCurrentDisplay()
    {
        var track = WorldMusicLibrary.Find(_bgmSoundId);
        if (_bgmCurrentName != null) _bgmCurrentName.text = track?.DisplayName ?? _bgmSoundId;
        if (_bgmCurrentAuthor != null) _bgmCurrentAuthor.text = track?.AuthorName ?? "";
    }

    // DOM とデータの同期ずれを防ぐため、リスト全体を再構築する。
    // リストは最大 ~10 件なので再構築コストは無視できる。
    private void RefreshBgmTrackList() => BuildBgmTrackList();

    // ── 人数上限 ──────────────────────────────────────────────────────────────

    private void ChangeMaxPlayers(int delta)
    {
        if (_settingsLogic == null) return;
        _settingsLogic.SetMaxPlayers(_settingsLogic.MaxPlayers + delta);
        if (_labelMaxPlayers != null) _labelMaxPlayers.text = _settingsLogic.MaxPlayers.ToString();
        UpdatePlayersHint();
    }

    private void UpdatePlayersHint()
    {
        if (_settingsLogic == null) return;
        bool premium = _settingsLogic.MaxPlayersUpperBound > WorldSettingsPanelLogic.NormalMaxPlayers;
        if (_labelPlayersHint == null) return;
        _labelPlayersHint.text = premium
            ? $"2〜{WorldSettingsPanelLogic.PremiumMaxPlayers} 人（プレミアム）"
            : $"2〜{WorldSettingsPanelLogic.NormalMaxPlayers} 人";
    }

    // ── フォグ / スクリーンエフェクト / 背景 ─────────────────────────────────

    private void UpdateFogData()
    {
        if (_settingsLogic == null) return;
        var fog = new FogData
        {
            enabled = _toggleFog?.value ?? false,
            color = _fieldFogColor?.value ?? "#E6E6E6",
            startDistance = _sliderFogStart?.value ?? 10f,
            endDistance = _sliderFogEnd?.value ?? 50f,
        };
        _settingsLogic.SetFog(fog);
    }

    private void SetScreenEffect(string type)
    {
        bool hasEffect = type != "none";
        _btnFxNone?.EnableInClassList("radio-btn--active", !hasEffect);
        _btnFxRain?.EnableInClassList("radio-btn--active", type == "rain");
        _fxIntensityRow?.EnableInClassList("overlay-hidden", !hasEffect);
        UpdateScreenEffect();
    }

    private void UpdateScreenEffect()
    {
        if (_settingsLogic == null) return;
        string type = (_btnFxRain?.ClassListContains("radio-btn--active") == true) ? "rain" : "none";
        _settingsLogic.SetScreenEffect(new ScreenEffectData
        {
            type = type,
            intensity = (int)(_sliderFxIntensity?.value ?? 100f),
        });
    }

    private void UpdateBackground()
    {
        if (_settingsLogic == null) return;
        string type = _btnBgSolid?.ClassListContains("radio-btn--active") == true ? "solid"
            : _btnBgGradient?.ClassListContains("radio-btn--active") == true ? "gradient"
            : "texture";
        _settingsLogic.SetBackground(new BackgroundData
        {
            type = type,
            colors = new[] { _fieldBgColor?.value ?? "#111111" },
        });
    }

    // ── 名前変更 ──────────────────────────────────────────────────────────────

    private void OnWorldNameChanged(string value)
    {
        _settingsLogic?.SetWorldName(value);
        // ヘッダーと設定タブを同期
        if (_fieldWorldName?.value != value)
            _fieldWorldName?.SetValueWithoutNotify(value);
        if (_settingsWorldName?.value != value)
            _settingsWorldName?.SetValueWithoutNotify(value);
        UpdatePublishButton();
    }

    // ── 保存 / 公開 ───────────────────────────────────────────────────────────

    private void OnSaveClicked()
    {
        CommitAndSave();
        Debug.Log("[WorldEditor] ドラフト保存");
        // TODO: Phase 12 API — WorldCreationManager.SaveDraftAsync()
    }

    private void OnPublishClicked()
    {
        CommitAndSave();
        var errors = _publishValidator.Validate(
            WorldCreationManager.Instance?.CurrentDefinition,
            _textureCost,
            _objectCount,
            _hasThumbnail,
            _publishedVersion);

        ShowPublishErrors(errors);
        if (errors.Count == 0)
        {
            Debug.Log("[WorldEditor] 公開処理を開始");
            // TODO: Phase 12 API — WorldCreationManager.PublishAsync()
        }
    }

    private void CommitAndSave()
    {
        var mgr = WorldCreationManager.Instance;
        // _settingsLogic は mgr.SettingsLogic と同一インスタンスのため
        // CommitSettingsChanges のみで正しく保存される（二重適用なし）
        if (mgr?.CurrentDefinition == null) return;
        mgr.CommitSettingsChanges();
    }

    private void UpdatePublishButton()
    {
        bool hasName = _settingsLogic?.IsWorldNameEmpty == false;
        _btnPublish?.SetEnabled(hasName);
        _btnPublishSettings?.SetEnabled(hasName);
    }

    private void ShowPublishErrors(IReadOnlyList<PublishError> errors)
    {
        if (_publishErrors == null) return;
        _publishErrors.Clear();
        _publishErrors.EnableInClassList("overlay-hidden", errors.Count == 0);

        foreach (var err in errors)
        {
            var label = new Label { text = ErrorMessage(err) };
            label.AddToClassList("publish-error-item");
            _publishErrors.Add(label);
        }
    }

    private static string ErrorMessage(PublishError err) =>
        err switch
        {
            PublishError.WorldNameEmpty => "ワールド名を入力してください",
            PublishError.ThumbnailMissing => "サムネイルを設定してください",
            PublishError.SpawnNotSet => "スポーン位置を設定してください",
            PublishError.PortalExitMissing => "すべての入口ポータルに出口を設定してください",
            PublishError.TextureCostExceeded => $"テクスチャコストが上限（{TextureCostCalculator.CostLimit}）を超えています",
            PublishError.ObjectCountExceeded => $"オブジェクト数が上限（{TextureCostCalculator.ObjectCountLimit}）を超えています",
            PublishError.VersionNumberOverflow => "バージョン番号が上限に達しているため公開できません",
            _ => err.ToString(),
        };

    // ── 戻る ─────────────────────────────────────────────────────────────────

    private void OnBackClicked()
    {
        Debug.Log("[WorldEditor] ワールド管理タブに戻る");
        // TODO: Phase 12 — ワールド管理タブへ遷移
        gameObject.SetActive(false);
    }
}
