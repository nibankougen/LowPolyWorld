using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// ワールドモード入場前のアバター選択画面コントローラー。
/// スロットアバター一覧・購入済みアバター一覧を表示し、選択後にダウンロードを経由して
/// UserManager.SelectAvatar を呼び出す。
/// 仕様: screens-and-modes.md セクション 2.2
/// </summary>
public class WorldAvatarSelectController : IDisposable
{
    private readonly VisualElement _root;
    private readonly WorldAvatarSelectLogic _logic = new();

    private Button _btnCancel;
    private Button _btnConfirm;
    private Button _btnTabSlot;
    private Button _btnTabPurchased;
    private ScrollView _avatarList;
    private VisualElement _loadingOverlay;
    private VisualElement _emptyState;

    private CancellationTokenSource _cts = new();
    private readonly List<Texture2D> _textures = new();

    public event Action OnCanceled;
    public event Action OnConfirmed;

    public WorldAvatarSelectController(VisualElement root)
    {
        _root = root;
        BindElements();
        _ = LoadAvatarsAsync(_cts.Token);
    }

    private void BindElements()
    {
        _btnCancel = _root.Q<Button>("btn-cancel");
        _btnConfirm = _root.Q<Button>("btn-confirm");
        _btnTabSlot = _root.Q<Button>("btn-tab-slot");
        _btnTabPurchased = _root.Q<Button>("btn-tab-purchased");
        _avatarList = _root.Q<ScrollView>("avatar-list");
        _loadingOverlay = _root.Q<VisualElement>("loading-overlay");
        _emptyState = _root.Q<VisualElement>("empty-state");

        _btnCancel?.RegisterCallback<ClickEvent>(_ => OnCanceled?.Invoke());
        _btnConfirm?.RegisterCallback<ClickEvent>(_ => OnConfirmClicked());
        _btnTabSlot?.RegisterCallback<ClickEvent>(_ => SwitchTab(AvatarSelectTab.Slot));
        _btnTabPurchased?.RegisterCallback<ClickEvent>(_ => SwitchTab(AvatarSelectTab.Purchased));

        UpdateConfirmButton();
    }

    private async Task LoadAvatarsAsync(CancellationToken ct)
    {
        SetLoading(true);

        // Slot avatars from startup data (pass slot limit for premium downgrade lock display)
        if (UserManager.Instance?.Avatars != null)
        {
            int slotLimit = UserManager.Instance.Capabilities?.avatarSlots ?? int.MaxValue;
            _logic.LoadSlotAvatars(UserManager.Instance.Avatars, slotLimit);
        }

        // Purchased avatars from API
        if (UserManager.Instance?.Api != null)
        {
            var (result, _) = await UserManager.Instance.Api.GetAsync<MyProductListResponse>(
                "/api/v1/me/purchases?category=avatar&limit=100", ct);
            if (!ct.IsCancellationRequested && result?.products != null)
                _logic.LoadPurchasedAvatars(result.products);
        }

        if (ct.IsCancellationRequested) return;

        SetLoading(false);
        RefreshList();
        UpdateTabButtons();
        UpdateConfirmButton();
    }

    private void SwitchTab(AvatarSelectTab tab)
    {
        _logic.SetActiveTab(tab);
        UpdateTabButtons();
        RefreshList();
    }

    private void UpdateTabButtons()
    {
        _btnTabSlot?.EnableInClassList("tab--active", _logic.ActiveTab == AvatarSelectTab.Slot);
        _btnTabPurchased?.EnableInClassList("tab--active", _logic.ActiveTab == AvatarSelectTab.Purchased);
    }

    private void RefreshList()
    {
        _avatarList?.Clear();
        var list = _logic.ActiveList;

        bool isEmpty = list.Count == 0;
        if (_emptyState != null)
            _emptyState.style.display = isEmpty ? DisplayStyle.Flex : DisplayStyle.None;
        if (_avatarList != null)
            _avatarList.style.display = isEmpty ? DisplayStyle.None : DisplayStyle.Flex;

        foreach (var avatar in list)
            _avatarList?.Add(BuildAvatarCard(avatar));
    }

