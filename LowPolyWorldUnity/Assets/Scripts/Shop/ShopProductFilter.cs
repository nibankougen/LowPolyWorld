using System;
using System.Collections.Generic;

/// <summary>
/// ショップ商品フィルタリング・ソートロジック。純粋 C# クラス。
/// </summary>
public class ShopProductFilter
{
    public enum SortMode
    {
        Popularity,
        Likes,
        Newest,
        Oldest,
    }

    public struct ProductEntry
    {
        public string Id;
        public string Category;
        public int RecentPurchaseCount;
        public int LikesCount;
        public DateTime CreatedAt;
        public int? TextureCost;
        public string ColliderSizeCategory; // "small" | "medium" | "large" | null
    }

    private const int PopularityThreshold = 3;

    /// <summary>
    /// 商品一覧をソートする。
    /// popularity ソート時は recent_purchase_count >= 3 の商品のみが上位に来る
    /// （閾値未満は通常の新着順に fallback）。
    /// </summary>
    public List<ProductEntry> Sort(IEnumerable<ProductEntry> products, SortMode mode)
    {
        var list = new List<ProductEntry>(products);
        switch (mode)
        {
            case SortMode.Popularity:
                list.Sort((a, b) =>
                {
                    bool aPopular = a.RecentPurchaseCount >= PopularityThreshold;
                    bool bPopular = b.RecentPurchaseCount >= PopularityThreshold;
                    if (aPopular != bPopular)
                        return bPopular.CompareTo(aPopular); // popular first
                    if (aPopular)
                        return b.RecentPurchaseCount.CompareTo(a.RecentPurchaseCount);
                    return b.CreatedAt.CompareTo(a.CreatedAt);
                });
                break;
            case SortMode.Likes:
                list.Sort((a, b) =>
                {
                    int c = b.LikesCount.CompareTo(a.LikesCount);
                    return c != 0 ? c : b.CreatedAt.CompareTo(a.CreatedAt);
                });
                break;
            case SortMode.Newest:
                list.Sort((a, b) => b.CreatedAt.CompareTo(a.CreatedAt));
                break;
            case SortMode.Oldest:
                list.Sort((a, b) => a.CreatedAt.CompareTo(b.CreatedAt));
                break;
        }
        return list;
    }

    /// <summary>オブジェクトのテクスチャコストでフィルタリング。</summary>
    public List<ProductEntry> FilterByTextureCost(IEnumerable<ProductEntry> products, int? min, int? max)
    {
        var result = new List<ProductEntry>();
        foreach (var p in products)
        {
            if (p.TextureCost == null)
                continue;
            if (min.HasValue && p.TextureCost.Value < min.Value)
                continue;
            if (max.HasValue && p.TextureCost.Value > max.Value)
                continue;
            result.Add(p);
        }
        return result;
    }

    /// <summary>コライダーサイズカテゴリでフィルタリング。</summary>
    public List<ProductEntry> FilterByColliderSize(IEnumerable<ProductEntry> products, string sizeCategory)
    {
        var result = new List<ProductEntry>();
        foreach (var p in products)
        {
            if (string.Equals(p.ColliderSizeCategory, sizeCategory, StringComparison.OrdinalIgnoreCase))
                result.Add(p);
        }
        return result;
    }
}
