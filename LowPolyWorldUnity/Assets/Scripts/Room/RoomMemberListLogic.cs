using System.Collections.Generic;

/// <summary>ルームメンバー1人の情報を保持するデータクラス。</summary>
public class RoomMemberInfo
{
    public readonly string UserId;
    public readonly string DisplayName;
    public readonly bool IsVerified;
    public readonly bool IsOwner;

    public RoomMemberInfo(string userId, string displayName, bool isVerified, bool isOwner)
    {
        UserId = userId;
        DisplayName = displayName;
        IsVerified = isVerified;
        IsOwner = isOwner;
    }
}

/// <summary>
/// ルームメンバー一覧を管理するロジッククラス（純粋 C#）。
/// メンバーの追加・削除・並び順（非表示は末尾）を担当する。
/// </summary>
public class RoomMemberListLogic
{
    private readonly List<RoomMemberInfo> _members = new();

    public int Count => _members.Count;

    /// <summary>メンバーを追加する。同一 userId は重複追加しない。</summary>
    public void Add(RoomMemberInfo member)
    {
        if (_members.Exists(m => m.UserId == member.UserId))
            return;
        _members.Add(member);
    }

    /// <summary>指定 userId のメンバーを削除する。存在しない場合は何もしない。</summary>
    public void Remove(string userId) => _members.RemoveAll(m => m.UserId == userId);

    /// <summary>全メンバーを削除する。</summary>
    public void Clear() => _members.Clear();

    /// <summary>指定 userId のメンバーが存在するかを返す。</summary>
    public bool Contains(string userId) => _members.Exists(m => m.UserId == userId);

    /// <summary>
    /// 表示順に並べたメンバー一覧を返す。
    /// 非表示メンバー（HideListLogic に含まれる）は末尾に移動する。
    /// hideList が null の場合は追加順をそのまま返す。
    /// </summary>
    public List<RoomMemberInfo> GetSortedMembers(HideListLogic hideList)
    {
        var visible = new List<RoomMemberInfo>();
        var hidden = new List<RoomMemberInfo>();

        foreach (var m in _members)
        {
            if (hideList != null && hideList.IsHidden(m.UserId))
                hidden.Add(m);
            else
                visible.Add(m);
        }

        visible.AddRange(hidden);
        return visible;
    }

    /// <summary>指定メンバーが非表示かどうかを返す。hideList が null の場合は常に false。</summary>
    public static bool IsMemberHidden(string userId, HideListLogic hideList) =>
        hideList != null && hideList.IsHidden(userId);
}
