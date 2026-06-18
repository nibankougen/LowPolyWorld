using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// ギミックタブの UI 制御（screens-and-modes.md 11.7.4）。WorldEditorController から生成される。
///
/// 担当: ルール・グループ合計ヘッダー / ステート定義エリア（折りたたみ・ワールド 0〜9・
/// プレイヤー 0〜3・タイマー 0〜4 の名前 + 初期値）/ ルール一覧（追加・改名・並び替え・削除）。
/// 編集状態は <see cref="GimmickTabLogic"/> が保持する。ルール内容（入力イベント / 条件 /
/// アクション）の編集画面は後続スライス（<see cref="RuleEditRequested"/> で上位へ通知する）。
/// </summary>
public class GimmickTabController
{
    /// <summary>ルールの「編集」を押したとき（ruleId）。上位がルール編集画面を開く（後続）。</summary>
    public event Action<string> RuleEditRequested;

    private readonly GimmickTabLogic _logic;

    private readonly Label _totalLabel;
    private readonly VisualElement _variablesOverlay;
    private readonly Button _variablesBack;
    private readonly Button _editVariablesBtn;
    private readonly Label _variablesSummary;
    private readonly VisualElement _worldStateList;
    private readonly VisualElement _playerStateList;
    private readonly VisualElement _timerStateList;
    private readonly Button _addTemplateBtn;
    private readonly VisualElement _ruleList;
    private readonly ScrollView _scroll;
    private readonly Label _flash;
    private readonly GimmickTemplatePickerController _templatePicker;

    // 折りたたまれているグループ（UI のみの状態・データには持たない）。
    private readonly HashSet<string> _collapsedGroups = new();

    private IVisualElementScheduledItem _flashHide;

    public GimmickTabController(VisualElement root, GimmickTabLogic logic = null)
    {
        if (root == null)
            throw new ArgumentNullException(nameof(root));
        _logic = logic ?? new GimmickTabLogic();

        _totalLabel = root.Q<Label>("gimmick-total-label");
        _variablesOverlay = root.Q("gimmick-variables");
        _variablesBack = root.Q<Button>("gimmick-variables-back");
        _editVariablesBtn = root.Q<Button>("gimmick-edit-variables");
        _variablesSummary = root.Q<Label>("gimmick-variables-summary");
        _worldStateList = root.Q("gimmick-world-states");
        _playerStateList = root.Q("gimmick-player-states");
        _timerStateList = root.Q("gimmick-timer-states");
        _addTemplateBtn = root.Q<Button>("gimmick-add-template");
        _ruleList = root.Q("gimmick-rule-list");
        _scroll = root.Q<ScrollView>(className: "gimmick-scroll");
        _flash = root.Q<Label>("gimmick-flash");
        _templatePicker = new GimmickTemplatePickerController(root);

        if (_editVariablesBtn != null) _editVariablesBtn.clicked += OpenVariables;
        if (_variablesBack != null) _variablesBack.clicked += CloseVariables;
        if (_addTemplateBtn != null) _addTemplateBtn.clicked += OnOpenTemplatePicker;

        Refresh();
    }

    /// <summary>開いているオーバーレイ（テンプレート選択・変数編集）を閉じる（タブ切替・ワールド再読込時に呼ぶ）。</summary>
    public void CloseOverlays()
    {
        _templatePicker?.Close();
        CloseVariables();
    }

    public GimmickTabLogic Logic => _logic;

    /// <summary>ロジックを外部で更新した後（LoadFrom 等）に UI を全面更新する。</summary>
    public void Refresh()
    {
        RefreshStateLists();
        RefreshRuleList();
        RefreshTotal();
    }

    // ── 変数（概要 + 編集オーバーレイ）─────────────────────────────────────────

    private void OpenVariables() => _variablesOverlay?.EnableInClassList("overlay-hidden", false);

    private void CloseVariables() => _variablesOverlay?.EnableInClassList("overlay-hidden", true);

    // トップページの変数概要（件数）を更新する。
    private void RefreshVariableSummary()
    {
        if (_variablesSummary == null)
            return;
        int w = _logic.WorldStateCount, p = _logic.PlayerStateCount, t = _logic.TimerCount;
        _variablesSummary.text = (w + p + t) == 0
            ? "なし"
            : $"ワールド {w}・プレイヤー {p}・タイマー {t}";
    }

    // 追加 / 削除式: 定義済みステートのみを行に展開する（追加・削除のたびに作り直す）。
    private void RefreshStateLists()
    {
        RebuildList(_worldStateList, _logic.WorldStateIndices, true, StateKind.World, OnAddWorldState, "＋ ワールド変数を追加");
        RebuildList(_playerStateList, _logic.PlayerStateIndices, true, StateKind.Player, OnAddPlayerState, "＋ プレイヤー変数を追加");
        RebuildList(_timerStateList, _logic.TimerIndices, false, StateKind.Timer, OnAddTimer, "＋ タイマーを追加");
        RefreshVariableSummary();
    }

