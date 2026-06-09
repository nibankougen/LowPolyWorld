using System;

/// <summary>
/// ワールドエディタ設定タブのデータ管理ロジッククラス（world-creation.md セクション 12 参照）。
/// WorldDefinitionJson との相互変換と、各フィールドのバリデーションを担当する。
/// </summary>
public class WorldSettingsPanelLogic
{
    public const int NormalMaxPlayers = 6;
    public const int PremiumMaxPlayers = 24;
    public const int MinPlayers = 2;
    public const int DefaultMaxPlayers = 6;
    public const int BgmVolumeMin = 0;
    public const int BgmVolumeMax = 100;

    private readonly bool _isPremium;

    // ── 基本設定 ──────────────────────────────────────────────────────────────
    public string WorldName { get; private set; } = "";
    public WorldTagLogic Tags { get; }
    public int MaxPlayers { get; private set; } = DefaultMaxPlayers;

    // ── BGM ──────────────────────────────────────────────────────────────────
    public string BgmSoundId { get; private set; } = "none";
    public int BgmVolume { get; private set; } = 100; // 0–100

    // ── 外観 ─────────────────────────────────────────────────────────────────
    public BackgroundData Background { get; private set; } = new();
    public string AmbientColor { get; private set; } = "#FFFFFF";
    public FogData Fog { get; private set; } = new();
    public ScreenEffectData ScreenEffect { get; private set; } = new();

    // ── 公開状態 ─────────────────────────────────────────────────────────────
    public bool IsPublic { get; private set; } = false;

    public int MaxPlayersUpperBound => _isPremium ? PremiumMaxPlayers : NormalMaxPlayers;

    public WorldSettingsPanelLogic(bool isPremium)
    {
        _isPremium = isPremium;
        Tags = new WorldTagLogic();
    }

    // ── セッター（バリデーション付き）────────────────────────────────────────

    /// <summary>ワールド名を設定する（空文字可・255文字まで）。</summary>
    public void SetWorldName(string name) => WorldName = name ?? "";

    /// <summary>
    /// 人数上限を設定する。範囲外の値はクランプされる。
    /// </summary>
    public void SetMaxPlayers(int value) =>
        MaxPlayers = Math.Clamp(value, MinPlayers, MaxPlayersUpperBound);

    /// <summary>BGM の soundId を設定する。</summary>
    public void SetBgmSoundId(string soundId) =>
        BgmSoundId = string.IsNullOrEmpty(soundId) ? "none" : soundId;

    /// <summary>BGM ボリュームを設定する（0–100、範囲外はクランプ）。</summary>
    public void SetBgmVolume(int volume) =>
        BgmVolume = Math.Clamp(volume, BgmVolumeMin, BgmVolumeMax);

    /// <summary>環境カラーを設定する（"#RRGGBB" 形式）。</summary>
    public void SetAmbientColor(string hex) =>
        AmbientColor = string.IsNullOrEmpty(hex) ? "#FFFFFF" : hex;

    /// <summary>フォグ設定を上書きする。</summary>
    public void SetFog(FogData fog) => Fog = fog ?? new FogData();

    /// <summary>スクリーンエフェクト設定を上書きする。</summary>
    public void SetScreenEffect(ScreenEffectData effect) =>
        ScreenEffect = effect ?? new ScreenEffectData();

    /// <summary>背景設定を上書きする。</summary>
    public void SetBackground(BackgroundData bg) => Background = bg ?? new BackgroundData();

    /// <summary>公開状態を設定する。</summary>
    public void SetIsPublic(bool value) => IsPublic = value;

    // ── WorldDefinitionJson との相互変換 ────────────────────────────────────

    /// <summary>
    /// <see cref="WorldDefinitionJson"/> の値をこのロジックに読み込む。
    /// </summary>
    public void LoadFrom(WorldDefinitionJson def)
    {
        if (def == null) return;
        SetWorldName(def.worldName);
        Tags.Clear();
        foreach (var tag in def.tags ?? Array.Empty<string>())
            Tags.TryAdd(tag);
        SetMaxPlayers(def.maxPlayers);
        if (def.worldBgm != null)
        {
            SetBgmSoundId(def.worldBgm.soundId);
            SetBgmVolume(def.worldBgm.volume);
        }
        SetAmbientColor(def.ambientColor);
        if (def.fog != null) SetFog(def.fog);
        if (def.screenEffect != null) SetScreenEffect(def.screenEffect);
        if (def.background != null) SetBackground(def.background);
    }

    /// <summary>
    /// 現在の設定値を既存の <see cref="WorldDefinitionJson"/> に書き戻す（部分更新）。
    /// </summary>
    public void ApplyTo(WorldDefinitionJson def)
    {
        if (def == null) return;
        def.worldName = WorldName;
        var tagList = Tags.GetTags();
        var tagsArray = new string[tagList.Count];
        for (int i = 0; i < tagList.Count; i++)
            tagsArray[i] = tagList[i];
        def.tags = tagsArray;
        def.maxPlayers = MaxPlayers;
        def.worldBgm = new WorldBgmData { soundId = BgmSoundId, volume = BgmVolume };
        def.ambientColor = AmbientColor;
        def.fog = Fog;
        def.screenEffect = ScreenEffect;
        def.background = Background;
    }

    // ── バリデーション ────────────────────────────────────────────────────────

    /// <summary>ワールド名が空か判定する（公開前チェック用）。</summary>
    public bool IsWorldNameEmpty => string.IsNullOrWhiteSpace(WorldName);
}
