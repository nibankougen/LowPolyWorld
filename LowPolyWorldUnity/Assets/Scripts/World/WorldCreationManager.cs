using UnityEngine;

/// <summary>
/// WorldScene 内でワールド作成・編集機能を統括する MonoBehaviour。
/// WorldScene の DontDestroyOnLoad でなく、ルームセッション中のみ存在するシーンオブジェクトとして配置する。
/// </summary>
public class WorldCreationManager : MonoBehaviour
{
    public static WorldCreationManager Instance { get; private set; }

    [SerializeField]
    private WorldMusicPlayer _worldMusicPlayer;

    [SerializeField]
    private WorldEnvironmentController _environment;

    private WorldSettingsPanelLogic _settingsLogic;
    private WorldDefinitionJson _currentDef;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>
    /// ワールド定義 JSON 文字列を読み込み、各サブシステムに反映する。
    /// ワールド入場時に呼び出す。
    /// </summary>
    public void LoadWorldDefinition(string json, bool isPremium)
    {
        _currentDef = WorldDefinition.FromJson(json);
        _settingsLogic = new WorldSettingsPanelLogic(isPremium);
        _settingsLogic.LoadFrom(_currentDef);
        ApplyBgmToPlayer();
        ApplyEnvironment();
    }

    /// <summary>
    /// 既に解析済みの WorldDefinitionJson を読み込む。
    /// ワールドエディタから呼び出し、UI と同じ settingsLogic インスタンスを共有させる。
    /// </summary>
    public void LoadWorldDef(WorldDefinitionJson def, bool isPremium)
    {
        _currentDef = def ?? WorldDefinition.CreateBlank();
        _settingsLogic = new WorldSettingsPanelLogic(isPremium);
        _settingsLogic.LoadFrom(_currentDef);
        ApplyBgmToPlayer();
        ApplyEnvironment();
    }

    /// <summary>
    /// 空白テンプレートで新規ワールドを初期化する。
    /// </summary>
    public void InitBlankWorld(string worldName, bool isPremium)
    {
        _currentDef = WorldDefinition.CreateBlank(worldName);
        _settingsLogic = new WorldSettingsPanelLogic(isPremium);
        _settingsLogic.LoadFrom(_currentDef);
        ApplyBgmToPlayer();
        ApplyEnvironment();
    }

    /// <summary>現在の設定ロジックを返す（UI から参照用）。</summary>
    public WorldSettingsPanelLogic SettingsLogic => _settingsLogic;

    /// <summary>現在のワールド定義を返す。</summary>
    public WorldDefinitionJson CurrentDefinition => _currentDef;

    /// <summary>
    /// 設定パネルの変更を WorldDefinitionJson に反映し、BGM・環境設定を更新する。
    /// </summary>
    public void CommitSettingsChanges()
    {
        if (_settingsLogic == null || _currentDef == null) return;
        _settingsLogic.ApplyTo(_currentDef);
        ApplyBgmToPlayer();
        _environment?.Apply(_currentDef);
    }

    // ── BGM 接続 ─────────────────────────────────────────────────────────────

    private void ApplyBgmToPlayer()
    {
        if (_worldMusicPlayer == null || _currentDef == null) return;
        var bgm = _currentDef.worldBgm ?? new WorldBgmData();
        float volumeNormalized = bgm.volume / 100f;
        _worldMusicPlayer.SetDefault(bgm.soundId, volumeNormalized);
    }

    // 環境設定（フォグ・環境カラー・背景・エフェクト）を適用する
    private void ApplyEnvironment()
    {
        _environment?.Apply(_currentDef);
    }
}