    private enum StateKind { World, Player, Timer }

    private void RebuildList(
        VisualElement list, IReadOnlyList<int> indices, bool withValue, StateKind kind, Action onAdd, string addLabel)
    {
        if (list == null)
            return;
        list.Clear();
        if (indices.Count == 0)
        {
            var empty = new Label("（なし）");
            empty.AddToClassList("gimmick-state-empty");
            list.Add(empty);
        }
        else
        {
            foreach (int index in indices)
                list.Add(BuildStateRow(index, withValue, kind));
        }

        // 一覧の最下部に追加ボタン
        var add = new Button(onAdd) { text = addLabel };
        add.AddToClassList("gimmick-template-top-btn");
        add.AddToClassList("gimmick-list-add--flush");
        list.Add(add);
    }

    private VisualElement BuildStateRow(int index, bool withValue, StateKind kind)
    {
        var row = new VisualElement();
        row.AddToClassList("gimmick-state-row");

        // インデックスは UI に出さず、名前で識別する（名前は必須）。
        var name = new TextField { maxLength = GimmickTabLogic.LabelMaxLength };
        name.AddToClassList("gimmick-state-name");
        name.SetValueWithoutNotify(LabelOf(kind, index));
        name.RegisterValueChangedCallback(e => SetLabel(kind, index, e.newValue));
        // 名前は必須: 空のままフォーカスを外したら既定名に戻す。
        name.RegisterCallback<FocusOutEvent>(_ =>
        {
            if (string.IsNullOrEmpty(LabelOf(kind, index)))
            {
                string def = DefaultStateName(kind, index);
                SetLabel(kind, index, def);
                name.SetValueWithoutNotify(def);
            }
        });
        row.Add(name);

        if (withValue)
        {
            var value = new IntegerField();
            value.AddToClassList("gimmick-state-value");
            value.SetValueWithoutNotify(kind == StateKind.Player
                ? _logic.GetPlayerStateInitial(index)
                : _logic.GetWorldStateInitial(index));
            value.RegisterValueChangedCallback(e =>
            {
                int clamped = GimmickTabLogic.ClampStateValue(e.newValue);
                if (kind == StateKind.Player) _logic.SetPlayerStateInitial(index, clamped);
                else _logic.SetWorldStateInitial(index, clamped);
                if (clamped != e.newValue)
                    value.SetValueWithoutNotify(clamped);
            });
            row.Add(value);
        }

        var del = new Button(() => RemoveState(kind, index)) { text = "", tooltip = "削除" };
        del.AddToClassList("gimmick-state-del");
        del.AddToClassList("gimmick-icon-btn");
        del.AddToClassList("gimmick-icon-btn--close");
        row.Add(del);

        return row;
    }

    private string LabelOf(StateKind kind, int index) => kind switch
    {
        StateKind.World => _logic.GetWorldStateLabel(index),
        StateKind.Player => _logic.GetPlayerStateLabel(index),
        _ => _logic.GetTimerLabel(index),
    };

    private void SetLabel(StateKind kind, int index, string label)
    {
        switch (kind)
        {
            case StateKind.World: _logic.SetWorldStateLabel(index, label); break;
            case StateKind.Player: _logic.SetPlayerStateLabel(index, label); break;
            case StateKind.Timer: _logic.SetTimerLabel(index, label); break;
        }
    }

    // 追加時 / 空入力時の既定名（名前必須のため非空を保証する）。
    private static string DefaultStateName(StateKind kind, int index) => kind switch
    {
        StateKind.World => $"ワールド変数{index + 1}",
        StateKind.Player => $"プレイヤー変数{index + 1}",
        _ => $"タイマー{index + 1}",
    };

    private void OnAddWorldState()
    {
        int i = _logic.AddWorldState();
        if (i < 0)
        {
            ShowFlash($"ワールド変数は最大 {GimmickTabLogic.MaxWorldStates} 個までです");
            return;
        }
        _logic.SetWorldStateLabel(i, DefaultStateName(StateKind.World, i));
        RefreshStateLists();
    }

    private void OnAddPlayerState()
    {
        int i = _logic.AddPlayerState();
        if (i < 0)
        {
            ShowFlash($"プレイヤー変数は最大 {GimmickTabLogic.MaxPlayerStates} 個までです");
            return;
        }
        _logic.SetPlayerStateLabel(i, DefaultStateName(StateKind.Player, i));
        RefreshStateLists();
    }

    private void OnAddTimer()
    {
        int i = _logic.AddTimer();
        if (i < 0)
        {
            ShowFlash($"タイマーは最大 {GimmickTabLogic.MaxTimers} 個までです");
            return;
        }
        _logic.SetTimerLabel(i, DefaultStateName(StateKind.Timer, i));
        RefreshStateLists();
    }

