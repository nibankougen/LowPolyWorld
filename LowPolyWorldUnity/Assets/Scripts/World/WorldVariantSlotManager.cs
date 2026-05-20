using System.Collections.Generic;

/// <summary>
/// ワールドの保存バリアントスロットを管理するロジッククラス。
/// 通常ユーザー: 最大 10 スロット / プレミアム: 最大 100 スロット。
/// プレミアム解約後は既存スロットを保持するが上限を超えた状態では新規追加をブロックする。
/// </summary>
public class WorldVariantSlotManager
{
    public const int NormalLimit = 10;
    public const int PremiumLimit = 100;

    private readonly List<string> _slots;

    public int Count => _slots.Count;
    public int Limit { get; }

    public WorldVariantSlotManager(IEnumerable<string> existingSlotIds, bool isPremium)
    {
        _slots = new List<string>(existingSlotIds);
        Limit = isPremium ? PremiumLimit : NormalLimit;
    }

    public bool CanAdd() => _slots.Count < Limit;

    public bool TryAdd(string slotId)
    {
        if (!CanAdd())
            return false;
        _slots.Add(slotId);
        return true;
    }

    public void Remove(string slotId) => _slots.Remove(slotId);

    public IReadOnlyList<string> GetSlots() => _slots.AsReadOnly();
}
