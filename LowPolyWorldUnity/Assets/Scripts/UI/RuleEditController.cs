using System;
using System.Collections.Generic;
using UnityEngine.UIElements;
using P = GimmickParamSchema.Param;

/// <summary>
/// ギミックのルール編集画面の UI 制御（screens-and-modes.md 11.7.4）。
/// <see cref="GimmickTabController"/> の「編集」/ 追加から開かれ、単一ルールの
/// 入力イベント / 条件 / アクションを編集する。編集状態は <see cref="GimmickRuleEditLogic"/> が保持する。
///
/// 種別選択 + 追加 / 削除 / 並び替え に加えて、選択中の種別に応じた**詳細パラメータフォーム**
/// （<see cref="GimmickParamSchema"/> 駆動）を表示する: 値参照・比較演算・対象プレイヤー・ステート /
/// タイマー番号・待機秒数・各種数値など。各入力欄は対象データオブジェクトのフィールドを直接編集する。
/// オブジェクト / ポータル等のワールド要素 ID は現状テキスト入力（3D タップ / 一覧選択は後続スライス）。
/// </summary>
public class RuleEditController
{
    /// <summary>戻る / 閉じたとき。上位がルール一覧を再表示する。</summary>
    public event Action Closed;

    private readonly VisualElement _overlay;
    private readonly Button _btnBack;
    private readonly TextField _title;
    private readonly VisualElement _triggerList;
    private readonly VisualElement _conditionList;
    private readonly VisualElement _actionList;
    private readonly Label _flash;

    private GimmickTabLogic _tabLogic;
    private GimmickRuleEditLogic _edit;
    private string _ruleId;
    private IVisualElementScheduledItem _flashHide;
    private readonly GimmickTypePickerController _picker;
    private readonly UiListDragReorder _triggerReorder;
    private readonly UiListDragReorder _condReorder;
    private readonly UiListDragReorder _actionReorder;

    public RuleEditController(VisualElement root)
    {
        if (root == null)
            throw new ArgumentNullException(nameof(root));

        _picker = new GimmickTypePickerController(root);
        _overlay = root.Q("gimmick-rule-editor");
        _btnBack = root.Q<Button>("rule-edit-back");
        _title = root.Q<TextField>("rule-edit-title");
        _triggerList = root.Q("rule-edit-trigger-list");
        _conditionList = root.Q("rule-edit-condition-list");
        _actionList = root.Q("rule-edit-action-list");
        _flash = root.Q<Label>("rule-edit-flash");

        if (_triggerList != null)
            _triggerReorder = UiListDragReorder.For(_triggerList, (from, to) => { _edit?.MoveTrigger(from, to); RefreshTriggers(); });
        if (_conditionList != null)
            _condReorder = UiListDragReorder.For(_conditionList, (from, to) => { _edit?.MoveCondition(from, to); RefreshConditions(); });
        if (_actionList != null)
            _actionReorder = UiListDragReorder.For(_actionList, (from, to) => { _edit?.MoveAction(from, to); RefreshActions(); });

        if (_title != null)
            _title.maxLength = GimmickTabLogic.LabelMaxLength;

        if (_btnBack != null) _btnBack.clicked += Close;
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
        _picker?.Close();
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
            ShowFlash($"きっかけは最大 {GimmickRuleEditLogic.MaxTriggers} 個までです");
        else
            RefreshTriggers();
    }

