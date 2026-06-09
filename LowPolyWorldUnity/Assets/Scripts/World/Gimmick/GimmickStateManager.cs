using System;
using System.Collections.Generic;

/// <summary>
/// ギミックシステムのワールドステートとプレイヤーステートを管理するロジッククラス
/// （world-creation.md セクション 9.1）。
///
/// ワールドステート: 最大 10 種 / 各 0〜255
/// プレイヤーステート: プレイヤーごとに最大 4 種 / 各 0〜255
/// </summary>
public class GimmickStateManager
{
    public const int MaxWorldStates = 10;
    public const int MaxPlayerStates = 4;
    public const int MaxTimers = 5;
    public const int MinStateValue = 0;
    public const int MaxStateValue = 255;

    private readonly int[] _worldStates = new int[MaxWorldStates];
    private readonly int[] _worldInitials; // 定義ファイルの初期値（リセット時に使用）
    private readonly Dictionary<string, int[]> _playerStates = new();

    public GimmickStateManager(int[] worldInitials = null)
    {
        _worldInitials = new int[MaxWorldStates];
        if (worldInitials != null)
        {
            for (int i = 0; i < Math.Min(worldInitials.Length, MaxWorldStates); i++)
                _worldInitials[i] = Clamp(worldInitials[i]);
        }
        Array.Copy(_worldInitials, _worldStates, MaxWorldStates);
    }

    // ── ワールドステート ───────────────────────────────────────────────────────

    public int GetWorldState(int index)
    {
        ValidateWorldIndex(index);
        return _worldStates[index];
    }

    public void SetWorldState(int index, int value)
    {
        ValidateWorldIndex(index);
        _worldStates[index] = Clamp(value);
    }

    public void ApplyWorldState(int index, StateOp op, int delta)
    {
        ValidateWorldIndex(index);
        _worldStates[index] = op switch
        {
            StateOp.Set => Clamp(delta),
            StateOp.Add => Clamp(_worldStates[index] + delta),
            StateOp.Subtract => Clamp(_worldStates[index] - delta),
            _ => _worldStates[index],
        };
    }

    // ── プレイヤーステート ────────────────────────────────────────────────────

    public int GetPlayerState(string playerId, int index)
    {
        ValidatePlayerIndex(index);
        return GetOrCreatePlayerStates(playerId)[index];
    }

    public void SetPlayerState(string playerId, int index, int value)
    {
        ValidatePlayerIndex(index);
        GetOrCreatePlayerStates(playerId)[index] = Clamp(value);
    }

    public void ApplyPlayerState(string playerId, int index, StateOp op, int delta)
    {
        ValidatePlayerIndex(index);
        var states = GetOrCreatePlayerStates(playerId);
        states[index] = op switch
        {
            StateOp.Set => Clamp(delta),
            StateOp.Add => Clamp(states[index] + delta),
            StateOp.Subtract => Clamp(states[index] - delta),
            _ => states[index],
        };
    }

    /// <summary>全プレイヤーの指定ステートの合計値を返す。</summary>
    public int SumAllPlayersState(int index)
    {
        ValidatePlayerIndex(index);
        int total = 0;
        foreach (var states in _playerStates.Values)
            total += states[index];
        return total;
    }

    // ── リセット ──────────────────────────────────────────────────────────────

    /// <summary>ワールドステートを初期値に戻す。</summary>
    public void ResetWorldStates() =>
        Array.Copy(_worldInitials, _worldStates, MaxWorldStates);

    /// <summary>指定プレイヤーのステートを全て 0 にリセットする。</summary>
    public void ResetPlayerStates(string playerId)
    {
        if (_playerStates.TryGetValue(playerId, out var states))
            Array.Clear(states, 0, MaxPlayerStates);
    }

    /// <summary>全プレイヤーのステートを 0 にリセットし、ワールドステートを初期値に戻す。</summary>
    public void ResetAll()
    {
        ResetWorldStates();
        foreach (var states in _playerStates.Values)
            Array.Clear(states, 0, MaxPlayerStates);
    }

    /// <summary>プレイヤーが退出したとき: そのプレイヤーのステートエントリを削除する。</summary>
    public void RemovePlayer(string playerId) => _playerStates.Remove(playerId);

    // ── プレイヤー管理 ────────────────────────────────────────────────────────

    /// <summary>プレイヤー一覧（入室順番号 = プレイヤー番号 の参照に使用）。</summary>
    public IReadOnlyList<string> PlayerIds { get; } = new List<string>() as IReadOnlyList<string>;

    // 注: PlayerIds は上位レイヤー（NetworkManager 統合時）で管理し、
    //     PlayerNumber 条件の評価時に渡す。本クラスは番号を管理しない。

    // ── Private ───────────────────────────────────────────────────────────────

    private int[] GetOrCreatePlayerStates(string playerId)
    {
        if (!_playerStates.TryGetValue(playerId, out var states))
        {
            states = new int[MaxPlayerStates];
            _playerStates[playerId] = states;
        }
        return states;
    }

    private static void ValidateWorldIndex(int index)
    {
        if ((uint)index >= MaxWorldStates)
            throw new ArgumentOutOfRangeException(nameof(index),
                $"ワールドステートインデックスは 0〜{MaxWorldStates - 1} の範囲で指定してください。");
    }

    private static void ValidatePlayerIndex(int index)
    {
        if ((uint)index >= MaxPlayerStates)
            throw new ArgumentOutOfRangeException(nameof(index),
                $"プレイヤーステートインデックスは 0〜{MaxPlayerStates - 1} の範囲で指定してください。");
    }

    internal static int Clamp(int v) => Math.Clamp(v, MinStateValue, MaxStateValue);
}
