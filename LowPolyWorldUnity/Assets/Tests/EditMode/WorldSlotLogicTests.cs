using NUnit.Framework;
using System.Collections.Generic;

public class WorldSlotLogicTests
{
    // ── 通常ユーザー ────────────────────────────────────────────────────────

    [Test]
    public void NormalUser_CanCreate_UpToFiveSlots()
    {
        var logic = new WorldSlotLogic(new List<WorldSlotEntry>(), isPremium: false);

        for (int i = 0; i < WorldSlotLogic.NormalLimit; i++)
        {
            Assert.IsTrue(logic.CanCreate(), $"スロット {i + 1} を作成できること");
            var entry = logic.TryCreate($"ワールド{i + 1}");
            Assert.IsNotNull(entry);
        }
        Assert.IsFalse(logic.CanCreate(), "5個目以降は作成不可");
        Assert.IsNull(logic.TryCreate("超過ワールド"));
    }

    [Test]
    public void NormalUser_LimitIs5()
    {
        var logic = new WorldSlotLogic(new List<WorldSlotEntry>(), isPremium: false);
        Assert.AreEqual(5, logic.Limit);
    }

    // ── プレミアムユーザー ──────────────────────────────────────────────────

    [Test]
    public void PremiumUser_LimitIs50()
    {
        var logic = new WorldSlotLogic(new List<WorldSlotEntry>(), isPremium: true);
        Assert.AreEqual(50, logic.Limit);
    }

    [Test]
    public void PremiumUser_CanCreate_UpTo50Slots()
    {
        var logic = new WorldSlotLogic(new List<WorldSlotEntry>(), isPremium: true);
        for (int i = 0; i < WorldSlotLogic.PremiumLimit; i++)
            logic.TryCreate($"W{i}");
        Assert.IsFalse(logic.CanCreate());
    }

    // ── 削除後の再追加 ──────────────────────────────────────────────────────

    [Test]
    public void Remove_FreesSlot_AllowsNewCreate()
    {
        var logic = new WorldSlotLogic(new List<WorldSlotEntry>(), isPremium: false);
        for (int i = 0; i < WorldSlotLogic.NormalLimit; i++)
            logic.TryCreate($"W{i}");

        var first = logic.GetSlots()[0];
        logic.Remove(first.WorldId);

        Assert.IsTrue(logic.CanCreate());
        Assert.IsNotNull(logic.TryCreate("新しいワールド"));
    }

    [Test]
    public void Remove_NonExistentId_DoesNothing()
    {
        var logic = new WorldSlotLogic(new List<WorldSlotEntry>(), isPremium: false);
        logic.TryCreate("W1");
        Assert.DoesNotThrow(() => logic.Remove("存在しないID"));
        Assert.AreEqual(1, logic.Count);
    }

    // ── プレミアム解約後ロック ───────────────────────────────────────────────

    [Test]
    public void AfterPremiumDowngrade_SixthSlotIsLocked()
    {
        var existing = new List<WorldSlotEntry>();
        for (int i = 0; i < WorldSlotLogic.NormalLimit + 1; i++)
            existing.Add(new WorldSlotEntry($"id_{i}", $"W{i}"));

        // 通常ユーザーとして既存スロット（6個）をロード
        var logic = new WorldSlotLogic(existing, isPremium: false);

        var slots = logic.GetSlots();
        for (int i = 0; i < WorldSlotLogic.NormalLimit; i++)
            Assert.IsFalse(logic.IsLocked(slots[i]), $"スロット {i} はロックされない");

        Assert.IsTrue(logic.IsLocked(slots[WorldSlotLogic.NormalLimit]), "6個目はロック");
    }

    [Test]
    public void PremiumUser_NoSlotsAreLocked()
    {
        var existing = new List<WorldSlotEntry>();
        for (int i = 0; i < 10; i++)
            existing.Add(new WorldSlotEntry($"id_{i}", $"W{i}"));

        var logic = new WorldSlotLogic(existing, isPremium: true);
        foreach (var slot in logic.GetSlots())
            Assert.IsFalse(logic.IsLocked(slot));
    }

    // ── GetSlots ────────────────────────────────────────────────────────────

    [Test]
    public void GetSlots_ReturnsAllSlots()
    {
        var logic = new WorldSlotLogic(new List<WorldSlotEntry>(), isPremium: false);
        logic.TryCreate("A");
        logic.TryCreate("B");
        Assert.AreEqual(2, logic.GetSlots().Count);
    }

    [Test]
    public void TryCreate_SetsCorrectWorldName()
    {
        var logic = new WorldSlotLogic(new List<WorldSlotEntry>(), isPremium: false);
        var entry = logic.TryCreate("テストワールド");
        Assert.AreEqual("テストワールド", entry.WorldName);
    }
}
