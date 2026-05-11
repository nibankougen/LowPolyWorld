using System;
using System.Threading;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 招待リンク管理パネルコントローラー。
/// 招待制ルーム作成後に表示し、リンクのコピー・再発行・入室を提供する。
/// 仕様: screens-and-modes.md セクション 9.5
/// </summary>
public class InviteLinkPanelController : IDisposable
{
    private readonly VisualElement _root;
    private readonly Label _lblRoomInfo;
    private readonly Label _lblLink;
    private readonly Label _lblUsage;
    private readonly Label _lblExpires;
    private readonly Button _btnCopy;
    private readonly Button _btnRenew;
    private readonly Button _btnEnter;
    private readonly Button _btnClose;

    private string _roomId;
    private InviteLinkResponse _currentLink;
    private CancellationTokenSource _cts = new();

    public event Action OnEnterRoom;
    public event Action OnClose;

    public InviteLinkPanelController(VisualElement root)
    {
        _root = root;
        _lblRoomInfo = root.Q<Label>("lbl-room-info");
        _lblLink = root.Q<Label>("lbl-link");
        _lblUsage = root.Q<Label>("lbl-usage");
        _lblExpires = root.Q<Label>("lbl-expires");
        _btnCopy = root.Q<Button>("btn-copy");
        _btnRenew = root.Q<Button>("btn-renew");
        _btnEnter = root.Q<Button>("btn-enter");
        _btnClose = root.Q<Button>("btn-close");

        _btnCopy?.RegisterCallback<ClickEvent>(_ => OnCopyClicked());
        _btnRenew?.RegisterCallback<ClickEvent>(_ => OnRenewClicked());
        _btnEnter?.RegisterCallback<ClickEvent>(_ => OnEnterRoom?.Invoke());
        _btnClose?.RegisterCallback<ClickEvent>(_ => OnClose?.Invoke());

        // 背景タップでも閉じる
        root.Q<VisualElement>("invite-overlay")?.RegisterCallback<ClickEvent>(e =>
        {
            if (e.target == root.Q<VisualElement>("invite-overlay"))
                OnClose?.Invoke();
        });
    }

    /// <summary>パネルを表示してリンクを発行または取得する。</summary>
    public void ShowAndFetchLink(string roomId, int maxPlayers)
    {
        _roomId = roomId;
        if (_lblRoomInfo != null)
            _lblRoomInfo.text = $"最大 {maxPlayers} 人まで招待できます";

        _cts.Cancel();
        _cts.Dispose();
        _cts = new CancellationTokenSource();
        _ = FetchOrCreateLinkAsync(_cts.Token);
    }

    private async System.Threading.Tasks.Task FetchOrCreateLinkAsync(CancellationToken ct)
    {
        SetLoading(true);
        if (UserManager.Instance == null) return;
        var api = UserManager.Instance.Api;

        // まず既存リンクを取得、なければ新規発行
        var (existing, getErr) = await api.GetAsync<InviteLinkResponse>(
            $"/api/v1/rooms/{_roomId}/invite-link", ct);

        if (ct.IsCancellationRequested) return;

        if (getErr != null || existing == null)
        {
            // 既存リンクなし → 新規発行
            var (created, postErr) = await api.PostJsonAsync<InviteLinkResponse>(
                $"/api/v1/rooms/{_roomId}/invite-link", new object(), ct);
            if (ct.IsCancellationRequested) return;
            if (postErr == null) _currentLink = created;
        }
        else
        {
            _currentLink = existing;
        }

        SetLoading(false);
        UpdateUI();
    }

    private async void OnRenewClicked()
    {
        if (UserManager.Instance == null || string.IsNullOrEmpty(_roomId)) return;
        var api = UserManager.Instance.Api;
        SetLoading(true);

        var (link, err) = await api.PostJsonAsync<InviteLinkResponse>(
            $"/api/v1/rooms/{_roomId}/invite-link", new object(), _cts.Token);

        SetLoading(false);
        if (err != null)
        {
            FlashMessageController.Current?.Show("再発行に失敗しました", FlashMessageType.Error);
            return;
        }
        _currentLink = link;
        UpdateUI();
        FlashMessageController.Current?.Show("招待リンクを再発行しました");
    }

    private void OnCopyClicked()
    {
        if (_currentLink == null) return;
        var url = BuildInviteUrl(_currentLink.token);
        GUIUtility.systemCopyBuffer = url;
        FlashMessageController.Current?.Show("招待リンクをコピーしました");
    }

    private void UpdateUI()
    {
        if (_currentLink == null)
        {
            if (_lblLink != null) _lblLink.text = "リンクの取得に失敗しました";
            return;
        }

        if (_lblLink != null) _lblLink.text = BuildInviteUrl(_currentLink.token);
        if (_lblUsage != null) _lblUsage.text = $"使用回数: {_currentLink.useCount} / {_currentLink.maxUses}";
        if (_lblExpires != null) _lblExpires.text = $"有効期限: {FormatExpiry(_currentLink.expiresAt)}";
    }

    private static string BuildInviteUrl(string token) =>
        $"https://lowpolyworld.app/invite/{token}";

    private static string FormatExpiry(string iso8601)
    {
        if (!System.DateTime.TryParse(iso8601, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt))
            return "—";
        var remaining = dt - System.DateTime.UtcNow;
        if (remaining.TotalDays >= 1) return $"約 {(int)remaining.TotalDays} 日後";
        if (remaining.TotalHours >= 1) return $"約 {(int)remaining.TotalHours} 時間後";
        return "まもなく期限切れ";
    }

    private void SetLoading(bool loading)
    {
        _btnCopy?.SetEnabled(!loading);
        _btnRenew?.SetEnabled(!loading);
        _btnEnter?.SetEnabled(!loading);
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }
}
