using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

/// <summary>
/// ギミックのルール編集画面の UI 制御（screens-and-modes.md 11.7.4）。
/// <see cref="GimmickTabController"/> の「編集」/ 追加から開かれ、単一ルールの
/// 入力イベント / 条件 / アクションを編集する。編集状態は <see cref="GimmickRuleEditLogic"/> が保持する。
///
/// 本スライスは「種別選択 + 追加 / 削除 / 並び替え + 文字メッセージのデフォルト言語入力」を担当する。
/// 種別ごとの詳細パラメータフォーム（値参照・比較演算・オブジェクト指定等）は後続スライス。
/// </summary>
public class RuleEditController
{
    /// <summary>戻る / 閉じたとき。上位がルール一覧を再表示する。</summary>
    public event Action Closed;

    private readonly VisualElement _overlay;
    private readonly Button _btnBack;
    private readonly TextField _title;
    private readonly Button _addTrigger;
    private readonly Button _addCondition;
    private readonly Button _addAction;
    private readonly VisualElement _triggerList;
    private readonly VisualElement _conditionList;
    private readonly VisualElement _actionList;
    private readonly Label _flash;

    private GimmickTabLogic _tabLogic;
    private GimmickRuleEditLogic _edit;
    private string _ruleId;
    private IVisualElementScheduledItem _flashHide;

    public RuleEditController(VisualElement root)
    {
        if (root == null)
            throw new ArgumentNullException(nameof(root));

        _overlay = root.Q("gimmick-rule-editor");
        _btnBack = root.Q<Button>("rule-edit-back");
        _title = root.Q<TextField>("rule-edit-title");
        _addTrigger = root.Q<Button>("rule-edit-add-trigger");
        _addCondition = root.Q<Button>("rule-edit-add-condition");
        _addAction = root.Q<Button>("rule-edit-add-action");
        _triggerList = root.Q("rule-edit-trigger-list");
        _conditionList = root.Q("rule-edit-condition-list");
        _actionList = root.Q("rule-edit-action-list");
        _flash = root.Q<Label>("rule-edit-flash");

        if (_title != null)
            _title.maxLength = GimmickTabLogic.LabelMaxLength;

        if (_btnBack != null) _btnBack.clicked += Close;
        if (_addTrigger != null) _addTrigger.clicked += OnAddTrigger;
        if (_addCondition != null) _addCondition.clicked += OnAddCondition;
        if (_addAction != null) _addAction.clicked += OnAddAction;
        _title?.RegisterCallback<FocusOutEvent>(_ => CommitTitle());
    }

    public bool IsOpen => _overlay != null && !_overlay.ClassListContains("overlay-hidden");

    /// <summary>指定ルールの編集画面を開く。ルールが見つからなければ何もしない。</summary>
    public void Open(GimmickTabLogic tabLogic, string ruleId)
    {
        if (tabLogic == null)
            return;
        var rule = FindRule(tabLogic, ruleId);
        if (rule == null)
            return;

        _tabLogic = tabLogic;
        _ruleId = ruleId;
        _edit = new GimmickRuleEditLogic(rule);

        _title?.SetValueWithoutNotify(rule.label);
        RefreshAll();
        _overlay?.EnableInClassList("overlay-hidden", false);
    }

    public void Close()
    {
        _overlay?.EnableInClassList("overlay-hidden", true);
        _edit = null;
        _tabLogic = null;
        _ruleId = null;
        Closed?.Invoke();
    }

    private static GimmickRule FindRule(GimmickTabLogic tabLogic, string ruleId)
    {
        foreach (var r in tabLogic.Rules)
            if (r.ruleId == ruleId)
                return r;
        return null;
    }

    private void CommitTitle()
    {
        if (_tabLogic == null || _title == null)
            return;
        // 空名は拒否して元の名前に戻す（命名ルール: 1〜20 文字・空不可）。
        if (!_tabLogic.RenameRule(_ruleId, _title.value))
        {
            var rule = FindRule(_tabLogic, _ruleId);
            _title.SetValueWithoutNotify(rule?.label ?? "");
        }
    }