    private void RemoveState(StateKind kind, int index)
    {
        bool removed = kind switch
        {
            StateKind.World => _logic.RemoveWorldState(index),
            StateKind.Player => _logic.RemovePlayerState(index),
            _ => _logic.RemoveTimer(index),
        };
        if (removed)
            RefreshStateLists();
    }

    // ── ルール一覧 ─────────────────────────────────────────────────────────────

    private void OnAddRule()
    {
        var rule = _logic.AddRule();
        if (rule == null)
        {
            ShowFlash($"ルール・グループは最大 {GimmickTabLogic.MaxRulesAndGroups} 個までです");
            return;
        }
        RefreshRuleList();
        RefreshTotal();
        RuleEditRequested?.Invoke(rule.ruleId); // 追加直後は編集画面へ（後続スライスで実装）
    }

    private void OnOpenTemplatePicker()
    {
        _templatePicker?.Open(OnInsertTemplate);
    }

    // テンプレート確定時: 自動割り当て + ルール挿入。失敗（容量不足）はフラッシュで通知。
    private void OnInsertTemplate(string templateId, IReadOnlyDictionary<string, int> values)
    {
        var result = GimmickTemplateLogic.Insert(_logic, templateId, values);
        if (!result.Success)
        {
            ShowFlash(result.Error);
            return;
        }
        // ステート定義（ラベル）とルール一覧の両方が変化するため全面更新する。
        Refresh();
    }

    private const int IndentPerDepth = 16;  // ネスト 1 段あたりの字下げ(px)
    private const float DragThreshold = 6f; // この px を超えて動いたらドラッグ開始（タップと区別）

    // 描画済みの可視行（ヒットテスト用・上から下の順）。
    private enum RowKind { Group, Rule }

    private struct RowInfo
    {
        public VisualElement Element;
        public string Id;
        public RowKind Kind;
        public int Depth;
        public string ContainerId; // この行が属するコンテナ（グループ=親 / ルール=所属グループ）
        public bool ExpandedGroup; // グループ行かつ展開中
    }

    private readonly List<RowInfo> _rows = new();

    // ドロップ先の種類。
    private enum DropKind { None, IntoGroup, BeforeRule, BeforeGroup, RootEnd }

    private struct DropTarget
    {
        public DropKind Kind;
        public string TargetId; // IntoGroup=グループID / BeforeRule=ルールID / BeforeGroup=グループID
    }

    // ドラッグ状態
    private string _dragId;
    private bool _dragIsGroup;
    private int _dragPointerId = -1;
    private float _dragStartY;
    private bool _dragging;
    private VisualElement _dragHandle;
    private HashSet<string> _dragSubtree; // グループドラッグ時の自身＋子孫（ドロップ不可判定）
    private DropTarget _currentDrop;
    private VisualElement _dropLine;       // 挿入位置を示す横線
    private VisualElement _highlightedGroup; // 「中に入れる」ハイライト中のグループ行
    private VisualElement _draggedRow;     // ドラッグ中の元の行（薄く表示）
    private VisualElement _dragGhost;      // ポインターに追従する半透明ゴースト
    private float _lastPointerY;           // 直近のポインター Y（自動スクロール tick 用）
    private IVisualElementScheduledItem _autoScroll; // 端でのオートスクロール tick

    private void RefreshRuleList()
    {
        if (_ruleList == null)
            return;
        _ruleList.Clear();
        _rows.Clear();
        _dropLine = null;
        _highlightedGroup = null;

        if (_logic.Rules.Count == 0 && _logic.GroupCount == 0)
        {
            var empty = new Label(
                "ルールは「〜したとき、〜する」の形です。\n上の「テンプレートから追加」で簡単に始められます。");
            empty.AddToClassList("gimmick-rule-empty");
            _ruleList.Add(empty);
        }
        else
        {
            // グループツリーを深さ優先で描画する（ルート = container ""）。
            RenderContainer("", 0);
        }

        // 一覧の最下部に追加ボタン（ルール / グループ）
        var addRule = new Button(OnAddRule) { text = "＋ ルールを追加" };
        addRule.AddToClassList("gimmick-template-top-btn");
        addRule.AddToClassList("gimmick-list-add--flush");
        _ruleList.Add(addRule);

        // グループ追加は icon_group_plus アイコン + ラベル（テンプレートボタンと同じ構成）
        var addGroup = new Button(() => OnAddGroup("")) { text = "" };
        addGroup.AddToClassList("gimmick-template-top-btn");
        addGroup.AddToClassList("gimmick-template-top-btn--icon");
        addGroup.AddToClassList("gimmick-list-add--flush");
        addGroup.AddToClassList("gimmick-group-add");
        var addGroupIcon = new VisualElement { pickingMode = PickingMode.Ignore };
        addGroupIcon.AddToClassList("gimmick-btn-icon");
        addGroupIcon.AddToClassList("icon-group-plus");
        addGroup.Add(addGroupIcon);
        var addGroupLabel = new Label("グループを追加") { pickingMode = PickingMode.Ignore };
        addGroupLabel.AddToClassList("gimmick-btn-label");
        addGroup.Add(addGroupLabel);
        _ruleList.Add(addGroup);
    }

