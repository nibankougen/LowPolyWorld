using NUnit.Framework;

public class FriendListLogicTests
{
    // ── フレンド上限チェック ──────────────────────────────────────────────────

    [Test]
    public void FriendCapacity_NormalUser_Is100()
    {
        var logic = new FriendListLogic(maxFriends: 100);
        Assert.AreEqual(100, logic.MaxFriends);
    }

    [Test]
    public void FriendCapacity_PremiumUser_Is1000()
    {
        var logic = new FriendListLogic(maxFriends: 1000);
        Assert.AreEqual(1000, logic.MaxFriends);
    }

    [Test]
    public void SendRequest_WhenAtCapacity_ReturnsFalse()
    {
        var logic = new FriendListLogic(maxFriends: 2);
        logic.ReceiveRequest("userA");
        logic.AcceptRequest("userA");
        logic.ReceiveRequest("userB");
        logic.AcceptRequest("userB");

        var result = logic.SendRequest("userC");

        Assert.IsFalse(result);
        Assert.AreEqual(FriendRelationStatus.None, logic.GetStatus("userC"));
    }

    [Test]
    public void AcceptRequest_WhenAtCapacity_ReturnsFalse()
    {
        var logic = new FriendListLogic(maxFriends: 2);
        logic.ReceiveRequest("userA");
        logic.AcceptRequest("userA");
        logic.ReceiveRequest("userB");
        logic.AcceptRequest("userB");
        logic.ReceiveRequest("userC");

        var result = logic.AcceptRequest("userC");

        Assert.IsFalse(result);
        Assert.AreEqual(FriendRelationStatus.RequestReceived, logic.GetStatus("userC"));
    }

    [Test]
    public void SendRequest_BelowCapacity_Succeeds()
    {
        var logic = new FriendListLogic(maxFriends: 100);
        var result = logic.SendRequest("user1");
        Assert.IsTrue(result);
        Assert.AreEqual(FriendRelationStatus.RequestSent, logic.GetStatus("user1"));
    }

    [Test]
    public void FriendCount_IncreasesOnAccept()
    {
        var logic = new FriendListLogic(maxFriends: 100);
        logic.ReceiveRequest("user1");
        logic.AcceptRequest("user1");
        Assert.AreEqual(1, logic.FriendCount);
    }

    [Test]
    public void FriendCount_DecreasesOnRemove()
    {
        var logic = new FriendListLogic(maxFriends: 100);
        logic.ReceiveRequest("user1");
        logic.AcceptRequest("user1");
        logic.RemoveFriend("user1");
        Assert.AreEqual(0, logic.FriendCount);
    }

    // ── 申請状態遷移 ─────────────────────────────────────────────────────────

    [Test]
    public void SendRequest_ToNewUser_TransitionsToRequestSent()
    {
        var logic = new FriendListLogic(maxFriends: 100);
        logic.SendRequest("user1");
        Assert.AreEqual(FriendRelationStatus.RequestSent, logic.GetStatus("user1"));
    }

    [Test]
    public void NotifyRequestAccepted_TransitionsRequestSentToFriends()
    {
        var logic = new FriendListLogic(maxFriends: 100);
        logic.SendRequest("user1");
        var result = logic.NotifyRequestAccepted("user1");
        Assert.IsTrue(result);
        Assert.AreEqual(FriendRelationStatus.Friends, logic.GetStatus("user1"));
    }

    [Test]
    public void ReceiveRequest_TransitionsToRequestReceived()
    {
        var logic = new FriendListLogic(maxFriends: 100);
        logic.ReceiveRequest("user1");
        Assert.AreEqual(FriendRelationStatus.RequestReceived, logic.GetStatus("user1"));
    }

    [Test]
    public void AcceptRequest_TransitionsToFriends()
    {
        var logic = new FriendListLogic(maxFriends: 100);
        logic.ReceiveRequest("user1");
        var result = logic.AcceptRequest("user1");
        Assert.IsTrue(result);
        Assert.AreEqual(FriendRelationStatus.Friends, logic.GetStatus("user1"));
    }

    [Test]
    public void RejectRequest_RemovesRelation()
    {
        var logic = new FriendListLogic(maxFriends: 100);
        logic.ReceiveRequest("user1");
        var result = logic.RejectRequest("user1");
        Assert.IsTrue(result);
        Assert.AreEqual(FriendRelationStatus.None, logic.GetStatus("user1"));
    }

    [Test]
    public void CancelRequest_RemovesRequestSent()
    {
        var logic = new FriendListLogic(maxFriends: 100);
        logic.SendRequest("user1");
        var result = logic.CancelRequest("user1");
        Assert.IsTrue(result);
        Assert.AreEqual(FriendRelationStatus.None, logic.GetStatus("user1"));
    }

    [Test]
    public void SendRequest_ToAlreadySentTarget_ReturnsFalse()
    {
        var logic = new FriendListLogic(maxFriends: 100);
        logic.SendRequest("user1");
        var result = logic.SendRequest("user1");
        Assert.IsFalse(result);
    }

    [Test]
    public void AcceptRequest_NoExistingRequest_ReturnsFalse()
    {
        var logic = new FriendListLogic(maxFriends: 100);
        var result = logic.AcceptRequest("user1");
        Assert.IsFalse(result);
    }