    private void RefreshAll()
    {
        RefreshTriggers();
        RefreshConditions();
        RefreshActions();
    }

    // ── 入力イベント ──────────────────────────────────────────────────────────

    private void OnAddTrigger()
    {
        if (_edit?.AddTrigger() == null)
            ShowFlash($"入力イベントは最大 {GimmickRuleEditLogic.MaxTriggers} 個までです");
        else
            RefreshTriggers();
    }

    private void RefreshTriggers()
    {
        if (_triggerList == null || _edit == null)
            return;
        _triggerList.Clear();
        if (_edit.Triggers.Count == 0)
        {
            _triggerList.Add(EmptyHint("入力イベントを追加してください"));
            return;
        }
        for (int i = 0; i < _edit.Triggers.Count; i++)
        {
            int index = i;
            var row = BuildRow(
                GimmickTypeCatalog.TriggerCategories, GimmickTypeCatalog.TriggerLabel, _edit.Triggers[i].type,
                newType => { _edit.SetTriggerType(index, newType); RefreshTriggers(); },
                () => { _edit.MoveTrigger(index, index - 1); RefreshTriggers(); },
                () => { _edit.MoveTrigger(index, index + 1); RefreshTriggers(); },
                () => { _edit.RemoveTrigger(index); RefreshTriggers(); });
            _triggerList.Add(row);
        }
    }

    // ── 条件 ──────────────────────────────────────────────────────────────────

    private void OnAddCondition()
    {
        if (_edit?.AddCondition() == null)
            ShowFlash($"条件は最大 {GimmickRuleEditLogic.MaxConditions} 個までです");
        else
            RefreshConditions();
    }

    private void RefreshConditions()
    {
        if (_conditionList == null || _edit == null)
            return;
        _conditionList.Clear();
        if (_edit.Conditions.Count == 0)
        {
            _conditionList.Add(EmptyHint("条件なし（常に成立）"));
            return;
        }
        for (int i = 0; i < _edit.Conditions.Count; i++)
        {
            int index = i;
            var row = BuildRow(
                GimmickTypeCatalog.ConditionCategories, GimmickTypeCatalog.ConditionLabel, _edit.Conditions[i].type,
                newType => { _edit.SetConditionType(index, newType); RefreshConditions(); },
                () => { _edit.MoveCondition(index, index - 1); RefreshConditions(); },
                () => { _edit.MoveCondition(index, index + 1); RefreshConditions(); },
                () => { _edit.RemoveCondition(index); RefreshConditions(); });
            _conditionList.Add(row);
        }
    }

    // ── アクション ────────────────────────────────────────────────────────────

    private void OnAddAction()
    {
        if (_edit?.AddAction() == null)
            ShowFlash($"アクションは最大 {GimmickRuleEditLogic.MaxActions} 個までです");
        else
            RefreshActions();
    }

    private void RefreshActions()
    {
        if (_actionList == null || _edit == null)
            return;
        _actionList.Clear();
        if (_edit.Actions.Count == 0)
        {
            _actionList.Add(EmptyHint("アクションを追加してください"));
            return;
        }
        for (int i = 0; i < _edit.Actions.Count; i++)
        {
            int index = i;
            var action = _edit.Actions[i];
            var row = BuildRow(
                GimmickTypeCatalog.ActionCategories, GimmickTypeCatalog.ActionLabel, action.type,
                newType => { _edit.SetActionType(index, newType); RefreshActions(); },
                () => { _edit.MoveAction(index, index - 1); RefreshActions(); },
                () => { _edit.MoveAction(index, index + 1); RefreshActions(); },
                () => { _edit.RemoveAction(index); RefreshActions(); });

            // 文字メッセージアクションはデフォルト言語のテキスト入力欄を表示する
            if (action.type == "showMessage")
                row.Add(BuildMessageField(index, action));

            _actionList.Add(row);
        }
    }

