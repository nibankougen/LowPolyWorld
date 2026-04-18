using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// HomeScene のナビゲーションバーとタブ切り替えを管理する MonoBehaviour。
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

        // デフォルトはワールドタブ
        SelectTab(_navWorld, _worldTabAsset);
    }

    private void OnDisable()
    {
        _worldListController?.Dispose();
        _worldListController = null;
    }

    private void SelectTab(Button tab, VisualTreeAsset contentAsset)
    {
        if (_activeTab != null)
            _activeTab.RemoveFromClassList("nav-tab--active");

        _activeTab = tab;
        _activeTab.AddToClassList("nav-tab--active");

        // Dispose previous tab controllers
        _settingsTabController = null;
        _worldListController?.Dispose();
        _worldListController = null;
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
            _worldListController.Initialize();
        }
    }
}
