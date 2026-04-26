using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// ショップタブの表示・インタラクションを管理するロジッククラス。
/// 固定ヘッダー（5タブ + コイン残高）・検索/ソート・商品一覧の無限スクロールを担当する。
/// </summary>
public class ShopTabController : IDisposable
{
    private enum Category { Premium, Avatar, Accessory, Object, Stamp }
    private enum SortMode { Popularity, Likes, Newest, Oldest }

    // ── UI references ─────────────────────────────────────────────────────────

    private readonly VisualElement _root;
    private readonly Button _tabPremium;
    private readonly Button _tabAvatar;
    private readonly Button _tabAccessory;
    private readonly Button _tabObject;
    private readonly Button _tabStamp;

    private readonly Label _coinBalance;

    private readonly VisualElement _searchFilterBar;
    private readonly TextField _searchInput;
    private readonly Button _sortPopularity;
    private readonly Button _sortLikes;
    private readonly Button _sortNewest;
    private readonly Button _sortOldest;
    private readonly VisualElement _objectFilterRow;
    private readonly Button _filterSizeAll;
    private readonly Button _filterSizeSmall;
    private readonly Button _filterSizeMedium;
    private readonly Button _filterSizeLarge;
    private readonly Button _filterCostAll;
    private readonly Button _filterCostLow;
    private readonly Button _filterCostMid;
    private readonly Button _filterCostHigh;

    private readonly ScrollView _premiumScroll;
    private readonly Button _btnBuyAnnual;
    private readonly Button _btnBuyMonthly;
    private readonly Label _planAnnualPrice;
    private readonly Label _planAnnualMonthly;
    private readonly Label _planMonthlyPrice;
    private readonly VisualElement _currentPlanArea;
    private readonly Label _currentPlanLabel;
    private readonly Label _nextRenewalLabel;

    private readonly ScrollView _productScroll;
    private readonly VisualElement _productList;
    private readonly VisualElement _loadMoreArea;
    private readonly VisualElement _emptyArea;

    private readonly VisualElement _coinDetailBackdrop;
    private readonly Label _coinDetailBalance;
    private readonly VisualElement _coinDetailWarning;
    private readonly VisualElement _coinLotList;
    private readonly VisualElement _coinLotEmpty;

    private readonly VisualElement _coinPurchaseBackdrop;
    private readonly VisualElement _coinPackList;

    private readonly VisualElement _errorToast;
    private readonly Label _errorToastLabel;

    // ── State ─────────────────────────────────────────────────────────────────

    private Category _currentCategory;
    private SortMode _currentSort = SortMode.Popularity;
    private string _currentColliderFilter = null;   // null = all
    private int? _currentTextureCostMin = null;
    private int? _currentTextureCostMax = null;
    private string _searchText = "";
    private string _cursor = null;
    private bool _hasMore = false;
    private bool _isLoading = false;
    private bool _coinDetailVisible = false;
    private bool _coinPurchaseVisible = false;
    private readonly List<ShopProductResponse> _loadedProducts = new();
    private readonly List<(ShopProductResponse product, Button btn)> _purchaseBtns = new();
    private readonly List<(string productId, Label priceLabel, Button buyBtn)> _coinPackRows = new();

    private CancellationTokenSource _cts = new();
    private IVisualElementScheduledItem _toastSchedule;

    // ── Constructor ───────────────────────────────────────────────────────────

