using NUnit.Framework;
using System.Collections.Generic;

public class TextureCostCalculatorTests
{
    // ── CostForSize ──────────────────────────────────────────────────────────

    [TestCase(16, 1)]
    [TestCase(32, 4)]
    [TestCase(64, 16)]
    [TestCase(128, 64)]
    [TestCase(256, 256)]
    [TestCase(512, 1024)]
    public void CostForSize_MatchesSpec(int sizePx, int expectedCost)
    {
        Assert.AreEqual(expectedCost, TextureCostCalculator.CostForSize(sizePx));
    }

    // ── Calculate ────────────────────────────────────────────────────────────

    [Test]
    public void Calculate_EmptyList_ReturnsZero()
    {
        int cost = TextureCostCalculator.Calculate(
            new WorldObjectInstance[0], _ => 64);
        Assert.AreEqual(0, cost);
    }

    [Test]
    public void Calculate_UniqueTypes_SumsCosts()
    {
        var objs = new[]
        {
            new WorldObjectInstance { objectTypeId = "desk" },
            new WorldObjectInstance { objectTypeId = "chair" },
        };
        // desk=128px (cost 64), chair=64px (cost 16) → total 80
        int cost = TextureCostCalculator.Calculate(objs, key => key == "desk" ? 128 : 64);
        Assert.AreEqual(80, cost);
    }

    [Test]
    public void Calculate_DuplicateTypeId_CountsOnce()
    {
        var objs = new[]
        {
            new WorldObjectInstance { objectTypeId = "desk" },
            new WorldObjectInstance { objectTypeId = "desk" },
            new WorldObjectInstance { objectTypeId = "desk" },
        };
        int cost = TextureCostCalculator.Calculate(objs, _ => 64);
        Assert.AreEqual(16, cost, "同じ objectTypeId は 1 回だけカウント");
    }

    [Test]
    public void Calculate_SavedVariantId_CountsSeparatelyFromObjectTypeId()
    {
        var objs = new[]
        {
            new WorldObjectInstance { objectTypeId = "desk", savedVariantId = null },
            new WorldObjectInstance { objectTypeId = "desk", savedVariantId = "variant_a" },
        };
        // objectTypeId "desk" と savedVariantId "variant_a" は別エントリ → 16 + 16 = 32
        int cost = TextureCostCalculator.Calculate(objs, _ => 64);
        Assert.AreEqual(32, cost);
    }

    [Test]
    public void Calculate_AlwaysHiddenTypeId_Excluded()
    {
        var objs = new[]
        {
            new WorldObjectInstance { objectTypeId = "visible_obj" },
            new WorldObjectInstance { objectTypeId = "hidden_obj" },
        };
        var hidden = new HashSet<string>(new[] { "hidden_obj" });
        int cost = TextureCostCalculator.Calculate(objs, _ => 64, hidden);
        Assert.AreEqual(16, cost, "常時非表示オブジェクトはコスト対象外");
    }

    [Test]
    public void Calculate_AllHidden_ReturnsZero()
    {
        var objs = new[]
        {
            new WorldObjectInstance { objectTypeId = "obj_a" },
            new WorldObjectInstance { objectTypeId = "obj_b" },
        };
        var hidden = new HashSet<string>(new[] { "obj_a", "obj_b" });
        int cost = TextureCostCalculator.Calculate(objs, _ => 64, hidden);
        Assert.AreEqual(0, cost);
    }

    [Test]
    public void Calculate_NullAlwaysHidden_DoesNotThrow()
    {
        var objs = new[] { new WorldObjectInstance { objectTypeId = "obj" } };
        Assert.DoesNotThrow(() => TextureCostCalculator.Calculate(objs, _ => 64, null));
    }

    [Test]
    public void Calculate_SwitchTarget_CountsBothTypes()
    {
        // ギミック「種類切り替え（A → B）」: A・B 両方のコストを合算（セクション 4.3）
        var objs = new[] { new WorldObjectInstance { objectTypeId = "type_a" } };
        int cost = TextureCostCalculator.Calculate(
            objs, _ => 64, switchTargetTypeIds: new[] { "type_b" });
        Assert.AreEqual(32, cost, "配置中の A と切り替え先 B の両方をカウント");
    }

    [Test]
    public void Calculate_SwitchTargetAlreadyPlaced_CountsOnce()
    {
        var objs = new[]
        {
            new WorldObjectInstance { objectTypeId = "type_a" },
            new WorldObjectInstance { objectTypeId = "type_b" },
        };
        int cost = TextureCostCalculator.Calculate(
            objs, _ => 64, switchTargetTypeIds: new[] { "type_b" });
        Assert.AreEqual(32, cost, "配置済みの切り替え先は重複カウントしない");
    }

    // ── CanAdd ───────────────────────────────────────────────────────────────

    [Test]
    public void CanAdd_WithinLimit_ReturnsTrue()
    {
        Assert.IsTrue(TextureCostCalculator.CanAdd(4080, 16));
    }

    [Test]
    public void CanAdd_ExactLimit_ReturnsTrue()
    {
        Assert.IsTrue(TextureCostCalculator.CanAdd(4080, 16), "4096 ちょうどは許可");
        Assert.IsTrue(TextureCostCalculator.CanAdd(0, TextureCostCalculator.CostLimit));
    }

    [Test]
    public void CanAdd_ExceedsLimit_ReturnsFalse()
    {
        Assert.IsFalse(TextureCostCalculator.CanAdd(4081, 16));
    }

    // ── UsageRatio ───────────────────────────────────────────────────────────

    [Test]
    public void UsageRatio_Zero_ReturnsZero()
    {
        Assert.AreEqual(0f, TextureCostCalculator.UsageRatio(0), 0.001f);
    }

    [Test]
    public void UsageRatio_Full_ReturnsOne()
    {
        Assert.AreEqual(1f, TextureCostCalculator.UsageRatio(TextureCostCalculator.CostLimit), 0.001f);
    }

    [Test]
    public void UsageRatio_Half_ReturnsPointFive()
    {
        Assert.AreEqual(0.5f, TextureCostCalculator.UsageRatio(2048), 0.001f);
    }

    [Test]
    public void UsageRatio_Overflow_ClampsToOne()
    {
        Assert.AreEqual(1f, TextureCostCalculator.UsageRatio(9999), 0.001f);
    }
}
