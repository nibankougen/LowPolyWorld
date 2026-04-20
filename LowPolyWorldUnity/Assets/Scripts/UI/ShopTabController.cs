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

    // ── State ─────────────────────────────────────────────────────────────────

    private Category _currentCategory;
    private SortMode _currentSort = SortMode.Popularity;
    private string _currentColliderFilter = null; // null = all
    private string _searchText = "";
    private string _cursor = null;
    private bool _hasMore = false;
    private bool _isLoading = false;
    private readonly List<ShopProductResponse> _loadedProducts = new();

    private CancellationTokenSource _cts = new();

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

        if (ShopManager.Instance != null)
            ShopManager.Instance.OnCoinBalanceChanged += UpdateCoinBalance;
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
    }

    // ── Coin detail panel ────────────────────────────────────────────────────

    private void ShowCoinDetail()
    {
        RefreshCoinDetailPanel();
        _coinDetailBackdrop.style.display = DisplayStyle.Flex;
    }

    private void HideCoinDetail()
    {
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
        // コイン購入画面への遷移（Unity IAP 実装後に接続）
        Debug.Log("[ShopTabController] Buy coins: Unity IAP not yet initialized.");
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

        // 価格は Unity IAP 実装後に設定する（Phase 8 後続タスク）
        _planAnnualPrice.text   = "価格を取得できませんでした";
        _planMonthlyPrice.text  = "価格を取得できませんでした";
        _planAnnualMonthly.text = "";
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

            // コライダーフィルターはサーバー側クエリパラメータで絞り込む
            // （ShopManager が collider_size_category パラメータを追加する必要があるが、
            //  現バージョンは FetchProductsAsync がまだ対応していないため、取得後クライアント側で絞る）
            var (res, err) = await ShopManager.Instance.FetchProductsAsync(
                category: categoryParam,
                sort: sortParam,
                search: search,
                after: _cursor,
                ct: ct);

            if (ct.IsCancellationRequested) return;

            if (err != null)
            {
                Debug.LogWarning($"[ShopTabController] FetchProducts failed: {err}");
                return;
            }

            var items = res.products ?? new List<ShopProductResponse>();

            // オブジェクトタブのコライダーサイズフィルタリング（クライアント側）
            if (_currentCategory == Category.Object && _currentColliderFilter != null)
            {
                items = items.FindAll(p =>
                    string.Equals(p.colliderSizeCategory, _currentColliderFilter,
                        StringComparison.OrdinalIgnoreCase));
            }

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

    private static VisualElement CreateProductCard(ShopProductResponse product)
    {
        var card = new VisualElement();
        card.AddToClassList("product-card");

        var thumb = new VisualElement();
        thumb.AddToClassList("product-card__thumb");
        card.Add(thumb);

        var info = new VisualElement();
        info.AddToClassList("product-card__info");

        var name = new Label { text = product.name ?? "" };
        name.AddToClassList("product-card__name");
        info.Add(name);

        var meta = new VisualElement();
        meta.AddToClassList("product-card__meta");

        var price = new Label { text = $"🪙 {product.priceCoins:N0}" };
        price.AddToClassList("product-card__price");
        meta.Add(price);

        var likes = new Label { text = $"♥ {product.likesCount}" };
        likes.AddToClassList("product-card__likes");
        meta.Add(likes);

        info.Add(meta);
        card.Add(info);

        return card;
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

        _productScroll.verticalScroller.valueChanged -= OnScrollChanged;

        if (ShopManager.Instance != null)
            ShopManager.Instance.OnCoinBalanceChanged -= UpdateCoinBalance;
    }
}