    public ShopTabController(VisualElement root)
    {
        _root = root;
        _tabPremium   = root.Q<Button>("tab-premium");
        _tabAvatar    = root.Q<Button>("tab-avatar");
        _tabAccessory = root.Q<Button>("tab-accessory");
        _tabObject    = root.Q<Button>("tab-object");
        _tabStamp     = root.Q<Button>("tab-stamp");

        _coinBalance = root.Q<Label>("coin-balance");

        _searchFilterBar   = root.Q<VisualElement>("search-filter-bar");
        _searchInput       = root.Q<TextField>("search-input");
        _sortPopularity    = root.Q<Button>("sort-popularity");
        _sortLikes         = root.Q<Button>("sort-likes");
        _sortNewest        = root.Q<Button>("sort-newest");
        _sortOldest        = root.Q<Button>("sort-oldest");
        _objectFilterRow   = root.Q<VisualElement>("object-filter-row");
        _filterSizeAll     = root.Q<Button>("filter-size-all");
        _filterSizeSmall   = root.Q<Button>("filter-size-small");
        _filterSizeMedium  = root.Q<Button>("filter-size-medium");
        _filterSizeLarge   = root.Q<Button>("filter-size-large");
        _filterCostAll     = root.Q<Button>("filter-cost-all");
        _filterCostLow     = root.Q<Button>("filter-cost-low");
        _filterCostMid     = root.Q<Button>("filter-cost-mid");
        _filterCostHigh    = root.Q<Button>("filter-cost-high");

        _premiumScroll     = root.Q<ScrollView>("premium-scroll");
        _btnBuyAnnual      = root.Q<Button>("btn-buy-annual");
        _btnBuyMonthly     = root.Q<Button>("btn-buy-monthly");
        _planAnnualPrice   = root.Q<Label>("plan-annual-price");
        _planAnnualMonthly = root.Q<Label>("plan-annual-monthly");
        _planMonthlyPrice  = root.Q<Label>("plan-monthly-price");
        _currentPlanArea   = root.Q<VisualElement>("current-plan-area");
        _currentPlanLabel  = root.Q<Label>("current-plan-label");
        _nextRenewalLabel  = root.Q<Label>("next-renewal-label");

        _productScroll = root.Q<ScrollView>("product-scroll");
        _productList   = root.Q<VisualElement>("product-list");
        _loadMoreArea  = root.Q<VisualElement>("load-more-area");
        _emptyArea     = root.Q<VisualElement>("empty-area");

        _coinDetailBackdrop = root.Q<VisualElement>("coin-detail-backdrop");
        _coinDetailBalance  = root.Q<Label>("coin-detail-balance");
        _coinDetailWarning  = root.Q<VisualElement>("coin-detail-warning");
        _coinLotList        = root.Q<VisualElement>("coin-lot-list");
        _coinLotEmpty       = root.Q<VisualElement>("coin-lot-empty");

        _coinPurchaseBackdrop = root.Q<VisualElement>("coin-purchase-backdrop");
        _coinPackList         = root.Q<VisualElement>("coin-pack-list");

        _errorToast      = root.Q<VisualElement>("error-toast");
        _errorToastLabel = root.Q<Label>("error-toast-label");

        BindButtons();
        UpdateCoinBalance();

        var isPremium = IsPremiumUser();
        SelectCategory(isPremium ? Category.Avatar : Category.Premium);
    }

    // ── Binding ───────────────────────────────────────────────────────────────

