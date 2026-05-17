using NUnit.Framework;
using System.Collections.Generic;

public class WorldAvatarSelectLogicTests
{
    // ── LoadSlotAvatars ───────────────────────────────────────────────────────

    [Test]
    public void LoadSlotAvatars_PopulatesList()
    {
        var logic = new WorldAvatarSelectLogic();
        logic.LoadSlotAvatars(new[]
        {
            new StartupAvatar { id = "a1", name = "Avatar1", vrmUrl = "http://x/a1.vrm", vrmHash = "h1" },
            new StartupAvatar { id = "a2", name = "Avatar2", vrmUrl = "http://x/a2.vrm", vrmHash = "h2" },
        });

        Assert.AreEqual(2, logic.SlotAvatars.Count);
        Assert.AreEqual("a1", logic.SlotAvatars[0].Id);
        Assert.AreEqual("a2", logic.SlotAvatars[1].Id);
    }

    [Test]
    public void LoadSlotAvatars_SkipsEntriesWithEmptyVrmUrl()
    {
        var logic = new WorldAvatarSelectLogic();
        logic.LoadSlotAvatars(new[]
        {
            new StartupAvatar { id = "a1", name = "A", vrmUrl = "", vrmHash = "" },
            new StartupAvatar { id = "a2", name = "B", vrmUrl = "http://x/b.vrm", vrmHash = "hb" },
        });

        Assert.AreEqual(1, logic.SlotAvatars.Count);
        Assert.AreEqual("a2", logic.SlotAvatars[0].Id);
    }

    [Test]
    public void LoadSlotAvatars_AutoSelectsFirstAvatar()
    {
        var logic = new WorldAvatarSelectLogic();
        logic.LoadSlotAvatars(new[]
        {
            new StartupAvatar { id = "a1", name = "A", vrmUrl = "http://x/a.vrm", vrmHash = "h" },
        });

        Assert.IsNotNull(logic.SelectedAvatar);
        Assert.AreEqual("a1", logic.SelectedAvatar.Id);
    }

    [Test]
    public void LoadSlotAvatars_DoesNotOverrideExistingSelection()
    {
        var logic = new WorldAvatarSelectLogic();
        logic.LoadSlotAvatars(new[]
        {
            new StartupAvatar { id = "a1", name = "A", vrmUrl = "http://x/a.vrm", vrmHash = "h" },
        });
        logic.LoadSlotAvatars(new[]
        {
            new StartupAvatar { id = "a2", name = "B", vrmUrl = "http://x/b.vrm", vrmHash = "h2" },
        });

        // Second load: previously auto-selected a1 is still selected (it was set before)
        Assert.AreEqual("a1", logic.SelectedAvatar.Id);
    }

    // ── LoadPurchasedAvatars ──────────────────────────────────────────────────

    [Test]
    public void LoadPurchasedAvatars_IncludesOnlyAvatarCategory()
    {
        var logic = new WorldAvatarSelectLogic();
        logic.LoadPurchasedAvatars(new[]
        {
            new MyProductEntry
            {
                productId = "p1",
                product = new ShopProductResponse { category = "avatar", name = "PA", assetUrl = "http://x/p.vrm", assetHash = "ph" },
            },
            new MyProductEntry
            {
                productId = "p2",
                product = new ShopProductResponse { category = "accessory", name = "Acc", assetUrl = "http://x/a.glb", assetHash = "ah" },
            },
        });

        Assert.AreEqual(1, logic.PurchasedAvatars.Count);
        Assert.AreEqual("p1", logic.PurchasedAvatars[0].Id);
        Assert.AreEqual(AvatarSource.DirectPurchase, logic.PurchasedAvatars[0].Source);
    }

    [Test]
    public void LoadPurchasedAvatars_SkipsEmptyAssetUrl()
    {
        var logic = new WorldAvatarSelectLogic();
        logic.LoadPurchasedAvatars(new[]
        {
            new MyProductEntry
            {
                productId = "p1",
                product = new ShopProductResponse { category = "avatar", name = "PA", assetUrl = "", assetHash = "" },
            },
        });

        Assert.AreEqual(0, logic.PurchasedAvatars.Count);
    }

    // ── Select ────────────────────────────────────────────────────────────────

