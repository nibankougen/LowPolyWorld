using System;
using UnityEngine.UIElements;

/// <summary>
/// インワールドメニューの4タブ切り替えと音量スライダーを管理するコントローラー。
/// WorldHUDController が所有し、メニューパネルの VisualElement を渡して使用する。
/// </summary>
public class InWorldMenuController : IDisposable
{
    public event Action OnCloseRequested;

    private readonly VisualElement _root;

    private Button _tabRoom;
    private Button _tabAvatar;
    private Button _tabWorlds;
    private Button _tabSettings;

    private VisualElement _contentRoom;
    private VisualElement _contentAvatar;
    private VisualElement _contentWorlds;
    private VisualElement _contentSettings;

    private Button _activeTab;
    private SettingsTabController _settingsTabController;

    public InWorldMenuController(VisualElement root)
    {
        _root = root;

        _root.Q<Button>("btn-close")?.RegisterCallback<ClickEvent>(_ => OnCloseRequested?.Invoke());
        _root.Q<VisualElement>("backdrop")?.RegisterCallback<ClickEvent>(_ => OnCloseRequested?.Invoke());

        _tabRoom = root.Q<Button>("tab-room");
        _tabAvatar = root.Q<Button>("tab-avatar");
        _tabWorlds = root.Q<Button>("tab-worlds");
        _tabSettings = root.Q<Button>("tab-settings");

        _contentRoom = root.Q<VisualElement>("content-room");
        _contentAvatar = root.Q<VisualElement>("content-avatar");
        _contentWorlds = root.Q<VisualElement>("content-worlds");
        _contentSettings = root.Q<VisualElement>("content-settings");

        _tabRoom?.RegisterCallback<ClickEvent>(_ => SelectTab(_tabRoom, _contentRoom));
        _tabAvatar?.RegisterCallback<ClickEvent>(_ => SelectTab(_tabAvatar, _contentAvatar));
        _tabWorlds?.RegisterCallback<ClickEvent>(_ => SelectTab(_tabWorlds, _contentWorlds));
        _tabSettings?.RegisterCallback<ClickEvent>(_ => SelectTab(_tabSettings, _contentSettings));

        _settingsTabController = new SettingsTabController(_contentSettings);

        // デフォルトはルームタブ
        SelectTab(_tabRoom, _contentRoom);
    }

    /// <summary>メニューを開く際に最新の WorldSettingsLogic を紐付ける。</summary>
    public void BindSettings(WorldSettingsLogic settings)
    {
        _settingsTabController?.Bind(settings);
    }

    private void SelectTab(Button tab, VisualElement content)
    {
        if (_activeTab != null)
            _activeTab.RemoveFromClassList("menu-tab--active");

        _contentRoom?.AddToClassList("menu-tab-content--hidden");
        _contentAvatar?.AddToClassList("menu-tab-content--hidden");
        _contentWorlds?.AddToClassList("menu-tab-content--hidden");
        _contentSettings?.AddToClassList("menu-tab-content--hidden");

        _activeTab = tab;
        _activeTab?.AddToClassList("menu-tab--active");
        content?.RemoveFromClassList("menu-tab-content--hidden");
    }

    public void Dispose()
    {
        _settingsTabController?.Unbind();
    }
}