    private void BindButtons()
    {
        _tabPremium.clicked   += () => SelectCategory(Category.Premium);
        _tabAvatar.clicked    += () => SelectCategory(Category.Avatar);
        _tabAccessory.clicked += () => SelectCategory(Category.Accessory);
        _tabObject.clicked    += () => SelectCategory(Category.Object);
        _tabStamp.clicked     += () => SelectCategory(Category.Stamp);

        _sortPopularity.clicked += () => SelectSort(SortMode.Popularity);
        _sortLikes.clicked      += () => SelectSort(SortMode.Likes);
        _sortNewest.clicked     += () => SelectSort(SortMode.Newest);
        _sortOldest.clicked     += () => SelectSort(SortMode.Oldest);

        _filterSizeAll.clicked    += () => SelectColliderFilter(null);
        _filterSizeSmall.clicked  += () => SelectColliderFilter("small");
        _filterSizeMedium.clicked += () => SelectColliderFilter("medium");
        _filterSizeLarge.clicked  += () => SelectColliderFilter("large");

        _filterCostAll.clicked  += () => SelectTextureCostFilter(null, null);
        _filterCostLow.clicked  += () => SelectTextureCostFilter(null, 4);
        _filterCostMid.clicked  += () => SelectTextureCostFilter(16, 64);
        _filterCostHigh.clicked += () => SelectTextureCostFilter(256, null);

        _searchInput.RegisterValueChangedCallback(evt =>
        {
            _searchText = evt.newValue ?? "";
            ReloadProducts();
        });

        _productScroll.verticalScroller.valueChanged += OnScrollChanged;

        _btnBuyAnnual.clicked  += OnBuyAnnualClicked;
        _btnBuyMonthly.clicked += OnBuyMonthlyClicked;

        // コイン詳細パネル
        _root.Q<VisualElement>("coin-area").RegisterCallback<ClickEvent>(_ => ShowCoinDetail());
        _root.Q<Button>("btn-coin-detail-close").clicked += HideCoinDetail;
        _coinDetailBackdrop.RegisterCallback<ClickEvent>(evt =>
        {
            if (evt.target == _coinDetailBackdrop)
                HideCoinDetail();
        });
        _root.Q<Button>("btn-buy-coins").clicked += OnBuyCoinsClicked;

        // コイン購入パネル
        _root.Q<Button>("btn-coin-purchase-close").clicked += HideCoinPurchasePanel;
        _coinPurchaseBackdrop.RegisterCallback<ClickEvent>(evt =>
        {
            if (evt.target == _coinPurchaseBackdrop)
                HideCoinPurchasePanel();
        });

        if (ShopManager.Instance != null)
            ShopManager.Instance.OnCoinBalanceChanged += UpdateCoinBalance;

        if (IapManager.Instance != null)
        {
            IapManager.Instance.OnStoreReady           += OnIapStoreReady;
            IapManager.Instance.OnStoreFailed          += OnIapStoreFailed;
            IapManager.Instance.OnCoinPurchaseCompleted += OnCoinPurchaseCompleted;
            IapManager.Instance.StorePurchaseFailed     += OnStorePurchaseFailed;
        }
    }

    // ── Category ──────────────────────────────────────────────────────────────

    private void SelectCategory(Category category)
    {
        _currentCategory = category;

        SetTabActive(_tabPremium,   category == Category.Premium);
        SetTabActive(_tabAvatar,    category == Category.Avatar);
        SetTabActive(_tabAccessory, category == Category.Accessory);
        SetTabActive(_tabObject,    category == Category.Object);
        SetTabActive(_tabStamp,     category == Category.Stamp);

        bool isPremiumTab = category == Category.Premium;
        _searchFilterBar.style.display = isPremiumTab ? DisplayStyle.None : DisplayStyle.Flex;
        _objectFilterRow.style.display = category == Category.Object ? DisplayStyle.Flex : DisplayStyle.None;
        _premiumScroll.style.display   = isPremiumTab ? DisplayStyle.Flex : DisplayStyle.None;
        _productScroll.style.display   = isPremiumTab ? DisplayStyle.None : DisplayStyle.Flex;
        _emptyArea.style.display       = DisplayStyle.None;

        if (isPremiumTab)
            RefreshPremiumTab();
        else
            ReloadProducts();
    }

    private static void SetTabActive(Button btn, bool active)
    {
        if (active)
            btn.AddToClassList("category-tab--active");
        else
            btn.RemoveFromClassList("category-tab--active");
    }

    // ── Sort ──────────────────────────────────────────────────────────────────

    private void SelectSort(SortMode mode)
    {
        _currentSort = mode;
        SetSortActive(_sortPopularity, mode == SortMode.Popularity);
        SetSortActive(_sortLikes,      mode == SortMode.Likes);
        SetSortActive(_sortNewest,     mode == SortMode.Newest);
        SetSortActive(_sortOldest,     mode == SortMode.Oldest);
        ReloadProducts();
    }

    private static void SetSortActive(Button btn, bool active)
    {
        if (active)
            btn.AddToClassList("sort-btn--active");
        else
            btn.RemoveFromClassList("sort-btn--active");
    }

    // ── Collider filter (object tab only) ─────────────────────────────────────

    private void SelectColliderFilter(string size)
    {
        _currentColliderFilter = size;
        SetFilterActive(_filterSizeAll,    size == null);
        SetFilterActive(_filterSizeSmall,  size == "small");
        SetFilterActive(_filterSizeMedium, size == "medium");
        SetFilterActive(_filterSizeLarge,  size == "large");
        ReloadProducts();
    }

