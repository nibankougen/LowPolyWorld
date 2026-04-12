using System;
using System.Collections.Generic;

/// <summary>
/// ルームオーナー移譲ロジック（純粋 C#）。
/// サーバー/ホスト上で動作し、退室時に最長在室ユーザーへ自動移譲する。
/// - 言語・人数設定の変更権は元作成者（originCreatorId）のみが保持する。
/// - ギミックステートマスターは現オーナーが担う。
/// </summary>
public class RoomOwnerLogic
{
    public event Action<ulong> OnOwnerChanged;

    private readonly ulong _originCreatorId;
    private readonly Dictionary<ulong, double> _memberJoinTimes = new();
    private double _clock;

    public ulong CurrentOwnerId { get; private set; }

    /// <summary>現在のオーナーがルームの元作成者かどうか。</summary>
    public bool OwnerIsCreator => CurrentOwnerId == _originCreatorId;

    public RoomOwnerLogic(ulong originCreatorId)
    {
        _originCreatorId = originCreatorId;
        CurrentOwnerId = originCreatorId;
    }

    /// <summary>時間を進める（毎フレーム呼び出し）。</summary>
    public void Tick(float deltaTime)
    {
        _clock += deltaTime;
    }

    /// <summary>メンバーが入室した。</summary>
    public void AddMember(ulong clientId)
    {
        _memberJoinTimes[clientId] = _clock;
    }

    /// <summary>
    /// メンバーが退室した。オーナーが退室した場合は最長在室ユーザーへ移譲する。
    /// </summary>
    public void RemoveMember(ulong clientId)
    {
        _memberJoinTimes.Remove(clientId);

        if (clientId != CurrentOwnerId) return;

        var next = GetNextOwnerCandidate();
        if (next == null) return;

        CurrentOwnerId = next.Value;
        OnOwnerChanged?.Invoke(CurrentOwnerId);
    }

    /// <summary>最長在室ユーザーの clientId を返す（メンバーがいない場合は null）。</summary>
    private ulong? GetNextOwnerCandidate()
    {
        ulong? best = null;
        double bestTime = double.MaxValue;

        foreach (var kv in _memberJoinTimes)
        {
            if (kv.Value < bestTime)
            {
                bestTime = kv.Value;
                best = kv.Key;
            }
        }

        return best;
    }

    /// <summary>現在のメンバー数（テスト用）。</summary>
    public int MemberCount => _memberJoinTimes.Count;

    /// <summary>指定クライアントがメンバーかどうか（テスト用）。</summary>
    public bool HasMember(ulong clientId) => _memberJoinTimes.ContainsKey(clientId);
}
