using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// ワールド一覧タブ（4サブタブ）を管理するコントローラー。
/// WorldTab.uxml をインスタンス化した VisualElement を受け取り初期化する。
/// </summary>
public class WorldListController
{
    private enum Tab { Home, Following, New, Liked }

    private readonly VisualElement _root;
    private readonly ApiClient _api;

    private VisualElement _worldList;
    private VisualElement _loadMoreArea;
    private VisualElement _emptyArea;
    private VisualElement _searchBar;

    private Button _tabHome;
    private Button _tabFollowing;
    private Button _tabNew;
    private Button _tabLiked;
    private TextField _searchInput;

    private Tab _activeTab = Tab.Home;
    private string _cursor;
    private bool _hasMore;
    private bool _isLoading;
    private int _loadGeneration; // incremented on each SelectTab to detect stale task completions

    private CancellationTokenSource _cts;
    private readonly List<Texture2D> _thumbTextures = new List<Texture2D>();

    // Called when user selects a world card
    public event Action<WorldResponse> OnWorldSelected;

    public WorldListController(VisualElement root, ApiClient api)
    {
        _root = root;
        _api = api;
        BindElements();
    }

    private void BindElements()
    {
        _worldList = _root.Q<VisualElement>("world-list");
        _loadMoreArea = _root.Q<VisualElement>("load-more-area");
        _emptyArea = _root.Q<VisualElement>("empty-area");
        _searchBar = _root.Q<VisualElement>("search-bar");
        _searchInput = _root.Q<TextField>("search-input");

        _tabHome = _root.Q<Button>("tab-home");
        _tabFollowing = _root.Q<Button>("tab-following");
        _tabNew = _root.Q<Button>("tab-new");
        _tabLiked = _root.Q<Button>("tab-liked");

        _tabHome.clicked += () => SelectTab(Tab.Home);
        _tabFollowing.clicked += () => SelectTab(Tab.Following);
        _tabNew.clicked += () => SelectTab(Tab.New);
        _tabLiked.clicked += () => SelectTab(Tab.Liked);

        var scroll = _root.Q<ScrollView>("world-scroll");
        scroll.verticalScroller.valueChanged += OnScrollChanged;

        _searchInput?.RegisterValueChangedCallback(_ => OnSearchChanged());
    }

    public void Initialize()
    {
        SelectTab(Tab.Home);
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        DestroyThumbTextures();
    }

    private void DestroyThumbTextures()
    {
        foreach (var tex in _thumbTextures)
            if (tex != null)
                UnityEngine.Object.Destroy(tex);
        _thumbTextures.Clear();
    }

    // ── Tab switching ─────────────────────────────────────────────────────────

    private void SelectTab(Tab tab)
    {
        _activeTab = tab;
        _cursor = null;
        _hasMore = false;
        _isLoading = false;
        _loadGeneration++;

        // Update active class
        foreach (var btn in new[] { _tabHome, _tabFollowing, _tabNew, _tabLiked })
            btn.RemoveFromClassList("sub-tab--active");

        var activeBtn = tab switch
        {
            Tab.Home => _tabHome,
            Tab.Following => _tabFollowing,
            Tab.New => _tabNew,
            Tab.Liked => _tabLiked,
            _ => _tabHome,
        };
        activeBtn.AddToClassList("sub-tab--active");

        // Show search bar only on home tab
        if (_searchBar != null)
            _searchBar.style.display = tab == Tab.Home ? DisplayStyle.Flex : DisplayStyle.None;

        _worldList.Clear();
        DestroyThumbTextures();

        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        _ = LoadNextPageAsync(_cts.Token, _loadGeneration);
    }

    // ── Infinite scroll ───────────────────────────────────────────────────────

    private void OnScrollChanged(float value)
    {
        var scroll = _root.Q<ScrollView>("world-scroll");
        if (scroll == null) return;

        var contentHeight = scroll.contentContainer.resolvedStyle.height;
        var viewportHeight = scroll.resolvedStyle.height;
        var scrollMax = contentHeight - viewportHeight;

        if (!_isLoading && _hasMore && value >= scrollMax - 100)
            _ = LoadNextPageAsync(_cts?.Token ?? CancellationToken.None, _loadGeneration);
    }

