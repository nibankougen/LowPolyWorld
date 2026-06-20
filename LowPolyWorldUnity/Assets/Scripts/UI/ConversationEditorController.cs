using System;
using UnityEngine;
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

    // 左スワイプで現れる削除ボタン領域の幅(px)。
    private const float SwipeRevealWidth = 64f;

    private readonly VisualElement _overlay;
    private readonly Button _btnBack;
    private readonly TextField _title;
    private readonly VisualElement _lineList;
    private readonly Label _flash;
    private readonly Label _defLang;
    private readonly UiListDragReorder _reorder;

    private readonly ConversationLibraryLogic _library;
    private readonly GimmickTabLogic _tabLogic; // 変数選択ドロップダウン用（定義済みワールド / プレイヤー変数）
    private readonly SpeakerLibraryLogic _speakers; // 話者選択ドロップダウン用（ワールド単位の話者定義）
    private readonly UiPopupMenu _popup; // セリフ行の「追加」「削除」のはみ出しメニュー
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
        _defLang = root.Q<Label>("conv-editor-deflang");
        if (_defLang != null)
            _defLang.text = $"デフォルト言語: {SupportedLanguages.LabelOf(DeviceLanguage.CurrentCode())}";

        if (_lineList != null)
            _reorder = UiListDragReorder.For(_lineList, OnReorderLine);

        _popup = new UiPopupMenu(_overlay);

        // 開いているスワイプ行の外側をタップ／スクロールしたら閉じる（他の部分をいじると閉じる）。
        _overlay?.RegisterCallback<PointerDownEvent>(OnOverlayPointerDown, TrickleDown.TrickleDown);

        if (_btnBack != null) _btnBack.clicked += Close;
        if (_title != null)
        {
            _title.maxLength = ConversationLibraryLogic.NameMaxLength;
            _title.RegisterCallback<FocusOutEvent>(_ => CommitTitle());
        }
    }

    public bool IsOpen => _overlay != null && !_overlay.ClassListContains("overlay-hidden");

    // 開いているスワイプ行の外側をタップしたら閉じる（その行の内側＝削除ボタン等は閉じない）。
    private void OnOverlayPointerDown(PointerDownEvent e)
    {
        var cur = UiSwipeReveal.Current;
        if (cur != null && e.target is VisualElement ve && !cur.ContainsTarget(ve))
            UiSwipeReveal.CloseCurrent();
    }

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
        _popup?.Close();
        UiSwipeReveal.CloseCurrent();
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

    private void AddChoiceForLine(string lineId)
    {
        if (_edit?.AddChoice(lineId) == null)
            ShowFlash($"選択肢は 1 行に最大 {ConversationEditLogic.MaxChoices} 個までです");
        else
            RefreshLines();
    }

    private void RefreshLines()
    {
        if (_lineList == null || _edit == null)
            return;
        _popup?.Close();
        UiSwipeReveal.CloseCurrent();
        _lineList.Clear();
        _reorder?.Reset();

        if (_edit.Lines.Count == 0)
        {
            var empty = new Label("セリフ行がありません");
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
        string app = DeviceLanguage.CurrentCode();

        // 行 = [背後の丸い削除ボタン] の上に [前景カード] を重ねたラッパー。
        // 前景を左にスワイプすると背後の赤い削除ボタンが現れる（UiSwipeReveal）。
        var rowWrap = new VisualElement();
        rowWrap.AddToClassList("conv-line-row");

        var swipeDelete = new VisualElement();
        swipeDelete.AddToClassList("conv-swipe-delete");
        var swipeBtn = new Button(() =>
        {
            UiSwipeReveal.CloseCurrent();
            if (_edit.RemoveLine(lineId))
                RefreshLines();
        }) { text = "", tooltip = "セリフ行を削除" };
        swipeBtn.AddToClassList("conv-swipe-delete-btn");
        swipeDelete.Add(swipeBtn);
        rowWrap.Add(swipeDelete);

        // カードは [縦長ドラッグハンドル | 内容列] の横並び（前景・不透明）
        var card = new VisualElement();
        card.AddToClassList("conv-line-card");
        rowWrap.Add(card);

        if (_reorder != null)
        {
            // 本文があれば「話者「本文…」」、本文がなければ話者名のみ、どちらも無ければ「（話者なし）」。
            // 並べ替えの行はラッパー（リスト直下の子）を登録する。
            string ghost = LinePreview(lineId);
            var handle = _reorder.CreateHandle(rowWrap, string.IsNullOrEmpty(ghost) ? "（話者なし）" : ghost);
            handle.AddToClassList("conv-line-handle"); // 左側・縦長の掴み領域
            card.Add(handle);
        }

        // 前景カードを左スワイプで開ける（背後の削除ボタンが出る）。
        _ = new UiSwipeReveal(card, SwipeRevealWidth);

        var content = new VisualElement();
        content.AddToClassList("conv-line-content");
        card.Add(content);

        // 1 行目: 話者選択（カスタムドロップダウン・選択肢に icon_speaker を含む）。削除は下部の「⋯」メニューへ。
        var head = new VisualElement();
        head.AddToClassList("conv-line-head");
        head.Add(BuildSpeakerSelect(lineId, line.speakerId));
        content.Add(head);

        // 2 行目: 本文（既定言語）。話者選択と同じ幅にする。
        var bodyRow = new VisualElement();
        bodyRow.AddToClassList("conv-body-row");
        var bodyField = BuildDefaultLangField(line.texts, app, ConversationEditLogic.TextMaxLength, true,
            (l, t) => _edit.SetLineText(lineId, l, t), l => _edit.RemoveLineText(lineId, l));
        bodyField.style.flexGrow = 1;
        bodyRow.Add(bodyField);
        content.Add(bodyRow);

        // ── ここから下は「使うときだけ」表示する詳細項目 ──
        bool hasOtherLang = HasOtherLangText(line.texts, app);
        string keyLangBody = lineId + ":langbody";

        // 言語別本文（他言語データあり or 開いたときだけ）
        if (hasOtherLang || _revealed.Contains(keyLangBody))
            content.Add(BuildLangDetailSection("言語別本文", line.texts, app, ConversationEditLogic.TextMaxLength, true,
                keyLangBody, hasOtherLang,
                (l, t) => _edit.SetLineText(lineId, l, t), l => _edit.RemoveLineText(lineId, l)));

        var onReach = line.onReach ??= new ConversationEffectJson();
        bool hasChoices = (line.choices?.Length ?? 0) > 0;
        bool hasGoto = !string.IsNullOrEmpty(line.gotoLineId); // "" = 次へ（既定）
        string keyEffect = lineId + ":onReach";
        string keyGoto = lineId + ":goto";

        // 到達時の変数変更（データあり or 開いたときだけ）
        if (onReach.kind != "none" || _revealed.Contains(keyEffect))
        {
            var sec = BuildEffectEditor("変数変更", onReach, () =>
            {
                onReach.kind = "none";
                _revealed.Remove(keyEffect);
                RefreshLines();
            });
            content.Add(sec);
        }

        // 分岐先（選択肢が無く、データあり or 開いたときだけ）
        if (!hasChoices && (hasGoto || _revealed.Contains(keyGoto)))
        {
            var gotoBox = OptionalSection("分岐先", () =>
            {
                _edit.SetLineGoto(lineId, ConversationEditLogic.GotoNext);
                _revealed.Remove(keyGoto);
                RefreshLines();
            });
            gotoBox.Add(BuildGoto(line.gotoLineId, g => _edit.SetLineGoto(lineId, g)));
            content.Add(gotoBox);
        }

        // 選択肢（あるときだけ表示）
        var choices = line.choices ?? Array.Empty<ConversationChoiceJson>();
        bool canChoice = choices.Length < ConversationEditLogic.MaxChoices;
        if (choices.Length > 0)
        {
            var choiceBox = OptionalSection("選択肢", null);
            for (int c = 0; c < choices.Length; c++)
                choiceBox.Add(BuildChoiceRow(lineId, c, choices[c]));
            // 選択肢が 1 つ以上あって追加可能なら、最後の選択肢の下に「＋ 選択肢を追加」ボタンを置く
            // （「セリフ行を追加」と同じ見た目・選択肢カードと同じ幅）。
            if (canChoice)
            {
                var addChoiceBtn = new Button(() => AddChoiceForLine(lineId)) { text = "＋ 選択肢を追加" };
                addChoiceBtn.AddToClassList("gimmick-template-top-btn");
                addChoiceBtn.AddToClassList("conv-choice-add");
                choiceBox.Add(addChoiceBtn);
            }
            content.Add(choiceBox);
        }

        // ── 下部ツール行: 左に「＋」（追加メニュー）・右に「⋯」（このセリフ行の操作）──
        bool canLangBody = !hasOtherLang && !_revealed.Contains(keyLangBody);
        bool canEffect = onReach.kind == "none" && !_revealed.Contains(keyEffect);
        bool canGoto = !hasChoices && !hasGoto && !_revealed.Contains(keyGoto);
        // 選択肢が 0 個のときだけ「＋」メニューから最初の選択肢を作れる
        // （1 個以上あるときは選択肢一覧の下の「＋ 選択肢を追加」ボタンで追加する）。
        bool canAddFirstChoice = choices.Length == 0;

        var tools = new VisualElement();
        tools.AddToClassList("conv-line-tools");

        // ＋（icon_plus・枠なし小）: 追加できる詳細項目を縦のはみ出しメニューで出す。
        var addItems = new System.Collections.Generic.List<UiPopupMenu.Item>();
        if (canLangBody)
            addItems.Add(new UiPopupMenu.Item("言語別本文", () => { _revealed.Add(keyLangBody); RefreshLines(); }));
        if (canEffect)
            addItems.Add(new UiPopupMenu.Item("変数変更", () => { _revealed.Add(keyEffect); RefreshLines(); }));
        if (canGoto)
            addItems.Add(new UiPopupMenu.Item("分岐先", () => { _revealed.Add(keyGoto); RefreshLines(); }));
        if (canAddFirstChoice)
            addItems.Add(new UiPopupMenu.Item("選択肢", () => AddChoiceForLine(lineId)));

        if (addItems.Count > 0)
        {
            var addBtn = ToolButton("conv-line-tool-btn--plus", "追加");
            addBtn.clicked += () => _popup.Open(addBtn, addItems, _lineList as ScrollView);
            tools.Add(addBtn);
        }

        var spacer = new VisualElement { pickingMode = PickingMode.Ignore };
        spacer.style.flexGrow = 1;
        tools.Add(spacer);

        // ⋯（icon_more・枠なし小）: 右側。「セリフ行を削除」だけを縦のはみ出しメニューで出す。
        var moreBtn = ToolButton("conv-line-tool-btn--more", "このセリフ行の操作");
        moreBtn.clicked += () => _popup.Open(moreBtn, new[]
        {
            new UiPopupMenu.Item("セリフ行を削除", () =>
            {
                if (_edit.RemoveLine(lineId))
                    RefreshLines();
            }, "ui-popup-item-icon--trash", "ui-popup-item--danger"),
        }, _lineList as ScrollView);
        tools.Add(moreBtn);

        content.Add(tools);

        return rowWrap;
    }

    private VisualElement BuildChoiceRow(string lineId, int choiceIndex, ConversationChoiceJson choice)
    {
        int ci = choiceIndex;
        string app = DeviceLanguage.CurrentCode();
        var card = new VisualElement();
        card.AddToClassList("conv-choice-card");

        // ヘッダー: 「選択肢 N」（削除は下部の「⋯」メニューへ・セリフ行と同じ）
        var head = new VisualElement();
        head.AddToClassList("conv-choice-head");
        var title = new Label($"選択肢 {ci + 1}");
        title.AddToClassList("conv-choice-title");
        head.Add(title);
        card.Add(head);

        // テキスト（既定言語）
        card.Add(RowLabel("テキスト"));
        card.Add(BuildDefaultLangField(choice.texts, app, ConversationEditLogic.ChoiceTextMaxLength, false,
            (l, t) => _edit.SetChoiceText(lineId, ci, l, t), l => _edit.RemoveChoiceText(lineId, ci, l)));

        // 言語別テキスト（他言語データあり or 開いたときだけ・✕ で閉じられる）
        bool hasOtherLang = HasOtherLangText(choice.texts, app);
        string keyLangText = lineId + ":c" + ci + ":langtext";
        if (hasOtherLang || _revealed.Contains(keyLangText))
            card.Add(BuildLangDetailSection("言語別テキスト", choice.texts, app, ConversationEditLogic.ChoiceTextMaxLength, false,
                keyLangText, hasOtherLang,
                (l, t) => _edit.SetChoiceText(lineId, ci, l, t), l => _edit.RemoveChoiceText(lineId, ci, l)));

        // 選んだあとの進行（常に表示）
        var row = new VisualElement();
        row.AddToClassList("conv-choice-row");
        row.Add(BuildGoto(choice.gotoLineId, g => _edit.SetChoiceGoto(lineId, ci, g)));
        card.Add(row);

        // 選択時の変数変更（データあり or 開いたときだけ・✕ で閉じられる）
        var eff = choice.effect ??= new ConversationEffectJson();
        string keyEffect = lineId + ":c" + ci + ":effect";
        if (eff.kind != "none" || _revealed.Contains(keyEffect))
            card.Add(BuildEffectEditor("選択時に変数を変更", eff, () =>
            {
                eff.kind = "none";
                _revealed.Remove(keyEffect);
                RefreshLines();
            }));

        // ── 下部ツール行: 左に「＋」（追加メニュー）・右に「⋯」（この選択肢の操作）──
        bool canLangText = !hasOtherLang && !_revealed.Contains(keyLangText);
        bool canEffect = eff.kind == "none" && !_revealed.Contains(keyEffect);

        var tools = new VisualElement();
        tools.AddToClassList("conv-line-tools");

        var addItems = new System.Collections.Generic.List<UiPopupMenu.Item>();
        if (canLangText)
            addItems.Add(new UiPopupMenu.Item("言語別テキスト", () => { _revealed.Add(keyLangText); RefreshLines(); }));
        if (canEffect)
            addItems.Add(new UiPopupMenu.Item("変数変更", () => { _revealed.Add(keyEffect); RefreshLines(); }));

        if (addItems.Count > 0)
        {
            var addBtn = ToolButton("conv-line-tool-btn--plus", "追加");
            addBtn.clicked += () => _popup.Open(addBtn, addItems, _lineList as ScrollView);
            tools.Add(addBtn);
        }

        var spacer = new VisualElement { pickingMode = PickingMode.Ignore };
        spacer.style.flexGrow = 1;
        tools.Add(spacer);

        var moreBtn = ToolButton("conv-line-tool-btn--more", "この選択肢の操作");
        moreBtn.clicked += () => _popup.Open(moreBtn, new[]
        {
            new UiPopupMenu.Item("選択肢を削除", () =>
            {
                if (_edit.RemoveChoice(lineId, ci))
                    RefreshLines();
            }, "ui-popup-item-icon--trash", "ui-popup-item--danger"),
        }, _lineList as ScrollView);
        tools.Add(moreBtn);

        card.Add(tools);
        return card;
    }

    // ── 話者選択（カスタムドロップダウン）/ 本文フィールド ──────────────────────────

    // 話者選択。選択肢は icon_speaker（話者色でティント）+ 名前。タップ/ドラッグ離しで選べる。
    private VisualElement BuildSpeakerSelect(string lineId, string currentSpeakerId)
    {
        string app = DeviceLanguage.CurrentCode();
        var options = new System.Collections.Generic.List<UiSelectField.Option>
        {
            new("（話者なし）", "ui-select-icon--speaker"),
        };
        var ids = new System.Collections.Generic.List<string> { "" };
        if (_speakers != null)
            foreach (var s in _speakers.Speakers)
            {
                string name = SpeakerLibraryLogic.ResolveName(s, app);
                Color? tint = SpeakerPalette.IsValidIndex(s.colorIndex)
                    ? SpeakerPalette.ColorOf(s.colorIndex)
                    : (Color?)null;
                options.Add(new UiSelectField.Option(
                    string.IsNullOrEmpty(name) ? "（名称なし）" : name, "ui-select-icon--speaker", tint));
                ids.Add(s.speakerId);
            }

        int sel = ids.IndexOf(currentSpeakerId ?? "");
        if (sel < 0) sel = 0; // 未知 ID（削除済み等）は「話者なし」表示

        var field = new UiSelectField();
        field.AddToClassList("conv-speaker-select");
        field.SetOptions(options, sel);
        field.SelectionChanged += i =>
        {
            if (i >= 0 && i < ids.Count)
                _edit.SetLineSpeakerId(lineId, ids[i]);
        };
        return field;
    }

    // 既定言語のテキスト入力欄（ラベルなし）。セリフ本文・選択肢テキストで共有（UiLangText）。
    private VisualElement BuildDefaultLangField(
        GimmickTextJson[] texts, string app, int maxLen, bool multiline,
        Action<string, string> set, Action<string> remove)
        => UiLangText.DefaultField(texts, app, maxLen, multiline, set, remove);

    // 既定言語以外の言語別テキスト欄。データが無いときは ⋯ メニューで閉じられる。セリフ本文・選択肢で共有。
    private VisualElement BuildLangDetailSection(
        string title, GimmickTextJson[] texts, string app, int maxLen, bool multiline,
        string revealKey, bool hasData, Action<string, string> set, Action<string> remove)
    {
        var box = OptionalSection(title, hasData ? (Action)null : () =>
        {
            _revealed.Remove(revealKey);
            RefreshLines();
        });
        UiLangText.FillLangFields(box, texts, app, maxLen, multiline, set, remove);
        return box;
    }

    // 既定言語以外にテキストがあるか。
    private static bool HasOtherLangText(GimmickTextJson[] texts, string app)
        => UiLangText.HasOtherLang(texts, app);

    private static TextField LangField(string label, int maxLen, bool multiline, string initial, Action<string> onChange)
        => UiLangText.Field(label, maxLen, multiline, initial, onChange);

    private static string TextForLang(GimmickTextJson[] texts, string lang)
        => UiLangText.TextForLang(texts, lang);

    // ── 統一した詳細セクション枠（言語別本文 / 変数変更 / 分岐先 / 選択肢 で共通）──────
    // タイトル + 任意の削除（右端の「⋯」メニュー＝セリフ行・選択肢と同じ削除経路）を持つ枠を返す。
    // 本文は呼び出し側が box に追加する。
    private VisualElement OptionalSection(string title, Action onRemove)
        => UiLangText.OptionalSection(title, onRemove, _popup, _lineList as ScrollView);

    // ── 変数変更（onReach / 選択肢 effect）────────────────────────────────────

    private static readonly string[] EffectKinds = { "none", "worldState", "playerState" };
    private static readonly string[] EffectKindLabels = { "なし", "ワールド変数", "プレイヤー変数" };

    private VisualElement BuildEffectEditor(string title, ConversationEffectJson eff, Action onRemove = null)
    {
        var box = OptionalSection(title, onRemove);

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

    // ドラッグ＆ドロップ並べ替え（from の行を to の位置へ）。gotoLineId 等の参照は行 ID で保持される。
    private void OnReorderLine(int from, int to)
    {
        if (_edit == null || (uint)from >= (uint)_edit.Lines.Count)
            return;
        if (_edit.MoveLine(_edit.Lines[from].lineId, to))
            RefreshLines();
    }

    // ── ヘルパー ───────────────────────────────────────────────────────────────

    private static Label RowLabel(string text) => UiLangText.RowLabel(text);

    // 下部ツール行の枠なし小アイコンボタン（＋ / ⋯）。
    private static Button ToolButton(string iconClass, string tooltip) => UiLangText.ToolButton(iconClass, tooltip);

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
