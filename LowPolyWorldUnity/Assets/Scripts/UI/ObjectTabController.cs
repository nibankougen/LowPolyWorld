using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// オブジェクトタブの UI 制御（screens-and-modes.md 11.7.3）。WorldEditorController から生成される。
///
/// サブタブ（利用中 / 保存・編集 / 所有）・所有パレット一覧・利用中一覧・
/// 選択ツールバー（複製 / 削除 / W·D·H ステッパー）・オブジェクト数表示・フラッシュを担当する。
/// 実際の配置処理（ObjectPlacementStore / 3D ビュー）への接続はイベント経由で
/// 上位レイヤー（ObjectEditSceneController）が行う。
/// </summary>
public class ObjectTabController
{
    /// <summary>所有パレットの 1 項目。</summary>
    public readonly struct PaletteItem
    {
        public readonly string TypeId;
        public readonly string Name;
        public readonly Color Swatch;

        public PaletteItem(string typeId, string name, Color swatch)
        {
            TypeId = typeId;
            Name = name;
            Swatch = swatch;
        }
    }

    /// <summary>利用中一覧の 1 項目。</summary>
    public readonly struct UsedItem
    {
        public readonly string InstanceId;
        public readonly string Name;
        public readonly int Cost;
        public readonly Color Swatch;

        public UsedItem(string instanceId, string name, int cost, Color swatch)
        {
            InstanceId = instanceId;
            Name = name;
            Cost = cost;
            Swatch = swatch;
        }
    }

    public event Action<int> SubTabChanged;          // 0 = 利用中 / 1 = 保存・編集 / 2 = 所有
    public event Action<string> PaletteObjectClicked; // typeId
    public event Action<string> UsedObjectSelected;   // instanceId
    public event Action DuplicateClicked;
    public event Action DeleteClicked;
    public event Action<int, int> ScaleStep;          // axis(0=W,1=D,2=H), delta(±1)

    private readonly Button[] _subTabs;
    private readonly VisualElement[] _subContainers;
    private readonly VisualElement _ownedList;
    private readonly VisualElement _usedList;
    private readonly VisualElement _selectionBar;
    private readonly Label _countLabel;
    private readonly Label _flash;
    private readonly Label _scaleW;
    private readonly Label _scaleD;
    private readonly Label _scaleH;
    private IVisualElementScheduledItem _flashHide;

    public ObjectTabController(VisualElement root)
    {
        if (root == null)
            throw new ArgumentNullException(nameof(root));

        _subTabs = new[]
        {
            root.Q<Button>("object-subtab-used"),
            root.Q<Button>("object-subtab-saved"),
            root.Q<Button>("object-subtab-owned"),
        };
        _subContainers = new[]
        {
            root.Q("object-used-container"),
            root.Q("object-saved-container"),
            root.Q("object-owned-container"),
        };
        for (int i = 0; i < _subTabs.Length; i++)
        {
            int index = i;
            if (_subTabs[i] != null)
                _subTabs[i].clicked += () => SwitchSubTab(index);
        }

        _ownedList = root.Q("object-owned-list");
        _usedList = root.Q("object-used-list");
        _countLabel = root.Q<Label>("object-count-label");
        _flash = root.Q<Label>("object-flash");

        _selectionBar = root.Q("object-selection-bar");
        root.Q<Button>("object-btn-duplicate").clicked += () => DuplicateClicked?.Invoke();
        root.Q<Button>("object-btn-delete").clicked += () => DeleteClicked?.Invoke();

        WireStepper(root, "object-scale-w", 0);
        WireStepper(root, "object-scale-d", 1);
        WireStepper(root, "object-scale-h", 2);
        _scaleW = root.Q<Label>("object-scale-w-label");
        _scaleD = root.Q<Label>("object-scale-d-label");
        _scaleH = root.Q<Label>("object-scale-h-label");

        // 既定は所有パレットを開く（すぐ配置を試せるように）
        SwitchSubTab(2);
        SetSelection(false);
    }

