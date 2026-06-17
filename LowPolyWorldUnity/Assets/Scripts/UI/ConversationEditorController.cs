using System;
using UnityEngine.UIElements;

/// <summary>
/// 1 つの会話のセリフ行・選択肢を編集するオーバーレイ UI（screens-and-modes.md 11.7.4b）。
///
/// 行の追加 / 削除 / 並び替え・話者 / 本文 / 選択肢の言語別入力（既定 = アプリの設定言語・
/// 「詳細」でそれ以外の言語）・分岐（次へ / 終了 / 別の行）・到達時 / 選択時の変数変更
/// （onReach / effect）を扱う。編集状態は <see cref="ConversationEditLogic"/> が保持する。
/// </summary>
public class ConversationEditorController
{
    /// <summary>＜戻るで閉じたとき（一覧側で名前・行数を更新する）。</summary>
    public event Action Closed;

    private readonly VisualElement _overlay;
    private readonly Button _btnBack;
    private readonly TextField _title;
    private readonly VisualElement _lineList;
    private readonly Label _flash;

    private readonly ConversationLibraryLogic _library;
    private readonly GimmickTabLogic _tabLogic; // 変数選択ドロップダウン用（定義済みワールド / プレイヤー変数）
    private readonly SpeakerLibraryLogic _speakers; // 話者選択ドロップダウン用（ワールド単位の話者定義）
    private ConversationJson _conversation;
    private ConversationEditLogic _edit;
    private IVisualElementScheduledItem _flashHide;

    // 既定では話者・本文だけ表示し、変数変更 / 分岐先などは「＋」で開いたものだけ表示する
    // （データが入っている項目は常に表示）。ここは "開いた" UI 状態（行 ID + 項目キー）を覚える。
    private readonly System.Collections.Generic.HashSet<string> _revealed = new();

    public ConversationEditorController(
        VisualElement root, ConversationLibraryLogic library = null, GimmickTabLogic tabLogic = null,
        SpeakerLibraryLogic speakers = null)
    {
        if (root == null)
            throw new ArgumentNullException(nameof(root));
        _library = library;
        _tabLogic = tabLogic;
        _speakers = speakers;

        _overlay = root.Q("conv-editor");
        _btnBack = root.Q<Button>("conv-editor-back");
        _title = root.Q<TextField>("conv-editor-title");
        _lineList = root.Q("conv-editor-lines");
        _flash = root.Q<Label>("conv-editor-flash");

        if (_btnBack != null) _btnBack.clicked += Close;
        if (_title != null)
        {
            _title.maxLength = ConversationLibraryLogic.NameMaxLength;
            _title.RegisterCallback<FocusOutEvent>(_ => CommitTitle());
        }
    }

    public bool IsOpen => _overlay != null && !_overlay.ClassListContains("overlay-hidden");

    public void Open(ConversationJson conversation)
    {
        if (_overlay == null || conversation == null)
            return;
        _conversation = conversation;
        _edit = new ConversationEditLogic(conversation);
        _revealed.Clear();
        _title?.SetValueWithoutNotify(conversation.name);
        RefreshLines();
        _overlay.EnableInClassList("overlay-hidden", false);
    }

    public void Close()
    {
        if (_overlay == null)
            return;
        CommitTitle();
        _overlay.EnableInClassList("overlay-hidden", true);
        Closed?.Invoke();
    }

    // ── タイトル（会話名）──────────────────────────────────────────────────────

    private void CommitTitle()
    {
        if (_conversation == null || _title == null)
            return;
        var name = ConversationLibraryLogic.SanitizeName(_title.value);
        if (string.IsNullOrEmpty(name))
        {
            _title.SetValueWithoutNotify(_conversation.name); // 空は拒否して元に戻す
            return;
        }
        _conversation.name = name;
    }

    // ── セリフ行 ───────────────────────────────────────────────────────────────

