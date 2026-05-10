using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

/// <summary>
/// HomeScene のナビゲーションバーとタブ切り替えを管理する MonoBehaviour。
/// ワールド一覧 → ワールド詳細 → その他ルーム の画面遷移スタックを管理する。
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class HomeScreenController : MonoBehaviour
{
    [Header("Tab Content (UXML)")]
    [SerializeField] private VisualTreeAsset _worldTabAsset;
    [SerializeField] private VisualTreeAsset _avatarTabAsset;
    [SerializeField] private VisualTreeAsset _worldManageTabAsset;
    [SerializeField] private VisualTreeAsset _shopTabAsset;
    [SerializeField] private VisualTreeAsset _settingsTabAsset;
    [SerializeField] private VisualTreeAsset _worldDetailAsset;
    [SerializeField] private VisualTreeAsset _roomListAsset;

    [Header("Social Screens (UXML)")]
    [SerializeField] private VisualTreeAsset _friendScreenAsset;
    [SerializeField] private VisualTreeAsset _followScreenAsset;
    [SerializeField] private VisualTreeAsset _userInfoPanelAsset;

    private UIDocument _document;
    private VisualElement _contentArea;

    private Button _navWorld;
    private Button _navAvatar;
    private Button _navWorldManage;
    private Button _navShop;
    private Button _navSettings;

    private Button _activeTab;
    private SettingsTabController _settingsTabController;
    private WorldListController _worldListController;
    private WorldDetailController _worldDetailController;
    private RoomListController _roomListController;
    private ShopTabController _shopTabController;
    private FriendScreenController _friendScreenController;
    private FollowScreenController _followScreenController;
    private UserInfoPanelController _userInfoController;

    // ユーザー情報パネルオーバーレイ
    private VisualElement _userInfoOverlay;

    private WorldResponse _currentWorld;

    private void Awake()
    {
        _document = GetComponent<UIDocument>();
    }

    private void OnEnable()
    {
        var root = _document.rootVisualElement;
        _contentArea = root.Q<VisualElement>("content-area");

        _navWorld = root.Q<Button>("nav-world");
        _navAvatar = root.Q<Button>("nav-avatar");
        _navWorldManage = root.Q<Button>("nav-world-manage");
        _navShop = root.Q<Button>("nav-shop");
        _navSettings = root.Q<Button>("nav-settings");

        _navWorld.clicked += () => SelectTab(_navWorld, _worldTabAsset);
        _navAvatar.clicked += () => SelectTab(_navAvatar, _avatarTabAsset);
        _navWorldManage.clicked += () => SelectTab(_navWorldManage, _worldManageTabAsset);
        _navShop.clicked += () => SelectTab(_navShop, _shopTabAsset);
        _navSettings.clicked += () => SelectTab(_navSettings, _settingsTabAsset);

        // ユーザー情報パネル（フラッシュメッセージ + オーバーレイ）
        var flashRoot = root.Q<VisualElement>("flash-root");
        if (flashRoot != null)
            _ = new FlashMessageController(flashRoot);

        if (_userInfoPanelAsset != null)
        {
            _userInfoOverlay = _userInfoPanelAsset.Instantiate();
            _userInfoOverlay.style.position = Position.Absolute;
            _userInfoOverlay.style.top = 0;
            _userInfoOverlay.style.left = 0;
            _userInfoOverlay.style.right = 0;
            _userInfoOverlay.style.bottom = 0;
            root.Add(_userInfoOverlay);
            _userInfoController = new UserInfoPanelController(_userInfoOverlay.Q<VisualElement>("uip-backdrop"));
            _userInfoController.OnFollowersRequested += userId => ShowFollowScreen(userId, followersTab: true);
            _userInfoController.OnFollowingRequested += userId => ShowFollowScreen(userId, followersTab: false);
        }

        SelectTab(_navWorld, _worldTabAsset);
    }

    private void OnDisable()
    {
        DisposeAllControllers();
        _userInfoController?.Dispose();
        _userInfoController = null;
    }

    private void DisposeAllControllers()
    {
        _worldListController?.Dispose();
        _worldListController = null;
        _worldDetailController?.Dispose();
        _worldDetailController = null;
        _roomListController?.Dispose();
        _roomListController = null;
        _shopTabController?.Dispose();
        _shopTabController = null;
        _friendScreenController?.Dispose();
        _friendScreenController = null;
        _followScreenController?.Dispose();
        _followScreenController = null;
        _settingsTabController = null;
    }

    // ── Tab switching ─────────────────────────────────────────────────────────

    private void SelectTab(Button tab, VisualTreeAsset contentAsset)
    {
        if (_activeTab != null)
            _activeTab.RemoveFromClassList("nav-tab--active");

        _activeTab = tab;
        _activeTab.AddToClassList("nav-tab--active");

        DisposeAllControllers();
        _contentArea.Clear();

        if (contentAsset == null)
            return;

        var content = contentAsset.Instantiate();
        content.style.flexGrow = 1;
        _contentArea.Add(content);

        if (tab == _navSettings)
        {
            _settingsTabController = new SettingsTabController(content);
            _settingsTabController.Bind(AudioManager.Instance?.Settings);
            _settingsTabController.OnFriendScreenRequested += ShowFriendScreen;
            _settingsTabController.OnFollowScreenRequested += () => ShowFollowScreen(null);
        }
        else if (tab == _navWorld && UserManager.Instance != null)
        {
            _worldListController = new WorldListController(content, UserManager.Instance.Api);
            _worldListController.OnWorldSelected += ShowWorldDetail;
            _worldListController.Initialize();
        }
        else if (tab == _navShop)
        {
            _shopTabController = new ShopTabController(content);
        }
    }

    // ── World detail ──────────────────────────────────────────────────────────

    private void ShowWorldDetail(WorldResponse world)
    {
        if (_worldDetailAsset == null) return;

        _currentWorld = world;

        _worldListController?.Dispose();
        _worldListController = null;
        _worldDetailController?.Dispose();
        _worldDetailController = null;
        _contentArea.Clear();

        var content = _worldDetailAsset.Instantiate();
        content.style.flexGrow = 1;
        _contentArea.Add(content);

        _worldDetailController = new WorldDetailController(content, UserManager.Instance.Api, world);
        _worldDetailController.OnBack += ShowWorldList;
        _worldDetailController.OnShowRoomList += ShowRoomList;
        _worldDetailController.OnEnterWorld += EnterWorld;
    }

    private void ShowWorldList()
    {
        _worldDetailController?.Dispose();
        _worldDetailController = null;
        _roomListController?.Dispose();
        _roomListController = null;
        SelectTab(_navWorld, _worldTabAsset);
    }

    // ── Room list ─────────────────────────────────────────────────────────────

    private void ShowRoomList()
    {
        if (_roomListAsset == null || _currentWorld == null) return;

        _worldDetailController?.Dispose();
        _worldDetailController = null;
        _roomListController?.Dispose();
        _roomListController = null;
        _contentArea.Clear();

        var content = _roomListAsset.Instantiate();
        content.style.flexGrow = 1;
        _contentArea.Add(content);

        var hasPremium = UserManager.Instance?.Capabilities?.inviteRoomCreate ?? false;
        _roomListController = new RoomListController(content, UserManager.Instance.Api, _currentWorld, hasPremium);
        _roomListController.OnBack += () => ShowWorldDetail(_currentWorld);
        _roomListController.OnEnterWorld += EnterWorld;
        _roomListController.Initialize();
    }

    // ── Social screens ────────────────────────────────────────────────────────

    private void ShowFriendScreen()
    {
        if (_friendScreenAsset == null) return;

        DisposeAllControllers();
        _contentArea.Clear();

        var content = _friendScreenAsset.Instantiate();
        content.style.flexGrow = 1;
        _contentArea.Add(content);

        _friendScreenController = new FriendScreenController(content);
        _friendScreenController.OnBackRequested += () => SelectTab(_navSettings, _settingsTabAsset);
        _friendScreenController.OnUserTapped += userId => _userInfoController?.ShowAsync(userId);
    }

    private void ShowFollowScreen(string targetUserId, bool followersTab = false)
    {
        if (_followScreenAsset == null) return;

        DisposeAllControllers();
        _contentArea.Clear();

        var content = _followScreenAsset.Instantiate();
        content.style.flexGrow = 1;
        _contentArea.Add(content);

        _followScreenController = new FollowScreenController(content, targetUserId);
        _followScreenController.OnBackRequested += () => SelectTab(_navSettings, _settingsTabAsset);
        _followScreenController.OnUserTapped += userId => _userInfoController?.ShowAsync(userId);
    }

    // ── Scene transition ──────────────────────────────────────────────────────

    private void EnterWorld(string worldId, string roomId, string glbUrl)
    {
        WorldSessionData.Set(worldId, roomId, glbUrl);
        DisposeAllControllers();
        SceneManager.LoadScene("WorldScene");
    }
}