    // ── Texture cost filter (object tab only) ─────────────────────────────────

    private void SelectTextureCostFilter(int? min, int? max)
    {
        _currentTextureCostMin = min;
        _currentTextureCostMax = max;
        SetFilterActive(_filterCostAll,  min == null && max == null);
        SetFilterActive(_filterCostLow,  min == null && max == 4);
        SetFilterActive(_filterCostMid,  min == 16 && max == 64);
        SetFilterActive(_filterCostHigh, min == 256 && max == null);
        ReloadProducts();
    }

    private static void SetFilterActive(Button btn, bool active)
    {
        if (active)
            btn.AddToClassList("filter-btn--active");
        else
            btn.RemoveFromClassList("filter-btn--active");
    }

    // ── Coin balance ──────────────────────────────────────────────────────────

    private void UpdateCoinBalance()
    {
        if (ShopManager.Instance == null) return;
        int balance = ShopManager.Instance.CoinBalance;
        _coinBalance.text = balance.ToString("N0");

        if (balance < 0)
            _coinBalance.AddToClassList("coin-balance--negative");
        else
            _coinBalance.RemoveFromClassList("coin-balance--negative");

        if (_coinDetailVisible)
            RefreshCoinDetailPanel();

        RefreshAllPurchaseButtons();
    }

    // ── Error toast ───────────────────────────────────────────────────────────

    private void ShowErrorToast(string message)
    {
        _errorToastLabel.text = message;
        _errorToast.style.display = DisplayStyle.Flex;
        _toastSchedule?.Pause();
        _toastSchedule = _errorToast.schedule
            .Execute(() => _errorToast.style.display = DisplayStyle.None)
            .StartingIn(3000);
    }

    // ── Coin detail panel ────────────────────────────────────────────────────

    private void ShowCoinDetail()
    {
        _coinDetailVisible = true;
        RefreshCoinDetailPanel();
        _coinDetailBackdrop.style.display = DisplayStyle.Flex;
    }

    private void HideCoinDetail()
    {
        _coinDetailVisible = false;
        _coinDetailBackdrop.style.display = DisplayStyle.None;
    }

    private void RefreshCoinDetailPanel()
    {
        if (ShopManager.Instance == null) return;

        var now = DateTime.UtcNow;
        int balance = ShopManager.Instance.CoinBalance;

        _coinDetailBalance.text = balance.ToString("N0");

        bool isNegative = balance < 0;
        if (isNegative)
            _coinDetailBalance.AddToClassList("coin-detail-balance--negative");
        else
            _coinDetailBalance.RemoveFromClassList("coin-detail-balance--negative");

        _coinDetailWarning.style.display = isNegative ? DisplayStyle.Flex : DisplayStyle.None;

        // ロット一覧（有効なもののみ・有効期限が近い順）
        _coinLotList.Clear();
        var lots = new List<CoinLedger.CoinLot>(ShopManager.Instance.Ledger.Lots);
        lots.RemoveAll(l => l.ValidUntil <= now);
        lots.Sort((a, b) => a.ValidUntil.CompareTo(b.ValidUntil));

        bool hasLots = lots.Count > 0;
        _coinLotEmpty.style.display = hasLots ? DisplayStyle.None : DisplayStyle.Flex;

        foreach (var lot in lots)
        {
            var row = new VisualElement();
            row.AddToClassList("coin-lot-row");

            var expiryLabel = new Label
            {
                text = $"有効期限: {lot.ValidUntil.ToLocalTime():yyyy/MM/dd}",
            };
            expiryLabel.AddToClassList("coin-lot-expiry");

            // 7日以内に期限切れになるロットを強調
            if ((lot.ValidUntil - now).TotalDays <= 7)
                expiryLabel.AddToClassList("coin-lot-expiry--soon");

            var amountLabel = new Label { text = $"🪙 {lot.Coins:N0}" };
            amountLabel.AddToClassList("coin-lot-amount");

            row.Add(expiryLabel);
            row.Add(amountLabel);
            _coinLotList.Add(row);
        }
    }

