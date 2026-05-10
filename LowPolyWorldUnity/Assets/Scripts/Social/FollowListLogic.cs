using System.Collections.Generic;

/// <summary>
/// 現在のユーザーのフォロー中リストをローカルで管理する（純粋 C#）。
/// フォロー関係は一方向。サーバー通信は行わず状態遷移のみを担当する。
/// </summary>
public class FollowListLogic
{
    private readonly HashSet<string> _followingIds = new();

    public int Count => _followingIds.Count;

    public bool IsFollowing(string userId) => _followingIds.Contains(userId);

    /// <summary>フォローする。既にフォロー中の場合は false を返す。</summary>
    public bool Follow(string userId) => _followingIds.Add(userId);

    /// <summary>フォローを解除する。フォローしていない場合は false を返す。</summary>
    public bool Unfollow(string userId) => _followingIds.Remove(userId);

    /// <summary>サーバーから取得したリストで全件置換する。</summary>
    public void SetAll(IEnumerable<string> userIds)
    {
        _followingIds.Clear();
        foreach (var id in userIds)
            _followingIds.Add(id);
    }

    public IReadOnlyCollection<string> GetAll() => _followingIds;
}