    // container（"" = ルート）直下のグループ → ルールの順に描画する（行は全て _ruleList の直接の子）。
    private void RenderContainer(string containerId, int depth)
    {
        foreach (var group in _logic.Groups)
        {
            if (group.parentGroupId != containerId)
                continue;
            bool expanded = !_collapsedGroups.Contains(group.groupId);
            var row = BuildGroupRow(group.groupId, group.name, depth);
            _ruleList.Add(row);
            _rows.Add(new RowInfo { Element = row, Id = group.groupId, Kind = RowKind.Group, Depth = depth, ContainerId = containerId, ExpandedGroup = expanded });
            if (expanded)
                RenderContainer(group.groupId, depth + 1);
        }

        foreach (var rule in _logic.Rules)
        {
            if ((rule.groupId ?? "") != containerId)
                continue;
            var row = BuildRuleRow(rule.ruleId, rule.label, GimmickRuleSummary.Of(rule), depth);
            _ruleList.Add(row);
            _rows.Add(new RowInfo { Element = row, Id = rule.ruleId, Kind = RowKind.Rule, Depth = depth, ContainerId = containerId });
        }
    }

    private VisualElement BuildGroupRow(string groupId, string name, int depth)
    {
        var row = new VisualElement();
        row.AddToClassList("gimmick-group-row");
        row.style.marginLeft = depth * IndentPerDepth;

        row.Add(BuildDragHandle(groupId, true));

        // 開閉トグル（▶ 折りたたみ / ▼ 展開）
        bool collapsed = _collapsedGroups.Contains(groupId);
        var toggle = BuildIconButton(collapsed ? "icon-right" : "icon-down", collapsed ? "展開" : "折りたたみ", () =>
        {
            if (!_collapsedGroups.Remove(groupId))
                _collapsedGroups.Add(groupId);
            RefreshRuleList();
        });
        toggle.AddToClassList("gimmick-group-toggle");
        row.Add(toggle);

        // グループ名（インライン改名・FocusOut 確定・空は既定名へ復帰）
        var nameField = new TextField { maxLength = GimmickTabLogic.LabelMaxLength };
        nameField.AddToClassList("gimmick-group-name");
        nameField.SetValueWithoutNotify(name);
        nameField.RegisterCallback<FocusOutEvent>(_ =>
        {
            if (!_logic.RenameGroup(groupId, nameField.value))
                nameField.SetValueWithoutNotify(GroupNameOf(groupId)); // 空など失敗時は元に戻す
        });
        row.Add(nameField);

        // グループ内へルール / サブグループを追加（4 段未満のときのみサブグループ可）
        row.Add(BuildRuleButton("gimmick-icon-btn--plus", "このグループにルールを追加", () => OnAddRuleInto(groupId)));
        if (_logic.GroupDepth(groupId) < GimmickTabLogic.MaxNestDepth)
            row.Add(BuildIconButton("icon-group-plus", "サブグループを追加", () => OnAddGroup(groupId)));

        row.Add(BuildRuleButton("gimmick-icon-btn--close", "グループを削除（中身は外へ出す）", () =>
        {
            if (_logic.DeleteGroup(groupId))
            {
                _collapsedGroups.Remove(groupId);
                RefreshRuleList();
                RefreshTotal();
            }
        }));

        return row;
    }

    private VisualElement BuildRuleRow(string ruleId, string label, string summary, int depth)
    {
        var row = new VisualElement();
        row.AddToClassList("gimmick-rule-row");
        row.style.marginLeft = depth * IndentPerDepth;

        row.Add(BuildDragHandle(ruleId, false));

        // 名前 + 動作サマリー（タップでルール編集画面へ・11.7.4）。
        var info = new VisualElement();
        info.AddToClassList("gimmick-rule-info");
        info.RegisterCallback<ClickEvent>(_ => RuleEditRequested?.Invoke(ruleId));

        var name = new Label(label);
        name.AddToClassList("gimmick-rule-name");
        info.Add(name);

        var summaryLabel = new Label(summary) { pickingMode = PickingMode.Ignore };
        summaryLabel.AddToClassList("gimmick-rule-summary");
        info.Add(summaryLabel);
        row.Add(info);

        row.Add(BuildRuleButton("gimmick-icon-btn--edit", "編集", () => RuleEditRequested?.Invoke(ruleId)));
        row.Add(BuildRuleButton("gimmick-icon-btn--close", "削除", () =>
        {
            if (_logic.DeleteRule(ruleId))
            {
                RefreshRuleList();
                RefreshTotal();
            }
        }));

        return row;
    }