    private void OnBuyCoinsClicked()
    {
        HideCoinDetail();
        ShowCoinPurchasePanel();
    }

    // ── Coin purchase panel ───────────────────────────────────────────────────

    private void ShowCoinPurchasePanel()
    {
        _coinPurchaseVisible = true;
        PopulateCoinPackList();
        _coinPurchaseBackdrop.style.display = DisplayStyle.Flex;
    }

    private void HideCoinPurchasePanel()
    {
        _coinPurchaseVisible = false;
        _coinPurchaseBackdrop.style.display = DisplayStyle.None;
    }

    private void PopulateCoinPackList()
    {
        _coinPackList.Clear();
        _coinPackRows.Clear();

        bool iapFailed = IapManager.Instance?.IsInitializeFailed ?? false;

        foreach (var productId in IapProductIds.AllCoinProductIds)
        {
            int coins = IapProductIds.CoinsForProductId(productId);
            string price = IapManager.Instance?.GetLocalizedPrice(productId);

            var row = new VisualElement();
            row.AddToClassList("coin-pack-row");

            var coinLabel = new Label { text = $"🪙 {coins:N0}" };
            coinLabel.AddToClassList("coin-pack-label");

            var priceLabel = new Label
            {
                text = price ?? (iapFailed ? "価格を取得できませんでした" : "取得中..."),
            };
            priceLabel.AddToClassList("coin-pack-price");

            var buyBtn = new Button { text = "購入" };
            buyBtn.AddToClassList("coin-pack-buy-btn");
            buyBtn.SetEnabled(price != null);

            string captured = productId;
            buyBtn.clicked += () => OnCoinPackBuyClicked(captured);

            row.Add(coinLabel);
            row.Add(priceLabel);
            row.Add(buyBtn);
            _coinPackList.Add(row);
            _coinPackRows.Add((productId, priceLabel, buyBtn));
        }
    }

    private void RefreshCoinPackPrices()
    {
        bool iapFailed = IapManager.Instance?.IsInitializeFailed ?? false;
        foreach (var (productId, priceLabel, buyBtn) in _coinPackRows)
        {
            string price = IapManager.Instance?.GetLocalizedPrice(productId);
            priceLabel.text = price ?? (iapFailed ? "価格を取得できませんでした" : "取得中...");
            buyBtn.SetEnabled(price != null);
        }
    }

    private void OnCoinPackBuyClicked(string productId)
    {
        if (IapManager.Instance == null || !IapManager.Instance.IsInitialized) return;

        // 購入中は全ボタンを無効化（重複タップ防止）
        foreach (var (_, _, btn) in _coinPackRows)
            btn.SetEnabled(false);

        IapManager.Instance.BuyProduct(productId);
    }

    private void OnIapStoreReady()
    {
        if (_coinPurchaseVisible)
            RefreshCoinPackPrices();
        if (_currentCategory == Category.Premium)
            RefreshPremiumTab();
    }

    private void OnIapStoreFailed()
    {
        if (_coinPurchaseVisible)
            RefreshCoinPackPrices();
        if (_currentCategory == Category.Premium)
            RefreshPremiumTab();
    }

    private void OnCoinPurchaseCompleted(int coins)
    {
        HideCoinPurchasePanel();
        // コイン残高は ShopManager.OnCoinBalanceChanged 経由で自動更新される
    }

    private void OnStorePurchaseFailed(string productId)
    {
        // 購入失敗時はボタンを再有効化
        RefreshCoinPackPrices();
    }

    // ── Premium tab ───────────────────────────────────────────────────────────