    private void RefreshTriggers()
    {
        if (_triggerList == null || _edit == null)
            return;
        _triggerList.Clear();
        _triggerReorder?.Reset();
        if (_edit.Triggers.Count == 0)
            _triggerList.Add(EmptyHint("きっかけなし"));
        for (int i = 0; i < _edit.Triggers.Count; i++)
        {
            int index = i;
            var trigger = _edit.Triggers[i];
            var row = BuildRow(
                "きっかけの種類", GimmickTypeCatalog.TriggerCategories,
                GimmickTypeCatalog.TriggerLabel, GimmickTypeCatalog.TriggerDesc, trigger.type,
                newType => { _edit.SetTriggerType(index, newType); RefreshTriggers(); },
                _triggerReorder,
                () => { _edit.RemoveTrigger(index); RefreshTriggers(); });
            AddParams(row, GimmickParamSchema.ForTrigger(trigger.type), tok => BuildTriggerParam(tok, trigger));
            _triggerList.Add(row);
        }
        _triggerList.Add(ListAddButton("＋ きっかけを追加", OnAddTrigger));
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
        _condReorder?.Reset();
        if (_edit.Conditions.Count == 0)
            _conditionList.Add(EmptyHint("条件なし（常に成立）"));
        for (int i = 0; i < _edit.Conditions.Count; i++)
        {
            int index = i;
            var cond = _edit.Conditions[i];
            var row = BuildRow(
                "条件の種類", GimmickTypeCatalog.ConditionCategories,
                GimmickTypeCatalog.ConditionLabel, GimmickTypeCatalog.ConditionDesc, cond.type,
                newType => { _edit.SetConditionType(index, newType); RefreshConditions(); },
                _condReorder,
                () => { _edit.RemoveCondition(index); RefreshConditions(); });
            AddParams(row, GimmickParamSchema.ForCondition(cond.type), tok => BuildConditionParam(tok, cond));
            _conditionList.Add(row);
        }
        _conditionList.Add(ListAddButton("＋ 条件を追加", OnAddCondition));
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
        _actionReorder?.Reset();
        if (_edit.Actions.Count == 0)
            _actionList.Add(EmptyHint("アクションなし"));
        for (int i = 0; i < _edit.Actions.Count; i++)
        {
            int index = i;
            var action = _edit.Actions[i];
            var row = BuildRow(
                "アクションの種類", GimmickTypeCatalog.ActionCategories,
                GimmickTypeCatalog.ActionLabel, GimmickTypeCatalog.ActionDesc, action.type,
                newType => { _edit.SetActionType(index, newType); RefreshActions(); },
                _actionReorder,
                () => { _edit.RemoveAction(index); RefreshActions(); });
            AddParams(row, GimmickParamSchema.ForAction(action.type), tok => BuildActionParam(tok, index, action));
            _actionList.Add(row);
        }
        _actionList.Add(ListAddButton("＋ アクションを追加", OnAddAction));
    }

    // 一覧最下部の全幅追加ボタン（リスト要素の幅に揃える）。
    private static Button ListAddButton(string text, Action onClick)
    {
        var btn = new Button(onClick) { text = text };
        btn.AddToClassList("gimmick-template-top-btn");
        btn.AddToClassList("gimmick-list-add--flush");
        return btn;
    }

    // ── パラメータフォーム ────────────────────────────────────────────────────

    // スキーマのトークン列を展開し、行にパラメータパネルを追加する（空なら何もしない）。
    private void AddParams(VisualElement row, IReadOnlyList<P> tokens, Func<P, VisualElement> build)
    {
        if (tokens == null || tokens.Count == 0)
            return;
        var panel = new VisualElement();
        panel.AddToClassList("gimmick-edit-params");
        foreach (var tok in tokens)
        {
            var field = build(tok);
            if (field != null)
                panel.Add(field);
        }
        row.Add(panel);
    }

    // 入力イベントのパラメータ欄。
    private VisualElement BuildTriggerParam(P token, GimmickTrigger t) => token switch
    {
        P.TrigObjectId => IdField("対象オブジェクト（空 = 全て）", t.targetId, v => t.targetId = v),
        P.TrigAreaId => IdField("対象エリア", t.targetId, v => t.targetId = v),
        P.TrigTimerIndex => TimerField("タイマー", t.timerIndex, v => t.timerIndex = v),
        P.TrigTimerSeconds => FloatField("到達秒", t.timerSeconds, v => t.timerSeconds = v),
        P.TrigSubroutineId => IdField("サブルーチン ID", t.targetId, v => t.targetId = v),
        _ => null,
    };

