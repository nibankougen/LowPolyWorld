/// <summary>
/// ワールド参加時のセッションデータをシーン間で受け渡す静的ストア。
/// HomeScene → WorldScene のシーン遷移前に書き込み、WorldScene 起動時に読み出す。
/// </summary>
public static class WorldSessionData
{
    public static string WorldId { get; private set; }
    public static string RoomId { get; private set; }
    public static string WorldGlbUrl { get; private set; }

    public static void Set(string worldId, string roomId, string worldGlbUrl)
    {
        WorldId = worldId;
        RoomId = roomId;
        WorldGlbUrl = worldGlbUrl;
    }

    public static void Clear()
    {
        WorldId = null;
        RoomId = null;
        WorldGlbUrl = null;
    }
}
