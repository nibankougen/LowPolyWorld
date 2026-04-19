using System;
using System.Collections.Generic;
using NUnit.Framework;

public class ShopProductFilterTests
{
    private static readonly DateTime Base = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private ShopProductFilter.ProductEntry MakeProduct(
        string id,
        int recentPurchaseCount = 0,
        int likesCount = 0,
        int daysOffset = 0,
        int? textureCost = null,
        string colliderSize = null)
    {
        return new ShopProductFilter.ProductEntry
        {
            Id = id,
            Category = "world_object",
            RecentPurchaseCount = recentPurchaseCount,
            LikesCount = likesCount,
            CreatedAt = Base.AddDays(daysOffset),
            TextureCost = textureCost,
            ColliderSizeCategory = colliderSize,
        };
    }

    [Test]
    public void PopularitySort_BelowThreshold_ExcludedFromTopResults()
    {
        var filter = new ShopProductFilter();
        var products = new List<ShopProductFilter.ProductEntry>
        {
            MakeProduct("low", recentPurchaseCount: 2),
            MakeProduct("popular", recentPurchaseCount: 5),
        };
        var sorted = filter.Sort(products, ShopProductFilter.SortMode.Popularity);
        Assert.AreEqual("popular", sorted[0].Id);
        Assert.AreEqual("low", sorted[1].Id);
    }

    [Test]
    public void PopularitySort_AllBelowThreshold_SortsByNewest()
    {
        var filter = new ShopProductFilter();
        var products = new List<ShopProductFilter.ProductEntry>
        {
            MakeProduct("old", recentPurchaseCount: 0, daysOffset: 0),
            MakeProduct("new", recentPurchaseCount: 2, daysOffset: 5),
        };
        var sorted = filter.Sort(products, ShopProductFilter.SortMode.Popularity);
        Assert.AreEqual("new", sorted[0].Id);
    }

    [Test]
    public void LikesSort_SortsByLikesDescending()
    {
        var filter = new ShopProductFilter();
        var products = new List<ShopProductFilter.ProductEntry>
        {
            MakeProduct("a", likesCount: 10),
            MakeProduct("b", likesCount: 50),
            MakeProduct("c", likesCount: 1),
        };
        var sorted = filter.Sort(products, ShopProductFilter.SortMode.Likes);
        Assert.AreEqual("b", sorted[0].Id);
        Assert.AreEqual("a", sorted[1].Id);
        Assert.AreEqual("c", sorted[2].Id);
    }

    [Test]
    public void FilterByTextureCost_FiltersCorrectly()
    {
        var filter = new ShopProductFilter();
        var products = new List<ShopProductFilter.ProductEntry>
        {
            MakeProduct("cheap", textureCost: 1),
            MakeProduct("medium", textureCost: 5),
            MakeProduct("expensive", textureCost: 10),
        };
        var filtered = filter.FilterByTextureCost(products, min: 3, max: 7);
        Assert.AreEqual(1, filtered.Count);
        Assert.AreEqual("medium", filtered[0].Id);
    }

    [Test]
    public void FilterByTextureCost_NullTextureCost_Excluded()
    {
        var filter = new ShopProductFilter();
        var products = new List<ShopProductFilter.ProductEntry>
        {
            MakeProduct("no_cost", textureCost: null),
            MakeProduct("with_cost", textureCost: 3),
        };
        var filtered = filter.FilterByTextureCost(products, min: 1, max: 10);
        Assert.AreEqual(1, filtered.Count);
        Assert.AreEqual("with_cost", filtered[0].Id);
    }

    [Test]
    public void FilterByColliderSize_FiltersCorrectly()
    {
        var filter = new ShopProductFilter();
        var products = new List<ShopProductFilter.ProductEntry>
        {
            MakeProduct("small", colliderSize: "small"),
            MakeProduct("medium", colliderSize: "medium"),
            MakeProduct("large", colliderSize: "large"),
        };
        var filtered = filter.FilterByColliderSize(products, "medium");
        Assert.AreEqual(1, filtered.Count);
        Assert.AreEqual("medium", filtered[0].Id);
    }

    [Test]
    public void FilterByColliderSize_CaseInsensitive()
    {
        var filter = new ShopProductFilter();
        var products = new List<ShopProductFilter.ProductEntry>
        {
            MakeProduct("p1", colliderSize: "Small"),
        };
        var filtered = filter.FilterByColliderSize(products, "small");
        Assert.AreEqual(1, filtered.Count);
    }
}