    // 条件のパラメータ欄。
    private VisualElement BuildConditionParam(P token, GimmickCondition c) => token switch
    {
        P.CondWorldStateIndex => StateField("ワールド変数", world: true, c.stateIndex, v => c.stateIndex = v),
        P.CondPlayerStateIndex => StateField("プレイヤー変数", world: false, c.stateIndex, v => c.stateIndex = v),
        P.CondTimerIndex => TimerField("タイマー", c.timerIndex, v => c.timerIndex = v),
        P.CondCompareOp => CompareOpField(c),
        P.CondThreshold => ValueRefField("比較する値", c.threshold),
        P.CondPlayerTarget => IdDropdown("対象プレイヤー", GimmickRuleEditLogic.PlayerTargets,
            GimmickParamSchema.PlayerTargetLabel, c.playerTarget, v => c.playerTarget = v),
        P.CondInventoryType => IdField("オブジェクト種別 ID", c.objectId, v => c.objectId = v),
        P.CondDistanceGrid => FloatField("距離（グリッド・1 = 0.5m）", c.distanceGrid, v => c.distanceGrid = v),
        P.CondRankOrder => IdDropdown("順位の方向", GimmickRuleEditLogic.RankOrders,
            GimmickParamSchema.RankOrderLabel, c.rankOrder, v => c.rankOrder = v),
        P.CondRankWithin => IntField("X 位以内（1 以上）", c.rankWithin, v => c.rankWithin = v),
        _ => null,
    };

    // アクションのパラメータ欄。
    private VisualElement BuildActionParam(P token, int actionIndex, GimmickAction a) => token switch
    {
        P.ActWorldStateIndex => StateField("ワールド変数", world: true, a.stateIndex, v => a.stateIndex = v),
        P.ActPlayerStateIndex => StateField("プレイヤー変数", world: false, a.stateIndex, v => a.stateIndex = v),
        P.ActStateOp => IdDropdown("演算", GimmickRuleEditLogic.StateOps,
            GimmickParamSchema.StateOpLabel, a.stateOp, v => a.stateOp = v),
        P.ActValue => ValueRefField("値", a.value),
        P.ActPlayerTarget => IdDropdown("対象プレイヤー", GimmickRuleEditLogic.PlayerTargets,
            GimmickParamSchema.PlayerTargetLabel, a.playerTarget, v => a.playerTarget = v),
        P.ActTimerIndex => TimerField("タイマー", a.timerIndex, v => a.timerIndex = v),
        P.ActObjectId => IdField("対象オブジェクト", a.targetId, v => a.targetId = v),
        P.ActVisible => BoolField("表示する", a.visible, v => a.visible = v),
        P.ActChangeTypeId => IdField("切り替え先の種別 ID", a.stringParam, v => a.stringParam = v),
        P.ActGrantTypeId => IdField("付与する種別 ID", a.targetId, v => a.targetId = v),
        P.ActSoundId => IdField("効果音 ID", a.targetId, v => a.targetId = v),
        P.ActVolume => FloatField("音量（0〜100）", a.floatParam, v => a.floatParam = v),
        P.ActPitch => FloatField("ピッチ（0.5〜2.0）", a.pitch, v => a.pitch = v),
        P.ActPlaybackRate => FloatField("再生速度（0.5〜2.0）", a.playbackRate, v => a.playbackRate = v),
        P.ActBgmId => IdField("BGM ID（none で停止）", a.targetId, v => a.targetId = v),
        P.ActMovePosition => PositionField("移動先（グリッド）", a.position),
        P.ActMoveSpeed => FloatField("移動速度", a.floatParam, v => a.floatParam = v),
        P.ActPortalExitId => IdField("出口ポータル ID", a.targetId, v => a.targetId = v),
        P.ActResetTarget => IdDropdown("リセット対象", GimmickRuleEditLogic.ResetTargets,
            GimmickParamSchema.ResetTargetLabel, a.resetTarget, v => a.resetTarget = v),
        P.ActEffectId => IdField("エフェクト ID", a.targetId, v => a.targetId = v),
        P.ActMoveSpeedPercent => FloatField("移動速度（0〜200%）", a.floatParam, v => a.floatParam = v),
        P.ActMarkerId => IdField("頭上マーカー ID", a.targetId, v => a.targetId = v),
        P.ActConversationId => IdField("会話 ID", a.targetId, v => a.targetId = v),
        P.ActSubroutineId => IdField("サブルーチン ID", a.targetId, v => a.targetId = v),
        P.ActWaitSeconds => FloatField("待機秒数（0〜60）", a.floatParam, v => a.floatParam = v),
        P.ActMessage => BuildMessageField(actionIndex, a),
        _ => null,
    };

