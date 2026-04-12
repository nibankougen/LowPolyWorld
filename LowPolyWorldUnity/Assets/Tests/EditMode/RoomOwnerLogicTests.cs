using NUnit.Framework;

public class RoomOwnerLogicTests
{
    // ---- AddMember / MemberCount ----

    [Test]
    public void AddMember_IncreasesMemberCount()
    {
        var logic = new RoomOwnerLogic(1UL);
        logic.AddMember(1UL);
        logic.AddMember(2UL);
        Assert.AreEqual(2, logic.MemberCount);
    }

    [Test]
    public void HasMember_ReturnsTrueAfterAdd()
    {
        var logic = new RoomOwnerLogic(1UL);
        logic.AddMember(42UL);
        Assert.IsTrue(logic.HasMember(42UL));
    }

    // ---- RemoveMember ----

    [Test]
    public void RemoveMember_DecreasesMemberCount()
    {
        var logic = new RoomOwnerLogic(1UL);
        logic.AddMember(1UL);
        logic.AddMember(2UL);
        logic.RemoveMember(2UL);
        Assert.AreEqual(1, logic.MemberCount);
    }

    [Test]
    public void RemoveMember_NonOwner_DoesNotChangeOwner()
    {
        var logic = new RoomOwnerLogic(1UL);
        logic.AddMember(1UL);
        logic.AddMember(2UL);
        logic.RemoveMember(2UL);
        Assert.AreEqual(1UL, logic.CurrentOwnerId);
    }

    // ---- オーナー移譲 ----

    [Test]
    public void RemoveMember_OwnerLeaves_TransfersToLongestStaying()
    {
        var logic = new RoomOwnerLogic(1UL);
        logic.AddMember(1UL);          // time=0
        logic.Tick(1f);
        logic.AddMember(2UL);          // time=1
        logic.Tick(1f);
        logic.AddMember(3UL);          // time=2

        logic.RemoveMember(1UL);       // オーナー退室 → 2 が最長在室

        Assert.AreEqual(2UL, logic.CurrentOwnerId);
    }

    [Test]
    public void RemoveMember_OwnerLeaves_FiresOnOwnerChanged()
    {
        ulong notified = 0;
        var logic = new RoomOwnerLogic(1UL);
        logic.OnOwnerChanged += id => notified = id;

        logic.AddMember(1UL);
        logic.Tick(1f);
        logic.AddMember(2UL);
        logic.RemoveMember(1UL);

        Assert.AreEqual(2UL, notified);
    }

    [Test]
    public void RemoveMember_LastMember_NoOwnerChangedEvent()
    {
        bool fired = false;
        var logic = new RoomOwnerLogic(1UL);
        logic.OnOwnerChanged += _ => fired = true;

        logic.AddMember(1UL);
        logic.RemoveMember(1UL);   // メンバーが誰もいない → 移譲先なし

        Assert.IsFalse(fired);
    }

    // ---- OwnerIsCreator ----

    [Test]
    public void OwnerIsCreator_TrueInitially()
    {
        var logic = new RoomOwnerLogic(10UL);
        Assert.IsTrue(logic.OwnerIsCreator);
    }

    [Test]
    public void OwnerIsCreator_FalseAfterTransfer()
    {
        var logic = new RoomOwnerLogic(1UL);
        logic.AddMember(1UL);
        logic.Tick(1f);
        logic.AddMember(2UL);
        logic.RemoveMember(1UL);

        Assert.IsFalse(logic.OwnerIsCreator);
    }

    // ---- 元オーナー再入室 ----

    [Test]
    public void OriginalCreatorRejoins_DoesNotAutoReclaim()
    {
        var logic = new RoomOwnerLogic(1UL);
        logic.AddMember(1UL);
        logic.Tick(1f);
        logic.AddMember(2UL);
        logic.RemoveMember(1UL);   // 2 がオーナー

        logic.Tick(1f);
        logic.AddMember(1UL);      // 元作成者が再入室

        // 所有権は自動で戻らない（通常の後入室扱い）
        Assert.AreEqual(2UL, logic.CurrentOwnerId);
    }
}