    private void OnAddLine()
    {
        if (_library != null && !_library.CanAddLine)
        {
            ShowFlash($"会話のセリフ行は全体で最大 {ConversationLibraryLogic.MaxTotalLines} 行までです");
            return;
        }
        if (_edit?.AddLine() == null)
        {
            ShowFlash($"この会話のセリフ行は最大 {ConversationEditLogic.MaxLines} 行までです");
            return;
        }
        RefreshLines();
        ScrollListToBottom(_lineList);
    }

    // 一覧（ScrollView）を最下部までスクロールする（行追加直後に末尾を見せる）。
    private static void ScrollListToBottom(VisualElement listEl)
    {
        if (listEl is not ScrollView sv || sv.childCount == 0)
            return;
        var last = sv[sv.childCount - 1];
        sv.schedule.Execute(() => sv.ScrollTo(last)).StartingIn(0);
    }

    private void RefreshLines()
    {
        if (_lineList == null || _edit == null)
            return;
        _lineList.Clear();

        if (_edit.Lines.Count == 0)
        {
            var empty = new Label("セリフ行がありません。下のボタンで追加します");
            empty.AddToClassList("conv-empty");
            _lineList.Add(empty);
        }
        else
        {
            for (int i = 0; i < _edit.Lines.Count; i++)
                _lineList.Add(BuildLineCard(i, _edit.Lines[i]));
        }

        // 一覧の最下部に追加ボタン
        var add = new Button(OnAddLine) { text = "＋ セリフ行を追加" };
        add.AddToClassList("gimmick-template-top-btn");
        add.AddToClassList("gimmick-list-add--inset");
        _lineList.Add(add);
    }

    private VisualElement BuildLineCard(int index, ConversationLineJson line)
    {
        string lineId = line.lineId;
        var card = new VisualElement();
        card.AddToClassList("conv-line-card");

        // ヘッダー: 番号・操作
        var head = new VisualElement();
        head.AddToClassList("conv-line-head");

        var idx = new Label($"{index + 1}.");
        idx.AddToClassList("conv-line-index");
        head.Add(idx);

        var spacer = new VisualElement();
        spacer.style.flexGrow = 1;
        head.Add(spacer);

        head.Add(IconButton("gimmick-icon-btn--up", "上へ", () => MoveLine(lineId, -1)));
        head.Add(IconButton("gimmick-icon-btn--down", "下へ", () => MoveLine(lineId, +1)));
        head.Add(IconButton("gimmick-icon-btn--close", "削除", () =>
        {
            if (_edit.RemoveLine(lineId))
                RefreshLines();
        }));
        card.Add(head);

        // 話者（定義済みから選択）・本文（言語別）— 既定ではここだけ考えればよい
        card.Add(BuildSpeakerField(lineId, line.speakerId));
        card.Add(MultilangBlock("本文", ConversationEditLogic.TextMaxLength, true, line.texts,
            (lang, text) => _edit.SetLineText(lineId, lang, text),
            lang => _edit.RemoveLineText(lineId, lang)));

        // ── ここから下は「使うときだけ」表示する詳細項目 ──
        var onReach = line.onReach ??= new ConversationEffectJson();
        bool hasChoices = (line.choices?.Length ?? 0) > 0;
        bool hasGoto = !string.IsNullOrEmpty(line.gotoLineId); // "" = 次へ（既定）
        string keyEffect = lineId + ":onReach";
        string keyGoto = lineId + ":goto";

        // 到達時の変数変更（データあり or 開いたときだけ）
        if (onReach.kind != "none" || _revealed.Contains(keyEffect))
        {
            card.Add(BuildEffectEditor("到達時に変数を変更", onReach, () =>
            {
                onReach.kind = "none";
                _revealed.Remove(keyEffect);
                RefreshLines();
            }));
        }

        // 分岐先（選択肢が無く、データあり or 開いたときだけ）
        if (!hasChoices && (hasGoto || _revealed.Contains(keyGoto)))
        {
            var gotoBox = new VisualElement();
            gotoBox.AddToClassList("conv-optional");
            var gotoHead = new VisualElement();
            gotoHead.AddToClassList("conv-optional-head");
            gotoHead.Add(RowLabel("この後の進行"));
            var gspacer = new VisualElement();
            gspacer.style.flexGrow = 1;
            gotoHead.Add(gspacer);
            gotoHead.Add(IconButton("gimmick-icon-btn--close", "「次へ」に戻す", () =>
            {
                _edit.SetLineGoto(lineId, ConversationEditLogic.GotoNext);
                _revealed.Remove(keyGoto);
                RefreshLines();
            }));
            gotoBox.Add(gotoHead);
            gotoBox.Add(BuildGoto(line.gotoLineId, g => _edit.SetLineGoto(lineId, g)));
            card.Add(gotoBox);
        }

        // 選択肢（あるときだけ表示）
        var choices = line.choices ?? Array.Empty<ConversationChoiceJson>();
        if (choices.Length > 0)
        {
            card.Add(RowLabel("選択肢"));
            for (int c = 0; c < choices.Length; c++)
                card.Add(BuildChoiceRow(lineId, c, choices[c]));
        }

        // ── 「＋」で必要な項目を出すツールバー ──
        var tools = new VisualElement();
        tools.AddToClassList("conv-chip-row");
        if (onReach.kind == "none" && !_revealed.Contains(keyEffect))
            tools.Add(Chip("＋ 変数変更", () => { _revealed.Add(keyEffect); RefreshLines(); }));
        if (!hasChoices && !hasGoto && !_revealed.Contains(keyGoto))
            tools.Add(Chip("＋ 分岐先", () => { _revealed.Add(keyGoto); RefreshLines(); }));
        tools.Add(Chip("＋ 選択肢", () =>
        {
            if (_edit.AddChoice(lineId) == null)
                ShowFlash($"選択肢は 1 行に最大 {ConversationEditLogic.MaxChoices} 個までです");
            else
                RefreshLines();
        }));
        card.Add(tools);

        return card;
    }