    private string GroupNameOf(string groupId)
    {
        foreach (var g in _logic.Groups)
            if (g.groupId == groupId)
                return g.name;
        return "";
    }

    // アイコンボタン（背景画像は USS の gimmick-icon-btn 修飾クラスで割り当て）
    private Button BuildRuleButton(string iconClass, string tooltip, Action onClick)
    {
        var btn = new Button(onClick) { text = "", tooltip = tooltip };
        btn.AddToClassList("gimmick-rule-btn");
        btn.AddToClassList("gimmick-icon-btn");
        btn.AddToClassList(iconClass);
        return btn;
    }

    // 共通アイコンクラス（icon-right / icon-down / icon-plus 等）を使うボタン
    private Button BuildIconButton(string iconClass, string tooltip, Action onClick)
    {
        var btn = new Button(onClick) { text = "", tooltip = tooltip };
        btn.AddToClassList("gimmick-rule-btn");
        btn.AddToClassList(iconClass);
        return btn;
    }

    private void OnAddGroup(string parentGroupId)
    {
        var id = _logic.CreateGroup(parentGroupId);
        if (id == null)
        {
            ShowFlash($"ルール・グループは最大 {GimmickTabLogic.MaxRulesAndGroups} 個までです");
            return;
        }
        RefreshRuleList();
        RefreshTotal();
    }

    private void OnAddRuleInto(string groupId)
    {
        var rule = _logic.AddRule(null, groupId);
        if (rule == null)
        {
            ShowFlash($"ルール・グループは最大 {GimmickTabLogic.MaxRulesAndGroups} 個までです");
            return;
        }
        _collapsedGroups.Remove(groupId); // 追加先を開いて見せる
        RefreshRuleList();
        RefreshTotal();
        RuleEditRequested?.Invoke(rule.ruleId);
    }

    // ── ドラッグ＆ドロップ（並べ替え / グループ出し入れ） ───────────────────────
    // ハンドル(☰)を起点にポインタードラッグする（タッチ対応・ScrollView のスクロールに
    // 奪われないよう CapturePointer + StopPropagation）。行間に落とすと並べ替え、
    // グループ本体に落とすとそのグループの中へ入る。一覧最下部はルートへ出す。

    private VisualElement BuildDragHandle(string id, bool isGroup)
    {
        var h = new VisualElement { tooltip = "ドラッグして並べ替え / グループ出し入れ" };
        h.AddToClassList("gimmick-drag-handle");
        h.RegisterCallback<PointerDownEvent>(e => OnHandleDown(e, id, isGroup, h));
        h.RegisterCallback<PointerMoveEvent>(OnHandleMove);
        h.RegisterCallback<PointerUpEvent>(OnHandleUp);
        return h;
    }

    private void OnHandleDown(PointerDownEvent e, string id, bool isGroup, VisualElement handle)
    {
        _dragId = id;
        _dragIsGroup = isGroup;
        _dragPointerId = e.pointerId;
        _dragStartY = e.position.y;
        _dragging = false;
        _dragHandle = handle;
        _dragSubtree = isGroup ? CollectGroupSubtree(id) : null;
        _currentDrop = default;
        handle.CapturePointer(e.pointerId);
        e.StopPropagation();
    }

    private void OnHandleMove(PointerMoveEvent e)
    {
        if (_dragId == null || e.pointerId != _dragPointerId)
            return;
        _lastPointerY = e.position.y;
        if (!_dragging)
        {
            if (Mathf.Abs(e.position.y - _dragStartY) < DragThreshold)
                return;
            _dragging = true;
            EnsureDropLine();
            BeginDragVisual();
            StartAutoScroll();
        }
        UpdateDrop(e.position.y);
        UpdateGhostPosition(e.position.y);
        e.StopPropagation();
    }

    private void OnHandleUp(PointerUpEvent e)
    {
        if (_dragId == null || e.pointerId != _dragPointerId)
            return;
        _dragHandle?.ReleasePointer(e.pointerId);

        bool wasDragging = _dragging;
        string dragId = _dragId;
        bool isGroup = _dragIsGroup;
        DropTarget drop = _currentDrop;

        // 状態リセット
        _dragId = null;
        _dragPointerId = -1;
        _dragging = false;
        _dragHandle = null;
        _dragSubtree = null;
        StopAutoScroll();
        ClearGroupHighlight();
        EndDragVisual();

        if (wasDragging)
            ApplyDrop(dragId, isGroup, drop);
        e.StopPropagation();
    }

    private void UpdateDrop(float pointerY)
    {
        _currentDrop = _dragIsGroup ? ComputeGroupDrop(pointerY) : ComputeRuleDrop(pointerY);
        ShowDropFeedback(_currentDrop);
    }