    [Test]
    public void Select_UpdatesSelectedAvatar()
    {
        var logic = new WorldAvatarSelectLogic();
        logic.LoadSlotAvatars(new[]
        {
            new StartupAvatar { id = "a1", name = "A", vrmUrl = "http://x/a.vrm", vrmHash = "h" },
            new StartupAvatar { id = "a2", name = "B", vrmUrl = "http://x/b.vrm", vrmHash = "h2" },
        });

        logic.Select(logic.SlotAvatars[1]);

        Assert.AreEqual("a2", logic.SelectedAvatar.Id);
    }

    [Test]
    public void HasSelection_WhenNoAvatars_ReturnsFalse()
    {
        var logic = new WorldAvatarSelectLogic();
        Assert.IsFalse(logic.HasSelection);
    }

    [Test]
    public void HasSelection_AfterAutoSelect_ReturnsTrue()
    {
        var logic = new WorldAvatarSelectLogic();
        logic.LoadSlotAvatars(new[]
        {
            new StartupAvatar { id = "a1", name = "A", vrmUrl = "http://x/a.vrm", vrmHash = "h" },
        });
        Assert.IsTrue(logic.HasSelection);
    }

    // ── Tab ───────────────────────────────────────────────────────────────────

    [Test]
    public void SetActiveTab_ChangesTab()
    {
        var logic = new WorldAvatarSelectLogic();
        logic.SetActiveTab(AvatarSelectTab.Purchased);
        Assert.AreEqual(AvatarSelectTab.Purchased, logic.ActiveTab);
    }

    [Test]
    public void ActiveList_SlotTab_ReturnsSlotAvatars()
    {
        var logic = new WorldAvatarSelectLogic();
        logic.LoadSlotAvatars(new[]
        {
            new StartupAvatar { id = "a1", name = "A", vrmUrl = "http://x/a.vrm", vrmHash = "h" },
        });
        logic.SetActiveTab(AvatarSelectTab.Slot);
        Assert.AreEqual(logic.SlotAvatars, logic.ActiveList);
    }

    [Test]
    public void ActiveList_PurchasedTab_ReturnsPurchasedAvatars()
    {
        var logic = new WorldAvatarSelectLogic();
        logic.SetActiveTab(AvatarSelectTab.Purchased);
        Assert.AreEqual(logic.PurchasedAvatars, logic.ActiveList);
    }

    // ── スロットロック（プレミアム解約後のスロット上限超過） ─────────────────────

    [Test]
    public void LoadSlotAvatars_WithSlotLimit_AvatarsWithinLimitNotLocked()
    {
        var logic = new WorldAvatarSelectLogic();
        logic.LoadSlotAvatars(
            new[]
            {
                new StartupAvatar { id = "a1", name = "A", vrmUrl = "http://x/a.vrm", vrmHash = "h1" },
                new StartupAvatar { id = "a2", name = "B", vrmUrl = "http://x/b.vrm", vrmHash = "h2" },
            },
            slotLimit: 3
        );

        Assert.IsFalse(logic.SlotAvatars[0].IsLocked);
        Assert.IsFalse(logic.SlotAvatars[1].IsLocked);
    }

    [Test]
    public void LoadSlotAvatars_WithSlotLimit_AvatarsBeyondLimitAreLocked()
    {
        var logic = new WorldAvatarSelectLogic();
        logic.LoadSlotAvatars(
            new[]
            {
                new StartupAvatar { id = "a1", name = "A", vrmUrl = "http://x/a.vrm", vrmHash = "h1" },
                new StartupAvatar { id = "a2", name = "B", vrmUrl = "http://x/b.vrm", vrmHash = "h2" },
                new StartupAvatar { id = "a3", name = "C", vrmUrl = "http://x/c.vrm", vrmHash = "h3" },
            },
            slotLimit: 2
        );

        Assert.IsFalse(logic.SlotAvatars[0].IsLocked);
        Assert.IsFalse(logic.SlotAvatars[1].IsLocked);
        Assert.IsTrue(logic.SlotAvatars[2].IsLocked);
    }

    [Test]
    public void LoadSlotAvatars_WithSlotLimitZero_AllAvatarsLocked()
    {
        var logic = new WorldAvatarSelectLogic();
        logic.LoadSlotAvatars(
            new[]
            {
                new StartupAvatar { id = "a1", name = "A", vrmUrl = "http://x/a.vrm", vrmHash = "h1" },
            },
            slotLimit: 0
        );

        Assert.IsTrue(logic.SlotAvatars[0].IsLocked);
    }