    // ── 比較演算（mod_eq のとき除数・余り欄を追加表示）────────────────────────

    private VisualElement CompareOpField(GimmickCondition c)
    {
        var wrap = new VisualElement();
        wrap.AddToClassList("gimmick-edit-param-group");

        var modBox = new VisualElement();
        void RefreshMod()
        {
            modBox.Clear();
            if (c.op == "mod_eq" && GimmickParamSchema.SupportsModParams(c.type))
            {
                modBox.Add(IntField("除数 X（2 以上）", c.modBy, v => c.modBy = v));
                modBox.Add(IntField("余り Y", c.modResult, v => c.modResult = v));
            }
        }

        wrap.Add(IdDropdown("比較", GimmickRuleEditLogic.CompareOps, GimmickParamSchema.CompareOpLabel, c.op,
            v => { c.op = v; RefreshMod(); }));
        wrap.Add(modBox);
        RefreshMod();
        return wrap;
    }

    // ── 値参照（固定値 / ステート参照 / 合計 / 乱数）───────────────────────────

    private VisualElement ValueRefField(string label, GimmickValueJson v)
    {
        var wrap = new VisualElement();
        wrap.AddToClassList("gimmick-edit-valueref");
        wrap.Add(new Label(label) { pickingMode = PickingMode.Ignore });

        var sub = new VisualElement();
        void RefreshSub()
        {
            sub.Clear();
            switch (v.kind)
            {
                case "fixed":
                    sub.Add(IntField("値", v.value, x => v.value = x));
                    break;
                case "worldState":
                    sub.Add(StateField("ワールド変数", world: true, v.stateIndex, x => v.stateIndex = x));
                    break;
                case "playerState":
                    sub.Add(StateField("プレイヤー変数", world: false, v.stateIndex, x => v.stateIndex = x));
                    sub.Add(IdDropdown("対象", ValueRefPlayerTargets,
                        GimmickParamSchema.PlayerTargetLabel, v.playerTarget, x => v.playerTarget = x));
                    break;
                case "allPlayersSum":
                    sub.Add(StateField("プレイヤー変数", world: false, v.stateIndex, x => v.stateIndex = x));
                    break;
                case "random":
                    sub.Add(IntField("最小値", v.min, x => v.min = x));
                    if (!v.maxIsPlayerCount)
                        sub.Add(IntField("最大値", v.max, x => v.max = x));
                    sub.Add(BoolField("最大値 = 現在人数", v.maxIsPlayerCount, x => { v.maxIsPlayerCount = x; RefreshSub(); }));
                    break;
            }
        }

        wrap.Add(IdDropdown("種別", GimmickRuleEditLogic.ValueKinds, GimmickParamSchema.ValueKindLabel, v.kind,
            x => { v.kind = x; RefreshSub(); }));
        wrap.Add(sub);
        RefreshSub();
        return wrap;
    }

    // 値参照のプレイヤーステート対象は input / opponent のみ（all は変換で拒否される）。
    private static readonly string[] ValueRefPlayerTargets = { "input", "opponent" };

    // ── 基本フィールドビルダー ────────────────────────────────────────────────

    private static TextField IdField(string label, string current, Action<string> onChange)
    {
        var f = new TextField(label) { value = current ?? "" };
        f.AddToClassList("gimmick-edit-param");
        f.RegisterValueChangedCallback(e => onChange(e.newValue ?? ""));
        return f;
    }

    private static IntegerField IntField(string label, int current, Action<int> onChange)
    {
        var f = new IntegerField(label) { value = current };
        f.AddToClassList("gimmick-edit-param");
        f.RegisterValueChangedCallback(e => onChange(e.newValue));
        return f;
    }

    private static FloatField FloatField(string label, float current, Action<float> onChange)
    {
        var f = new FloatField(label) { value = current };
        f.AddToClassList("gimmick-edit-param");
        f.RegisterValueChangedCallback(e => onChange(e.newValue));
        return f;
    }

    private static Toggle BoolField(string label, bool current, Action<bool> onChange)
    {
        var f = new Toggle(label) { value = current };
        f.AddToClassList("gimmick-edit-param");
        f.RegisterValueChangedCallback(e => onChange(e.newValue));
        return f;
    }