    private VisualElement BuildAvatarCard(SelectableAvatar avatar)
    {
        var card = new VisualElement();
        card.AddToClassList("avatar-card");

        bool isSelected = _logic.SelectedAvatar == avatar;
        if (isSelected) card.AddToClassList("avatar-card--selected");
        if (avatar.IsLocked) card.AddToClassList("avatar-card--locked");

        var thumb = new VisualElement();
        thumb.AddToClassList("avatar-card__thumb");
        card.Add(thumb);

        var name = new Label(avatar.Name);
        name.AddToClassList("avatar-card__name");
        card.Add(name);

        if (avatar.Source == AvatarSource.DirectPurchase)
        {
            var badge = new Label("購入済み");
            badge.AddToClassList("avatar-card__badge");
            card.Add(badge);
        }

        if (avatar.IsLocked)
        {
            var lockBadge = new Label("ロック中");
            lockBadge.AddToClassList("avatar-card__lock-badge");
            card.Add(lockBadge);
        }
        else
        {
            card.RegisterCallback<ClickEvent>(_ =>
            {
                _logic.Select(avatar);
                RefreshList(); // Rebuild to update selection highlight
                UpdateConfirmButton();
            });
        }

        if (!string.IsNullOrEmpty(avatar.ThumbnailUrl))
            _ = LoadThumbnailAsync(avatar.ThumbnailUrl, thumb, _cts.Token);

        return card;
    }

    private async Task LoadThumbnailAsync(string url, VisualElement target, CancellationToken ct)
    {
        var client = new ApiClient("");
        var (data, _) = await client.GetBytesAsync(url, ct);
        if (ct.IsCancellationRequested || data == null || data.Length == 0) return;

        var tex = new Texture2D(2, 2);
        if (tex.LoadImage(data))
        {
            _textures.Add(tex);
            target.style.backgroundImage = new StyleBackground(tex);
        }
        else
        {
            UnityEngine.Object.Destroy(tex);
        }
    }

    private void UpdateConfirmButton()
    {
        if (_btnConfirm == null) return;
        bool canConfirm = _logic.HasSelection && !(_logic.SelectedAvatar?.IsLocked ?? false);
        _btnConfirm.SetEnabled(canConfirm);
        _btnConfirm.EnableInClassList("btn--disabled", !canConfirm);
    }

    private void OnConfirmClicked()
    {
        var selected = _logic.SelectedAvatar;
        if (selected == null || selected.IsLocked) return;
        _ = DownloadAndConfirmAsync(selected, _cts.Token);
    }

    private async Task DownloadAndConfirmAsync(SelectableAvatar avatar, CancellationToken ct)
    {
        SetLoading(true);

        bool isOwn = avatar.Source == AvatarSource.Slot;
        var (localPath, error) = await CacheManager.Instance.GetOrDownloadAsync(
            avatar.VrmUrl, avatar.VrmHash, "vrm", isOwn, ct);

        if (ct.IsCancellationRequested) return;
        SetLoading(false);

        if (error != null)
        {
            Debug.LogWarning($"[WorldAvatarSelect] VRM download failed: {error}");
            FlashMessageController.Current?.Show("アバターの読み込みに失敗しました", FlashMessageType.Error);
            return;
        }

        UserManager.Instance?.SelectAvatar(localPath);
        OnConfirmed?.Invoke();
    }

    private void SetLoading(bool visible)
    {
        if (_loadingOverlay != null)
            _loadingOverlay.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        _btnConfirm?.SetEnabled(!visible && _logic.HasSelection);
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
        foreach (var tex in _textures)
            if (tex != null)
                UnityEngine.Object.Destroy(tex);
        _textures.Clear();
    }
}
