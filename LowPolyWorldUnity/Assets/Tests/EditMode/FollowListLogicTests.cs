using NUnit.Framework;

public class FollowListLogicTests
{
    // ── フォロー/フォロー解除の状態遷移 ─────────────────────────────────────

    [Test]
    public void Follow_TransitionsToFollowing()
    {
        var logic = new FollowListLogic();
        var result = logic.Follow("user1");
        Assert.IsTrue(result);
        Assert.IsTrue(logic.IsFollowing("user1"));
    }

    [Test]
    public void Unfollow_TransitionsToNotFollowing()
    {
        var logic = new FollowListLogic();
        logic.Follow("user1");
        var result = logic.Unfollow("user1");
        Assert.IsTrue(result);
        Assert.IsFalse(logic.IsFollowing("user1"));
    }

    [Test]
    public void Follow_AlreadyFollowing_ReturnsFalseAndKeepsCount()
    {
        var logic = new FollowListLogic();
        logic.Follow("user1");
        var result = logic.Follow("user1");
        Assert.IsFalse(result);
        Assert.AreEqual(1, logic.Count);
    }

    [Test]
    public void Unfollow_NotFollowing_ReturnsFalse()
    {
        var logic = new FollowListLogic();
        var result = logic.Unfollow("user1");
        Assert.IsFalse(result);
    }

    [Test]
    public void IsFollowing_UnknownUser_ReturnsFalse()
    {
        var logic = new FollowListLogic();
        Assert.IsFalse(logic.IsFollowing("unknown"));
    }

    [Test]
    public void Follow_ThenUnfollow_ThenFollowAgain_Succeeds()
    {
        var logic = new FollowListLogic();
        logic.Follow("user1");
        logic.Unfollow("user1");
        var result = logic.Follow("user1");
        Assert.IsTrue(result);
        Assert.IsTrue(logic.IsFollowing("user1"));
    }

    [Test]
    public void Count_ReflectsCurrentFollowingNumber()
    {
        var logic = new FollowListLogic();
        logic.Follow("user1");
        logic.Follow("user2");
        logic.Follow("user3");
        Assert.AreEqual(3, logic.Count);
    }

    [Test]
    public void Count_DecreasesOnUnfollow()
    {
        var logic = new FollowListLogic();
        logic.Follow("user1");
        logic.Follow("user2");
        logic.Unfollow("user1");
        Assert.AreEqual(1, logic.Count);
    }

    // ── SetAll / GetAll ───────────────────────────────────────────────────────

    [Test]
    public void SetAll_ReplacesExistingList()
    {
        var logic = new FollowListLogic();
        logic.Follow("user1");
        logic.SetAll(new[] { "user2", "user3" });
        Assert.IsFalse(logic.IsFollowing("user1"));
        Assert.IsTrue(logic.IsFollowing("user2"));
        Assert.IsTrue(logic.IsFollowing("user3"));
        Assert.AreEqual(2, logic.Count);
    }

    [Test]
    public void SetAll_Empty_ClearsAll()
    {
        var logic = new FollowListLogic();
        logic.Follow("user1");
        logic.SetAll(System.Array.Empty<string>());
        Assert.AreEqual(0, logic.Count);
        Assert.IsFalse(logic.IsFollowing("user1"));
    }

    [Test]
    public void GetAll_ReturnsAllFollowingUsers()
    {
        var logic = new FollowListLogic();
        logic.Follow("user1");
        logic.Follow("user2");
        var all = logic.GetAll();
        Assert.AreEqual(2, all.Count);
        CollectionAssert.Contains(all, "user1");
        CollectionAssert.Contains(all, "user2");
    }
}