    [Test]
    public void RejectRequest_NoExistingRequest_ReturnsFalse()
    {
        var logic = new FriendListLogic(maxFriends: 100);
        var result = logic.RejectRequest("user1");
        Assert.IsFalse(result);
    }

    [Test]
    public void CancelRequest_NoSentRequest_ReturnsFalse()
    {
        var logic = new FriendListLogic(maxFriends: 100);
        var result = logic.CancelRequest("user1");
        Assert.IsFalse(result);
    }

    [Test]
    public void NotifyRequestAccepted_WithNoSentRequest_ReturnsFalse()
    {
        var logic = new FriendListLogic(maxFriends: 100);
        var result = logic.NotifyRequestAccepted("user1");
        Assert.IsFalse(result);
    }

    // ── 相互承認の成立条件・解除処理 ─────────────────────────────────────────

    [Test]
    public void SendRequest_WhenRequestAlreadyReceived_BecomesFriends()
    {
        var logic = new FriendListLogic(maxFriends: 100);
        logic.ReceiveRequest("user1"); // 相手から申請あり
        var result = logic.SendRequest("user1"); // こちらも申請 → 相互承認成立
        Assert.IsTrue(result);
        Assert.AreEqual(FriendRelationStatus.Friends, logic.GetStatus("user1"));
    }

    [Test]
    public void ReceiveRequest_WhenRequestAlreadySent_BecomesFriends()
    {
        var logic = new FriendListLogic(maxFriends: 100);
        logic.SendRequest("user1"); // こちらから申請済み
        logic.ReceiveRequest("user1"); // 相手も申請 → 相互承認成立
        Assert.AreEqual(FriendRelationStatus.Friends, logic.GetStatus("user1"));
    }

    [Test]
    public void MutualApproval_DoesNotOccur_WhenOnlyOneSideHasRequest()
    {
        var logic = new FriendListLogic(maxFriends: 100);
        logic.SendRequest("user1");
        // 相手はまだ申請していない → Friends にならない
        Assert.AreEqual(FriendRelationStatus.RequestSent, logic.GetStatus("user1"));
    }

    [Test]
    public void RemoveFriend_TransitionsFriendsToNone()
    {
        var logic = new FriendListLogic(maxFriends: 100);
        logic.ReceiveRequest("user1");
        logic.AcceptRequest("user1");
        var result = logic.RemoveFriend("user1");
        Assert.IsTrue(result);
        Assert.AreEqual(FriendRelationStatus.None, logic.GetStatus("user1"));
    }

    [Test]
    public void RemoveFriend_NonFriend_ReturnsFalse()
    {
        var logic = new FriendListLogic(maxFriends: 100);
        var result = logic.RemoveFriend("user1");
        Assert.IsFalse(result);
    }

    [Test]
    public void RemoveFriend_PendingRequest_ReturnsFalse()
    {
        var logic = new FriendListLogic(maxFriends: 100);
        logic.SendRequest("user1");
        var result = logic.RemoveFriend("user1");
        Assert.IsFalse(result);
        Assert.AreEqual(FriendRelationStatus.RequestSent, logic.GetStatus("user1"));
    }

    // ── SetAll ────────────────────────────────────────────────────────────────

    [Test]
    public void SetAll_ReplacesExistingRelations()
    {
        var logic = new FriendListLogic(maxFriends: 100);
        logic.SendRequest("user1");
        logic.SetAll(
            new[]
            {
                ("user2", FriendRelationStatus.Friends),
                ("user3", FriendRelationStatus.RequestReceived),
            }
        );
        Assert.AreEqual(FriendRelationStatus.None, logic.GetStatus("user1"));
        Assert.AreEqual(FriendRelationStatus.Friends, logic.GetStatus("user2"));
        Assert.AreEqual(FriendRelationStatus.RequestReceived, logic.GetStatus("user3"));
        Assert.AreEqual(1, logic.FriendCount);
    }

    [Test]
    public void ReceiveRequest_MutualApprovalAtCapacity_DowngradesToRequestReceived()
    {
        var logic = new FriendListLogic(maxFriends: 1);
        logic.SendRequest("userB"); // こちらから申請済み（フレンド数 0、上限以下）
        logic.ReceiveRequest("userA"); // 別ユーザーから申請受信
        logic.AcceptRequest("userA"); // 上限（1人）に到達
        // userB から逆申請 → 上限のため相互承認失敗 → RequestReceived に降格
        var result = logic.ReceiveRequest("userB");
        Assert.IsFalse(result);
        Assert.AreEqual(FriendRelationStatus.RequestReceived, logic.GetStatus("userB"));
    }

    [Test]
    public void SetAll_Empty_ClearsAll()
    {
        var logic = new FriendListLogic(maxFriends: 100);
        logic.ReceiveRequest("user1");
        logic.AcceptRequest("user1");
        logic.SetAll(System.Array.Empty<(string, FriendRelationStatus)>());
        Assert.AreEqual(0, logic.FriendCount);
        Assert.AreEqual(FriendRelationStatus.None, logic.GetStatus("user1"));
    }
}