    private static DropdownField IdDropdown(
        string label, IReadOnlyList<string> ids, Func<string, string> labelOf, string current, Action<string> onChange)
    {
        var choices = new List<string>();
        foreach (var id in ids)
            choices.Add(labelOf(id));
        int idx = IndexOf(ids, current);
        if (idx < 0) idx = 0;
        var f = new DropdownField(label, choices, idx);
        f.AddToClassList("gimmick-edit-param");
        f.RegisterValueChangedCallback(e =>
        {
            int i = choices.IndexOf(e.newValue);
            if (i >= 0) onChange(ids[i]);
        });
        return f;
    }

    // 定義済みステートのドロップダウン（未定義なら数値入力にフォールバック）。
    private VisualElement StateField(string label, bool world, int current, Action<int> onChange)
    {
        var indices = world ? _tabLogic?.WorldStateIndices : _tabLogic?.PlayerStateIndices;
        if (indices == null || indices.Count == 0)
            return IntField(label + "（番号）", current, onChange);

        var choices = new List<string>();
        foreach (var i in indices)
        {
            string name = world ? _tabLogic.GetWorldStateLabel(i) : _tabLogic.GetPlayerStateLabel(i);
            choices.Add(string.IsNullOrEmpty(name) ? $"（無名 {i}）" : name);
        }
        int sel = IndexOfInt(indices, current);
        if (sel < 0) sel = 0;
        var f = new DropdownField(label, choices, sel);
        f.AddToClassList("gimmick-edit-param");
        // 同名があり得るため表示文字列でなくドロップダウンの index で対応付ける。
        f.RegisterValueChangedCallback(_ =>
        {
            int i = f.index;
            if (i >= 0 && i < indices.Count) onChange(indices[i]);
        });
        return f;
    }

    // 定義済みタイマーのドロップダウン（未定義なら数値入力にフォールバック）。
    private VisualElement TimerField(string label, int current, Action<int> onChange)
    {
        var indices = _tabLogic?.TimerIndices;
        if (indices == null || indices.Count == 0)
            return IntField(label + "（番号）", current, onChange);

        var choices = new List<string>();
        foreach (var i in indices)
        {
            string name = _tabLogic.GetTimerLabel(i);
            choices.Add(string.IsNullOrEmpty(name) ? $"（無名 {i}）" : name);
        }
        int sel = IndexOfInt(indices, current);
        if (sel < 0) sel = 0;
        var f = new DropdownField(label, choices, sel);
        f.AddToClassList("gimmick-edit-param");
        f.RegisterValueChangedCallback(_ =>
        {
            int i = f.index;
            if (i >= 0 && i < indices.Count) onChange(indices[i]);
        });
        return f;
    }

    // 3 軸グリッド座標入力。
    private VisualElement PositionField(string label, IntVec3Json pos)
    {
        var wrap = new VisualElement();
        wrap.AddToClassList("gimmick-edit-param-group");
        wrap.Add(new Label(label) { pickingMode = PickingMode.Ignore });
        var row = new VisualElement();
        row.AddToClassList("gimmick-edit-param-xyz");
        row.Add(IntField("X", pos.x, v => pos.x = v));
        row.Add(IntField("Y", pos.y, v => pos.y = v));
        row.Add(IntField("Z", pos.z, v => pos.z = v));
        wrap.Add(row);
        return wrap;
    }

