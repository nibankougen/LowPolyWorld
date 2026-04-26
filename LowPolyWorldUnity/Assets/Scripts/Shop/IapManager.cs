using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Extension;

/// <summary>
/// Unity IAP を管理する DontDestroyOnLoad シングルトン。
/// コインパック（コンシューマブル）20 種とサブスクリプション 2 種を登録する。
/// </summary>
public class IapManager : MonoBehaviour, IDetailedStoreListener
{
    public static IapManager Instance { get; private set; }

    private IStoreController _controller;
    private bool _initialized;
    private bool _initFailed;

    /// <summary>ストア初期化成功時に発火する。価格表示の更新に使う。</summary>
    public event Action OnStoreReady;

    /// <summary>ストア初期化失敗時に発火する。</summary>
    public event Action OnStoreFailed;

    /// <summary>コイン購入がサーバー記録まで完了したとき発火する。引数は付与コイン数。</summary>
    public event Action<int> OnCoinPurchaseCompleted;

    /// <summary>プラットフォームの購入フローが失敗したとき発火する。引数は Product ID。</summary>
    public event Action<string> StorePurchaseFailed;

    public bool IsInitialized     => _initialized;
    public bool IsInitializeFailed => _initFailed;

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
        InitializePurchasing();
    }

    private void InitializePurchasing()
    {
        var builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance());

        foreach (var id in IapProductIds.AllCoinProductIds)
            builder.AddProduct(id, ProductType.Consumable);

        builder.AddProduct(IapProductIds.PremiumMonthly, ProductType.Subscription);
        builder.AddProduct(IapProductIds.PremiumYearly,  ProductType.Subscription);

        UnityPurchasing.Initialize(this, builder);
    }

    // ── IStoreListener ────────────────────────────────────────────────────────

    public void OnInitialized(IStoreController controller, IExtensionProvider extensions)
    {
        _controller = controller;
        _initialized = true;
        OnStoreReady?.Invoke();
    }

    public void OnInitializeFailed(InitializationFailureReason error)
    {
        _initFailed = true;
        Debug.LogWarning($"[IapManager] Init failed: {error}");
        OnStoreFailed?.Invoke();
    }

    public void OnInitializeFailed(InitializationFailureReason error, string message)
    {
        _initFailed = true;
        Debug.LogWarning($"[IapManager] Init failed: {error} — {message}");
        OnStoreFailed?.Invoke();
    }

    // ── IDetailedStoreListener ────────────────────────────────────────────────

    public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs args)
    {
        var product = args.purchasedProduct;
        int coins = IapProductIds.CoinsForProductId(product.definition.id);

        if (coins > 0)
        {
            _ = RecordAndConfirmAsync(product, coins);
            return PurchaseProcessingResult.Pending;
        }

        // サブスクリプション: サーバー記録は Phase 10 で実装
        return PurchaseProcessingResult.Complete;
    }

    // IStoreListener の基底メソッド — IDetailedStoreListener 実装時は詳細版が呼ばれるため空実装
    public void OnPurchaseFailed(Product product, PurchaseFailureReason reason) { }

    public void OnPurchaseFailed(Product product, PurchaseFailureDescription description)
    {
        Debug.LogWarning(
            $"[IapManager] Purchase failed: {product.definition.id} — {description.reason}: {description.message}");
        StorePurchaseFailed?.Invoke(product.definition.id);
    }

    // ── Internal ──────────────────────────────────────────────────────────────

    private async Task RecordAndConfirmAsync(Product product, int coins)
    {
        string platform = Application.platform == RuntimePlatform.IPhonePlayer ? "ios" : "android";
        var now = DateTime.UtcNow;

        float localAmount = 0f;
        string localCurrency = "JPY";
        if (product.metadata != null)
        {
            localAmount   = (float)product.metadata.localizedPrice;
            localCurrency = product.metadata.isoCurrencyCode ?? "JPY";
        }

        // JPY の場合は為替レート 1.0、他通貨はクライアントでは取得できないため 0（サーバー側で補完）
        float fxRate = string.Equals(localCurrency, "JPY", StringComparison.OrdinalIgnoreCase) ? 1.0f : 0f;

        var req = new RecordCoinPurchaseRequest
        {
            platform                = platform,
            platform_transaction_id = product.transactionID,
            storefront_country      = null,
            purchase_timestamp      = now.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            valid_until             = now.AddMonths(6).ToString("yyyy-MM-ddTHH:mm:ssZ"),
            coins_amount            = coins,
            local_amount            = localAmount,
            local_currency          = localCurrency,
            fx_rate_to_jpy          = fxRate,
        };

        try
        {
            if (ShopManager.Instance != null)
            {
                string err = await ShopManager.Instance.RecordCoinPurchaseAsync(req);
                if (err != null)
                    Debug.LogWarning($"[IapManager] RecordCoinPurchase failed: {err}");
            }
        }
        catch (Exception e)
        {
            // サーバー記録の失敗はログのみ。ConfirmPendingPurchase は必ず呼ぶ（重複請求防止）
            Debug.LogError($"[IapManager] RecordCoinPurchase threw: {e}");
        }
        finally
        {
            _controller?.ConfirmPendingPurchase(product);
            OnCoinPurchaseCompleted?.Invoke(coins);
        }
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// 指定 Product ID のローカライズ済み価格文字列を返す。
    /// 未初期化・登録外の ID の場合は null。
    /// </summary>
    public string GetLocalizedPrice(string productId)
    {
        if (!_initialized || _controller == null) return null;
        return _controller.products.WithID(productId)?.metadata?.localizedPriceString;
    }

    /// <summary>プラットフォームストアに購入リクエストを発行する。</summary>
    public void BuyProduct(string productId)
    {
        if (!_initialized || _controller == null)
        {
            Debug.LogWarning("[IapManager] BuyProduct called before store initialized.");
            return;
        }
        _controller.InitiatePurchase(productId);
    }
}
