/// <summary>
/// Unity IAP で使用する Product ID の定数定義。
/// App Store Connect / Google Play Console に登録する ID と一致させること。
///
/// コイン購入商品 20 種:
///   100〜1000 コイン（100単位、10種）+ 1500 コイン（1種）+ 2000〜10000 コイン（1000単位、9種）= 20種
///
/// サブスクリプション 2 種:
///   プレミアム月額 / 年額
/// </summary>
public static class IapProductIds
{
    private const string Prefix = "com.nibankougen.lowpolyworld";

    // ── Coin packs (consumable) ───────────────────────────────────────────────

    public const string Coins0100 = Prefix + ".coins_0100";
    public const string Coins0200 = Prefix + ".coins_0200";
    public const string Coins0300 = Prefix + ".coins_0300";
    public const string Coins0400 = Prefix + ".coins_0400";
    public const string Coins0500 = Prefix + ".coins_0500";
    public const string Coins0600 = Prefix + ".coins_0600";
    public const string Coins0700 = Prefix + ".coins_0700";
    public const string Coins0800 = Prefix + ".coins_0800";
    public const string Coins0900 = Prefix + ".coins_0900";
    public const string Coins1000 = Prefix + ".coins_1000";
    public const string Coins1500 = Prefix + ".coins_1500"; // intermediate pack
    public const string Coins2000 = Prefix + ".coins_2000";
    public const string Coins3000 = Prefix + ".coins_3000";
    public const string Coins4000 = Prefix + ".coins_4000";
    public const string Coins5000 = Prefix + ".coins_5000";
    public const string Coins6000 = Prefix + ".coins_6000";
    public const string Coins7000 = Prefix + ".coins_7000";
    public const string Coins8000 = Prefix + ".coins_8000";
    public const string Coins9000 = Prefix + ".coins_9000";
    public const string Coins10000 = Prefix + ".coins_10000";

    /// <summary>全コインパック Product ID（UI 表示順）。</summary>
    public static readonly string[] AllCoinProductIds =
    {
        Coins0100, Coins0200, Coins0300, Coins0400, Coins0500,
        Coins0600, Coins0700, Coins0800, Coins0900, Coins1000,
        Coins1500,
        Coins2000, Coins3000, Coins4000, Coins5000,
        Coins6000, Coins7000, Coins8000, Coins9000, Coins10000,
    };

    /// <summary>Product ID から付与コイン数を返す。登録外の ID は 0。</summary>
    public static int CoinsForProductId(string productId) => productId switch
    {
        Coins0100 => 100,
        Coins0200 => 200,
        Coins0300 => 300,
        Coins0400 => 400,
        Coins0500 => 500,
        Coins0600 => 600,
        Coins0700 => 700,
        Coins0800 => 800,
        Coins0900 => 900,
        Coins1000 => 1000,
        Coins1500 => 1500,
        Coins2000 => 2000,
        Coins3000 => 3000,
        Coins4000 => 4000,
        Coins5000 => 5000,
        Coins6000 => 6000,
        Coins7000 => 7000,
        Coins8000 => 8000,
        Coins9000 => 9000,
        Coins10000 => 10000,
        _ => 0,
    };

    // ── Subscriptions (non-consumable) ────────────────────────────────────────

    public const string PremiumMonthly = Prefix + ".premium_monthly";
    public const string PremiumYearly  = Prefix + ".premium_yearly";
}
