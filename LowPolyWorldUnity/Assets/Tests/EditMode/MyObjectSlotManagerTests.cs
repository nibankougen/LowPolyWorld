using System.Collections.Generic;
using NUnit.Framework;

public class MyObjectSlotManagerTests
{
    // ── 通常ユーザー: 上限 10 ─────────────────────────────────────────────────────

    [Test]
    public void NormalUser_EmptySlots_CanAdd()
    {
        var mgr = new MyObjectSlotManager(new List<string>(), isPremium: false);
        Assert.IsTrue(mgr.CanAdd());
    }

    [Test]
    public void NormalUser_BelowLimit_TryAddReturnsTrue()
    {
        var mgr = new MyObjectSlotManager(new List<string>(), isPremium: false);
        bool ok = mgr.TryAdd("obj_a");
        Assert.IsTrue(ok);
        Assert.AreEqual(1, mgr.Count);
    }

    [Test]
    public void NormalUser_AtLimit_CannotAdd()
    {
        var mgr = new MyObjectSlotManager(MakeSlots(MyObjectSlotManager.NormalLimit), isPremium: false);
        Assert.IsFalse(mgr.CanAdd());
    }

    [Test]
    public void NormalUser_AtLimit_TryAddReturnsFalse()
    {
        var mgr = new MyObjectSlotManager(MakeSlots(MyObjectSlotManager.NormalLimit), isPremium: false);
        Assert.IsFalse(mgr.TryAdd("new_obj"));
        Assert.AreEqual(MyObjectSlotManager.NormalLimit, mgr.Count);
    }

    // ── プレミアムユーザー: 上限 100 ──────────────────────────────────────────────

    [Test]
    public void PremiumUser_AtNormalLimit_CanStillAdd()
    {
        var mgr = new MyObjectSlotManager(MakeSlots(MyObjectSlotManager.NormalLimit), isPremium: true);
        Assert.IsTrue(mgr.CanAdd());
    }

    [Test]
    public void PremiumUser_AtPremiumLimit_CannotAdd()
    {
        var mgr = new MyObjectSlotManager(MakeSlots(MyObjectSlotManager.PremiumLimit), isPremium: true);
        Assert.IsFalse(mgr.CanAdd());
    }

    [Test]
    public void PremiumUser_AtPremiumLimit_TryAddReturnsFalse()
    {
        var mgr = new MyObjectSlotManager(MakeSlots(MyObjectSlotManager.PremiumLimit), isPremium: true);
        Assert.IsFalse(mgr.TryAdd("new_obj"));
    }

    // ── プレミアム解約後ロック ─────────────────────────────────────────────────────

    [Test]
    public void Downgraded_SlotsOverNormalLimit_CannotAdd()
    {
        // プレミアム時に 15 スロット使用 → 解約後(isPremium=false)は追加不可
        var mgr = new MyObjectSlotManager(MakeSlots(15), isPremium: false);
        Assert.IsFalse(mgr.CanAdd());
    }

    [Test]
    public void Downgraded_SlotsOverNormalLimit_ExistingSlotsPreserved()
    {
        // 解約後もデータは削除されず 15 スロットが保持される
        var mgr = new MyObjectSlotManager(MakeSlots(15), isPremium: false);
        Assert.AreEqual(15, mgr.Count);
    }

    // ── 削除後の再追加 ────────────────────────────────────────────────────────────

    [Test]
    public void Remove_ExistingSlot_DecreasesCount()
    {
        var mgr = new MyObjectSlotManager(new List<string> { "obj_a", "obj_b" }, isPremium: false);
        mgr.Remove("obj_a");
        Assert.AreEqual(1, mgr.Count);
    }

    [Test]
    public void Remove_WhenAtLimit_AllowsTryAddAgain()
    {
        var mgr = new MyObjectSlotManager(MakeSlots(MyObjectSlotManager.NormalLimit), isPremium: false);
        mgr.Remove("slot_0");
        Assert.IsTrue(mgr.TryAdd("new_obj"));
    }

    // ── Limit プロパティ ──────────────────────────────────────────────────────────

    [Test]
    public void Limit_NormalUser_IsNormalLimit()
    {
        var mgr = new MyObjectSlotManager(new List<string>(), isPremium: false);
        Assert.AreEqual(MyObjectSlotManager.NormalLimit, mgr.Limit);
    }

    [Test]
    public void Limit_PremiumUser_IsPremiumLimit()
    {
        var mgr = new MyObjectSlotManager(new List<string>(), isPremium: true);
        Assert.AreEqual(MyObjectSlotManager.PremiumLimit, mgr.Limit);
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