    [Test]
    public void LoadSlotAvatars_WithoutSlotLimit_NoAvatarsLocked()
    {
        var logic = new WorldAvatarSelectLogic();
        logic.LoadSlotAvatars(new[]
        {
            new StartupAvatar { id = "a1", name = "A", vrmUrl = "http://x/a.vrm", vrmHash = "h1" },
            new StartupAvatar { id = "a2", name = "B", vrmUrl = "http://x/b.vrm", vrmHash = "h2" },
        });

        Assert.IsFalse(logic.SlotAvatars[0].IsLocked);
        Assert.IsFalse(logic.SlotAvatars[1].IsLocked);
    }

    // ── モデレーションステータス ───────────────────────────────────────────────

    [Test]
    public void LoadSlotAvatars_ModerationStatus_IsStoredCorrectly()
    {
        var logic = new WorldAvatarSelectLogic();
        logic.LoadSlotAvatars(new[]
        {
            new StartupAvatar { id = "a1", name = "A", vrmUrl = "http://x/a.vrm", vrmHash = "h1", moderationStatus = "approved" },
            new StartupAvatar { id = "a2", name = "B", vrmUrl = "http://x/b.vrm", vrmHash = "h2", moderationStatus = "pending" },
            new StartupAvatar { id = "a3", name = "C", vrmUrl = "http://x/c.vrm", vrmHash = "h3", moderationStatus = "rejected" },
        });

        Assert.IsFalse(logic.SlotAvatars[0].IsPending);
        Assert.IsFalse(logic.SlotAvatars[0].IsRejected);
        Assert.IsTrue(logic.SlotAvatars[1].IsPending);
        Assert.IsFalse(logic.SlotAvatars[1].IsRejected);
        Assert.IsFalse(logic.SlotAvatars[2].IsPending);
        Assert.IsTrue(logic.SlotAvatars[2].IsRejected);
    }

    [Test]
    public void LoadSlotAvatars_AutoSelect_SkipsRejectedAvatar()
    {
        var logic = new WorldAvatarSelectLogic();
        logic.LoadSlotAvatars(new[]
        {
            new StartupAvatar { id = "a1", name = "A", vrmUrl = "http://x/a.vrm", vrmHash = "h1", moderationStatus = "rejected" },
            new StartupAvatar { id = "a2", name = "B", vrmUrl = "http://x/b.vrm", vrmHash = "h2", moderationStatus = "approved" },
        });

        // rejected を飛ばして approved を自動選択する
        Assert.AreEqual("a2", logic.SelectedAvatar.Id);
    }

    [Test]
    public void LoadSlotAvatars_AutoSelect_PendingIsSelectable()
    {
        var logic = new WorldAvatarSelectLogic();
        logic.LoadSlotAvatars(new[]
        {
            new StartupAvatar { id = "a1", name = "A", vrmUrl = "http://x/a.vrm", vrmHash = "h1", moderationStatus = "pending" },
        });

        // pending は自動選択される（審査中でも本人は使用可）
        Assert.IsNotNull(logic.SelectedAvatar);
        Assert.AreEqual("a1", logic.SelectedAvatar.Id);
    }

    [Test]
    public void LoadSlotAvatars_AllRejected_FallsBackToFirstAvatar()
    {
        var logic = new WorldAvatarSelectLogic();
        logic.LoadSlotAvatars(new[]
        {
            new StartupAvatar { id = "a1", name = "A", vrmUrl = "http://x/a.vrm", vrmHash = "h1", moderationStatus = "rejected" },
            new StartupAvatar { id = "a2", name = "B", vrmUrl = "http://x/b.vrm", vrmHash = "h2", moderationStatus = "rejected" },
        });

        // 全件 rejected でも最初のアバターを選択しておく（confirm ボタン側で制御）
        Assert.AreEqual("a1", logic.SelectedAvatar.Id);
    }

    [Test]
    public void SelectableAvatar_NullModerationStatus_TreatedAsApproved()
    {
        var avatar = new SelectableAvatar("id", "name", "url", "hash", null, AvatarSource.Slot,
            moderationStatus: null);

        Assert.IsFalse(avatar.IsPending);
        Assert.IsFalse(avatar.IsRejected);
    }
}
