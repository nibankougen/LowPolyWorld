using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// ワールド詳細・ルーム参加画面コントローラー。
/// recommended-join API を呼び出し、join/create/confirm_english アクションを処理する。
/// </summary>
public class WorldDetailController
{
    private readonly VisualElement _root;
    private readonly ApiClient _api;
    private readonly WorldResponse _world;

    private VisualElement _thumb;
    private Label _nameLabel;
    private Label _likesLabel;
    private Label _playersLabel;
    private Button _btnJoinPublic;
    private Button _btnCreateFriends;
    private Button _btnOtherRooms;
    private Button _btnBack;
    private VisualElement _loadingOverlay;
    private VisualElement _englishModal;
    private Button _btnEnglishOk;
    private Button _btnEnglishCancel;

    private CancellationTokenSource _cts;
    private string _pendingEnglishRoomId;

    private readonly List<Texture2D> _thumbTextures = new List<Texture2D>();

    public event Action OnBack;
    public event Action OnShowRoomList;
    public event Action<string, string, string> OnEnterWorld; // worldId, roomId, glbUrl

    public WorldDetailController(VisualElement root, ApiClient api, WorldResponse world)
    {
        _root = root;
        _api = api;
        _world = world;
        BindElements();
        PopulateWorldInfo();
    }

    private void BindElements()
    {
        _thumb = _root.Q<VisualElement>("detail-thumb");
        _nameLabel = _root.Q<Label>("detail-name");
        _likesLabel = _root.Q<Label>("detail-likes");
        _playersLabel = _root.Q<Label>("detail-players");
        _btnJoinPublic = _root.Q<Button>("btn-join-public");
        _btnCreateFriends = _root.Q<Button>("btn-create-friends");
        _btnOtherRooms = _root.Q<Button>("btn-other-rooms");
        _btnBack = _root.Q<Button>("btn-back");
        _loadingOverlay = _root.Q<VisualElement>("detail-loading");
        _englishModal = _root.Q<VisualElement>("english-modal");
        _btnEnglishOk = _root.Q<Button>("btn-english-ok");
        _btnEnglishCancel = _root.Q<Button>("btn-english-cancel");

        _btnBack.clicked += () => OnBack?.Invoke();
        _btnJoinPublic.clicked += OnJoinPublicClicked;
        _btnCreateFriends.clicked += OnCreateFriendsClicked;
        _btnOtherRooms.clicked += () => OnShowRoomList?.Invoke();
        _btnEnglishOk.clicked += OnEnglishOkClicked;
        _btnEnglishCancel.clicked += OnEnglishCancelClicked;
    }

    private void PopulateWorldInfo()
    {
        _nameLabel.text = _world.name;
        _likesLabel.text = $"♥ {_world.likesCount}";
        _playersLabel.text = $"▶ {_world.maxPlayers}人";

        if (!string.IsNullOrEmpty(_world.thumbnailUrl))
        {
            _cts = new CancellationTokenSource();
            _ = LoadThumbnailAsync(_world.thumbnailUrl, _thumb, _cts.Token);
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        foreach (var tex in _thumbTextures)
            if (tex != null)
                UnityEngine.Object.Destroy(tex);
        _thumbTextures.Clear();
    }

    // ── Join public room ──────────────────────────────────────────────────────

    private void OnJoinPublicClicked()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        _ = JoinPublicRoomAsync(_cts.Token);
    }

    private async Task JoinPublicRoomAsync(CancellationToken ct)
    {
        SetLoading(true);

        var path = $"/api/v1/worlds/{_world.id}/rooms/recommended-join";
        var (result, error) = await _api.PostJsonAsync<RecommendedJoinResponse>(path, new object(), ct);

        if (ct.IsCancellationRequested) return;
        SetLoading(false);

        if (error != null || result == null)
        {
            ShowError("ルームへの参加に失敗しました");
            return;
        }

        switch (result.action)
        {
            case "join":
                EnterWorld(_world.id, result.roomId);
                break;

            case "create":
                await CreateAndJoinRoomAsync("public", ct);
                break;

            case "confirm_english":
                _pendingEnglishRoomId = result.roomId;
                _englishModal.style.display = DisplayStyle.Flex;
                break;
        }
    }

    private void OnEnglishOkClicked()
    {
        _englishModal.style.display = DisplayStyle.None;
        if (string.IsNullOrEmpty(_pendingEnglishRoomId)) return;

        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        _ = JoinSpecificRoomAsync(_pendingEnglishRoomId, _cts.Token);
    }

    private void OnEnglishCancelClicked()
    {
        _englishModal.style.display = DisplayStyle.None;
        _pendingEnglishRoomId = null;

        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        _ = CreateAndJoinRoomAsync("public", _cts.Token);
    }

    private async Task JoinSpecificRoomAsync(string roomId, CancellationToken ct)
    {
        SetLoading(true);

        var path = $"/api/v1/rooms/{roomId}/join";
        var (_, error) = await _api.PostJsonAsync<RoomResponse>(path, new object(), ct);

        if (ct.IsCancellationRequested) return;
        SetLoading(false);

        if (error != null)
        {
            ShowError("ルームへの参加に失敗しました");
            return;
        }

        EnterWorld(_world.id, roomId);
    }

    // ── Create friends-only room ──────────────────────────────────────────────

    private void OnCreateFriendsClicked()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        _ = CreateAndJoinRoomAsync("friends_only", _cts.Token);
    }

    private async Task CreateAndJoinRoomAsync(string roomType, CancellationToken ct)
    {
        SetLoading(true);

        var path = $"/api/v1/worlds/{_world.id}/rooms";
        var body = new CreateRoomRequest { room_type = roomType };
        var (result, error) = await _api.PostJsonAsync<RoomResponse>(path, body, ct);

        if (ct.IsCancellationRequested) return;
        SetLoading(false);

        if (error != null || result == null)
        {
            ShowError("ルームの作成に失敗しました");
            return;
        }

        EnterWorld(_world.id, result.id);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void EnterWorld(string worldId, string roomId)
    {
        OnEnterWorld?.Invoke(worldId, roomId, _world.glbUrl);
    }

    private void SetLoading(bool visible)
    {
        _loadingOverlay.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        _btnJoinPublic.SetEnabled(!visible);
        _btnCreateFriends.SetEnabled(!visible);
        _btnOtherRooms.SetEnabled(!visible);
    }

    private static void ShowError(string message)
    {
        Debug.LogWarning($"[WorldDetail] {message}");
    }

    private async Task LoadThumbnailAsync(string url, VisualElement target, CancellationToken ct)
    {
        var client = new ApiClient("");
        var (data, _) = await client.GetBytesAsync(url, ct);
        if (ct.IsCancellationRequested || data == null || data.Length == 0) return;

        var tex = new Texture2D(2, 2);
        if (tex.LoadImage(data))
        {
            _thumbTextures.Add(tex);
            target.style.backgroundImage = new StyleBackground(tex);
        }
        else
        {
            UnityEngine.Object.Destroy(tex);
        }
    }
}
