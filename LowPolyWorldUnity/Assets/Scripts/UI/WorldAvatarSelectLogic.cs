using System.Collections.Generic;

/// <summary>アバター選択画面のタブ種別。</summary>
public enum AvatarSelectTab
{
    Slot,
    Purchased,
}

/// <summary>アバターの参照元（スロットまたは購入済み直接利用）。</summary>
public enum AvatarSource
{
    Slot,
    DirectPurchase,
}

/// <summary>アバター選択画面で一行として表示される選択候補。</summary>
public class SelectableAvatar
{
    public readonly string Id;
    public readonly string Name;
    public readonly string VrmUrl;
    public readonly string VrmHash;
    public readonly string ThumbnailUrl;
    public readonly AvatarSource Source;

    /// <summary>スロット上限超過によりロックされているか（プレミアム解約後のスロットロック表示に使用）。</summary>
    public readonly bool IsLocked;

    /// <summary>モデレーションステータス。"pending" / "approved" / "rejected" のいずれか。</summary>
    public readonly string ModerationStatus;

    public bool IsPending => ModerationStatus == "pending";
    public bool IsRejected => ModerationStatus == "rejected";

    public SelectableAvatar(
        string id,
        string name,
        string vrmUrl,
        string vrmHash,
        string thumbnailUrl,
        AvatarSource source,
        bool isLocked = false,
        string moderationStatus = "approved"
    )
    {
        Id = id;
        Name = name;
        VrmUrl = vrmUrl;
        VrmHash = vrmHash;
        ThumbnailUrl = thumbnailUrl;
        Source = source;
        IsLocked = isLocked;
        ModerationStatus = string.IsNullOrEmpty(moderationStatus) ? "approved" : moderationStatus;
    }
}

/// <summary>
/// ワールドモード入場前のアバター選択ロジック（純粋 C#）。
/// スロットアバター一覧・購入済みアバター一覧・選択状態・アクティブタブを管理する。
/// </summary>
public class WorldAvatarSelectLogic
{
    public List<SelectableAvatar> SlotAvatars { get; } = new();
    public List<SelectableAvatar> PurchasedAvatars { get; } = new();
    public SelectableAvatar SelectedAvatar { get; private set; }
    public AvatarSelectTab ActiveTab { get; private set; } = AvatarSelectTab.Slot;

    public bool HasSelection => SelectedAvatar != null;

    public IReadOnlyList<SelectableAvatar> ActiveList =>
        ActiveTab == AvatarSelectTab.Slot
            ? (IReadOnlyList<SelectableAvatar>)SlotAvatars
            : PurchasedAvatars;

    /// <summary>
    /// スロットアバター一覧を読み込む。最初の使用可能アバターを自動選択する。
    /// slotLimit 以降のアバターは IsLocked = true になる（プレミアム解約後のスロットロック表示用）。
    /// </summary>
    public void LoadSlotAvatars(IEnumerable<StartupAvatar> avatars, int slotLimit = int.MaxValue)
    {
        SlotAvatars.Clear();
        int index = 0;
        foreach (var a in avatars)
        {
            if (string.IsNullOrEmpty(a.vrmUrl)) continue;
            bool isLocked = index >= slotLimit;
            SlotAvatars.Add(
                new SelectableAvatar(
                    a.id, a.name, a.vrmUrl, a.vrmHash, a.textureUrl,
                    AvatarSource.Slot, isLocked, a.moderationStatus)
            );
            index++;
        }
        if (SelectedAvatar == null)
        {
            // 非ロック・非拒否のアバターを優先して自動選択
            SelectedAvatar = SlotAvatars.Find(av => !av.IsLocked && !av.IsRejected)
                ?? (SlotAvatars.Count > 0 ? SlotAvatars[0] : null);
        }
    }

    /// <summary>購入済みアバター一覧を読み込む（category == "avatar" のみ）。</summary>
    public void LoadPurchasedAvatars(IEnumerable<MyProductEntry> purchases)
    {
        PurchasedAvatars.Clear();
        foreach (var p in purchases)
        {
            if (p.product?.category != "avatar") continue;
            if (string.IsNullOrEmpty(p.product.assetUrl)) continue;
            PurchasedAvatars.Add(
                new SelectableAvatar(
                    p.productId,
                    p.product.name,
                    p.product.assetUrl,
                    p.product.assetHash,
                    p.product.thumbnailUrl,
                    AvatarSource.DirectPurchase
                )
            );
        }
    }

    public void Select(SelectableAvatar avatar) => SelectedAvatar = avatar;

    public void SetActiveTab(AvatarSelectTab tab) => ActiveTab = tab;
}
