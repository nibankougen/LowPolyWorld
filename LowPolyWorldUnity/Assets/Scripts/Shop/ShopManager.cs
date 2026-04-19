using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// ショップ・コイン状態を管理する DontDestroyOnLoad シングルトン。
/// MonoBehaviour は API 呼び出しのオーケストレーションのみを担当し、
/// コインロジックは CoinLedger（純粋 C#）に委譲する。
/// </summary>
public class ShopManager : MonoBehaviour
{
    public static ShopManager Instance { get; private set; }

    /// <summary>コイン残高・ロット管理ロジック。</summary>
    public CoinLedger Ledger { get; } = new CoinLedger();

    /// <summary>現在のコイン残高（UTC 基準）。</summary>
    public int CoinBalance => Ledger.GetBalance(DateTime.UtcNow);

    /// <summary>コイン残高が変化したときに発火する。</summary>
    public event Action OnCoinBalanceChanged;

    // ── Lifecycle ────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Bootstrapper がログイン後に呼び出す初期化処理。
    /// コイン残高を取得してローカル状態を構築する。
    /// </summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await RefreshCoinBalanceAsync(ct);
    }

    // ── Coin balance ─────────────────────────────────────────────────────────

    /// <summary>サーバーからコイン残高を取得して Ledger を更新する。</summary>
    public async Task RefreshCoinBalanceAsync(CancellationToken ct = default)
    {
        var api = UserManager.Instance.Api;
        var (res, err) = await api.GetAsync<CoinBalanceResponse>("/api/v1/me/coins", ct);
        if (err != null)
        {
            Debug.LogWarning($"[ShopManager] GetCoinBalance failed: {err}");
            return;
        }

        var lots = new List<CoinLedger.CoinLot>();
        if (res.lots != null)
        {
            foreach (var l in res.lots)
            {
                if (DateTime.TryParse(l.validUntil, out var expiry))
                    lots.Add(new CoinLedger.CoinLot
                    {
                        PurchaseId = l.purchaseId,
                        Coins = l.coinsAmount,
                        ValidUntil = expiry.ToUniversalTime(),
                    });
            }
        }

        Ledger.SetFromApi(lots, res.totalDeducted, res.totalSpent);
        OnCoinBalanceChanged?.Invoke();
    }

    // ── Products ─────────────────────────────────────────────────────────────

    /// <summary>
    /// ショップ商品一覧を取得する。
    /// </summary>
    /// <param name="category">null = 全カテゴリ / "avatar" / "accessory" / "world_object" / "stamp"</param>
    /// <param name="sort">null = "popularity" / "likes" / "newest" / "oldest"</param>
    /// <param name="search">名前またはタグ検索文字列（null = 検索なし）</param>
    /// <param name="after">カーソル（null = 先頭）</param>
    public async Task<(ShopProductListResponse result, string error)> FetchProductsAsync(
        string category = null,
        string sort = null,
        string search = null,
        string after = null,
        int limit = 20,
        CancellationToken ct = default)
    {
        var sb = new System.Text.StringBuilder("/api/v1/shop/products?limit=");
        sb.Append(limit);
        if (!string.IsNullOrEmpty(category)) sb.Append("&category=").Append(Uri.EscapeDataString(category));
        if (!string.IsNullOrEmpty(sort))     sb.Append("&sort=").Append(Uri.EscapeDataString(sort));
        if (!string.IsNullOrEmpty(search))   sb.Append("&search=").Append(Uri.EscapeDataString(search));
        if (!string.IsNullOrEmpty(after))    sb.Append("&after=").Append(Uri.EscapeDataString(after));

        return await UserManager.Instance.Api.GetAsync<ShopProductListResponse>(sb.ToString(), ct);
    }

    /// <summary>商品詳細を取得する。</summary>
    public async Task<(ShopProductResponse result, string error)> FetchProductAsync(
        string productId,
        CancellationToken ct = default)
    {
        return await UserManager.Instance.Api.GetAsync<ShopProductResponse>(
            $"/api/v1/shop/products/{productId}", ct);
    }

    /// <summary>
    /// 商品を購入する。成功時はコイン残高を即時更新する。
    /// </summary>
    /// <returns>null = 成功 / エラーコード文字列</returns>
    public async Task<string> PurchaseProductAsync(
        string productId,
        string idempotencyKey = null,
        CancellationToken ct = default)
    {
        if (!Ledger.CanPurchaseProduct(0, DateTime.UtcNow))
            return "insufficient_coins";

        var body = new PurchaseProductRequest
        {
            idempotency_key = idempotencyKey ?? Guid.NewGuid().ToString(),
        };

        var err = await UserManager.Instance.Api.PostJsonNoBodyAsync(
            $"/api/v1/shop/products/{productId}/purchase", body, ct);

        if (err == null)
            await RefreshCoinBalanceAsync(ct);

        return err;
    }

    /// <summary>商品にいいねする。</summary>
    public async Task<string> LikeProductAsync(string productId, CancellationToken ct = default)
    {
        return await UserManager.Instance.Api.PostJsonNoBodyAsync(
            $"/api/v1/shop/products/{productId}/like", null, ct);
    }

    /// <summary>商品のいいねを解除する。</summary>
    public async Task<string> UnlikeProductAsync(string productId, CancellationToken ct = default)
    {
        return await UserManager.Instance.Api.DeleteAsync(
            $"/api/v1/shop/products/{productId}/like", ct);
    }

    // ── Purchased products ───────────────────────────────────────────────────

    /// <summary>購入済み商品一覧を取得する。</summary>
    public async Task<(MyProductListResponse result, string error)> FetchMyProductsAsync(
        string after = null,
        int limit = 20,
        CancellationToken ct = default)
    {
        var path = $"/api/v1/me/products?limit={limit}";
        if (!string.IsNullOrEmpty(after))
            path += $"&after={Uri.EscapeDataString(after)}";

        return await UserManager.Instance.Api.GetAsync<MyProductListResponse>(path, ct);
    }

    // ── Coin purchase (IAP result recording) ─────────────────────────────────

    /// <summary>
    /// IAP 購入完了後にサーバーへコイン購入を記録する。
    /// 成功時はコイン残高を即時更新する。
    /// </summary>
    public async Task<string> RecordCoinPurchaseAsync(
        RecordCoinPurchaseRequest req,
        CancellationToken ct = default)
    {
        var err = await UserManager.Instance.Api.PostJsonNoBodyAsync(
            "/api/v1/me/coins/purchases", req, ct);

        if (err == null)
            await RefreshCoinBalanceAsync(ct);

        return err;
    }
}