    private void RefreshPremiumTab()
    {
        bool isPremium = IsPremiumUser();
        _currentPlanArea.style.display = isPremium ? DisplayStyle.Flex : DisplayStyle.None;

        if (isPremium)
        {
            _currentPlanLabel.text = "プレミアムプラン 加入中";
            _nextRenewalLabel.text = "";
        }

        bool iapReady  = IapManager.Instance?.IsInitialized ?? false;
        bool iapFailed = IapManager.Instance?.IsInitializeFailed ?? false;
        string fallback = iapFailed ? "価格を取得できませんでした" : "価格を取得中...";

        _planAnnualPrice.text  = (iapReady
            ? IapManager.Instance.GetLocalizedPrice(IapProductIds.PremiumYearly)
            : null) ?? fallback;

        _planMonthlyPrice.text = (iapReady
            ? IapManager.Instance.GetLocalizedPrice(IapProductIds.PremiumMonthly)
            : null) ?? fallback;

        _planAnnualMonthly.text = "";

        // サブスクリプション購入フローは Phase 10 で配線
        _btnBuyAnnual.SetEnabled(false);
        _btnBuyMonthly.SetEnabled(false);
    }

    private void OnBuyAnnualClicked()
    {
        // Unity IAP 実装後に購入フローを接続する（Phase 8 後続タスク）
        Debug.Log("[ShopTabController] Annual plan purchase: Unity IAP not yet initialized.");
    }

    private void OnBuyMonthlyClicked()
    {
        Debug.Log("[ShopTabController] Monthly plan purchase: Unity IAP not yet initialized.");
    }

    // ── Product loading ───────────────────────────────────────────────────────

    private void ReloadProducts()
    {
        _cts.Cancel();
        _cts.Dispose();
        _cts = new CancellationTokenSource();

        _loadedProducts.Clear();
        _purchaseBtns.Clear();
        _productList.Clear();
        _cursor = null;
        _hasMore = false;
        _isLoading = false;
        _loadMoreArea.style.display = DisplayStyle.None;
        _emptyArea.style.display    = DisplayStyle.None;

        _ = LoadNextPageAsync(_cts.Token);
    }

    private async Task LoadNextPageAsync(CancellationToken ct)
    {
        if (_isLoading || ShopManager.Instance == null) return;
        _isLoading = true;
        _loadMoreArea.style.display = DisplayStyle.Flex;

        try
        {
            string categoryParam = _currentCategory switch
            {
                Category.Avatar    => "avatar",
                Category.Accessory => "accessory",
                Category.Object    => "world_object",
                Category.Stamp     => "stamp",
                _                  => null,
            };
            string sortParam = _currentSort switch
            {
                SortMode.Likes   => "likes",
                SortMode.Newest  => "newest",
                SortMode.Oldest  => "oldest",
                _                => "popularity",
            };
            string search = string.IsNullOrWhiteSpace(_searchText) ? null : _searchText.Trim();

            bool isObjectTab = _currentCategory == Category.Object;
            var (res, err) = await ShopManager.Instance.FetchProductsAsync(
                category: categoryParam,
                sort: sortParam,
                search: search,
                after: _cursor,
                colliderSizeCategory: isObjectTab ? _currentColliderFilter : null,
                textureCostMin: isObjectTab ? _currentTextureCostMin : null,
                textureCostMax: isObjectTab ? _currentTextureCostMax : null,
                ct: ct);

            if (ct.IsCancellationRequested) return;

            if (err != null)
            {
                Debug.LogWarning($"[ShopTabController] FetchProducts failed: {err}");
                return;
            }

            var items = res.products ?? new List<ShopProductResponse>();

            foreach (var product in items)
            {
                _loadedProducts.Add(product);
                _productList.Add(CreateProductCard(product));
            }

            _cursor  = res.cursor?.next;
            _hasMore = res.cursor?.hasMore ?? false;

            _emptyArea.style.display = _loadedProducts.Count == 0
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }
        catch (OperationCanceledException) { }
        catch (Exception e)
        {
            Debug.LogError($"[ShopTabController] LoadNextPage error: {e}");
        }
        finally
        {
            _isLoading = false;
            _loadMoreArea.style.display = DisplayStyle.None;
        }
    }

    // ── Infinite scroll ───────────────────────────────────────────────────────

    private void OnScrollChanged(float value)
    {
        if (!_hasMore || _isLoading) return;
        var scroller = _productScroll.verticalScroller;
        if (scroller.highValue <= 0) return;
        if (value >= scroller.highValue * 0.85f)
            _ = LoadNextPageAsync(_cts.Token);
    }

    // ── Product card ──────────────────────────────────────────────────────────

