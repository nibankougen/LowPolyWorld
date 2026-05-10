using System.Collections.Generic;

public enum InviteRoomState
{
    Open,
    Locked,
    Closed,
}

/// <summary>
/// 招待制ルームのローカル状態管理（純粋 C#）。
/// 状態遷移: OPEN → LOCKED（作成者退室）→ CLOSED（全員退室）。
/// 作成者が再入室しても OPEN には戻らない。
/// 仕様: screens-and-modes.md セクション 9.6
/// </summary>
public class InviteRoomLogic
{
    private readonly string _creatorId;
    private readonly HashSet<string> _participants = new();
    private bool _creatorHasLeft;

    /// <summary>作成者が入室済みの状態でルームを初期化する。</summary>
    public InviteRoomLogic(string creatorId)
    {
        _creatorId = creatorId;
        _participants.Add(creatorId);
    }

    public InviteRoomState State
    {
        get
        {
            if (_participants.Count == 0)
                return InviteRoomState.Closed;
            if (_creatorHasLeft)
                return InviteRoomState.Locked;
            return InviteRoomState.Open;
        }
    }

    public int ParticipantCount => _participants.Count;

    /// <summary>新規入室を受け付けるか（OPEN 状態のみ true）。</summary>
    public bool CanJoin() => State == InviteRoomState.Open;

    /// <summary>参加者を追加する。</summary>
    public void AddParticipant(string userId) => _participants.Add(userId);

    /// <summary>
    /// 参加者を退室させる。
    /// 作成者退室 → LOCKED。全員退室 → CLOSED。
    /// </summary>
    public void RemoveParticipant(string userId)
    {
        _participants.Remove(userId);
        if (userId == _creatorId)
            _creatorHasLeft = true;
    }
}