    private void OnSearchChanged()
    {
        _cursor = null;
        _worldList.Clear();
        DestroyThumbTextures();
        _isLoading = false;
        _loadGeneration++;
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        _ = LoadNextPageAsync(_cts.Token, _loadGeneration);
    }

    // ── Data loading ──────────────────────────────────────────────────────────

    // generation is captured at task start; if it differs from _loadGeneration when the
    // task resumes, the tab has switched and we discard the result without touching _isLoading.
    private async Task LoadNextPageAsync(CancellationToken ct, int generation)
    {
        if (_isLoading) return;
        _isLoading = true;
        _loadMoreArea.style.display = DisplayStyle.Flex;

        try
        {
            var path = BuildPath();
            var (result, error) = await _api.GetAsync<WorldListResponse>(path, ct);

            if (generation != _loadGeneration) return; // stale result — tab was switched
            if (ct.IsCancellationRequested) return;

            if (error != null || result == null)
            {
                _emptyArea.style.display = _worldList.childCount == 0 ? DisplayStyle.Flex : DisplayStyle.None;
                return;
            }

            var worlds = result.data ?? new List<WorldResponse>();
            foreach (var world in worlds)
                _worldList.Add(BuildWorldCard(world));

            _cursor = result.cursor?.next;
            _hasMore = result.cursor?.hasMore ?? false;

            _emptyArea.style.display = _worldList.childCount == 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }
        finally
        {
            if (generation == _loadGeneration)
            {
                _isLoading = false;
                _loadMoreArea.style.display = DisplayStyle.None;
            }
        }
    }

    private string BuildPath()
    {
        var query = _cursor != null ? $"?after={Uri.EscapeDataString(_cursor)}&limit=20" : "?limit=20";
        return _activeTab switch
        {
            Tab.Following => "/api/v1/worlds/following" + query,
            Tab.New => "/api/v1/worlds/new" + query,
            Tab.Liked => "/api/v1/worlds/liked" + query,
            _ => "/api/v1/worlds/new" + query, // Home: use new as fallback until Phase 14
        };
    }

    // ── Card builder ──────────────────────────────────────────────────────────

    private VisualElement BuildWorldCard(WorldResponse world)
    {
        var card = new VisualElement();
        card.AddToClassList("world-card");

        var thumb = new VisualElement();
        thumb.AddToClassList("world-card__thumb");
        card.Add(thumb);

        var info = new VisualElement();
        info.AddToClassList("world-card__info");
        card.Add(info);

        var nameLabel = new Label(world.name);
        nameLabel.AddToClassList("world-card__name");
        info.Add(nameLabel);

        var meta = new VisualElement();
        meta.AddToClassList("world-card__meta");
        info.Add(meta);

        var players = new Label($"▶ {world.maxPlayers}人");
        players.AddToClassList("world-card__players");
        meta.Add(players);

        var likes = new Label($"♥ {world.likesCount}");
        likes.AddToClassList("world-card__likes");
        meta.Add(likes);

        card.RegisterCallback<ClickEvent>(_ => OnWorldSelected?.Invoke(world));

        // Async thumbnail load — uses controller CTS so it cancels on tab switch / dispose
        if (!string.IsNullOrEmpty(world.thumbnailUrl))
            _ = LoadThumbnailAsync(world.thumbnailUrl, thumb, _cts?.Token ?? CancellationToken.None);

        return card;
    }

    private async Task LoadThumbnailAsync(string url, VisualElement target, CancellationToken ct)
    {
        var client = new ApiClient(""); // GetBytesAsync uses the url param directly (full URL)
        var (data, _) = await client.GetBytesAsync(url, ct);
        if (ct.IsCancellationRequested || data == null || data.Length == 0) return;

        var tex = new Texture2D(2, 2);
        if (tex.LoadImage(data))
        {
            _thumbTextures.Add(tex);
            target.style.backgroundImage = new StyleBackground(tex);
        }
        else
        {
            UnityEngine.Object.Destroy(tex);
        }
    }
}
