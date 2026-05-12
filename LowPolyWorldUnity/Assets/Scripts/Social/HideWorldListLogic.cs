using System.Collections.Generic;

/// <summary>
/// 非表示ワールドリストのローカル管理（純粋 C#）。
/// HideManager が初期化・更新し、WorldListController がフィルタリングに使用する。
/// </summary>
public class HideWorldListLogic
{
    private readonly HashSet<string> _hiddenWorldIds = new();

    public int Count => _hiddenWorldIds.Count;

    public void Add(string worldId) => _hiddenWorldIds.Add(worldId);

    public void Remove(string worldId) => _hiddenWorldIds.Remove(worldId);

    /// <summary>サーバーから取得したリストで全件置換する。</summary>
    public void SetAll(IEnumerable<string> worldIds)
    {
        _hiddenWorldIds.Clear();
        foreach (var id in worldIds)
            _hiddenWorldIds.Add(id);
    }

    public IReadOnlyCollection<string> GetAll() => _hiddenWorldIds;

    public bool IsHidden(string worldId) => _hiddenWorldIds.Contains(worldId);
}
