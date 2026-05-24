using System;

/// <summary>
/// ワールド BGM のステート管理（デフォルトトラック・ギミックオーバーライド）。
/// </summary>
public class WorldMusicLogic
{
    public readonly struct TrackState
    {
        public string SoundId { get; }
        public float Volume { get; }
        public bool Loop { get; }

        public TrackState(string soundId, float volume, bool loop)
        {
            SoundId = soundId;
            Volume = volume;
            Loop = loop;
        }
    }

    private TrackState _default = new("none", 1f, true);
    private TrackState? _gimmickOverride;

    public TrackState Current => _gimmickOverride ?? _default;

    /// <summary>
    /// ワールド入場時にデフォルトトラックを設定する。
    /// オーバーライド中は再生中のトラックを変えず、Current は変わらない。
    /// </summary>
    public TrackState SetDefault(string soundId, float volume)
    {
        _default = new(soundId, Clamp01(volume), true);
        return Current;
    }

    /// <summary>
    /// ギミック「BGM を切り替える」アクション。状態リセットまで維持される。
    /// </summary>
    public TrackState SwitchTo(string soundId, float volume, bool loop)
    {
        var state = new TrackState(soundId, Clamp01(volume), loop);
        _gimmickOverride = state;
        return state;
    }

    /// <summary>
    /// ギミック「状態リセット」アクション（対象: ワールド / すべて）。デフォルトトラックへ復帰する。
    /// </summary>
    public TrackState ResetToDefault()
    {
        _gimmickOverride = null;
        return _default;
    }

    public bool IsOverridden => _gimmickOverride != null;

    private static float Clamp01(float v) => Math.Clamp(v, 0f, 1f);
}
