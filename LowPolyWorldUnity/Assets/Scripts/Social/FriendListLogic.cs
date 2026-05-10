using System.Collections.Generic;

public enum FriendRelationStatus
{
    None,
    RequestSent,
    RequestReceived,
    Friends,
}

/// <summary>
/// 現在のユーザーから見たフレンド関係をローカルで管理する（純粋 C#）。
/// サーバー通信は行わず、状態遷移のみを担当する。
/// </summary>
public class FriendListLogic
{
    private readonly int _maxFriends;
    private readonly Dictionary<string, FriendRelationStatus> _relations = new();

    public FriendListLogic(int maxFriends)
    {
        _maxFriends = maxFriends;
    }

    public int MaxFriends => _maxFriends;

    public int FriendCount
    {
        get
        {
            int count = 0;
            foreach (var kv in _relations)
                if (kv.Value == FriendRelationStatus.Friends)
                    count++;
            return count;
        }
    }

    public bool IsAtCapacity => FriendCount >= _maxFriends;

    public FriendRelationStatus GetStatus(string userId) =>
        _relations.TryGetValue(userId, out var status) ? status : FriendRelationStatus.None;

    /// <summary>
    /// フレンド申請を送る。
    /// 既に相手から申請が届いていた場合は相互承認として即座に Friends へ遷移する。
    /// 上限達成時・既に関係がある場合は false を返す。
    /// </summary>
    public bool SendRequest(string targetId)
    {
        var current = GetStatus(targetId);

        if (current == FriendRelationStatus.Friends || current == FriendRelationStatus.RequestSent)
            return false;

        if (IsAtCapacity)
            return false;

        if (current == FriendRelationStatus.RequestReceived)
        {
            // 相互申請 → 相互承認成立
            _relations[targetId] = FriendRelationStatus.Friends;
            return true;
        }

        _relations[targetId] = FriendRelationStatus.RequestSent;
        return true;
    }

    /// <summary>
    /// 相手からの申請を受信してローカル状態に記録する。
    /// 既にこちらから申請済みの場合は相互承認として Friends へ遷移する（上限達成時は RequestReceived に降格）。
    /// </summary>
    /// <returns>相互承認で Friends になった場合 true。RequestReceived に追加または上限ブロックの場合 false。</returns>
    public bool ReceiveRequest(string requesterId)
    {
        var current = GetStatus(requesterId);

        if (current == FriendRelationStatus.Friends || current == FriendRelationStatus.RequestReceived)
            return false;

        if (current == FriendRelationStatus.RequestSent)
        {
            if (IsAtCapacity)
            {
                // 上限達成 → 相互承認成立できず RequestReceived に降格
                _relations[requesterId] = FriendRelationStatus.RequestReceived;
                return false;
            }
            // 相互申請 → 相互承認成立
            _relations[requesterId] = FriendRelationStatus.Friends;
            return true;
        }

        _relations[requesterId] = FriendRelationStatus.RequestReceived;
        return false;
    }

    /// <summary>受信した申請を承認する。上限達成時は false を返す。</summary>
    public bool AcceptRequest(string requesterId)
    {
        if (GetStatus(requesterId) != FriendRelationStatus.RequestReceived)
            return false;
        if (IsAtCapacity)
            return false;
        _relations[requesterId] = FriendRelationStatus.Friends;
        return true;
    }

    /// <summary>受信した申請を拒否する。</summary>
    public bool RejectRequest(string requesterId)
    {
        if (GetStatus(requesterId) != FriendRelationStatus.RequestReceived)
            return false;
        _relations.Remove(requesterId);
        return true;
    }

    /// <summary>送った申請をキャンセルする。</summary>
    public bool CancelRequest(string targetId)
    {
        if (GetStatus(targetId) != FriendRelationStatus.RequestSent)
            return false;
        _relations.Remove(targetId);
        return true;
    }

    /// <summary>サーバーから「送った申請が承認された」通知を受け取り Friends に遷移させる。</summary>
    public bool NotifyRequestAccepted(string targetId)
    {
        if (GetStatus(targetId) != FriendRelationStatus.RequestSent)
            return false;
        _relations[targetId] = FriendRelationStatus.Friends;
        return true;
    }

    /// <summary>フレンドを解除する。</summary>
    public bool RemoveFriend(string userId)
    {
        if (GetStatus(userId) != FriendRelationStatus.Friends)
            return false;
        _relations.Remove(userId);
        return true;
    }

    /// <summary>サーバーから取得したリストで全件置換する。</summary>
    public void SetAll(IEnumerable<(string userId, FriendRelationStatus status)> entries)
    {
        _relations.Clear();
        foreach (var (userId, status) in entries)
            _relations[userId] = status;
    }

    public IReadOnlyDictionary<string, FriendRelationStatus> GetAll() => _relations;
}
