using NUnit.Framework;

public class InviteRoomLogicTests
{
    // ── 初期状態 ─────────────────────────────────────────────────────────────

    [Test]
    public void InitialState_IsOpen()
    {
        var logic = new InviteRoomLogic("creator");
        Assert.AreEqual(InviteRoomState.Open, logic.State);
    }

    [Test]
    public void InitialParticipantCount_IsOne()
    {
        var logic = new InviteRoomLogic("creator");
        Assert.AreEqual(1, logic.ParticipantCount);
    }

    [Test]
    public void CanJoin_WhenOpen_ReturnsTrue()
    {
        var logic = new InviteRoomLogic("creator");
        Assert.IsTrue(logic.CanJoin());
    }

    // ── 参加者追加 ────────────────────────────────────────────────────────────

    [Test]
    public void AddParticipant_IncreasesCount()
    {
        var logic = new InviteRoomLogic("creator");
        logic.AddParticipant("userA");
        Assert.AreEqual(2, logic.ParticipantCount);
    }

    [Test]
    public void AddParticipant_StateRemainsOpen()
    {
        var logic = new InviteRoomLogic("creator");
        logic.AddParticipant("userA");
        Assert.AreEqual(InviteRoomState.Open, logic.State);
    }

    // ── OPEN → LOCKED 遷移 ───────────────────────────────────────────────────

    [Test]
    public void RemoveCreator_WithOthersPresent_TransitionsToLocked()
    {
        var logic = new InviteRoomLogic("creator");
        logic.AddParticipant("userA");
        logic.AddParticipant("userB");

        logic.RemoveParticipant("creator");

        Assert.AreEqual(InviteRoomState.Locked, logic.State);
    }

    [Test]
    public void RemoveNonCreator_StateRemainsOpen()
    {
        var logic = new InviteRoomLogic("creator");
        logic.AddParticipant("userA");

        logic.RemoveParticipant("userA");

        Assert.AreEqual(InviteRoomState.Open, logic.State);
    }

    [Test]
    public void CanJoin_WhenLocked_ReturnsFalse()
    {
        var logic = new InviteRoomLogic("creator");
        logic.AddParticipant("userA");
        logic.RemoveParticipant("creator");

        Assert.IsFalse(logic.CanJoin());
    }

    // ── LOCKED → CLOSED 遷移 ─────────────────────────────────────────────────

    [Test]
    public void RemoveLastParticipant_WhenLocked_TransitionsToClosed()
    {
        var logic = new InviteRoomLogic("creator");
        logic.AddParticipant("userA");
        logic.RemoveParticipant("creator"); // → Locked

        logic.RemoveParticipant("userA"); // 最後の1人退室 → Closed

        Assert.AreEqual(InviteRoomState.Closed, logic.State);
    }

    [Test]
    public void CanJoin_WhenClosed_ReturnsFalse()
    {
        var logic = new InviteRoomLogic("creator");
        logic.AddParticipant("userA");
        logic.RemoveParticipant("creator");
        logic.RemoveParticipant("userA");

        Assert.IsFalse(logic.CanJoin());
    }

    // ── OPEN → CLOSED（作成者が最後の1人として退室）────────────────────────

    [Test]
    public void RemoveCreator_WhenAlone_TransitionsToClosed()
    {
        var logic = new InviteRoomLogic("creator");

        logic.RemoveParticipant("creator");

        Assert.AreEqual(InviteRoomState.Closed, logic.State);
    }

    // ── 作成者再入室でも OPEN に戻らない ─────────────────────────────────────

    [Test]
    public void CreatorReentry_AfterLocked_StateRemainsLocked()
    {
        var logic = new InviteRoomLogic("creator");
        logic.AddParticipant("userA");
        logic.RemoveParticipant("creator"); // → Locked

        // 作成者が再入室（本来 CanJoin() = false で弾かれるが、ロジック単体で検証）
        logic.AddParticipant("creator");

        Assert.AreEqual(InviteRoomState.Locked, logic.State);
    }

    // ── 複数人退室の連続遷移 ─────────────────────────────────────────────────

    [Test]
    public void FullTransition_OpenToLockedToClosed()
    {
        var logic = new InviteRoomLogic("creator");
        logic.AddParticipant("userA");
        logic.AddParticipant("userB");

        Assert.AreEqual(InviteRoomState.Open, logic.State);

        logic.RemoveParticipant("creator");
        Assert.AreEqual(InviteRoomState.Locked, logic.State);
        Assert.IsTrue(logic.ParticipantCount > 0);

        logic.RemoveParticipant("userA");
        Assert.AreEqual(InviteRoomState.Locked, logic.State);

        logic.RemoveParticipant("userB");
        Assert.AreEqual(InviteRoomState.Closed, logic.State);
        Assert.AreEqual(0, logic.ParticipantCount);
    }
}
