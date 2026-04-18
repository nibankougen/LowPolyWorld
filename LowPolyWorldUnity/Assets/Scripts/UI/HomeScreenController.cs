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

        SelectTab(_navWorld, _worldTabAsset);
    }

    private void OnDisable()
    {
        DisposeAllControllers();
    }

    private void DisposeAllControllers()
    {
        _worldListController?.Dispose();
        _worldListController = null;
        _worldDetailController?.Dispose();
        _worldDetailController = null;
        _roomListController?.Dispose();
        _roomListController = null;
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
        }
        else if (tab == _navWorld && UserManager.Instance != null)
        {
            _worldListController = new WorldListController(content, UserManager.Instance.Api);
            _worldListController.OnWorldSelected += ShowWorldDetail;
            _worldListController.Initialize();
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

        var hasPremium = UserManager.Instance?.Capabilities?.invite_room_create ?? false;
        _roomListController = new RoomListController(content, UserManager.Instance.Api, _currentWorld, hasPremium);
        _roomListController.OnBack += () => ShowWorldDetail(_currentWorld);
        _roomListController.OnEnterWorld += EnterWorld;
        _roomListController.Initialize();
    }

    // ── Scene transition ──────────────────────────────────────────────────────

    private void EnterWorld(string worldId, string roomId, string glbUrl)
    {
        WorldSessionData.Set(worldId, roomId, glbUrl);
        DisposeAllControllers();
        SceneManager.LoadScene("WorldScene");
    }
}
