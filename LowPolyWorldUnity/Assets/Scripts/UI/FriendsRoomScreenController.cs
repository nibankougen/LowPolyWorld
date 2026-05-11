using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// フレンドがいるルーム一覧画面コントローラー。
/// 仕様: screens-and-modes.md セクション 2.15
/// </summary>
public class FriendsRoomScreenController : IDisposable
{
    private readonly VisualElement _root;
    private readonly ScrollView _list;
    private readonly VisualElement _empty;
    private readonly VisualElement _loading;

    // worldId → worldName のキャッシュ（Startup で取得したもの）
    private readonly Dictionary<string, string> _worldNames;

    private List<RoomResponse> _rooms = new();
    private CancellationTokenSource _cts = new();

    public event Action OnBackRequested;
    public event Action<string, string, string> OnEnterWorld; // worldId, roomId, glbUrl

    public FriendsRoomScreenController(VisualElement root, Dictionary<string, string> worldNames = null)
    {
        _root = root;
        _list = root.Q<ScrollView>("room-list");
        _empty = root.Q<VisualElement>("empty-state");
        _loading = root.Q<VisualElement>("loading-state");
        _worldNames = worldNames ?? new Dictionary<string, string>();

        root.Q<Button>("btn-back")?.RegisterCallback<ClickEvent>(_ => OnBackRequested?.Invoke());

        LoadAsync();
    }

    private async void LoadAsync()
    {
        if (UserManager.Instance == null) return;

        _loading?.RemoveFromClassList("overlay-hidden");
        _empty?.AddToClassList("overlay-hidden");
        _list?.Clear();

        var api = UserManager.Instance.Api;
        var ct = _cts.Token;

        try
        {
            var (res, _) = await api.GetAsync<RoomListResponse>("/api/v1/me/friends/rooms", ct);
            _rooms = res?.data ?? new List<RoomResponse>();
        }
        catch (OperationCanceledException) { return; }
        catch (Exception e)
        {
            Debug.LogWarning($"[FriendsRoomScreen] load failed: {e.Message}");
        }

        _loading?.AddToClassList("overlay-hidden");
        Refresh();
    }

    private void Refresh()
    {
        _list?.Clear();

        if (_rooms.Count == 0)
        {
            _empty?.RemoveFromClassList("overlay-hidden");
            return;
        }

        _empty?.AddToClassList("overlay-hidden");
        foreach (var room in _rooms)
            _list?.Add(BuildRoomCard(room));
    }

    private VisualElement BuildRoomCard(RoomResponse room)
    {
        var card = new VisualElement();
        card.AddToClassList("room-card");

        // ワールド名
        _worldNames.TryGetValue(room.worldId, out var worldName);
        var worldLabel = new Label(worldName ?? $"ワールド {room.worldId[..Math.Min(8, room.worldId.Length)]}...");
        worldLabel.AddToClassList("room-card__world-name");
        card.Add(worldLabel);

        // ルーム種別
        var typeLabel = new Label(RoomTypeToJapanese(room.roomType));
        typeLabel.AddToClassList("room-card__room-type");
        card.Add(typeLabel);

        // 人数 + 参加ボタン
        var bottom = new VisualElement();
        bottom.AddToClassList("room-card__bottom");

        var players = new Label($"{room.currentPlayers} / {room.maxPlayers}人");
        players.AddToClassList("room-card__players");
        bottom.Add(players);

        var isFull = room.currentPlayers >= room.maxPlayers;
        var joinBtn = new Button(() => OnJoinClicked(room));
        joinBtn.text = isFull ? "満員" : "参加";
        joinBtn.AddToClassList("room-card__join-btn");
        if (isFull) joinBtn.AddToClassList("room-card__join-btn--full");
        joinBtn.SetEnabled(!isFull);
        bottom.Add(joinBtn);

        card.Add(bottom);
        return card;
    }

    private static string RoomTypeToJapanese(string roomType) =>
        roomType switch
        {
            "public" => "公開ルーム",
            "friends_only" => "フレンドのみ",
            "followers_only" => "フォロワー限定",
            "invite_only" => "招待制",
            _ => roomType,
        };

    private void OnJoinClicked(RoomResponse room)
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        _ = JoinRoomAsync(room, _cts.Token);
    }

    private async System.Threading.Tasks.Task JoinRoomAsync(RoomResponse room, CancellationToken ct)
    {
        if (UserManager.Instance == null) return;
        var api = UserManager.Instance.Api;

        var path = $"/api/v1/rooms/{room.id}/join";
        var (_, error) = await api.PostJsonAsync<RoomResponse>(path, new object(), ct);

        if (ct.IsCancellationRequested) return;

        if (error != null)
        {
            FlashMessageController.Current?.Show("ルームへの参加に失敗しました", FlashMessageType.Error);
            return;
        }

        // GLB URL を取得してからワールドへ入る
        var (world, worldErr) = await api.GetAsync<WorldResponse>($"/api/v1/worlds/{room.worldId}", ct);
        if (ct.IsCancellationRequested) return;

        var glbUrl = worldErr == null && world != null ? world.glbUrl : string.Empty;
        OnEnterWorld?.Invoke(room.worldId, room.id, glbUrl);
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
        _list?.Clear();
    }
}
