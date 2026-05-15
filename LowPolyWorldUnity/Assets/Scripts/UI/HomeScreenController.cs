using System.Threading;
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
    [SerializeField] private VisualTreeAsset _hiddenUsersScreenAsset;
    [SerializeField] private VisualTreeAsset _hiddenWorldsScreenAsset;
    [SerializeField] private VisualTreeAsset _friendsRoomScreenAsset;
    [SerializeField] private VisualTreeAsset _inviteLinkPanelAsset;

    [Header("Account Settings")]
    [SerializeField] private VisualTreeAsset _accountSettingsScreenAsset;

    [Header("World Entry")]
    [SerializeField] private VisualTreeAsset _worldAvatarSelectAsset;

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
    private HiddenUsersScreenController _hiddenUsersController;
    private HiddenWorldsScreenController _hiddenWorldsController;
    private FriendsRoomScreenController _friendsRoomController;
    private UserInfoPanelController _userInfoController;
    private WorldAvatarSelectController _worldAvatarSelectController;
    private AccountSettingsController _accountSettingsController;

    // 招待リンクパネル（オーバーレイ）
    private VisualElement _inviteLinkOverlay;
    private InviteLinkPanelController _inviteLinkController;

    private FlashMessageController _flashController;
    private CancellationTokenSource _cts;

    // ユーザー情報パネルオーバーレイ
    private VisualElement _userInfoOverlay;

    private WorldResponse _currentWorld;

    private void Awake()
    {
        _document = GetComponent<UIDocument>();
    }

    private void OnEnable()
    {
        _cts = new CancellationTokenSource();

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

        var flashRoot = root.Q<VisualElement>("flash-root");
        if (flashRoot != null)
            _flashController = new FlashMessageController(flashRoot);

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

        if (_inviteLinkPanelAsset != null)
        {
            _inviteLinkOverlay = _inviteLinkPanelAsset.Instantiate();
            _inviteLinkOverlay.style.position = Position.Absolute;
            _inviteLinkOverlay.style.top = 0;
            _inviteLinkOverlay.style.left = 0;
            _inviteLinkOverlay.style.right = 0;
            _inviteLinkOverlay.style.bottom = 0;
            _inviteLinkOverlay.style.display = DisplayStyle.None;
            root.Add(_inviteLinkOverlay);
            _inviteLinkController = new InviteLinkPanelController(_inviteLinkOverlay);
            _inviteLinkController.OnEnterRoom += OnInviteLinkEnterRoom;
            _inviteLinkController.OnClose += HideInvitePanel;
        }

        if (DeepLinkHandler.Instance != null)
            DeepLinkHandler.Instance.OnInviteTokenReceived += HandleInviteToken;
        DeepLinkHandler.Instance?.MarkReady();

        SelectTab(_navWorld, _worldTabAsset);
    }

    private void OnDisable()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;

        if (DeepLinkHandler.Instance != null)
            DeepLinkHandler.Instance.OnInviteTokenReceived -= HandleInviteToken;

        DisposeAllControllers();
        _userInfoController?.Dispose();
        _userInfoController = null;
        _inviteLinkController?.Dispose();
        _inviteLinkController = null;
        _flashController?.Dispose();
        _flashController = null;
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
        _hiddenUsersController?.Dispose();
        _hiddenUsersController = null;
        _hiddenWorldsController?.Dispose();
        _hiddenWorldsController = null;
        _friendsRoomController?.Dispose();
        _friendsRoomController = null;
        _settingsTabController = null;
        _worldAvatarSelectController?.Dispose();
        _worldAvatarSelectController = null;
        _accountSettingsController?.Dispose();
        _accountSettingsController = null;
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
            _settingsTabController.OnHiddenUsersRequested += ShowHiddenUsersScreen;
            _settingsTabController.OnHiddenWorldsRequested += ShowHiddenWorldsScreen;
            _settingsTabController.OnAccountSettingsRequested += ShowAccountSettingsScreen;
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
        _worldDetailController.OnUserRestricted += ShowRestrictionDialog;
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
        _roomListController.OnInviteRoomCreated += ShowInvitePanel;
        _roomListController.OnUserRestricted += ShowRestrictionDialog;
        _roomListController.Initialize();
    }

    // ── Invite link panel ─────────────────────────────────────────────────────

    private string _pendingInviteRoomId;

    private void ShowInvitePanel(string roomId, int maxPlayers)
    {
        if (_inviteLinkController == null || _inviteLinkOverlay == null) return;
        _pendingInviteRoomId = roomId;
        _inviteLinkOverlay.style.display = DisplayStyle.Flex;
        _inviteLinkController.ShowAndFetchLink(roomId, maxPlayers);
    }

    private void HideInvitePanel()
    {
        if (_inviteLinkOverlay != null)
            _inviteLinkOverlay.style.display = DisplayStyle.None;
    }

    private void OnInviteLinkEnterRoom()
    {
        HideInvitePanel();
        if (!string.IsNullOrEmpty(_pendingInviteRoomId) && _currentWorld != null)
            EnterWorld(_currentWorld.id, _pendingInviteRoomId, _currentWorld.glbUrl);
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
        _friendScreenController.OnFriendsRoomsRequested += ShowFriendsRoomScreen;
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

    private void ShowHiddenUsersScreen()
    {
        if (_hiddenUsersScreenAsset == null) return;

        DisposeAllControllers();
        _contentArea.Clear();

        var content = _hiddenUsersScreenAsset.Instantiate();
        content.style.flexGrow = 1;
        _contentArea.Add(content);

        _hiddenUsersController = new HiddenUsersScreenController(content);
        _hiddenUsersController.OnBackRequested += () => SelectTab(_navSettings, _settingsTabAsset);
    }

    private void ShowHiddenWorldsScreen()
    {
        if (_hiddenWorldsScreenAsset == null) return;

        DisposeAllControllers();
        _contentArea.Clear();

        var content = _hiddenWorldsScreenAsset.Instantiate();
        content.style.flexGrow = 1;
        _contentArea.Add(content);

        _hiddenWorldsController = new HiddenWorldsScreenController(content);
        _hiddenWorldsController.OnBackRequested += () => SelectTab(_navSettings, _settingsTabAsset);
    }

    private void ShowFriendsRoomScreen()
    {
        if (_friendsRoomScreenAsset == null) return;

        DisposeAllControllers();
        _contentArea.Clear();

        var content = _friendsRoomScreenAsset.Instantiate();
        content.style.flexGrow = 1;
        _contentArea.Add(content);

        _friendsRoomController = new FriendsRoomScreenController(content);
        _friendsRoomController.OnBackRequested += ShowFriendScreen;
        _friendsRoomController.OnEnterWorld += EnterWorld;
        _friendsRoomController.OnUserRestricted += ShowRestrictionDialog;
    }

    // ── Account settings ─────────────────────────────────────────────────────

    private void ShowAccountSettingsScreen()
    {
        if (_accountSettingsScreenAsset == null) return;

        DisposeAllControllers();
        _contentArea.Clear();

        var content = _accountSettingsScreenAsset.Instantiate();
        content.style.flexGrow = 1;
        _contentArea.Add(content);

        _accountSettingsController = new AccountSettingsController(content);
        _accountSettingsController.OnBackRequested += () => SelectTab(_navSettings, _settingsTabAsset);
        _accountSettingsController.OnLoggedOut += () =>
        {
            DisposeAllControllers();
            SceneManager.LoadScene("HomeScene");
        };
    }

    // ── Deep link invite ──────────────────────────────────────────────────────

    private async void HandleInviteToken(string token)
    {
        if (UserManager.Instance?.Api == null) return;
        var ct = _cts?.Token ?? CancellationToken.None;

        var (room, error) = await UserManager.Instance.Api.PostJsonAsync<RoomResponse>(
            $"/api/v1/invite/{token}/join", null, ct);

        if (error != null)
        {
            string msg = error switch
            {
                "invite_expired" => "招待リンクの有効期限が切れています",
                "invite_limit_reached" => "招待リンクの使用上限に達しています",
                "room_not_open" => "このルームは現在入室できません",
                "not_found" => "招待リンクが無効です",
                "room_full" => "ルームが満員です",
                _ => "招待リンクの処理に失敗しました",
            };
            FlashMessageController.Current?.Show(msg, FlashMessageType.Error);
            return;
        }

        var (worldData, worldError) = await UserManager.Instance.Api.GetAsync<WorldDataResponse>(
            $"/api/v1/worlds/{room.worldId}", ct);

        if (worldError != null || worldData?.data == null)
        {
            FlashMessageController.Current?.Show("ワールドの読み込みに失敗しました", FlashMessageType.Error);
            return;
        }

        EnterWorld(room.worldId, room.id, worldData.data.glbUrl);
    }

    // ── Restriction dialog ────────────────────────────────────────────────────

    private void ShowRestrictionDialog()
    {
        var root = _document.rootVisualElement;

        var backdrop = new VisualElement();
        backdrop.style.position = Position.Absolute;
        backdrop.style.top = 0;
        backdrop.style.left = 0;
        backdrop.style.right = 0;
        backdrop.style.bottom = 0;
        backdrop.style.backgroundColor = new Color(0f, 0f, 0f, 0.6f);
        backdrop.style.alignItems = Align.Center;
        backdrop.style.justifyContent = Justify.Center;

        var box = new VisualElement();
        box.style.backgroundColor = new Color(30f / 255, 30f / 255, 40f / 255);
        box.style.borderTopLeftRadius = 18;
        box.style.borderTopRightRadius = 18;
        box.style.borderBottomLeftRadius = 18;
        box.style.borderBottomRightRadius = 18;
        box.style.paddingTop = 30;
        box.style.paddingBottom = 30;
        box.style.paddingLeft = 28;
        box.style.paddingRight = 28;
        box.style.marginLeft = 24;
        box.style.marginRight = 24;
        box.style.maxWidth = 600;

        var title = new Label("参加が制限されています");
        title.style.fontSize = 26;
        title.style.color = Color.white;
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        title.style.marginBottom = 16;
        title.style.unityTextAlign = TextAnchor.MiddleCenter;
        box.Add(title);

        var body = new Label(
            "このアカウントは現在、公開ルームへの参加が制限されています。\n"
                + "制限の解除については下記までお問い合わせください。"
        );
        body.style.fontSize = 22;
        body.style.color = new Color(180f / 255, 180f / 255, 195f / 255);
        body.style.whiteSpace = WhiteSpace.Normal;
        body.style.marginBottom = 16;
        box.Add(body);

        var btnContact = new Button(() => Application.OpenURL("mailto:nibankougen@gmail.com"))
        {
            text = "nibankougen@gmail.com",
        };
        btnContact.style.backgroundColor = new StyleColor(Color.clear);
        btnContact.style.color = new Color(100f / 255, 170f / 255, 255f / 255);
        btnContact.style.fontSize = 22;
        btnContact.style.borderTopWidth = 0;
        btnContact.style.borderBottomWidth = 0;
        btnContact.style.borderLeftWidth = 0;
        btnContact.style.borderRightWidth = 0;
        btnContact.style.marginBottom = 20;
        box.Add(btnContact);

        var btnClose = new Button(() => root.Remove(backdrop)) { text = "閉じる" };
        btnClose.style.height = 60;
        btnClose.style.fontSize = 24;
        btnClose.style.borderTopLeftRadius = 12;
        btnClose.style.borderTopRightRadius = 12;
        btnClose.style.borderBottomLeftRadius = 12;
        btnClose.style.borderBottomRightRadius = 12;
        var borderColor = new StyleColor(new Color(80f / 255, 80f / 255, 100f / 255));
        btnClose.style.borderTopWidth = 1;
        btnClose.style.borderBottomWidth = 1;
        btnClose.style.borderLeftWidth = 1;
        btnClose.style.borderRightWidth = 1;
        btnClose.style.borderTopColor = borderColor;
        btnClose.style.borderBottomColor = borderColor;
        btnClose.style.borderLeftColor = borderColor;
        btnClose.style.borderRightColor = borderColor;
        btnClose.style.backgroundColor = new StyleColor(Color.clear);
        btnClose.style.color = new Color(180f / 255, 180f / 255, 195f / 255);
        box.Add(btnClose);

        backdrop.Add(box);
        backdrop.RegisterCallback<ClickEvent>(e =>
        {
            if (e.target == backdrop)
                root.Remove(backdrop);
        });
        root.Add(backdrop);
    }

    // ── Scene transition ──────────────────────────────────────────────────────

    private void EnterWorld(string worldId, string roomId, string glbUrl)
    {
        WorldSessionData.Set(worldId, roomId, glbUrl);
        ShowAvatarSelect();
    }

    private void ShowAvatarSelect()
    {
        if (_worldAvatarSelectAsset == null)
        {
            DoEnterWorldScene();
            return;
        }

        DisposeAllControllers();
        _contentArea.Clear();

        var content = _worldAvatarSelectAsset.Instantiate();
        content.style.flexGrow = 1;
        _contentArea.Add(content);

        _worldAvatarSelectController = new WorldAvatarSelectController(content);
        _worldAvatarSelectController.OnConfirmed += DoEnterWorldScene;
        _worldAvatarSelectController.OnCanceled += CancelWorldEntry;
    }

    private void DoEnterWorldScene()
    {
        _worldAvatarSelectController?.Dispose();
        _worldAvatarSelectController = null;
        DisposeAllControllers();
        SceneManager.LoadScene("WorldScene");
    }

    private void CancelWorldEntry()
    {
        WorldSessionData.Clear();
        _worldAvatarSelectController?.Dispose();
        _worldAvatarSelectController = null;
        ShowWorldList();
    }
}