    // ルールのドロップ先を決める（行間の「境界」ベース）:
    //  1. グループ本体（中央帯）への直接ドロップ → そのグループの中へ（閉じたグループはこれだけが入口）
    //  2. 行間ギャップ: 境界より上 = 直上に表示中の最内グループの末尾へ / 境界より下 = その外側（下の行の直前）
    private DropTarget ComputeRuleDrop(float pointerY)
    {
        // 1. グループ本体への直接ドロップ（開閉問わず中へ）
        foreach (var r in _rows)
            if (r.Kind == RowKind.Group && InGroupBody(r.Element, pointerY))
                return new DropTarget { Kind = DropKind.IntoGroup, TargetId = r.Id };

        // 2. 行間ギャップを求める（pointer の上にある行 = above / 下 = below）
        int idx = _rows.Count;
        for (int i = 0; i < _rows.Count; i++)
            if (pointerY < _rows[i].Element.worldBound.center.y)
            {
                idx = i;
                break;
            }
        bool hasAbove = idx > 0;
        bool hasBelow = idx < _rows.Count;
        RowInfo above = hasAbove ? _rows[idx - 1] : default;
        RowInfo below = hasBelow ? _rows[idx] : default;

        if (!hasBelow)
            return new DropTarget { Kind = DropKind.RootEnd }; // 一覧の最下部 = すべての外（ルート末尾）
        if (!hasAbove)
            return MakeRuleTarget(below.ContainerId, below); // 先頭 = 下の行のコンテナへ、その直前

        string aboveInner = InnerContainerOfAbove(above); // 直上の最内（閉じたグループは入れない）
        string belowCont = below.ContainerId;

        // above / below が同じコンテナ → 境界なし。単純に下の行の直前へ
        if (aboveInner == belowCont)
            return MakeRuleTarget(belowCont, below);

        // 境界あり: 上側 = 最内グループの末尾へ / 下側 = 外側（下の行の直前）
        float boundaryY = (above.Element.worldBound.yMax + below.Element.worldBound.yMin) * 0.5f;
        return pointerY < boundaryY
            ? MakeRuleTarget(aboveInner, default) // 中へ（末尾）
            : MakeRuleTarget(belowCont, below);   // 外へ
    }

    // コンテナ + 任意のアンカー行から DropTarget を作る。
    // anchor が下のルール行なら「その直前」、無ければ container 末尾（ルートなら RootEnd）。
    private DropTarget MakeRuleTarget(string container, RowInfo anchor)
    {
        if (anchor.Element != null && anchor.Kind == RowKind.Rule && (anchor.ContainerId ?? "") == (container ?? ""))
            return new DropTarget { Kind = DropKind.BeforeRule, TargetId = anchor.Id };
        return string.IsNullOrEmpty(container)
            ? new DropTarget { Kind = DropKind.RootEnd }
            : new DropTarget { Kind = DropKind.IntoGroup, TargetId = container };
    }

    // 直上の行から入れるべき「最内のコンテナ」。
    //  ルール → その所属グループ / 展開中グループの直下 → そのグループ / 閉じたグループ → そのグループの外（親）
    private string InnerContainerOfAbove(RowInfo above)
    {
        if (above.Kind == RowKind.Rule)
            return above.ContainerId ?? "";
        // グループ行
        return above.ExpandedGroup ? above.Id : (above.ContainerId ?? "");
    }

    // グループのドロップ先（自身＋子孫は対象外）。ルールと同じ「境界」モデル。
    private DropTarget ComputeGroupDrop(float pointerY)
    {
        // 1. 他グループ本体への直接ドロップ → その中へ（閉じたグループもこれで入る）
        foreach (var r in _rows)
            if (r.Kind == RowKind.Group && !IsInDraggedSubtree(r) && InGroupBody(r.Element, pointerY))
                return new DropTarget { Kind = DropKind.IntoGroup, TargetId = r.Id };

        // 2. ドラッグ中のサブツリーを除いた可視行で above / below を求める
        RowInfo above = default, below = default;
        bool hasAbove = false, hasBelow = false;
        foreach (var r in _rows)
        {
            if (IsInDraggedSubtree(r))
                continue;
            if (r.Element.worldBound.center.y > pointerY)
            {
                below = r;
                hasBelow = true;
                break;
            }
            above = r;
            hasAbove = true;
        }

        if (!hasBelow)
            return new DropTarget { Kind = DropKind.RootEnd }; // 最下部 = すべての外
        if (!hasAbove)
            return MakeGroupTarget(below.ContainerId, below);

        string aboveInner = InnerContainerOfAbove(above); // 閉じたグループは入れない
        string belowCont = below.ContainerId;
        if (aboveInner == belowCont)
            return MakeGroupTarget(belowCont, below);

        // 境界あり: 上側 = 最内グループの末尾の子へ / 下側 = その外側
        float boundaryY = (above.Element.worldBound.yMax + below.Element.worldBound.yMin) * 0.5f;
        return pointerY < boundaryY
            ? MakeGroupTarget(aboveInner, default)
            : MakeGroupTarget(belowCont, below);
    }

