using NUnit.Framework;

public class NotificationStoreTests
{
    private static NotificationItem MakeItem(
        string id,
        NotificationType type = NotificationType.FriendRequest,
        bool isRead = false
    )
    {
        var item = new NotificationItem(id, type, $"body_{id}", 0L);
        item.IsRead = isRead;
        return item;
    }

    // ── 未読件数カウント ──────────────────────────────────────────────────────

    [Test]
    public void UnreadCount_Initially_IsZero()
    {
        var store = new NotificationStore();
        Assert.AreEqual(0, store.UnreadCount);
    }

    [Test]
    public void UnreadCount_AfterAddUnread_Increases()
    {
        var store = new NotificationStore();
        store.Add(MakeItem("n1"));
        store.Add(MakeItem("n2"));
        Assert.AreEqual(2, store.UnreadCount);
    }

    [Test]
    public void UnreadCount_AlreadyReadNotification_NotCounted()
    {
        var store = new NotificationStore();
        store.Add(MakeItem("n1", isRead: true));
        store.Add(MakeItem("n2", isRead: false));
        Assert.AreEqual(1, store.UnreadCount);
    }

    // ── 既読マーク ────────────────────────────────────────────────────────────

    [Test]
    public void MarkRead_ExistingId_ReturnsTrue()
    {
        var store = new NotificationStore();
        store.Add(MakeItem("n1"));
        Assert.IsTrue(store.MarkRead("n1"));
    }

    [Test]
    public void MarkRead_ExistingId_SetsIsRead()
    {
        var store = new NotificationStore();
        store.Add(MakeItem("n1"));
        store.MarkRead("n1");
        Assert.IsTrue(store.GetAll()[0].IsRead);
    }

    [Test]
    public void MarkRead_NonExistentId_ReturnsFalse()
    {
        var store = new NotificationStore();
        Assert.IsFalse(store.MarkRead("no_such_id"));
    }

    [Test]
    public void UnreadCount_AfterMarkRead_Decreases()
    {
        var store = new NotificationStore();
        store.Add(MakeItem("n1"));
        store.Add(MakeItem("n2"));
        store.MarkRead("n1");
        Assert.AreEqual(1, store.UnreadCount);
    }

    [Test]
    public void MarkAllRead_SetsAllToRead()
    {
        var store = new NotificationStore();
        store.Add(MakeItem("n1"));
        store.Add(MakeItem("n2"));
        store.Add(MakeItem("n3"));

        store.MarkAllRead();

        Assert.AreEqual(0, store.UnreadCount);
        foreach (var n in store.GetAll())
            Assert.IsTrue(n.IsRead);
    }

    [Test]
    public void MarkAllRead_EmptyStore_DoesNotThrow()
    {
        var store = new NotificationStore();
        Assert.DoesNotThrow(() => store.MarkAllRead());
    }

    // ── 種別フィルタリング ────────────────────────────────────────────────────

    [Test]
    public void GetByType_ReturnsOnlyMatchingType()
    {
        var store = new NotificationStore();
        store.Add(MakeItem("n1", NotificationType.FriendRequest));
        store.Add(MakeItem("n2", NotificationType.WorldPublished));
        store.Add(MakeItem("n3", NotificationType.FriendRequest));

        var result = store.GetByType(NotificationType.FriendRequest);

        Assert.AreEqual(2, result.Count);
        foreach (var n in result)
            Assert.AreEqual(NotificationType.FriendRequest, n.Type);
    }

    [Test]
    public void GetByType_NoMatch_ReturnsEmpty()
    {
        var store = new NotificationStore();
        store.Add(MakeItem("n1", NotificationType.FriendRequest));

        var result = store.GetByType(NotificationType.CoinExpiry7d);

        Assert.AreEqual(0, result.Count);
    }

    [Test]
    public void GetByType_AllTypes_AreCovered()
    {
        var store = new NotificationStore();
        store.Add(MakeItem("a", NotificationType.FriendRequest));
        store.Add(MakeItem("b", NotificationType.WorldPublished));
        store.Add(MakeItem("c", NotificationType.ProductReleased));
        store.Add(MakeItem("d", NotificationType.CoinExpiry30d));
        store.Add(MakeItem("e", NotificationType.CoinExpiry7d));

        Assert.AreEqual(1, store.GetByType(NotificationType.FriendRequest).Count);
        Assert.AreEqual(1, store.GetByType(NotificationType.WorldPublished).Count);
        Assert.AreEqual(1, store.GetByType(NotificationType.ProductReleased).Count);
        Assert.AreEqual(1, store.GetByType(NotificationType.CoinExpiry30d).Count);
        Assert.AreEqual(1, store.GetByType(NotificationType.CoinExpiry7d).Count);
    }

    // ── GetAll / Add / SetAll ─────────────────────────────────────────────────

    [Test]
    public void GetAll_ReturnsAllNotifications()
    {
        var store = new NotificationStore();
        store.Add(MakeItem("n1"));
        store.Add(MakeItem("n2"));
        Assert.AreEqual(2, store.GetAll().Count);
    }

    [Test]
    public void Add_InsertsAtFront_NewestFirst()
    {
        var store = new NotificationStore();
        store.Add(MakeItem("old"));
        store.Add(MakeItem("new"));

        Assert.AreEqual("new", store.GetAll()[0].Id);
        Assert.AreEqual("old", store.GetAll()[1].Id);
    }

    [Test]
    public void SetAll_ReplacesExisting()
    {
        var store = new NotificationStore();
        store.Add(MakeItem("n1"));

        store.SetAll(new[] { MakeItem("n2"), MakeItem("n3") });

        Assert.AreEqual(2, store.Count);
        Assert.AreEqual("n2", store.GetAll()[0].Id);
    }

    [Test]
    public void SetAll_Empty_ClearsAll()
    {
        var store = new NotificationStore();
        store.Add(MakeItem("n1"));

        store.SetAll(System.Array.Empty<NotificationItem>());

        Assert.AreEqual(0, store.Count);
        Assert.AreEqual(0, store.UnreadCount);
    }
}