    private VisualElement BuildChoiceRow(string lineId, int choiceIndex, ConversationChoiceJson choice)
    {
        var card = new VisualElement();
        card.AddToClassList("conv-choice-card");

        // ヘッダー: 「選択肢 N」+ 右上にこの選択肢を削除する✕
        var head = new VisualElement();
        head.AddToClassList("conv-choice-head");
        var title = new Label($"選択肢 {choiceIndex + 1}");
        title.AddToClassList("conv-choice-title");
        head.Add(title);
        var spacer = new VisualElement();
        spacer.style.flexGrow = 1;
        head.Add(spacer);
        head.Add(IconButton("gimmick-icon-btn--close", "この選択肢を削除", () =>
        {
            if (_edit.RemoveChoice(lineId, choiceIndex))
                RefreshLines();
        }));
        card.Add(head);

        // 選択肢テキスト（言語別）
        card.Add(MultilangBlock("テキスト", ConversationEditLogic.ChoiceTextMaxLength, false, choice.texts,
            (lang, text) => _edit.SetChoiceText(lineId, choiceIndex, lang, text),
            lang => _edit.RemoveChoiceText(lineId, choiceIndex, lang)));

        // 選んだあとの進行
        var row = new VisualElement();
        row.AddToClassList("conv-choice-row");
        row.Add(BuildGoto(choice.gotoLineId, g => _edit.SetChoiceGoto(lineId, choiceIndex, g)));
        card.Add(row);

        // 選択時の変数変更（データあり or 開いたときだけ・なければ「＋」で出す）
        var eff = choice.effect ??= new ConversationEffectJson();
        string keyEffect = lineId + ":c" + choiceIndex + ":effect";
        if (eff.kind != "none" || _revealed.Contains(keyEffect))
        {
            card.Add(BuildEffectEditor("選択時に変数を変更", eff, () =>
            {
                eff.kind = "none";
                _revealed.Remove(keyEffect);
                RefreshLines();
            }));
        }
        else
        {
            var tools = new VisualElement();
            tools.AddToClassList("conv-chip-row");
            tools.Add(Chip("＋ 変数変更", () => { _revealed.Add(keyEffect); RefreshLines(); }));
            card.Add(tools);
        }

        return card;
    }

