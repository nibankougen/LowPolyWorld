using System.Collections.Generic;
using NUnit.Framework;

public class WorldVariantSlotManagerTests
{
    // ── 通常ユーザー: 上限 10 ─────────────────────────────────────────────────────

    [Test]
    public void NormalUser_EmptySlots_CanAdd()
    {
        var mgr = new WorldVariantSlotManager(new List<string>(), isPremium: false);
        Assert.IsTrue(mgr.CanAdd());
    }

    [Test]
    public void NormalUser_BelowLimit_TryAddReturnsTrue()
    {
        var mgr = new WorldVariantSlotManager(new List<string>(), isPremium: false);
        bool ok = mgr.TryAdd("slot_a");
        Assert.IsTrue(ok);
        Assert.AreEqual(1, mgr.Count);
    }

    [Test]
    public void NormalUser_AtLimit_CannotAdd()
    {
        var mgr = new WorldVariantSlotManager(MakeSlots(WorldVariantSlotManager.NormalLimit), isPremium: false);
        Assert.IsFalse(mgr.CanAdd());
    }

    [Test]
    public void NormalUser_AtLimit_TryAddReturnsFalse()
    {
        var mgr = new WorldVariantSlotManager(MakeSlots(WorldVariantSlotManager.NormalLimit), isPremium: false);
        Assert.IsFalse(mgr.TryAdd("new_slot"));
        Assert.AreEqual(WorldVariantSlotManager.NormalLimit, mgr.Count);
    }

    // ── プレミアムユーザー: 上限 100 ──────────────────────────────────────────────

    [Test]
    public void PremiumUser_AtNormalLimit_CanStillAdd()
    {
        var mgr = new WorldVariantSlotManager(MakeSlots(WorldVariantSlotManager.NormalLimit), isPremium: true);
        Assert.IsTrue(mgr.CanAdd());
    }

    [Test]
    public void PremiumUser_AtPremiumLimit_CannotAdd()
    {
        var mgr = new WorldVariantSlotManager(MakeSlots(WorldVariantSlotManager.PremiumLimit), isPremium: true);
        Assert.IsFalse(mgr.CanAdd());
    }

    [Test]
    public void PremiumUser_AtPremiumLimit_TryAddReturnsFalse()
    {
        var mgr = new WorldVariantSlotManager(MakeSlots(WorldVariantSlotManager.PremiumLimit), isPremium: true);
        Assert.IsFalse(mgr.TryAdd("new_slot"));
    }

    // ── プレミアム解約後ロック ─────────────────────────────────────────────────────

    [Test]
    public void Downgraded_SlotsOverNormalLimit_CannotAdd()
    {
        // プレミアム時に 15 スロット使用 → 解約後(isPremium=false)は追加不可
        var mgr = new WorldVariantSlotManager(MakeSlots(15), isPremium: false);
        Assert.IsFalse(mgr.CanAdd());
    }

    [Test]
    public void Downgraded_SlotsOverNormalLimit_ExistingSlotsPreserved()
    {
        // 解約後もデータは削除されず 15 スロットが保持される
        var mgr = new WorldVariantSlotManager(MakeSlots(15), isPremium: false);
        Assert.AreEqual(15, mgr.Count);
    }

    // ── 削除後の再追加 ────────────────────────────────────────────────────────────

    [Test]
    public void Remove_ExistingSlot_DecreasesCount()
    {
        var mgr = new WorldVariantSlotManager(new List<string> { "slot_a", "slot_b" }, isPremium: false);
        mgr.Remove("slot_a");
        Assert.AreEqual(1, mgr.Count);
    }

    [Test]
    public void Remove_WhenAtLimit_AllowsTryAddAgain()
    {
        var mgr = new WorldVariantSlotManager(MakeSlots(WorldVariantSlotManager.NormalLimit), isPremium: false);
        mgr.Remove("slot_0");
        Assert.IsTrue(mgr.TryAdd("new_slot"));
    }

    // ── Limit プロパティ ──────────────────────────────────────────────────────────

    [Test]
    public void Limit_NormalUser_IsNormalLimit()
    {
        var mgr = new WorldVariantSlotManager(new List<string>(), isPremium: false);
        Assert.AreEqual(WorldVariantSlotManager.NormalLimit, mgr.Limit);
    }

    [Test]
    public void Limit_PremiumUser_IsPremiumLimit()
    {
        var mgr = new WorldVariantSlotManager(new List<string>(), isPremium: true);
        Assert.AreEqual(WorldVariantSlotManager.PremiumLimit, mgr.Limit);
    }

    // ── ヘルパー ──────────────────────────────────────────────────────────────────

    private static List<string> MakeSlots(int count)
    {
        var slots = new List<string>(count);
        for (int i = 0; i < count; i++)
            slots.Add($"slot_{i}");
        return slots;
    }
}