    // コンテナ + 任意のアンカー行から グループ用 DropTarget を作る。
    // anchor が同コンテナのグループ行なら「その直前」、無ければ container 末尾（ルートなら RootEnd）。
    private DropTarget MakeGroupTarget(string container, RowInfo anchor)
    {
        if (anchor.Element != null && anchor.Kind == RowKind.Group && (anchor.ContainerId ?? "") == (container ?? ""))
            return new DropTarget { Kind = DropKind.BeforeGroup, TargetId = anchor.Id };
        return string.IsNullOrEmpty(container)
            ? new DropTarget { Kind = DropKind.RootEnd }
            : new DropTarget { Kind = DropKind.IntoGroup, TargetId = container };
    }

    // 行がドラッグ中グループの自身＋子孫に属するか（グループ=自ID / ルール=所属コンテナ）。
    private bool IsInDraggedSubtree(RowInfo r)
    {
        if (_dragSubtree == null)
            return false;
        return r.Kind == RowKind.Group ? _dragSubtree.Contains(r.Id) : _dragSubtree.Contains(r.ContainerId);
    }

    // 行の中央 50% 帯にポインタがあるか（= 「この中へ入れる」と解釈する領域）。
    private static bool InGroupBody(VisualElement row, float pointerY)
    {
        var wb = row.worldBound;
        return pointerY >= wb.yMin + wb.height * 0.25f && pointerY <= wb.yMax - wb.height * 0.25f;
    }

    private void ApplyDrop(string dragId, bool isGroup, DropTarget drop)
    {
        bool ok = true;
        if (isGroup)
        {
            switch (drop.Kind)
            {
                case DropKind.IntoGroup: ok = _logic.MoveGroupToParentEnd(dragId, drop.TargetId); break;
                case DropKind.BeforeGroup:
                    ok = dragId == drop.TargetId || _logic.MoveGroupBefore(dragId, drop.TargetId);
                    break;
                case DropKind.RootEnd: ok = _logic.MoveGroupToParentEnd(dragId, ""); break;
            }
        }
        else
        {
            switch (drop.Kind)
            {
                case DropKind.IntoGroup: ok = _logic.MoveRuleToContainerEnd(dragId, drop.TargetId); break;
                case DropKind.BeforeRule: ok = _logic.MoveRuleBefore(dragId, drop.TargetId); break;
                case DropKind.RootEnd: ok = _logic.MoveRuleToContainerEnd(dragId, ""); break;
            }
        }

        if (!ok && drop.Kind != DropKind.None)
            ShowFlash("ここには移動できません（ネストが深すぎる / 自身の中など）");
        RefreshRuleList();
    }

    // 自身＋全子孫グループ ID（グループドラッグのドロップ不可判定用）。
    private HashSet<string> CollectGroupSubtree(string groupId)
    {
        var set = new HashSet<string> { groupId };
        bool changed = true;
        while (changed)
        {
            changed = false;
            foreach (var g in _logic.Groups)
                if (set.Contains(g.parentGroupId) && set.Add(g.groupId))
                    changed = true;
        }
        return set;
    }

    // ── ドロップ視覚フィードバック（挿入線 + グループハイライト） ─────────────────

    private void EnsureDropLine()
    {
        if (_dropLine != null)
            return;
        _dropLine = new VisualElement { pickingMode = PickingMode.Ignore };
        _dropLine.AddToClassList("gimmick-drop-line");
        _ruleList.Add(_dropLine);
    }

    private void ShowDropFeedback(DropTarget drop)
    {
        ClearGroupHighlight();
        EnsureDropLine();

        if (drop.Kind == DropKind.IntoGroup)
        {
            // グループ行をハイライトし、挿入線は隠す。
            _dropLine.style.display = DisplayStyle.None;
            var row = FindRow(drop.TargetId, RowKind.Group);
            if (row.Element != null)
            {
                row.Element.AddToClassList("gimmick-drop-into");
                _highlightedGroup = row.Element;
            }
            return;
        }

        _dropLine.style.display = DisplayStyle.Flex;
        if (drop.Kind == DropKind.BeforeRule || drop.Kind == DropKind.BeforeGroup)
        {
            var kind = drop.Kind == DropKind.BeforeRule ? RowKind.Rule : RowKind.Group;
            var row = FindRow(drop.TargetId, kind);
            if (row.Element != null)
            {
                _dropLine.style.top = row.Element.layout.yMin;
                _dropLine.style.left = row.Depth * IndentPerDepth;
            }
        }
        else // RootEnd: 最後の行の下・ルート位置
        {
            float bottom = _rows.Count > 0 ? _rows[_rows.Count - 1].Element.layout.yMax : 0f;
            _dropLine.style.top = bottom;
            _dropLine.style.left = 0;
        }
    }

