using System.Collections.Generic;

/// <summary>
/// ユーザーが所有するワールドスロットを管理するロジッククラス。
/// 通常ユーザー: 5 スロット / プレミアム: 50 スロット。
/// プレミアム解約後は 6 個目以降のスロットをロックする（データ保持・ロード不可）。
/// </summary>
public class WorldSlotLogic
{
    public const int NormalLimit = 5;
    public const int PremiumLimit = 50;

    private readonly List<WorldSlotEntry> _slots;

    public int Count => _slots.Count;
    public int Limit { get; }
    public bool IsPremium { get; }

    public WorldSlotLogic(IEnumerable<WorldSlotEntry> existingSlots, bool isPremium)
    {
        _slots = new List<WorldSlotEntry>(existingSlots);
        IsPremium = isPremium;
        Limit = isPremium ? PremiumLimit : NormalLimit;
    }

    /// <summary>新規ワールドを作成できるか。</summary>
    public bool CanCreate() => _slots.Count < Limit;

    /// <summary>
    /// 新規ワールドスロットを追加する。
    /// </summary>
    /// <returns>追加された <see cref="WorldSlotEntry"/>。上限到達時は null。</returns>
    public WorldSlotEntry TryCreate(string worldName)
    {
        if (!CanCreate())
            return null;
        var entry = new WorldSlotEntry(GenerateLocalId(), worldName);
        _slots.Add(entry);
        return entry;
    }

    /// <summary>スロットを削除してスロット番号を解放する。</summary>
    public void Remove(string worldId)
    {
        _slots.RemoveAll(s => s.WorldId == worldId);
    }

    /// <summary>
    /// 指定スロットがロック状態か判定する。
    /// プレミアム解約後、通常上限 (<see cref="NormalLimit"/>) を超えるインデックスのスロットはロックされる。
    /// このリストに存在しないエントリを渡した場合は true（ロック）を返す。
    /// </summary>
    public bool IsLocked(WorldSlotEntry entry)
    {
        if (IsPremium)
            return false;
        int index = _slots.FindIndex(s => s.WorldId == entry.WorldId);
        return index < 0 || index >= NormalLimit;
    }

    /// <returns>全スロットの読み取り専用リスト。</returns>
    public IReadOnlyList<WorldSlotEntry> GetSlots() => _slots.AsReadOnly();

    private int _localIdCounter;

    private string GenerateLocalId() => $"local_{++_localIdCounter}";
}

/// <summary>
/// ワールドスロット 1 件のメタデータ。
/// </summary>
public class WorldSlotEntry
{
    public string WorldId { get; }
    public string WorldName { get; set; }
    public bool IsPublic { get; set; }
    public int PublishedVersion { get; set; } // 0 = 未公開
    public string ThumbnailUrl { get; set; }

    public WorldSlotEntry(string worldId, string worldName)
    {
        WorldId = worldId;
        WorldName = worldName;
    }
}
