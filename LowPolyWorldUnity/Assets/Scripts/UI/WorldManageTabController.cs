using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// ワールド管理タブ（セクション 11）の UI コントローラー。
/// 5 タブ構成（自分のワールド / 編集オブジェクト / 所有オブジェクト / 編集地形 / 所有地形）。
/// Phase 12 基盤: 「自分のワールド」タブのみフル実装。他タブはスタブ。
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class WorldManageTabController : MonoBehaviour
{
    private UIDocument _doc;
    private VisualElement _root;

    // タブボタン
    private Button _tabMyWorlds;
    private Button _tabEditObj;
    private Button _tabOwnedObj;
    private Button _tabEditTerr;
    private Button _tabOwnedTerr;

    // パネル
    private VisualElement _panelMyWorlds;
    private VisualElement _panelEditObj;
    private VisualElement _panelOwnedObj;
    private VisualElement _panelEditTerr;
    private VisualElement _panelOwnedTerr;

    // 自分のワールドタブ
    private Label _slotUsageLabel;
    private ScrollView _worldGrid;
    private VisualElement _emptyWorlds;

    private WorldSlotLogic _slotLogic;

    private void Awake() => _doc = GetComponent<UIDocument>();

    private void OnEnable()
    {
        _root = _doc.rootVisualElement.Q("world-manage-root");

        _tabMyWorlds = _root.Q<Button>("tab-my-worlds");
        _tabEditObj = _root.Q<Button>("tab-edit-obj");
        _tabOwnedObj = _root.Q<Button>("tab-owned-obj");
        _tabEditTerr = _root.Q<Button>("tab-edit-terr");
        _tabOwnedTerr = _root.Q<Button>("tab-owned-terr");

        _panelMyWorlds = _root.Q("panel-my-worlds");
        _panelEditObj = _root.Q("panel-edit-obj");
        _panelOwnedObj = _root.Q("panel-owned-obj");
        _panelEditTerr = _root.Q("panel-edit-terr");
        _panelOwnedTerr = _root.Q("panel-owned-terr");

        _slotUsageLabel = _root.Q<Label>("slot-usage-label");
        _worldGrid = _root.Q<ScrollView>("world-grid");
        _emptyWorlds = _root.Q("empty-worlds");

        _tabMyWorlds.clicked += () => SwitchTab(0);
        _tabEditObj.clicked += () => SwitchTab(1);
        _tabOwnedObj.clicked += () => SwitchTab(2);
        _tabEditTerr.clicked += () => SwitchTab(3);
        _tabOwnedTerr.clicked += () => SwitchTab(4);

        SwitchTab(0);
        RefreshWorldList();
    }

    // ── 公開 API ─────────────────────────────────────────────────────────────

    /// <summary>
    /// ワールドスロット一覧を表示する。HomeScreen 等から呼び出す。
    /// </summary>
    public void SetSlotLogic(WorldSlotLogic slotLogic)
    {
        _slotLogic = slotLogic;
        RefreshWorldList();
    }

    // ── タブ切り替え ─────────────────────────────────────────────────────────

    private void SwitchTab(int index)
    {
        var tabs = new[] { _tabMyWorlds, _tabEditObj, _tabOwnedObj, _tabEditTerr, _tabOwnedTerr };
        var panels = new[]
        {
            _panelMyWorlds,
            _panelEditObj,
            _panelOwnedObj,
            _panelEditTerr,
            _panelOwnedTerr,
        };

        for (int i = 0; i < tabs.Length; i++)
        {
            bool active = i == index;
            tabs[i].EnableInClassList("category-tab--active", active);
            panels[i].EnableInClassList("overlay-hidden", !active);
        }
    }

    // ── ワールド一覧更新 ─────────────────────────────────────────────────────

    private void RefreshWorldList()
    {
        if (_worldGrid == null) return;

        _worldGrid.Clear();

        if (_slotLogic == null)
        {
            ShowEmptyState(true);
            return;
        }

        var slots = _slotLogic.GetSlots();
        UpdateSlotUsageLabel(slots);

        bool hasFreeSlot = _slotLogic.CanCreate();

        // 新規作成カード (空きがある場合のみ)
        if (hasFreeSlot)
            _worldGrid.Add(BuildNewWorldCard());

        // 既存ワールドカード
        foreach (var slot in slots)
            _worldGrid.Add(BuildWorldCard(slot));

        bool isEmpty = slots.Count == 0 && !hasFreeSlot;
        ShowEmptyState(isEmpty);
    }

    private void UpdateSlotUsageLabel(IReadOnlyList<WorldSlotEntry> slots)
    {
        if (_slotUsageLabel == null || _slotLogic == null) return;
        _slotUsageLabel.text = $"{slots.Count} / {_slotLogic.Limit}";
    }

    private void ShowEmptyState(bool show)
    {
        _emptyWorlds?.EnableInClassList("overlay-hidden", !show);
    }

    // ── カードビルダー ────────────────────────────────────────────────────────

    private VisualElement BuildNewWorldCard()
    {
        var card = new VisualElement();
        card.AddToClassList("world-card");
        card.AddToClassList("world-card--new");

        var icon = new Label { text = "+" };
        icon.AddToClassList("world-card__new-icon");
        var label = new Label { text = "新規作成" };
        label.AddToClassList("world-card__new-label");

        card.Add(icon);
        card.Add(label);
        card.RegisterCallback<ClickEvent>(_ => OnNewWorldClicked());
        return card;
    }

    private VisualElement BuildWorldCard(WorldSlotEntry slot)
    {
        bool locked = _slotLogic.IsLocked(slot);

        var card = new VisualElement();
        card.AddToClassList("world-card");

        // サムネイル
        var thumb = new VisualElement();
        thumb.AddToClassList("world-card__thumbnail");
        card.Add(thumb);

        // フッター
        var footer = new VisualElement();
        footer.AddToClassList("world-card__footer");

        var nameLabel = new Label { text = slot.WorldName };
        nameLabel.AddToClassList("world-card__name");
        footer.Add(nameLabel);

        var badge = new Label();
        badge.AddToClassList("world-card__badge");
        if (slot.PublishedVersion == 0)
        {
            badge.text = "未公開";
            badge.AddToClassList("world-card__badge--unpublished");
        }
        else if (slot.IsPublic)
        {
            badge.text = "公開中";
            badge.AddToClassList("world-card__badge--public");
        }
        else
        {
            badge.text = "非公開";
            badge.AddToClassList("world-card__badge--private");
        }
        footer.Add(badge);
        card.Add(footer);

        // ロックオーバーレイ (プレミアム解約後スロット)
        if (locked)
        {
            var overlay = new VisualElement();
            overlay.AddToClassList("world-card__locked-overlay");
            var lockIcon = new Label { text = "🔒" };
            lockIcon.AddToClassList("world-card__lock-icon");
            overlay.Add(lockIcon);
            card.Add(overlay);
        }
        else
        {
            card.RegisterCallback<ClickEvent>(_ => OnWorldCardClicked(slot));
        }

        return card;
    }

    // ── イベントハンドラ ─────────────────────────────────────────────────────

    private void OnNewWorldClicked()
    {
        // TODO: Phase 12 — テンプレート選択モーダルを表示してワールドエディタへ遷移
        Debug.Log("[WorldManageTab] 新規作成ボタンがタップされました");
    }

    private void OnWorldCardClicked(WorldSlotEntry slot)
    {
        // TODO: Phase 12 — ワールドエディタ（WorldEditor シーン or オーバーレイ）へ遷移
        Debug.Log($"[WorldManageTab] ワールド '{slot.WorldName}' ({slot.WorldId}) が選択されました");
    }
}
