using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// インワールドメニューの4タブ切り替えと音量スライダーを管理するコントローラー。
/// WorldHUDController が所有し、メニューパネルの VisualElement を渡して使用する。
/// </summary>
public class InWorldMenuController : IDisposable
{
    public event Action OnCloseRequested;

    /// <summary>アバタータブでアバター変更が確定されたときに発火。</summary>
    public event Action<StartupAvatar> OnAvatarChangeRequested;

    /// <summary>ワールド一覧タブでワールドタップ時に発火。</summary>
    public event Action<WorldResponse> OnWorldSelected;

    private readonly VisualElement _root;
    private readonly CancellationTokenSource _cts = new();

    // セッション残り時間
    private Label _labelSessionRemaining;

    // タブ
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

    // アバタータブ
    private Button _subtabSlot;
    private Button _subtabShop;
    private VisualElement _avatarSlotContent;
    private VisualElement _avatarShopContent;
    private Button _activeSubtab;
    private ScrollView _avatarSlotList;

    private VisualElement _avatarConfirmBackdrop;
    private Label _avatarConfirmName;
    private Button _btnAvatarConfirmOk;
    private Button _btnAvatarConfirmCancel;
    private StartupAvatar _pendingAvatarChange;

    // ワールド一覧タブ
    private Label _labelWorldsLoading;
    private ScrollView _worldsList;
    private bool _worldsLoaded;

    public InWorldMenuController(VisualElement root)
    {
        _root = root;

        _root.Q<Button>("btn-close")?.RegisterCallback<ClickEvent>(_ => OnCloseRequested?.Invoke());
        _root.Q<VisualElement>("backdrop")?.RegisterCallback<ClickEvent>(_ => OnCloseRequested?.Invoke());
        _labelSessionRemaining = _root.Q<Label>("label-session-remaining");

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

        SetupAvatarTab();
        SetupWorldsTab();

        // デフォルトはルームタブ
        SelectTab(_tabRoom, _contentRoom);
    }

    /// <summary>メニューを開く際に最新の WorldSettingsLogic を紐付ける。</summary>
    public void BindSettings(WorldSettingsLogic settings)
    {
        _settingsTabController?.Bind(settings);
    }

    /// <summary>コントロールボタントグルを紐付ける。</summary>
    public void BindControlButtons(bool initialValue, Action<bool> onChange)
    {
        _settingsTabController?.BindControlButtons(initialValue, onChange);
    }

    /// <summary>セッション残り時間表示を更新する（毎秒呼び出し）。</summary>
    public void UpdateSessionRemaining(float remainingSeconds)
    {
        if (_labelSessionRemaining == null) return;
        int total = (int)System.Math.Ceiling((double)remainingSeconds);
        int h = total / 3600;
        int m = (total % 3600) / 60;
        int s = total % 60;
        _labelSessionRemaining.text = $"{h:00}:{m:00}:{s:00}";
    }

    // ---- アバタータブ ----------------------------------------------------------------

    private void SetupAvatarTab()
    {
        _subtabSlot = _root.Q<Button>("subtab-slot");
        _subtabShop = _root.Q<Button>("subtab-shop");
        _avatarSlotContent = _root.Q<VisualElement>("avatar-slot-content");
        _avatarShopContent = _root.Q<VisualElement>("avatar-shop-content");
        _avatarSlotList = _root.Q<ScrollView>("avatar-slot-list");

        _avatarConfirmBackdrop = _root.Q<VisualElement>("avatar-confirm-backdrop");
        _avatarConfirmName = _root.Q<Label>("avatar-confirm-name");
        _btnAvatarConfirmOk = _root.Q<Button>("btn-avatar-confirm-ok");
        _btnAvatarConfirmCancel = _root.Q<Button>("btn-avatar-confirm-cancel");

        _subtabSlot?.RegisterCallback<ClickEvent>(_ => SelectAvatarSubtab(_subtabSlot, _avatarSlotContent));
        _subtabShop?.RegisterCallback<ClickEvent>(_ => SelectAvatarSubtab(_subtabShop, _avatarShopContent));
        _btnAvatarConfirmOk?.RegisterCallback<ClickEvent>(_ => OnAvatarConfirmOk());
        _btnAvatarConfirmCancel?.RegisterCallback<ClickEvent>(_ => HideAvatarConfirm());
        _avatarConfirmBackdrop?.RegisterCallback<ClickEvent>(e =>
        {
            if (e.target == _avatarConfirmBackdrop)
                HideAvatarConfirm();
        });

        SelectAvatarSubtab(_subtabSlot, _avatarSlotContent);
    }