    // ── 話者（定義済みから選択）────────────────────────────────────────────────

    private VisualElement BuildSpeakerField(string lineId, string currentSpeakerId)
    {
        var wrap = new VisualElement();
        wrap.AddToClassList("conv-ml");

        string app = DeviceLanguage.CurrentCode();
        var labels = new System.Collections.Generic.List<string> { "（話者なし）" };
        var ids = new System.Collections.Generic.List<string> { "" };
        if (_speakers != null)
            foreach (var s in _speakers.Speakers)
            {
                string name = SpeakerLibraryLogic.ResolveName(s, app);
                labels.Add(string.IsNullOrEmpty(name) ? "（名称未設定）" : name);
                ids.Add(s.speakerId);
            }

        int sel = ids.IndexOf(currentSpeakerId ?? "");
        if (sel < 0) sel = 0; // 未知 ID（削除済み等）は「話者なし」表示
        var dd = new DropdownField("話者", labels, sel);
        dd.AddToClassList("conv-ml-field");
        dd.RegisterValueChangedCallback(_ =>
        {
            int i = dd.index;
            if (i >= 0 && i < ids.Count)
                _edit.SetLineSpeakerId(lineId, ids[i]);
        });
        wrap.Add(dd);
        return wrap;
    }

    // ── 言語別テキスト入力（既定 = アプリの設定言語・詳細でそれ以外の言語）──────

    private VisualElement MultilangBlock(
        string title, int maxLen, bool multiline, GimmickTextJson[] source,
        Func<string, string, bool> set, Func<string, bool> remove)
    {
        var box = new VisualElement();
        box.AddToClassList("conv-ml");

        string app = DeviceLanguage.CurrentCode();
        string init = TextForLang(source, app);
        if (string.IsNullOrEmpty(init))
            init = TextForLang(source, ""); // 旧 "" 既定を初期表示で引き継ぐ

        box.Add(LangField($"{title}（{SupportedLanguages.LabelOf(app)}）", maxLen, multiline, init, v =>
        {
            if (string.IsNullOrEmpty(v))
            {
                remove(app);
            }
            else
            {
                set(app, v);
                remove(""); // 旧 "" 既定をアプリ設定言語へ移行
            }
        }));

        var detail = new Foldout { text = "詳細（言語別）", value = false };
        detail.AddToClassList("conv-ml-detail");
        foreach (var lang in SupportedLanguages.All)
        {
            if (lang.Code == app)
                continue;
            string code = lang.Code;
            detail.Add(LangField(lang.Label, maxLen, multiline, TextForLang(source, code), v =>
            {
                if (string.IsNullOrEmpty(v))
                    remove(code);
                else
                    set(code, v);
            }));
        }
        box.Add(detail);
        return box;
    }

    private static TextField LangField(string label, int maxLen, bool multiline, string initial, Action<string> onChange)
    {
        var f = new TextField(label) { multiline = multiline, maxLength = maxLen };
        f.AddToClassList("conv-ml-field");
        f.SetValueWithoutNotify(initial);
        f.RegisterValueChangedCallback(e => onChange(e.newValue));
        return f;
    }

    private static string TextForLang(GimmickTextJson[] texts, string lang)
    {
        if (texts == null)
            return "";
        foreach (var t in texts)
            if (t != null && t.lang == lang)
                return t.text ?? "";
        return "";
    }

    // ── 変数変更（onReach / 選択肢 effect）────────────────────────────────────

    private static readonly string[] EffectKinds = { "none", "worldState", "playerState" };
    private static readonly string[] EffectKindLabels = { "なし", "ワールド変数", "プレイヤー変数" };