    private VisualElement CreateProductCard(ShopProductResponse product)
    {
        var card = new VisualElement();
        card.AddToClassList("product-card");

        var thumb = new VisualElement();
        thumb.AddToClassList("product-card__thumb");
        card.Add(thumb);

        var info = new VisualElement();
        info.AddToClassList("product-card__info");

        var nameRow = new VisualElement();
        nameRow.AddToClassList("product-card__name-row");

        var nameLabel = new Label { text = product.name ?? "" };
        nameLabel.AddToClassList("product-card__name");
        nameRow.Add(nameLabel);

        // Edit OK/NG バッジ（スタンプ以外のカテゴリに表示）
        if (!string.Equals(product.category, "stamp", StringComparison.OrdinalIgnoreCase))
        {
            var editBadge = new Label { text = product.editAllowed ? "編集可" : "編集不可" };
            editBadge.AddToClassList("product-card__edit-badge");
            editBadge.AddToClassList(
                product.editAllowed
                    ? "product-card__edit-badge--ok"
                    : "product-card__edit-badge--ng"
            );
            nameRow.Add(editBadge);
        }

        info.Add(nameRow);

        var meta = new VisualElement();
        meta.AddToClassList("product-card__meta");

        var price = new Label { text = $"🪙 {product.priceCoins:N0}" };
        price.AddToClassList("product-card__price");
        meta.Add(price);

        // いいね数 + いいね/いいね解除ボタン
        var likeState = new LikeState
        {
            LikedByMe  = product.likedByMe,
            LikesCount = product.likesCount,
        };

        var likesLabel = new Label();
        likesLabel.AddToClassList("product-card__likes");

        var likeBtn = new Button();
        likeBtn.AddToClassList("product-card__like-btn");

        RefreshLikeUI(likesLabel, likeBtn, likeState);

        likeBtn.clicked += () => OnLikeBtnClicked(product.id, likeState, likesLabel, likeBtn);

        meta.Add(likesLabel);
        meta.Add(likeBtn);

        info.Add(meta);

        // 購入ボタン / 購入済みバッジ
        var purchaseRow = new VisualElement();
        purchaseRow.AddToClassList("product-card__purchase-row");

        var purchaseBtn = new Button();
        purchaseBtn.AddToClassList("product-card__purchase-btn");
        SetPurchaseBtnState(purchaseBtn, product.purchasedByMe);
        if (!product.purchasedByMe)
        {
            bool isNeg = ShopManager.Instance != null && ShopManager.Instance.CoinBalance < 0;
            SetPurchaseBtnBalanceNegative(purchaseBtn, isNeg);
        }
        purchaseBtn.clicked += () => OnPurchaseBtnClicked(product, purchaseBtn);
        _purchaseBtns.Add((product, purchaseBtn));

        purchaseRow.Add(purchaseBtn);
        info.Add(purchaseRow);

        card.Add(info);

        return card;
    }

    private static void SetPurchaseBtnState(Button btn, bool purchased)
    {
        if (purchased)
        {
            btn.text = "購入済み";
            btn.AddToClassList("product-card__purchase-btn--purchased");
            btn.RemoveFromClassList("product-card__purchase-btn--balance-negative");
            btn.SetEnabled(false);
        }
        else
        {
            btn.text = "購入する";
            btn.RemoveFromClassList("product-card__purchase-btn--purchased");
            btn.SetEnabled(true);
        }
    }

    private static void SetPurchaseBtnBalanceNegative(Button btn, bool isNegative)
    {
        if (isNegative)
            btn.AddToClassList("product-card__purchase-btn--balance-negative");
        else
            btn.RemoveFromClassList("product-card__purchase-btn--balance-negative");
    }

    private void RefreshAllPurchaseButtons()
    {
        bool isNegative = ShopManager.Instance != null && ShopManager.Instance.CoinBalance < 0;
        foreach (var (product, btn) in _purchaseBtns)
        {
            if (!product.purchasedByMe)
                SetPurchaseBtnBalanceNegative(btn, isNegative);
        }
    }