    private void BuildSlotAvatarList()
    {
        if (_avatarSlotList == null) return;
        _avatarSlotList.Clear();

        var avatars = UserManager.Instance?.Avatars;
        if (avatars == null || avatars.Count == 0)
        {
            var empty = new Label("所持アバターがありません");
            empty.AddToClassList("menu-stub-text");
            _avatarSlotList.Add(empty);
            return;
        }

        var caps = UserManager.Instance?.Capabilities;
        int slotLimit = caps?.avatarSlots ?? int.MaxValue;

        for (int i = 0; i < avatars.Count; i++)
        {
            var avatar = avatars[i];
            bool isLocked = i >= slotLimit;
            var card = BuildAvatarCard(avatar, isLocked);
            _avatarSlotList.Add(card);
        }
    }

    private VisualElement BuildAvatarCard(StartupAvatar avatar, bool isLocked)
    {
        var card = new VisualElement();
        card.AddToClassList("menu-avatar-card");
        if (isLocked)
            card.AddToClassList("menu-avatar-card--locked");

        var nameLabel = new Label(avatar.name ?? "Avatar");
        nameLabel.AddToClassList("menu-avatar-name");
        card.Add(nameLabel);

        if (isLocked)
        {
            var lockLabel = new Label("🔒");
            lockLabel.AddToClassList("menu-avatar-lock");
            card.Add(lockLabel);
        }

        if (!isLocked)
        {
            card.RegisterCallback<ClickEvent>(_ => ShowAvatarConfirm(avatar));
        }

        return card;
    }

    private void ShowAvatarConfirm(StartupAvatar avatar)
    {
        _pendingAvatarChange = avatar;
        if (_avatarConfirmName != null)
            _avatarConfirmName.text = avatar.name ?? "Avatar";
        _avatarConfirmBackdrop?.RemoveFromClassList("overlay-hidden");
    }

    private void HideAvatarConfirm()
    {
        _pendingAvatarChange = null;
        _avatarConfirmBackdrop?.AddToClassList("overlay-hidden");
    }

    private void OnAvatarConfirmOk()
    {
        if (_pendingAvatarChange != null)
            OnAvatarChangeRequested?.Invoke(_pendingAvatarChange);
        HideAvatarConfirm();
    }

    private void SelectAvatarSubtab(Button subtab, VisualElement content)
    {
        _activeSubtab?.RemoveFromClassList("menu-subtab--active");
        _avatarSlotContent?.AddToClassList("menu-subcontent--hidden");
        _avatarShopContent?.AddToClassList("menu-subcontent--hidden");

        _activeSubtab = subtab;
        _activeSubtab?.AddToClassList("menu-subtab--active");
        content?.RemoveFromClassList("menu-subcontent--hidden");
    }

    // ---- ワールド一覧タブ -------------------------------------------------------------

    private void SetupWorldsTab()
    {
        _labelWorldsLoading = _root.Q<Label>("label-worlds-loading");
        _worldsList = _root.Q<ScrollView>("worlds-list");
    }

    private void BuildWorldsList()
    {
        if (_worldsList == null) return;
        _worldsList.Clear();

        var worlds = UserManager.Instance?.Worlds;
        if (_labelWorldsLoading != null)
            _labelWorldsLoading.style.display = DisplayStyle.None;

        if (worlds == null || worlds.Count == 0)
        {
            var empty = new Label("ワールドがありません");
            empty.AddToClassList("menu-stub-text");
            _worldsList.Add(empty);
            return;
        }

        foreach (var world in worlds)
        {
            var card = BuildWorldCard(world);
            _worldsList.Add(card);
        }
    }

    private VisualElement BuildWorldCard(WorldResponse world)
    {
        var card = new VisualElement();
        card.AddToClassList("menu-world-card");

        var nameLabel = new Label(world.name ?? "World");
        nameLabel.AddToClassList("menu-world-name");
        card.Add(nameLabel);

        var playersLabel = new Label($"👥 {world.maxPlayers}人");
        playersLabel.AddToClassList("menu-world-players");
        card.Add(playersLabel);

        card.RegisterCallback<ClickEvent>(_ => OnWorldSelected?.Invoke(world));

        return card;
    }

    // ---- タブ切り替え ----------------------------------------------------------------

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

        // 遅延初期化: タブを開いたときに一度だけリストを構築
        if (content == _contentAvatar)
            BuildSlotAvatarList();
        else if (content == _contentWorlds && !_worldsLoaded)
        {
            _worldsLoaded = true;
            BuildWorldsList();
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
        _settingsTabController?.Unbind();
        HideAvatarConfirm();
    }
}