    private VisualElement BuildEffectEditor(string title, ConversationEffectJson eff, Action onRemove = null)
    {
        var box = new VisualElement();
        box.AddToClassList("conv-effect");

        var head = new VisualElement();
        head.AddToClassList("conv-optional-head");
        head.Add(RowLabel(title));
        if (onRemove != null)
        {
            var sp = new VisualElement();
            sp.style.flexGrow = 1;
            head.Add(sp);
            head.Add(IconButton("gimmick-icon-btn--close", "やめる", onRemove));
        }
        box.Add(head);

        var sub = new VisualElement();
        void RefreshSub()
        {
            sub.Clear();
            if (eff.kind != "worldState" && eff.kind != "playerState")
                return;
            bool world = eff.kind == "worldState";
            sub.Add(VariableField(world, eff.stateIndex, v => eff.stateIndex = v));
            if (!world)
                sub.Add(IdDropdown("対象", GimmickRuleEditLogic.PlayerTargets,
                    GimmickParamSchema.PlayerTargetLabel, eff.playerTarget, v => eff.playerTarget = v));
            sub.Add(IdDropdown("演算", GimmickRuleEditLogic.StateOps,
                GimmickParamSchema.StateOpLabel, eff.stateOp, v => eff.stateOp = v));
            var valField = new IntegerField("値（0〜255）") { value = eff.value };
            valField.AddToClassList("conv-effect-field");
            valField.RegisterValueChangedCallback(e => eff.value = Clamp255(e.newValue));
            sub.Add(valField);
        }

        var kindChoices = new System.Collections.Generic.List<string>(EffectKindLabels);
        int sel = Array.IndexOf(EffectKinds, eff.kind);
        if (sel < 0) sel = 0;
        var kindDd = new DropdownField(kindChoices, sel);
        kindDd.AddToClassList("conv-effect-field");
        kindDd.RegisterValueChangedCallback(_ =>
        {
            int i = kindDd.index;
            if (i >= 0 && i < EffectKinds.Length)
            {
                eff.kind = EffectKinds[i];
                RefreshSub();
            }
        });
        box.Add(kindDd);
        box.Add(sub);
        RefreshSub();
        return box;
    }

    // 定義済み変数のドロップダウン（未定義なら番号入力にフォールバック）。
    private VisualElement VariableField(bool world, int current, Action<int> onChange)
    {
        var indices = world ? _tabLogic?.WorldStateIndices : _tabLogic?.PlayerStateIndices;
        string label = world ? "ワールド変数" : "プレイヤー変数";
        if (indices == null || indices.Count == 0)
        {
            var f = new IntegerField(label + "（番号）") { value = current };
            f.AddToClassList("conv-effect-field");
            f.RegisterValueChangedCallback(e => onChange(e.newValue));
            return f;
        }

        var choices = new System.Collections.Generic.List<string>();
        foreach (var i in indices)
        {
            string name = world ? _tabLogic.GetWorldStateLabel(i) : _tabLogic.GetPlayerStateLabel(i);
            choices.Add(string.IsNullOrEmpty(name) ? $"（無名 {i}）" : name);
        }
        int sel = IndexOfInt(indices, current);
        if (sel < 0) sel = 0;
        var dd = new DropdownField(label, choices, sel);
        dd.AddToClassList("conv-effect-field");
        dd.RegisterValueChangedCallback(_ =>
        {
            int i = dd.index;
            if (i >= 0 && i < indices.Count) onChange(indices[i]);
        });
        return dd;
    }

    private static DropdownField IdDropdown(
        string label, System.Collections.Generic.IReadOnlyList<string> ids,
        Func<string, string> labelOf, string current, Action<string> onChange)
    {
        var choices = new System.Collections.Generic.List<string>();
        foreach (var id in ids)
            choices.Add(labelOf(id));
        int idx = -1;
        for (int i = 0; i < ids.Count; i++)
            if (ids[i] == current) { idx = i; break; }
        if (idx < 0) idx = 0;
        var dd = new DropdownField(label, choices, idx);
        dd.AddToClassList("conv-effect-field");
        dd.RegisterValueChangedCallback(_ =>
        {
            int i = dd.index;
            if (i >= 0 && i < ids.Count) onChange(ids[i]);
        });
        return dd;
    }

    private static int IndexOfInt(System.Collections.Generic.IReadOnlyList<int> list, int value)
    {
        for (int i = 0; i < list.Count; i++)
            if (list[i] == value)
                return i;
        return -1;
    }

