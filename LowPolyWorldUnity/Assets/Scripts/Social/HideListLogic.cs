using System.Collections.Generic;

/// <summary>
/// 非表示ユーザーリストのローカル管理（純粋 C#）。
/// ルーム参加時にサーバーから取得したリストを SetAll で初期化し、
/// 追加・解除は Add / Remove で行う。
/// 描画・音声のフィルタリング判定を提供する。
/// </summary>
public class HideListLogic
{
    private readonly HashSet<string> _hiddenUserIds = new();

    public int Count => _hiddenUserIds.Count;

    public void Add(string userId) => _hiddenUserIds.Add(userId);

    public void Remove(string userId) => _hiddenUserIds.Remove(userId);

    /// <summary>サーバーから取得したリストで全件置換する。</summary>
    public void SetAll(IEnumerable<string> userIds)
    {
        _hiddenUserIds.Clear();
        foreach (var id in userIds)
            _hiddenUserIds.Add(id);
    }

    public IReadOnlyCollection<string> GetAll() => _hiddenUserIds;

    public bool IsHidden(string userId) => _hiddenUserIds.Contains(userId);

    /// <summary>アバター描画をスキップすべきか。非表示リストに含まれる場合のみ true。</summary>
    public bool ShouldSkipRendering(string userId) => _hiddenUserIds.Contains(userId);

    /// <summary>Vivox 音声をミュートすべきか。非表示リストに含まれる場合のみ true。</summary>
    public bool ShouldMuteVoice(string userId) => _hiddenUserIds.Contains(userId);
}
