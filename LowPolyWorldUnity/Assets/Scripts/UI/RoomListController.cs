using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.UIElements;

/// <summary>
/// その他ルーム画面コントローラー。公開ルーム一覧表示と各種ルーム作成を提供する。
/// </summary>
public class RoomListController
{
    private readonly VisualElement _root;
    private readonly ApiClient _api;
    private readonly WorldResponse _world;
    private readonly bool _hasPremium;

    private VisualElement _roomItems;
    private VisualElement _roomEmpty;
    private VisualElement _roomLoading;
    private Button _btnBack;
    private Button _btnCreatePublic;
    private Button _btnCreateFollowers;
    private Button _btnCreateInvite;

    private CancellationTokenSource _cts;

    public event Action OnBack;
    public event Action<string, string, string> OnEnterWorld; // worldId, roomId, glbUrl

    public RoomListController(VisualElement root, ApiClient api, WorldResponse world, bool hasPremium)
    {
        _root = root;
        _api = api;
        _world = world;
        _hasPremium = hasPremium;
        BindElements();
    }

    private void BindElements()
    {
        _roomItems = _root.Q<VisualElement>("room-items");
        _roomEmpty = _root.Q<VisualElement>("room-empty");
        _roomLoading = _root.Q<VisualElement>("room-loading");
        _btnBack = _root.Q<Button>("btn-back");
        _btnCreatePublic = _root.Q<Button>("btn-create-public");
        _btnCreateFollowers = _root.Q<Button>("btn-create-followers");
        _btnCreateInvite = _root.Q<Button>("btn-create-invite");

        _btnBack.clicked += () => OnBack?.Invoke();
        _btnCreatePublic.clicked += () => OnCreateRoom("public");
        _btnCreateFollowers.clicked += () => OnCreateRoom("followers_only");
        _btnCreateInvite.clicked += OnCreateInviteClicked;

        if (!_hasPremium)
            _btnCreateInvite.SetEnabled(false);
    }

    public void Initialize()
    {
        _cts = new CancellationTokenSource();
        _ = LoadRoomsAsync(_cts.Token);
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    // ── Load rooms ────────────────────────────────────────────────────────────

    private async Task LoadRoomsAsync(CancellationToken ct)
    {
        _roomLoading.style.display = DisplayStyle.Flex;
        _roomItems.Clear();
        _roomEmpty.style.display = DisplayStyle.None;

        var path = $"/api/v1/worlds/{_world.id}/rooms";
        var (result, error) = await _api.GetAsync<List<RoomResponse>>(path, ct);

        if (ct.IsCancellationRequested) return;
        _roomLoading.style.display = DisplayStyle.None;

        if (error != null || result == null || result.Count == 0)
        {
            _roomEmpty.style.display = DisplayStyle.Flex;
            return;
        }

        foreach (var room in result)
            _roomItems.Add(BuildRoomCard(room));
    }

    private VisualElement BuildRoomCard(RoomResponse room)
    {
        var card = new VisualElement();
        card.AddToClassList("room-card");

        var lang = new Label(room.language?.ToUpper() ?? "?");
        lang.AddToClassList("room-card__lang");
        card.Add(lang);

        var players = new Label($"{room.currentPlayers} / {room.maxPlayers}人");
        players.AddToClassList("room-card__players");
        card.Add(players);

        var isFull = room.currentPlayers >= room.maxPlayers;

        if (isFull)
        {
            var badge = new Label("満員");
            badge.AddToClassList("room-card__badge");
            card.Add(badge);
        }

        var joinBtn = new Button();
        joinBtn.text = isFull ? "満員" : "参加";
        joinBtn.AddToClassList("room-card__join-btn");
        if (isFull)
            joinBtn.AddToClassList("room-card__join-btn--full");
        joinBtn.SetEnabled(!isFull);
        joinBtn.clicked += () => OnJoinRoom(room.id);
        card.Add(joinBtn);

        return card;
    }

    // ── Room actions ──────────────────────────────────────────────────────────

    private void OnJoinRoom(string roomId)
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        _ = JoinRoomAsync(roomId, _cts.Token);
    }

    private async Task JoinRoomAsync(string roomId, CancellationToken ct)
    {
        SetButtonsEnabled(false);

        var path = $"/api/v1/rooms/{roomId}/join";
        var (_, error) = await _api.PostJsonAsync<RoomResponse>(path, new object(), ct);

        if (ct.IsCancellationRequested) return;

        if (error != null)
        {
            SetButtonsEnabled(true);
            return;
        }

        OnEnterWorld?.Invoke(_world.id, roomId, _world.glbUrl);
    }

    private void OnCreateRoom(string roomType)
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        _ = CreateAndJoinRoomAsync(roomType, _cts.Token);
    }

    private void OnCreateInviteClicked()
    {
        if (!_hasPremium) return;
        OnCreateRoom("invite_only");
    }

    private async Task CreateAndJoinRoomAsync(string roomType, CancellationToken ct)
    {
        SetButtonsEnabled(false);

        var path = $"/api/v1/worlds/{_world.id}/rooms";
        var body = new CreateRoomRequest { room_type = roomType };
        var (result, error) = await _api.PostJsonAsync<RoomResponse>(path, body, ct);

        if (ct.IsCancellationRequested) return;

        if (error != null || result == null)
        {
            SetButtonsEnabled(true);
            return;
        }

        OnEnterWorld?.Invoke(_world.id, result.id, _world.glbUrl);
    }

    private void SetButtonsEnabled(bool enabled)
    {
        _btnCreatePublic.SetEnabled(enabled);
        _btnCreateFollowers.SetEnabled(enabled);
        if (_hasPremium)
            _btnCreateInvite.SetEnabled(enabled);
    }
}
