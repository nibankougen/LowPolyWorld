using NUnit.Framework;

public class LikeLogicTests
{
    private const string CurrentUser = "user-001";
    private const string OtherUser  = "user-002";
    private const string WorldA     = "world-aaa";
    private const string WorldB     = "world-bbb";

    // ── 自己いいね禁止 ────────────────────────────────────────────────────────

    [Test]
    public void CanLike_SelfOwnedWorld_ReturnsFalse()
    {
        var logic = new LikeLogic(CurrentUser);
        Assert.IsFalse(logic.CanLike(WorldA, ownerUserId: CurrentUser));
    }

    [Test]
    public void TryLike_SelfOwnedWorld_ReturnsFalse_StateUnchanged()
    {
        var logic = new LikeLogic(CurrentUser);
        var result = logic.TryLike(WorldA, ownerUserId: CurrentUser);
        Assert.IsFalse(result);
        Assert.IsFalse(logic.IsLiked(WorldA));
    }

    // ── 重複いいね禁止 ────────────────────────────────────────────────────────

    [Test]
    public void CanLike_AlreadyLikedWorld_ReturnsFalse()
    {
        var logic = new LikeLogic(CurrentUser);
        logic.TryLike(WorldA, OtherUser);
        Assert.IsFalse(logic.CanLike(WorldA, OtherUser));
    }

    [Test]
    public void TryLike_AlreadyLiked_ReturnsFalse()
    {
        var logic = new LikeLogic(CurrentUser);
        logic.TryLike(WorldA, OtherUser);
        var result = logic.TryLike(WorldA, OtherUser);
        Assert.IsFalse(result);
    }

    // ── 通常いいね ────────────────────────────────────────────────────────────

    [Test]
    public void TryLike_OtherOwnedWorld_ReturnsTrue_StateUpdated()
    {
        var logic = new LikeLogic(CurrentUser);
        var result = logic.TryLike(WorldA, OtherUser);
        Assert.IsTrue(result);
        Assert.IsTrue(logic.IsLiked(WorldA));
    }

    [Test]
    public void CanLike_OtherOwnedUnlikedWorld_ReturnsTrue()
    {
        var logic = new LikeLogic(CurrentUser);
        Assert.IsTrue(logic.CanLike(WorldA, OtherUser));
    }

    // ── いいね解除 ────────────────────────────────────────────────────────────

    [Test]
    public void TryUnlike_LikedWorld_ReturnsTrueAndRemovesLike()
    {
        var logic = new LikeLogic(CurrentUser);
        logic.TryLike(WorldA, OtherUser);
        var result = logic.TryUnlike(WorldA);
        Assert.IsTrue(result);
        Assert.IsFalse(logic.IsLiked(WorldA));
    }

    [Test]
    public void TryUnlike_NotLikedWorld_ReturnsFalse()
    {
        var logic = new LikeLogic(CurrentUser);
        var result = logic.TryUnlike(WorldA);
        Assert.IsFalse(result);
    }

    [Test]
    public void TryUnlike_ThenLikeAgain_Succeeds()
    {
        var logic = new LikeLogic(CurrentUser);
        logic.TryLike(WorldA, OtherUser);
        logic.TryUnlike(WorldA);
        var result = logic.TryLike(WorldA, OtherUser);
        Assert.IsTrue(result);
        Assert.IsTrue(logic.IsLiked(WorldA));
    }

    // ── 複数ワールド独立 ──────────────────────────────────────────────────────

    [Test]
    public void IndependentWorlds_LikeOneDoesNotAffectOther()
    {
        var logic = new LikeLogic(CurrentUser);
        logic.TryLike(WorldA, OtherUser);
        Assert.IsTrue(logic.IsLiked(WorldA));
        Assert.IsFalse(logic.IsLiked(WorldB));
    }

    // ── 初期状態セット ────────────────────────────────────────────────────────

    [Test]
    public void SetInitialLikedWorlds_MarksWorldsAsLiked()
    {
        var logic = new LikeLogic(CurrentUser);
        logic.SetInitialLikedWorlds(new[] { WorldA, WorldB });
        Assert.IsTrue(logic.IsLiked(WorldA));
        Assert.IsTrue(logic.IsLiked(WorldB));
    }

    [Test]
    public void SetInitialLikedWorlds_ClearsPreviousState()
    {
        var logic = new LikeLogic(CurrentUser);
        logic.TryLike(WorldA, OtherUser);
        logic.SetInitialLikedWorlds(new[] { WorldB });
        Assert.IsFalse(logic.IsLiked(WorldA));
        Assert.IsTrue(logic.IsLiked(WorldB));
    }
}
