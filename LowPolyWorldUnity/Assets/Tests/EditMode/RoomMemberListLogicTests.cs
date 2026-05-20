using NUnit.Framework;

public class RoomMemberListLogicTests
{
    private static RoomMemberInfo Info(
        string userId,
        string displayName = "Test",
        bool isVerified = false,
        bool isOwner = false
    ) => new(userId, displayName, isVerified, isOwner);

    // ── Add / Count ──────────────────────────────────────────────────────────────

    [Test]
    public void Add_NewMember_CountIncreases()
    {
        var logic = new RoomMemberListLogic();
        logic.Add(Info("u1"));
        Assert.AreEqual(1, logic.Count);
    }

    [Test]
    public void Add_DuplicateUserId_NotAdded()
    {
        var logic = new RoomMemberListLogic();
        logic.Add(Info("u1"));
        logic.Add(Info("u1"));
        Assert.AreEqual(1, logic.Count);
    }

    // ── Remove ───────────────────────────────────────────────────────────────────

    [Test]
    public void Remove_ExistingMember_CountDecreases()
    {
        var logic = new RoomMemberListLogic();
        logic.Add(Info("u1"));
        logic.Remove("u1");
        Assert.AreEqual(0, logic.Count);
    }

    [Test]
    public void Remove_NonExistent_NoException()
    {
        var logic = new RoomMemberListLogic();
        Assert.DoesNotThrow(() => logic.Remove("ghost"));
    }

    // ── Contains ─────────────────────────────────────────────────────────────────

    [Test]
    public void Contains_AddedMember_ReturnsTrue()
    {
        var logic = new RoomMemberListLogic();
        logic.Add(Info("u1"));
        Assert.IsTrue(logic.Contains("u1"));
    }

    [Test]
    public void Contains_AbsentMember_ReturnsFalse()
    {
        var logic = new RoomMemberListLogic();
        Assert.IsFalse(logic.Contains("u1"));
    }

    // ── Clear ────────────────────────────────────────────────────────────────────

    [Test]
    public void Clear_SetsCountToZero()
    {
        var logic = new RoomMemberListLogic();
        logic.Add(Info("u1"));
        logic.Add(Info("u2"));
        logic.Clear();
        Assert.AreEqual(0, logic.Count);
    }

    // ── GetSortedMembers ─────────────────────────────────────────────────────────

    [Test]
    public void GetSortedMembers_NullHideList_ReturnsSameOrder()
    {
        var logic = new RoomMemberListLogic();
        logic.Add(Info("u1"));
        logic.Add(Info("u2"));
        var result = logic.GetSortedMembers(null);
        Assert.AreEqual(2, result.Count);
        Assert.AreEqual("u1", result[0].UserId);
        Assert.AreEqual("u2", result[1].UserId);
    }

    [Test]
    public void GetSortedMembers_HiddenMemberMovedToEnd()
    {
        var logic = new RoomMemberListLogic();
        logic.Add(Info("u1"));
        logic.Add(Info("u2-hidden"));
        logic.Add(Info("u3"));

        var hideList = new HideListLogic();
        hideList.Add("u2-hidden");

        var result = logic.GetSortedMembers(hideList);
        Assert.AreEqual(3, result.Count);
        Assert.AreEqual("u1", result[0].UserId);
        Assert.AreEqual("u3", result[1].UserId);
        Assert.AreEqual("u2-hidden", result[2].UserId);
    }

    [Test]
    public void GetSortedMembers_AllHidden_AllReturnedInOriginalOrder()
    {
        var logic = new RoomMemberListLogic();
        logic.Add(Info("u1"));
        logic.Add(Info("u2"));

        var hideList = new HideListLogic();
        hideList.Add("u1");
        hideList.Add("u2");

        var result = logic.GetSortedMembers(hideList);
        Assert.AreEqual(2, result.Count);
        Assert.AreEqual("u1", result[0].UserId);
        Assert.AreEqual("u2", result[1].UserId);
    }

    [Test]
    public void GetSortedMembers_NoHidden_VisibleFirstSectionPreservesOrder()
    {
        var logic = new RoomMemberListLogic();
        logic.Add(Info("u1"));
        logic.Add(Info("u2"));
        logic.Add(Info("u3"));

        var hideList = new HideListLogic();

        var result = logic.GetSortedMembers(hideList);
        Assert.AreEqual(3, result.Count);
        Assert.AreEqual("u1", result[0].UserId);
        Assert.AreEqual("u2", result[1].UserId);
        Assert.AreEqual("u3", result[2].UserId);
    }

    // ── IsMemberHidden ────────────────────────────────────────────────────────────

    [Test]
    public void IsMemberHidden_NullHideList_ReturnsFalse()
    {
        Assert.IsFalse(RoomMemberListLogic.IsMemberHidden("u1", null));
    }

    [Test]
    public void IsMemberHidden_HiddenUser_ReturnsTrue()
    {
        var hideList = new HideListLogic();
        hideList.Add("u1");
        Assert.IsTrue(RoomMemberListLogic.IsMemberHidden("u1", hideList));
    }

    [Test]
    public void IsMemberHidden_NotHiddenUser_ReturnsFalse()
    {
        var hideList = new HideListLogic();
        Assert.IsFalse(RoomMemberListLogic.IsMemberHidden("u1", hideList));
    }
}