    private VisualElement BuildMessageField(int actionIndex, GimmickAction action)
    {
        var wrap = new VisualElement();
        wrap.AddToClassList("gimmick-edit-message");

        var field = new TextField("メッセージ")
        {
            multiline = true,
            maxLength = GimmickRuleEditLogic.MaxMessageLength,
            value = DefaultMessageText(action),
        };
        field.AddToClassList("gimmick-edit-message-field");
        field.RegisterValueChangedCallback(e =>
        {
            if (string.IsNullOrEmpty(e.newValue))
                _edit.RemoveActionMessage(actionIndex, "");
            else
                _edit.SetActionMessage(actionIndex, "", e.newValue);
        });
        wrap.Add(field);
        return wrap;
    }

    private static string DefaultMessageText(GimmickAction action)
    {
        if (action.texts == null)
            return "";
        foreach (var t in action.texts)
            if (t != null && t.lang == "")
                return t.text ?? "";
        return "";
    }

    // ── 行ビルダー ────────────────────────────────────────────────────────────

    // カテゴリ + 種別の 2 段ドロップダウン + 上 / 下 / 削除 ボタンの 1 行を生成する。
    // 種別が多いので、まずカテゴリ（ジャンル）で絞り込んでから種別を選ぶ。
    private VisualElement BuildRow(
        IReadOnlyList<GimmickTypeCatalog.Category> categories,
        Func<string, string> labelOf,
        string currentType,
        Action<string> onTypeChanged,
        Action onMoveUp,
        Action onMoveDown,
        Action onRemove)
    {
        var row = new VisualElement();
        row.AddToClassList("gimmick-edit-row");

        // 上段: カテゴリドロップダウン + 並び替え / 削除ボタン
        var main = new VisualElement();
        main.AddToClassList("gimmick-edit-row-main");

        int catIndex = GimmickTypeCatalog.CategoryIndexOf(categories, currentType);
        var category = categories[catIndex];

        var catChoices = new List<string>(categories.Count);
        foreach (var cat in categories)
            catChoices.Add(cat.Label);

        var categoryDropdown = new DropdownField(catChoices, catIndex);
        categoryDropdown.AddToClassList("gimmick-edit-category");
        categoryDropdown.RegisterValueChangedCallback(_ =>
        {
            int idx = categoryDropdown.index;
            if ((uint)idx < (uint)categories.Count)
                // カテゴリ変更時はそのカテゴリの先頭種別に切り替える（再構築で種別欄が更新される）
                onTypeChanged(categories[idx].TypeIds[0]);
        });
        main.Add(categoryDropdown);

        main.Add(MakeBtn("gimmick-icon-btn--up", "上へ移動", onMoveUp));
        main.Add(MakeBtn("gimmick-icon-btn--down", "下へ移動", onMoveDown));
        main.Add(MakeBtn("gimmick-icon-btn--close", "削除", onRemove));
        row.Add(main);

        // 下段: 選択中カテゴリ内の種別ドロップダウン
        var typeChoices = new List<string>(category.TypeIds.Length);
        foreach (var id in category.TypeIds)
            typeChoices.Add(labelOf(id));

        int typeIndex = Array.IndexOf(category.TypeIds, currentType);
        if (typeIndex < 0) typeIndex = 0;

        var typeDropdown = new DropdownField(typeChoices, typeIndex);
        typeDropdown.AddToClassList("gimmick-edit-type");
        typeDropdown.RegisterValueChangedCallback(_ =>
        {
            int idx = typeDropdown.index;
            if ((uint)idx < (uint)category.TypeIds.Length)
                onTypeChanged(category.TypeIds[idx]);
        });
        row.Add(typeDropdown);

        return row;
    }

    // アイコンボタン（背景画像は USS の修飾クラスで割り当て）
    private static Button MakeBtn(string iconClass, string tooltip, Action onClick)
    {
        var btn = new Button(onClick) { text = "", tooltip = tooltip };
        btn.AddToClassList("gimmick-edit-row-btn");
        btn.AddToClassList("gimmick-icon-btn");
        btn.AddToClassList(iconClass);
        return btn;
    }

    private static Label EmptyHint(string text)
    {
        var label = new Label(text);
        label.AddToClassList("gimmick-edit-empty");
        return label;
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
