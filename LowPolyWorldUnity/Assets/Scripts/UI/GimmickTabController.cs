using System;
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
    private readonly VisualElement _statesBody;
    private readonly VisualElement _worldStateList;
    private readonly VisualElement _playerStateList;
    private readonly VisualElement _timerStateList;
    private readonly Button _addRuleBtn;
    private readonly VisualElement _ruleList;
    private readonly Label _flash;

    private bool _statesExpanded = true;
    private IVisualElementScheduledItem _flashHide;

    public GimmickTabController(VisualElement root, GimmickTabLogic logic = null)
    {
        if (root == null)
            throw new ArgumentNullException(nameof(root));
        _logic = logic ?? new GimmickTabLogic();

        _totalLabel = root.Q<Label>("gimmick-total-label");
        _statesToggle = root.Q<Button>("gimmick-states-toggle");
        _statesBody = root.Q("gimmick-states-body");
        _worldStateList = root.Q("gimmick-world-states");
        _playerStateList = root.Q("gimmick-player-states");
        _timerStateList = root.Q("gimmick-timer-states");
        _addRuleBtn = root.Q<Button>("gimmick-add-rule");
        _ruleList = root.Q("gimmick-rule-list");
        _flash = root.Q<Label>("gimmick-flash");

        if (_statesToggle != null) _statesToggle.clicked += ToggleStates;
        if (_addRuleBtn != null) _addRuleBtn.clicked += OnAddRule;

        BuildStateRows();
        Refresh();
    }

    public GimmickTabLogic Logic => _logic;

    /// <summary>ロジックを外部で更新した後（LoadFrom 等）に UI を全面更新する。</summary>
    public void Refresh()
    {
        SyncStateValues();
        RefreshRuleList();
        RefreshTotal();
    }

    // ── ステート定義 ───────────────────────────────────────────────────────────

    private void ToggleStates()
    {
        _statesExpanded = !_statesExpanded;
        _statesBody?.EnableInClassList("overlay-hidden", !_statesExpanded);
        if (_statesToggle != null)
            _statesToggle.text = (_statesExpanded ? "▼" : "▶") + " ステート定義";
    }

    private void BuildStateRows()
    {
        _worldStateList?.Clear();
        for (int i = 0; i < GimmickTabLogic.MaxWorldStates; i++)
            _worldStateList?.Add(BuildStateRow(i, withValue: true, isPlayer: false));

        _playerStateList?.Clear();
        for (int i = 0; i < GimmickTabLogic.MaxPlayerStates; i++)
            _playerStateList?.Add(BuildStateRow(i, withValue: true, isPlayer: true));

        _timerStateList?.Clear();
        for (int i = 0; i < GimmickTabLogic.MaxTimers; i++)
            _timerStateList?.Add(BuildTimerRow(i));
    }

    private VisualElement BuildStateRow(int index, bool withValue, bool isPlayer)
    {
        var row = new VisualElement();
        row.AddToClassList("gimmick-state-row");

        var idx = new Label(index.ToString());
        idx.AddToClassList("gimmick-state-index");
        row.Add(idx);

        var name = new TextField { maxLength = GimmickTabLogic.LabelMaxLength };
        name.AddToClassList("gimmick-state-name");
        name.RegisterValueChangedCallback(e =>
        {
            if (isPlayer) _logic.SetPlayerStateLabel(index, e.newValue);
            else _logic.SetWorldStateLabel(index, e.newValue);
        });
        row.Add(name);

        if (withValue)
        {
            var value = new IntegerField();
            value.AddToClassList("gimmick-state-value");
            value.RegisterValueChangedCallback(e =>
            {
                int clamped = GimmickTabLogic.ClampStateValue(e.newValue);
                if (isPlayer) _logic.SetPlayerStateInitial(index, clamped);
                else _logic.SetWorldStateInitial(index, clamped);
                if (clamped != e.newValue)
                    value.SetValueWithoutNotify(clamped);
            });
            row.Add(value);
        }

        return row;
    }

    private VisualElement BuildTimerRow(int index)
    {
        var row = new VisualElement();
        row.AddToClassList("gimmick-state-row");

        var idx = new Label(index.ToString());
        idx.AddToClassList("gimmick-state-index");
        row.Add(idx);

        var name = new TextField { maxLength = GimmickTabLogic.LabelMaxLength };
        name.AddToClassList("gimmick-state-name");
        name.RegisterValueChangedCallback(e => _logic.SetTimerLabel(index, e.newValue));
        row.Add(name);

        return row;
    }

    // 行のテキストフィールドはインデックス順に並ぶので順番に値を流し込む。
    private void SyncStateValues()
    {
        SyncList(_worldStateList, GimmickTabLogic.MaxWorldStates,
            i => _logic.GetWorldStateLabel(i), i => _logic.GetWorldStateInitial(i), hasValue: true);
        SyncList(_playerStateList, GimmickTabLogic.MaxPlayerStates,
            i => _logic.GetPlayerStateLabel(i), i => _logic.GetPlayerStateInitial(i), hasValue: true);
        SyncList(_timerStateList, GimmickTabLogic.MaxTimers,
            i => _logic.GetTimerLabel(i), null, hasValue: false);
    }

    private static void SyncList(
        VisualElement list, int count, Func<int, string> labelOf, Func<int, int> valueOf, bool hasValue)
    {
        if (list == null)
            return;
        for (int i = 0; i < count && i < list.childCount; i++)
        {
            var row = list[i];
            row.Q<TextField>()?.SetValueWithoutNotify(labelOf(i));
            if (hasValue && valueOf != null)
                row.Q<IntegerField>()?.SetValueWithoutNotify(valueOf(i));
        }
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

    private void RefreshRuleList()
    {
        if (_ruleList == null)
            return;
        _ruleList.Clear();

        if (_logic.Rules.Count == 0)
        {
            var empty = new Label("ルールがありません。＋ で追加します");
            empty.AddToClassList("gimmick-rule-empty");
            _ruleList.Add(empty);
            return;
        }

        foreach (var rule in _logic.Rules)
            _ruleList.Add(BuildRuleRow(rule.ruleId, rule.label));
    }

    private VisualElement BuildRuleRow(string ruleId, string label)
    {
        var row = new VisualElement();
        row.AddToClassList("gimmick-rule-row");

        var name = new TextField { value = label, maxLength = GimmickTabLogic.LabelMaxLength };
        name.AddToClassList("gimmick-rule-name");
        // 空入力での確定は元の名前に戻す（命名ルール: 空不可）。
        name.RegisterCallback<FocusOutEvent>(_ =>
        {
            if (!_logic.RenameRule(ruleId, name.value))
                name.SetValueWithoutNotify(label);
            else
                label = name.value.Trim();
        });
        row.Add(name);

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
