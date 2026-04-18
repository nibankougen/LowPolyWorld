using System.Collections.Generic;

/// <summary>
/// ワールドいいね状態をクライアント側でトラッキングするロジッククラス（純粋 C#）。
/// - 自己いいね禁止（ownerId == currentUserId）
/// - 重複いいね禁止（既に liked なワールド）
/// - いいね解除の状態遷移
/// サーバー側でも同じルールを持つため、このクラスはUI楽観更新・ボタン活性判定に使用する。
/// </summary>
public class LikeLogic
{
    private readonly string _currentUserId;
    private readonly HashSet<string> _likedWorldIds = new HashSet<string>();

    public LikeLogic(string currentUserId)
    {
        _currentUserId = currentUserId;
    }

    /// <summary>現在 liked な状態かどうかを返す。</summary>
    public bool IsLiked(string worldId) => _likedWorldIds.Contains(worldId);

    /// <summary>
    /// いいね可能かどうかを判定する。
    /// 自己いいね・重複いいねの場合は false。
    /// </summary>
    public bool CanLike(string worldId, string ownerUserId)
    {
        if (ownerUserId == _currentUserId)
            return false;
        return !_likedWorldIds.Contains(worldId);
    }

    /// <summary>
    /// いいね状態に遷移する。成功時 true、既にいいね済み・自己いいねの場合 false。
    /// </summary>
    public bool TryLike(string worldId, string ownerUserId)
    {
        if (!CanLike(worldId, ownerUserId))
            return false;
        _likedWorldIds.Add(worldId);
        return true;
    }

    /// <summary>
    /// いいね解除する。いいねしていない場合は false。
    /// </summary>
    public bool TryUnlike(string worldId)
    {
        return _likedWorldIds.Remove(worldId);
    }

    /// <summary>
    /// サーバーから取得したいいね済みワールド ID 一覧で初期状態を設定する。
    /// </summary>
    public void SetInitialLikedWorlds(IEnumerable<string> worldIds)
    {
        _likedWorldIds.Clear();
        foreach (var id in worldIds)
            _likedWorldIds.Add(id);
    }
}
