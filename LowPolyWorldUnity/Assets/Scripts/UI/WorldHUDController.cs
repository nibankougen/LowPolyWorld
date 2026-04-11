using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// WorldScene HUD ボタンとインワールドメニューの表示/非表示を管理する MonoBehaviour。
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class WorldHUDController : MonoBehaviour
{
    [SerializeField] private UIDocument _inWorldMenuDocument;

    private UIDocument _document;
    private VisualElement _inWorldMenuRoot;

    private const string KeyShowControlButtons = "ShowControlButtons";

    // コントロールボタン表示状態（設定 19.5 と共有）
    private bool _showControlButtons;

    private Button _btnJump;
    private Button _btnSprint;

    private InWorldMenuController _menuController;

    private void Awake()
    {
        _document = GetComponent<UIDocument>();
    }

    private void OnEnable()
    {
        var root = _document.rootVisualElement;

        root.Q<Button>("btn-menu")?.RegisterCallback<ClickEvent>(_ => OpenMenu());
        root.Q<Button>("btn-camera")?.RegisterCallback<ClickEvent>(_ => OnCameraButtonClicked());
        root.Q<Button>("btn-bell")?.RegisterCallback<ClickEvent>(_ => OnBellButtonClicked());
        root.Q<Button>("btn-action")?.RegisterCallback<ClickEvent>(_ => OnActionButtonClicked());

        _btnJump = root.Q<Button>("btn-jump");
        _btnSprint = root.Q<Button>("btn-sprint");

        _btnJump?.RegisterCallback<ClickEvent>(_ => OnJumpButtonClicked());
        _btnSprint?.RegisterCallback<ClickEvent>(_ => OnSprintButtonClicked());

        _showControlButtons = PlayerPrefs.GetInt(KeyShowControlButtons, 0) == 1;

        if (_inWorldMenuDocument != null)
        {
            _inWorldMenuRoot = _inWorldMenuDocument.rootVisualElement;
            _menuController = new InWorldMenuController(_inWorldMenuRoot);
            _menuController.OnCloseRequested += CloseMenu;
            _menuController.BindControlButtons(_showControlButtons, OnControlButtonsToggled);
            _inWorldMenuRoot.style.display = DisplayStyle.None;
        }

        ApplyControlButtonVisibility();
    }

    private void OnDisable()
    {
        _menuController?.Dispose();
        _menuController = null;
    }

    /// <summary>コントロールボタン表示を切り替える（設定 19.5 から呼ぶ）。</summary>
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

    private void OnCameraButtonClicked() => Debug.Log("[HUD] Camera button clicked");
    private void OnBellButtonClicked() => Debug.Log("[HUD] Bell button clicked");
    private void OnActionButtonClicked() => Debug.Log("[HUD] Action button clicked");
    private void OnJumpButtonClicked() => Debug.Log("[HUD] Jump button clicked");
    private void OnSprintButtonClicked() => Debug.Log("[HUD] Sprint button clicked");
}
