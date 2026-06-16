using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

public class GimmickTypeCatalogTests
{
    // ── カテゴリが正規 ID を過不足なく分類していること ──────────────────────────

    private static void AssertCoversExactly(
        IReadOnlyList<GimmickTypeCatalog.Category> categories, string[] canonical)
    {
        var fromCatalog = categories.SelectMany(c => c.TypeIds).ToList();

        // 重複なし（各 ID はちょうど 1 カテゴリ）
        CollectionAssert.AllItemsAreUnique(fromCatalog);
        // 正規 ID 集合と完全一致（漏れ・余りなし）
        CollectionAssert.AreEquivalent(canonical, fromCatalog);
        // 空カテゴリがない
        Assert.IsFalse(categories.Any(c => c.TypeIds.Length == 0));
        // カテゴリ名が空でない
        Assert.IsFalse(categories.Any(c => string.IsNullOrEmpty(c.Label)));
    }

    [Test]
    public void TriggerCategories_CoverAllTriggerTypes()
    {
        AssertCoversExactly(GimmickTypeCatalog.TriggerCategories, GimmickRuleEditLogic.TriggerTypes);
    }

    [Test]
    public void ConditionCategories_CoverAllConditionTypes()
    {
        AssertCoversExactly(GimmickTypeCatalog.ConditionCategories, GimmickRuleEditLogic.ConditionTypes);
    }

    [Test]
    public void ActionCategories_CoverAllActionTypes()
    {
        AssertCoversExactly(GimmickTypeCatalog.ActionCategories, GimmickRuleEditLogic.ActionTypes);
    }

    // ── ラベルが全種別に存在すること（フォールバックで ID がそのまま返らないこと）──

    [Test]
    public void AllTypes_HaveJapaneseLabels()
    {
        foreach (var id in GimmickRuleEditLogic.TriggerTypes)
            Assert.AreNotEqual(id, GimmickTypeCatalog.TriggerLabel(id), $"トリガー {id} のラベル未定義");
        foreach (var id in GimmickRuleEditLogic.ConditionTypes)
            Assert.AreNotEqual(id, GimmickTypeCatalog.ConditionLabel(id), $"条件 {id} のラベル未定義");
        foreach (var id in GimmickRuleEditLogic.ActionTypes)
            Assert.AreNotEqual(id, GimmickTypeCatalog.ActionLabel(id), $"アクション {id} のラベル未定義");
    }

    // ── CategoryIndexOf ────────────────────────────────────────────────────────

    [Test]
    public void CategoryIndexOf_FindsContainingCategory()
    {
        // showMessage は「演出・会話」カテゴリに属する
        int idx = GimmickTypeCatalog.CategoryIndexOf(GimmickTypeCatalog.ActionCategories, "showMessage");
        Assert.AreEqual("演出・会話", GimmickTypeCatalog.ActionCategories[idx].Label);
        CollectionAssert.Contains(GimmickTypeCatalog.ActionCategories[idx].TypeIds, "showMessage");
    }

    [Test]
    public void CategoryIndexOf_UnknownReturnsZero()
    {
        Assert.AreEqual(0, GimmickTypeCatalog.CategoryIndexOf(GimmickTypeCatalog.ActionCategories, "bogus"));
    }
}