    private static int Clamp255(int v) => v < 0 ? 0 : v > 255 ? 255 : v;

    // 分岐先セレクタ: 次へ / 会話終了 / 各行（行参照は行 ID で保持＝並べ替えで参照は変わらない）。
    // 右側の余白に、参照先の行の「話者 + 本文の冒頭」を薄くプレビュー表示する。
    private VisualElement BuildGoto(string current, Action<string> onSet)
    {
        var row = new VisualElement();
        row.AddToClassList("conv-goto-row");

        var labels = new System.Collections.Generic.List<string> { "次へ", "会話終了" };
        var targets = new System.Collections.Generic.List<string> { ConversationEditLogic.GotoNext, ConversationEditLogic.GotoEnd };
        for (int i = 0; i < _edit.Lines.Count; i++)
        {
            labels.Add($"→ 行{i + 1}");
            targets.Add(_edit.Lines[i].lineId);
        }

        int sel = targets.IndexOf(current ?? "");
        if (sel < 0) sel = 0;

        var dd = new DropdownField(labels, sel);
        dd.AddToClassList("conv-goto");

        var preview = new Label { pickingMode = PickingMode.Ignore };
        preview.AddToClassList("conv-goto-preview");
        preview.text = LinePreview(targets[sel]);

        dd.RegisterValueChangedCallback(_ =>
        {
            int i = dd.index;
            if (i >= 0 && i < targets.Count)
            {
                onSet(targets[i]);
                preview.text = LinePreview(targets[i]);
            }
        });

        row.Add(dd);
        row.Add(preview);
        return row;
    }

    // 行参照のプレビュー文（「話者「本文の冒頭…」」）。行参照でない（次へ / 終了 / 未存在）は空。
    private string LinePreview(string lineId)
    {
        if (string.IsNullOrEmpty(lineId) || lineId == ConversationEditLogic.GotoEnd)
            return "";
        ConversationLineJson line = null;
        foreach (var l in _edit.Lines)
            if (l.lineId == lineId) { line = l; break; }
        if (line == null)
            return "";

        string app = DeviceLanguage.CurrentCode();
        string speaker = _speakers != null ? SpeakerLibraryLogic.ResolveName(_speakers.Find(line.speakerId), app) : "";
        string body = TextForLang(line.texts, app);
        if (string.IsNullOrEmpty(body))
            body = TextForLang(line.texts, "");

        const int max = 12;
        string snippet = body.Length > max ? body.Substring(0, max) + "…" : body;
        if (!string.IsNullOrEmpty(speaker) && !string.IsNullOrEmpty(snippet))
            return $"{speaker}「{snippet}」";
        if (!string.IsNullOrEmpty(speaker))
            return speaker;
        return snippet;
    }

    private void MoveLine(string lineId, int delta)
    {
        int idx = IndexOfLine(lineId);
        if (idx < 0)
            return;
        if (_edit.MoveLine(lineId, idx + delta))
            RefreshLines();
    }

    private int IndexOfLine(string lineId)
    {
        for (int i = 0; i < _edit.Lines.Count; i++)
            if (_edit.Lines[i].lineId == lineId)
                return i;
        return -1;
    }

    // ── ヘルパー ───────────────────────────────────────────────────────────────

    private static Label RowLabel(string text)
    {
        var l = new Label(text);
        l.AddToClassList("conv-row-label");
        return l;
    }

    private Button IconButton(string iconClass, string tooltip, Action onClick)
    {
        var btn = new Button(onClick) { text = "", tooltip = tooltip };
        btn.AddToClassList("conv-small-btn");
        btn.AddToClassList("gimmick-icon-btn");
        btn.AddToClassList(iconClass);
        return btn;
    }

    // 「＋ 〜」で必要な詳細項目を出すチップ。
    private static Button Chip(string text, Action onClick)
    {
        var btn = new Button(onClick) { text = text };
        btn.AddToClassList("conv-add-chip");
        return btn;
    }

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
