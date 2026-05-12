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

    public SelectableAvatar(
        string id,
        string name,
        string vrmUrl,
        string vrmHash,
        string thumbnailUrl,
        AvatarSource source
    )
    {
        Id = id;
        Name = name;
        VrmUrl = vrmUrl;
        VrmHash = vrmHash;
        ThumbnailUrl = thumbnailUrl;
        Source = source;
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

    /// <summary>スロットアバター一覧を読み込む。最初のアバターを自動選択する。</summary>
    public void LoadSlotAvatars(IEnumerable<StartupAvatar> avatars)
    {
        SlotAvatars.Clear();
        foreach (var a in avatars)
        {
            if (string.IsNullOrEmpty(a.vrmUrl)) continue;
            SlotAvatars.Add(
                new SelectableAvatar(a.id, a.name, a.vrmUrl, a.vrmHash, a.textureUrl, AvatarSource.Slot)
            );
        }
        if (SelectedAvatar == null && SlotAvatars.Count > 0)
            SelectedAvatar = SlotAvatars[0];
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