    private VisualElement BuildMessageField(int actionIndex, GimmickAction action)
    {
        var wrap = new VisualElement();
        wrap.AddToClassList("gimmick-edit-message");

        // 既定言語はアプリの設定言語（対応外は英語）。メッセージはこの言語コードで保存する。
        string sysLang = DeviceLanguage.CurrentCode();

        // 旧 "" 既定テキストがあればシステム言語の初期表示として引き継ぐ（編集時にその言語へ移行）。
        string init = MessageText(action, sysLang);
        if (string.IsNullOrEmpty(init))
            init = MessageText(action, SupportedLanguages.Default);

        var primary = new TextField($"メッセージ（{SupportedLanguages.LabelOf(sysLang)}）")
        {
            multiline = true,
            maxLength = GimmickRuleEditLogic.MaxMessageLength,
            value = init,
        };
        primary.AddToClassList("gimmick-edit-message-field");
        primary.RegisterValueChangedCallback(e =>
        {
            if (string.IsNullOrEmpty(e.newValue))
            {
                _edit.RemoveActionMessage(actionIndex, sysLang);
            }
            else
            {
                _edit.SetActionMessage(actionIndex, sysLang, e.newValue);
                _edit.RemoveActionMessage(actionIndex, SupportedLanguages.Default); // 旧 "" 既定を移行
            }
        });
        wrap.Add(primary);

        // 詳細: システム言語以外の言語別上書き（未入力の言語は再生時に英語優先でフォールバック）。
        var detail = new Foldout { text = "詳細（言語別）", value = false };
        detail.AddToClassList("gimmick-edit-message-detail");
        foreach (var lang in SupportedLanguages.All)
            if (lang.Code != sysLang)
                detail.Add(MessageLangField(actionIndex, action, lang.Label, lang.Code));
        wrap.Add(detail);

        return wrap;
    }

    // 指定言語の文字メッセージ入力欄（空にするとその言語を削除）。
    private TextField MessageLangField(int actionIndex, GimmickAction action, string label, string lang)
    {
        var field = new TextField(label)
        {
            multiline = true,
            maxLength = GimmickRuleEditLogic.MaxMessageLength,
            value = MessageText(action, lang),
        };
        field.AddToClassList("gimmick-edit-message-field");
        field.RegisterValueChangedCallback(e =>
        {
            if (string.IsNullOrEmpty(e.newValue))
                _edit.RemoveActionMessage(actionIndex, lang);
            else
                _edit.SetActionMessage(actionIndex, lang, e.newValue);
        });
        return field;
    }

    private static string MessageText(GimmickAction action, string lang)
    {
        if (action.texts == null)
            return "";
        foreach (var t in action.texts)
            if (t != null && t.lang == lang)
                return t.text ?? "";
        return "";
    }

    private static int IndexOf(IReadOnlyList<string> list, string value)
    {
        for (int i = 0; i < list.Count; i++)
            if (list[i] == value)
                return i;
        return -1;
    }

    private static int IndexOfInt(IReadOnlyList<int> list, int value)
    {
        for (int i = 0; i < list.Count; i++)
            if (list[i] == value)
                return i;
        return -1;
    }

    // ── 行ビルダー ────────────────────────────────────────────────────────────

    // 種類セレクタボタン + ドラッグハンドル + 削除 ボタンの 1 行を生成する。
    // セレクタをタップすると、カテゴリ見出し付きの選択リスト（ピッカー）が開く。
    // 並べ替えはハンドルのドラッグ＆ドロップ（reorder ヘルパー）で行う。
    private VisualElement BuildRow(
        string pickerTitle,
        IReadOnlyList<GimmickTypeCatalog.Category> categories,
        Func<string, string> labelOf,
        Func<string, string> descOf,
        string currentType,
        Action<string> onTypeChanged,
        UiListDragReorder reorder,
        Action onRemove)
    {
        var row = new VisualElement();
        row.AddToClassList("gimmick-edit-row");

        var main = new VisualElement();
        main.AddToClassList("gimmick-edit-row-main");

        if (reorder != null)
        {
            string ghost = labelOf(currentType);
            main.Add(reorder.CreateHandle(row, string.IsNullOrEmpty(ghost) ? "（種類なし）" : ghost));
        }

        // 現在の種類を表示するセレクタボタン（タップで選択リストを開く・右端に icon_next）
        var typeButton = new Button(() =>
            _picker?.Open(pickerTitle, categories, labelOf, descOf, currentType, onTypeChanged));
        typeButton.AddToClassList("gimmick-edit-typebtn");

        var typeLabel = new Label(labelOf(currentType)) { pickingMode = PickingMode.Ignore };
        typeLabel.AddToClassList("gimmick-edit-typebtn-label");
        typeButton.Add(typeLabel);

        var chevron = new VisualElement { pickingMode = PickingMode.Ignore };
        chevron.AddToClassList("gimmick-edit-typebtn-next");
        chevron.AddToClassList("icon-next");
        typeButton.Add(chevron);

        main.Add(typeButton);

        main.Add(MakeBtn("gimmick-icon-btn--close", "削除", onRemove));

        row.Add(main);
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