    // ── 公開 API ─────────────────────────────────────────────────────────────

    /// <summary>所有パレットを再構築する。</summary>
    public void SetOwnedObjects(IReadOnlyList<PaletteItem> items)
    {
        _ownedList.Clear();
        foreach (var item in items)
        {
            string typeId = item.TypeId;
            var card = MakeCard(item.Name, item.Swatch, costText: null);
            card.RegisterCallback<ClickEvent>(_ => PaletteObjectClicked?.Invoke(typeId));
            _ownedList.Add(card);
        }
    }

    /// <summary>利用中一覧を再構築する（selectedId のカードを選択状態にする）。</summary>
    public void SetUsedObjects(IReadOnlyList<UsedItem> items, string selectedId)
    {
        _usedList.Clear();
        foreach (var item in items)
        {
            string id = item.InstanceId;
            var card = MakeCard(item.Name, item.Swatch, costText: $"コスト {item.Cost}");
            card.EnableInClassList("object-item--selected", id == selectedId);
            card.RegisterCallback<ClickEvent>(_ => UsedObjectSelected?.Invoke(id));
            _usedList.Add(card);
        }
    }

    /// <summary>選択ツールバー（複製/削除/サイズ）の表示を切り替える。</summary>
    public void SetSelection(bool hasSelection) =>
        _selectionBar?.EnableInClassList("overlay-hidden", !hasSelection);

    /// <summary>オブジェクト数ラベル「N / 400」を更新する。</summary>
    public void SetCount(int count)
    {
        if (_countLabel != null)
            _countLabel.text = $"{count} / {ObjectPlacementStore.MaxObjects}";
    }

    /// <summary>W/D/H ラベル（0.25m 単位の整数 → メートル表記）を更新する。</summary>
    public void SetScaleLabels(int w, int d, int h)
    {
        if (_scaleW != null) _scaleW.text = Meters(w);
        if (_scaleD != null) _scaleD.text = Meters(d);
        if (_scaleH != null) _scaleH.text = Meters(h);
    }

    /// <summary>フラッシュメッセージを表示する（自動で消える）。</summary>
    public void ShowFlash(string message)
    {
        if (_flash == null)
            return;
        _flash.text = message;
        _flash.EnableInClassList("overlay-hidden", false);
        _flashHide?.Pause();
        _flashHide = _flash.schedule
            .Execute(() => _flash.EnableInClassList("overlay-hidden", true))
            .StartingIn(2000);
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private void WireStepper(VisualElement root, string prefix, int axis)
    {
        var dec = root.Q<Button>($"{prefix}-dec");
        var inc = root.Q<Button>($"{prefix}-inc");
        if (dec != null) dec.clicked += () => ScaleStep?.Invoke(axis, -1);
        if (inc != null) inc.clicked += () => ScaleStep?.Invoke(axis, +1);
    }

    private void SwitchSubTab(int index)
    {
        for (int i = 0; i < _subTabs.Length; i++)
        {
            _subTabs[i]?.EnableInClassList("object-subtab--active", i == index);
            _subContainers[i]?.EnableInClassList("overlay-hidden", i != index);
        }
        SubTabChanged?.Invoke(index);
    }

    private static VisualElement MakeCard(string name, Color swatch, string costText)
    {
        var card = new VisualElement();
        card.AddToClassList("object-item");

        var thumb = new VisualElement();
        thumb.AddToClassList("object-item-thumb");
        thumb.style.backgroundColor = swatch;
        card.Add(thumb);

        var label = new Label(name);
        label.AddToClassList("object-item-name");
        card.Add(label);

        if (!string.IsNullOrEmpty(costText))
        {
            var cost = new Label(costText);
            cost.AddToClassList("object-item-cost");
            card.Add(cost);
        }
        return card;
    }

    private static string Meters(int units) => $"{units * ObjectPlaceholderTransform.SizeUnit:0.##}m";
}