    private async void OnPurchaseBtnClicked(ShopProductResponse product, Button purchaseBtn)
    {
        if (ShopManager.Instance == null) return;

        if (ShopManager.Instance.CoinBalance < 0)
        {
            ShowErrorToast("コイン残高が不足しています。コインを購入して残高を回復してください。");
            return;
        }

        purchaseBtn.SetEnabled(false);
        purchaseBtn.RemoveFromClassList("product-card__purchase-btn--balance-negative");
        purchaseBtn.text = "処理中...";

        try
        {
            string err = await ShopManager.Instance.PurchaseProductAsync(product, ct: _cts.Token);

            if (err != null)
            {
                SetPurchaseBtnState(purchaseBtn, purchased: false);
                bool isNeg = ShopManager.Instance != null && ShopManager.Instance.CoinBalance < 0;
                SetPurchaseBtnBalanceNegative(purchaseBtn, isNeg);
                ShowErrorToast(PurchaseErrorMessage(err));
                Debug.LogWarning($"[ShopTabController] Purchase failed: {err}");
            }
            else
            {
                product.purchasedByMe = true;
                SetPurchaseBtnState(purchaseBtn, purchased: true);
            }
        }
        catch (OperationCanceledException) { }
    }

    private static string PurchaseErrorMessage(string errorCode) => errorCode switch
    {
        "insufficient_coins" => "コインが不足しています。",
        "already_purchased"  => "この商品は購入済みです。",
        _                    => "購入に失敗しました。時間をおいて再試行してください。",
    };

    private static void RefreshLikeUI(Label likesLabel, Button likeBtn, LikeState state)
    {
        likesLabel.text = $"♥ {state.LikesCount}";
        if (state.LikedByMe)
        {
            likeBtn.text = "♥";
            likeBtn.AddToClassList("product-card__like-btn--active");
        }
        else
        {
            likeBtn.text = "♡";
            likeBtn.RemoveFromClassList("product-card__like-btn--active");
        }
    }

    private async void OnLikeBtnClicked(string productId, LikeState state, Label likesLabel, Button likeBtn)
    {
        if (ShopManager.Instance == null) return;

        bool wasLiked = state.LikedByMe;

        // 楽観的 UI 更新
        state.LikedByMe  = !wasLiked;
        state.LikesCount += wasLiked ? -1 : 1;
        RefreshLikeUI(likesLabel, likeBtn, state);
        likeBtn.SetEnabled(false);

        try
        {
            string err = wasLiked
                ? await ShopManager.Instance.UnlikeProductAsync(productId, _cts.Token)
                : await ShopManager.Instance.LikeProductAsync(productId, _cts.Token);

            if (err != null)
            {
                // API エラー時は元の状態に戻す
                state.LikedByMe  = wasLiked;
                state.LikesCount += wasLiked ? 1 : -1;
                RefreshLikeUI(likesLabel, likeBtn, state);
                Debug.LogWarning($"[ShopTabController] Like toggle failed: {err}");
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            likeBtn.SetEnabled(true);
        }
    }

    private sealed class LikeState
    {
        public bool LikedByMe;
        public int  LikesCount;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static bool IsPremiumUser()
    {
        var profile = UserManager.Instance?.Profile;
        return profile != null &&
               string.Equals(profile.subscriptionTier, "premium",
                   StringComparison.OrdinalIgnoreCase);
    }

    // ── IDisposable ───────────────────────────────────────────────────────────

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
        _toastSchedule?.Pause();
        _purchaseBtns.Clear();
        _coinPackRows.Clear();

        _productScroll.verticalScroller.valueChanged -= OnScrollChanged;

        if (ShopManager.Instance != null)
            ShopManager.Instance.OnCoinBalanceChanged -= UpdateCoinBalance;

        if (IapManager.Instance != null)
        {
            IapManager.Instance.OnStoreReady            -= OnIapStoreReady;
            IapManager.Instance.OnStoreFailed           -= OnIapStoreFailed;
            IapManager.Instance.OnCoinPurchaseCompleted -= OnCoinPurchaseCompleted;
            IapManager.Instance.StorePurchaseFailed      -= OnStorePurchaseFailed;
        }
    }
}