    private void ClearGroupHighlight()
    {
        if (_highlightedGroup != null)
        {
            _highlightedGroup.RemoveFromClassList("gimmick-drop-into");
            _highlightedGroup = null;
        }
    }

    private RowInfo FindRow(string id, RowKind kind)
    {
        foreach (var r in _rows)
            if (r.Kind == kind && r.Id == id)
                return r;
        return default;
    }

    // ドラッグ開始時: 元の行を薄くし、ポインター追従ゴーストを生成する。
    private void BeginDragVisual()
    {
        var kind = _dragIsGroup ? RowKind.Group : RowKind.Rule;
        _draggedRow = FindRow(_dragId, kind).Element;
        _draggedRow?.AddToClassList("gimmick-row-dragging");

        _dragGhost = new VisualElement { pickingMode = PickingMode.Ignore };
        _dragGhost.AddToClassList("gimmick-drag-ghost");
        var label = new Label(DraggedLabel(_dragId, _dragIsGroup)) { pickingMode = PickingMode.Ignore };
        label.AddToClassList("gimmick-drag-ghost-label");
        _dragGhost.Add(label);
        _ruleList.Add(_dragGhost);
    }

    // ゴーストをポインターの縦位置に追従させる（_ruleList ローカル座標へ変換・指の中央に合わせる）。
    private void UpdateGhostPosition(float pointerY)
    {
        if (_dragGhost == null)
            return;
        float localY = pointerY - _ruleList.worldBound.yMin;
        float h = _dragGhost.resolvedStyle.height;
        if (h <= 0f)
            h = 34f;
        _dragGhost.style.top = localY - h * 0.5f;
    }

    private void EndDragVisual()
    {
        _draggedRow?.RemoveFromClassList("gimmick-row-dragging");
        _draggedRow = null;
        _dragGhost?.RemoveFromHierarchy();
        _dragGhost = null;
    }

    private string DraggedLabel(string id, bool isGroup)
    {
        if (isGroup)
            return GroupNameOf(id);
        foreach (var r in _logic.Rules)
            if (r.ruleId == id)
                return r.label;
        return "";
    }

    // ── 端でのオートスクロール ──────────────────────────────────────────────────
    // ドラッグ中はポインターが動かなくても継続的にスクロールしたいので、tick で駆動する。

    private const float AutoScrollMargin = 52f;   // ビューポート端からこの px 以内で発動
    private const float AutoScrollMaxSpeed = 16f;  // 1 tick あたりの最大スクロール量(px)

    private void StartAutoScroll()
    {
        if (_scroll == null || _autoScroll != null)
            return;
        _autoScroll = _ruleList.schedule.Execute(AutoScrollTick).Every(16);
    }

    private void StopAutoScroll()
    {
        _autoScroll?.Pause();
        _autoScroll = null;
    }

    private void AutoScrollTick()
    {
        if (!_dragging || _scroll == null)
            return;

        var vp = _scroll.contentViewport.worldBound;
        float dy = 0f;
        if (_lastPointerY < vp.yMin + AutoScrollMargin)
        {
            // 上端に近いほど速く上へ
            float t = Mathf.Clamp01((vp.yMin + AutoScrollMargin - _lastPointerY) / AutoScrollMargin);
            dy = -AutoScrollMaxSpeed * t;
        }
        else if (_lastPointerY > vp.yMax - AutoScrollMargin)
        {
            float t = Mathf.Clamp01((_lastPointerY - (vp.yMax - AutoScrollMargin)) / AutoScrollMargin);
            dy = AutoScrollMaxSpeed * t;
        }
        if (Mathf.Approximately(dy, 0f))
            return;

        float lo = _scroll.verticalScroller.lowValue;
        float hi = _scroll.verticalScroller.highValue;
        float newY = Mathf.Clamp(_scroll.scrollOffset.y + dy, lo, hi);
        if (Mathf.Approximately(newY, _scroll.scrollOffset.y))
            return; // これ以上スクロールできない（端）

        _scroll.scrollOffset = new Vector2(_scroll.scrollOffset.x, newY);
        // スクロール後の座標でドロップ先・ゴーストを更新する。
        UpdateDrop(_lastPointerY);
        UpdateGhostPosition(_lastPointerY);
    }

    private void RefreshTotal()
    {
        if (_totalLabel != null)
            _totalLabel.text = $"{_logic.TotalCount} / {GimmickTabLogic.MaxRulesAndGroups}";
    }

    // ── フラッシュ ─────────────────────────────────────────────────────────────

    private void ShowFlash(string message)
    {
        if (_flash == null)
            return;
        _flash.text = message;
        _flash.EnableInClassList("overlay-hidden", false);
        _flashHide?.Pause();
        _flashHide = _flash.schedule.Execute(() => _flash.EnableInClassList("overlay-hidden", true)).StartingIn(1800);
    }
}
