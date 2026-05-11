using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// WorldScene HUD ボタンとインワールドメニューの表示/非表示を管理する MonoBehaviour。
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class WorldHUDController : MonoBehaviour
{
    [SerializeField] private UIDocument _inWorldMenuDocument;
    [SerializeField] private RoomSessionController _sessionController;
    [SerializeField] private VisualTreeAsset _notificationCenterAsset;
    [SerializeField] private VisualTreeAsset _userInfoPanelAsset;
    [SerializeField] private PhotoModeController _photoModeController;

    private UIDocument _document;
    private VisualElement _inWorldMenuRoot;
    private float _sessionUpdateTimer;

    private const string KeyShowControlButtons = "ShowControlButtons";

    private bool _showControlButtons;
    private Button _btnJump;
    private Button _btnSprint;
    private Label _badgeBell;

    private InWorldMenuController _menuController;
    private FlashMessageController _flashController;
    private NotificationCenterController _notifController;
    private UserInfoPanelController _userInfoController;

    private VisualElement _notifPanel;
    private VisualElement _userInfoBackdrop;

    private void Awake()
    {
        _document = GetComponent<UIDocument>();
    }

    private void OnEnable()
    {
        var root = _document.rootVisualElement;

        // フラッシュメッセージ
        var flashRoot = root.Q<VisualElement>("flash-root");
        if (flashRoot != null)
            _flashController = new FlashMessageController(flashRoot);

        _photoModeController?.Initialize(root);

        root.Q<Button>("btn-menu")?.RegisterCallback<ClickEvent>(_ => OpenMenu());
        root.Q<Button>("btn-camera")?.RegisterCallback<ClickEvent>(_ => OnCameraButtonClicked());
        root.Q<Button>("btn-bell")?.RegisterCallback<ClickEvent>(_ => OnBellButtonClicked());
        root.Q<Button>("btn-action")?.RegisterCallback<ClickEvent>(_ => OnActionButtonClicked());

        _btnJump = root.Q<Button>("btn-jump");
        _btnSprint = root.Q<Button>("btn-sprint");
        _badgeBell = root.Q<Label>("badge-bell");

        _btnJump?.RegisterCallback<ClickEvent>(_ => OnJumpButtonClicked());
        _btnSprint?.RegisterCallback<ClickEvent>(_ => OnSprintButtonClicked());

        _showControlButtons = PlayerPrefs.GetInt(KeyShowControlButtons, 0) == 1;

        // インワールドメニュー
        if (_inWorldMenuDocument != null)
        {
            _inWorldMenuRoot = _inWorldMenuDocument.rootVisualElement;
            _menuController = new InWorldMenuController(_inWorldMenuRoot);
            _menuController.OnCloseRequested += CloseMenu;
            _menuController.BindControlButtons(_showControlButtons, OnControlButtonsToggled);
            _inWorldMenuRoot.style.display = DisplayStyle.None;
        }

        // 通知センターパネル（HUD 上に重ねる）
        if (_notificationCenterAsset != null)
        {
            _notifPanel = _notificationCenterAsset.Instantiate();
            _notifPanel.style.position = Position.Absolute;
            _notifPanel.style.top = 0;
            _notifPanel.style.right = 0;
            _notifPanel.style.bottom = 0;
            _notifPanel.style.width = Length.Percent(100);
            _notifPanel.AddToClassList("overlay-hidden");
            root.Add(_notifPanel);

            _notifController = new NotificationCenterController(_notifPanel.Q<VisualElement>("notif-panel"));
            _notifController.OnCloseRequested += CloseNotificationCenter;
        }

        // ユーザー情報パネル
        if (_userInfoPanelAsset != null)
        {
            _userInfoBackdrop = _userInfoPanelAsset.Instantiate();
            _userInfoBackdrop.style.position = Position.Absolute;
            _userInfoBackdrop.style.top = 0;
            _userInfoBackdrop.style.left = 0;
            _userInfoBackdrop.style.right = 0;
            _userInfoBackdrop.style.bottom = 0;
            root.Add(_userInfoBackdrop);

            _userInfoController = new UserInfoPanelController(_userInfoBackdrop.Q<VisualElement>("uip-backdrop"));
        }

        // 通知未読バッジ購読
        if (NotificationManager.Instance != null)
            NotificationManager.Instance.OnUnreadCountChanged += RefreshBadge;

        ApplyControlButtonVisibility();
        RefreshBadge();
    }

    private void OnDisable()
    {
        _menuController?.Dispose();
        _menuController = null;
        _flashController?.Dispose();
        _flashController = null;
        _notifController?.Dispose();
        _notifController = null;
        _userInfoController?.Dispose();
        _userInfoController = null;

        if (NotificationManager.Instance != null)
            NotificationManager.Instance.OnUnreadCountChanged -= RefreshBadge;
    }

    // ── 通知バッジ ─────────────────────────────────────────────────────────────

    private void RefreshBadge()
    {
        if (_badgeBell == null) return;
        int count = NotificationManager.Instance?.Store.UnreadCount ?? 0;
        if (count > 0)
        {
            _badgeBell.text = count > 99 ? "99+" : count.ToString();
            _badgeBell.RemoveFromClassList("overlay-hidden");
        }
        else
        {
            _badgeBell.AddToClassList("overlay-hidden");
        }
    }

    // ── コントロールボタン ─────────────────────────────────────────────────────

    public void SetControlButtonsVisible(bool visible)
    {
        _showControlButtons = visible;
        ApplyControlButtonVisibility();
    }

    private void OnControlButtonsToggled(bool visible)
    {
        PlayerPrefs.SetInt(KeyShowControlButtons, visible ? 1 : 0);
        PlayerPrefs.Save();
        SetControlButtonsVisible(visible);
    }

    private void ApplyControlButtonVisibility()
    {
        if (_btnJump != null)
        {
            if (_showControlButtons)
                _btnJump.RemoveFromClassList("hud-hidden");
            else
                _btnJump.AddToClassList("hud-hidden");
        }

        if (_btnSprint != null)
        {
            if (_showControlButtons)
                _btnSprint.RemoveFromClassList("hud-hidden");
            else
                _btnSprint.AddToClassList("hud-hidden");
        }
    }

    // ── メニュー ───────────────────────────────────────────────────────────────

    private void OpenMenu()
    {
        if (_inWorldMenuRoot != null)
        {
            _inWorldMenuRoot.style.display = DisplayStyle.Flex;
            _menuController?.BindSettings(AudioManager.Instance?.Settings);
        }
    }

    private void CloseMenu()
    {
        if (_inWorldMenuRoot != null)
            _inWorldMenuRoot.style.display = DisplayStyle.None;
    }

    // ── 通知センター ───────────────────────────────────────────────────────────

    private void OnBellButtonClicked()
    {
        if (_notifPanel == null)
        {
            Debug.Log("[HUD] Bell button clicked (no notif panel assigned)");
            return;
        }
        if (_notifPanel.ClassListContains("overlay-hidden"))
        {
            _notifPanel.RemoveFromClassList("overlay-hidden");
            _notifController?.Show();
        }
        else
        {
            CloseNotificationCenter();
        }
    }

    private void CloseNotificationCenter()
    {
        _notifPanel?.AddToClassList("overlay-hidden");
        RefreshBadge();
    }

    // ── ユーザー情報パネル ─────────────────────────────────────────────────────

    /// <summary>指定ユーザーの情報パネルを開く（アバタータップ時など）。</summary>
    public void ShowUserInfo(string userId)
    {
        _userInfoController?.ShowAsync(userId);
    }

    // ── Update ────────────────────────────────────────────────────────────────

    private void Update()
    {
        if (_sessionController == null || _menuController == null) return;
        _sessionUpdateTimer += Time.deltaTime;
        if (_sessionUpdateTimer >= 1f)
        {
            _sessionUpdateTimer = 0f;
            _menuController.UpdateSessionRemaining(_sessionController.GetRemainingSeconds());
        }
    }

    private void OnCameraButtonClicked() => _photoModeController?.Enter();
    private void OnActionButtonClicked() => Debug.Log("[HUD] Action button clicked");
    private void OnJumpButtonClicked() => Debug.Log("[HUD] Jump button clicked");
    private void OnSprintButtonClicked() => Debug.Log("[HUD] Sprint button clicked");
}
