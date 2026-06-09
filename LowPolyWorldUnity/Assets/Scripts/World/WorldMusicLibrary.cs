/// <summary>
/// ワールド BGM の内蔵サウンドライブラリ定義（world-creation.md セクション 14.2）。
/// ショップ購入曲（soundId: shop_{itemId}）はここには含まれない。
/// </summary>
public static class WorldMusicLibrary
{
    public static readonly WorldMusicTrack[] BuiltInTracks =
    {
        new("none", "なし", TrackKind.None, ""),
        new("rain", "雨音", TrackKind.Ambient, "LowPolyWorld"),
        new("ocean", "波音・海辺", TrackKind.Ambient, "LowPolyWorld"),
        new("wind", "風音", TrackKind.Ambient, "LowPolyWorld"),
        new("cave", "洞窟・残響", TrackKind.Ambient, "LowPolyWorld"),
        new("darkFactory", "暗い工場の音", TrackKind.Ambient, "LowPolyWorld"),
        new("bgmFunNightStage", "楽しい夜のステージ", TrackKind.Bgm, "kougen"),
        new("bgmBrightPlains", "明るい平原", TrackKind.Bgm, "kougen"),
        new("bgmATenseMoment", "緊張の瞬間", TrackKind.Bgm, "kougen"),
    };

    /// <summary>soundId からトラック情報を検索する。見つからなければ null。</summary>
    public static WorldMusicTrack? Find(string soundId)
    {
        foreach (var t in BuiltInTracks)
            if (t.SoundId == soundId)
                return t;
        return null;
    }
}

/// <summary>
/// 内蔵 BGM トラック 1 件のメタデータ。
/// </summary>
public readonly struct WorldMusicTrack
{
    public string SoundId { get; }
    public string DisplayName { get; }
    public TrackKind Kind { get; }
    public string AuthorName { get; }

    public WorldMusicTrack(string soundId, string displayName, TrackKind kind, string authorName)
    {
        SoundId = soundId;
        DisplayName = displayName;
        Kind = kind;
        AuthorName = authorName;
    }
}

public enum TrackKind
{
    None,
    Ambient,
    Bgm,
    Shop,
}
