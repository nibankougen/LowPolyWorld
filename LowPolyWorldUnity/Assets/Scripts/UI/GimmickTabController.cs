using System;
using System.Collections.Generic;
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
    private readonly Button _statesToggle;
    private readonly VisualElement _statesArrow;
    private readonly VisualElement _statesBody;
    private readonly VisualElement _worldStateList;
    private readonly VisualElement _playerStateList;
    private readonly VisualElement _timerStateList;
    private readonly Button _addTemplateBtn;
    private readonly VisualElement _ruleList;
    private readonly Label _flash;
    private readonly GimmickTemplatePickerController _templatePicker;

    // 変数一覧は詳細設定として既定で折りたたむ（UXML 側も overlay-hidden / icon-right で開始）。
    private bool _statesExpanded = false;
    private IVisualElementScheduledItem _flashHide;

    public GimmickTabController(VisualElement root, GimmickTabLogic logic = null)
    {
        if (root == null)
            throw new ArgumentNullException(nameof(root));
        _logic = logic ?? new GimmickTabLogic();

        _totalLabel = root.Q<Label>("gimmick-total-label");
        _statesToggle = root.Q<Button>("gimmick-states-toggle");
        _statesArrow = root.Q("gimmick-states-arrow");
        _statesBody = root.Q("gimmick-states-body");
        _worldStateList = root.Q("gimmick-world-states");
        _playerStateList = root.Q("gimmick-player-states");
        _timerStateList = root.Q("gimmick-timer-states");
        _addTemplateBtn = root.Q<Button>("gimmick-add-template");
        _ruleList = root.Q("gimmick-rule-list");
        _flash = root.Q<Label>("gimmick-flash");
        _templatePicker = new GimmickTemplatePickerController(root);

        if (_statesToggle != null) _statesToggle.clicked += ToggleStates;
        if (_addTemplateBtn != null) _addTemplateBtn.clicked += OnOpenTemplatePicker;

        Refresh();
    }

    /// <summary>開いているテンプレート選択オーバーレイを閉じる（タブ切替・ワールド再読込時に呼ぶ）。</summary>
    public void CloseOverlays() => _templatePicker?.Close();

    public GimmickTabLogic Logic => _logic;

    /// <summary>ロジックを外部で更新した後（LoadFrom 等）に UI を全面更新する。</summary>
    public void Refresh()
    {
        RefreshStateLists();
        RefreshRuleList();
        RefreshTotal();
    }

    // ── ステート定義 ───────────────────────────────────────────────────────────

    private void ToggleStates()
    {
        _statesExpanded = !_statesExpanded;
        _statesBody?.EnableInClassList("overlay-hidden", !_statesExpanded);
        // 開いている時は ▼（icon_down）・閉じている時は ▶（icon_right）
        _statesArrow?.EnableInClassList("icon-down", _statesExpanded);
        _statesArrow?.EnableInClassList("icon-right", !_statesExpanded);
    }

    // 追加 / 削除式: 定義済みステートのみを行に展開する（追加・削除のたびに作り直す）。
    private void RefreshStateLists()
    {
        RebuildList(_worldStateList, _logic.WorldStateIndices, true, StateKind.World, OnAddWorldState, "＋ ワールド変数を追加");
        RebuildList(_playerStateList, _logic.PlayerStateIndices, true, StateKind.Player, OnAddPlayerState, "＋ プレイヤー変数を追加");
        RebuildList(_timerStateList, _logic.TimerIndices, false, StateKind.Timer, OnAddTimer, "＋ タイマーを追加");
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
        list.Add(add);
    }

    private VisualElement BuildStateRow(int index, bool withValue, StateKind kind)
    {
        var row = new VisualElement();
        row.AddToClassList("gimmick-state-row");

        var idx = new Label(index.ToString());
        idx.AddToClassList("gimmick-state-index");
        row.Add(idx);

        var name = new TextField { maxLength = GimmickTabLogic.LabelMaxLength };
        name.AddToClassList("gimmick-state-name");
        name.SetValueWithoutNotify(LabelOf(kind, index));
        name.RegisterValueChangedCallback(e =>
        {
            switch (kind)
            {
                case StateKind.World: _logic.SetWorldStateLabel(index, e.newValue); break;
                case StateKind.Player: _logic.SetPlayerStateLabel(index, e.newValue); break;
                case StateKind.Timer: _logic.SetTimerLabel(index, e.newValue); break;
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

    private void OnAddWorldState()
    {
        if (_logic.AddWorldState() < 0)
            ShowFlash($"ワールド変数は最大 {GimmickTabLogic.MaxWorldStates} 個までです");
        else
            RefreshStateLists();
    }

    private void OnAddPlayerState()
    {
        if (_logic.AddPlayerState() < 0)
            ShowFlash($"プレイヤー変数は最大 {GimmickTabLogic.MaxPlayerStates} 個までです");
        else
            RefreshStateLists();
    }

    private void OnAddTimer()
    {
        if (_logic.AddTimer() < 0)
            ShowFlash($"タイマーは最大 {GimmickTabLogic.MaxTimers} 個までです");
        else
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

    private void RefreshRuleList()
    {
        if (_ruleList == null)
            return;
        _ruleList.Clear();

        if (_logic.Rules.Count == 0)
        {
            var empty = new Label(
                "ルールは「〜したとき、〜する」の形です。\n上の「テンプレートから追加」で簡単に始められます。");
            empty.AddToClassList("gimmick-rule-empty");
            _ruleList.Add(empty);
        }
        else
        {
            foreach (var rule in _logic.Rules)
                _ruleList.Add(BuildRuleRow(rule.ruleId, rule.label, GimmickRuleSummary.Of(rule)));
        }

        // 一覧の最下部に追加ボタン
        var add = new Button(OnAddRule) { text = "＋ ルールを追加" };
        add.AddToClassList("gimmick-template-top-btn");
        _ruleList.Add(add);
    }

    private VisualElement BuildRuleRow(string ruleId, string label, string summary)
    {
        var row = new VisualElement();
        row.AddToClassList("gimmick-rule-row");

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
        row.Add(BuildRuleButton("gimmick-icon-btn--up", "上へ移動", () => MoveRule(ruleId, -1)));
        row.Add(BuildRuleButton("gimmick-icon-btn--down", "下へ移動", () => MoveRule(ruleId, +1)));
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

    // アイコンボタン（背景画像は USS の修飾クラスで割り当て。tooltip にラベルを設定）
    private Button BuildRuleButton(string iconClass, string tooltip, Action onClick)
    {
        var btn = new Button(onClick) { text = "", tooltip = tooltip };
        btn.AddToClassList("gimmick-rule-btn");
        btn.AddToClassList("gimmick-icon-btn");
        btn.AddToClassList(iconClass);
        return btn;
    }

    private void MoveRule(string ruleId, int delta)
    {
        int idx = IndexOfRule(ruleId);
        if (idx < 0)
            return;
        if (_logic.MoveRule(ruleId, idx + delta))
            RefreshRuleList();
    }

    private int IndexOfRule(string ruleId)
    {
        for (int i = 0; i < _logic.Rules.Count; i++)
            if (_logic.Rules[i].ruleId == ruleId)
                return i;
        return -1;
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
