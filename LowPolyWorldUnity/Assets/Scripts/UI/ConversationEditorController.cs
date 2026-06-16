using System;
using UnityEngine.UIElements;

/// <summary>
/// 1 つの会話のセリフ行・選択肢を編集するオーバーレイ UI（screens-and-modes.md 11.7.4b）。
///
/// 行の追加 / 削除 / 並び替え・話者 / 本文（デフォルト言語）入力・分岐（次へ / 終了 / 別の行）・
/// 選択肢の追加 / 削除を扱う。編集状態は <see cref="ConversationEditLogic"/> が保持する。
/// （言語別入力の「詳細」・到達/選択時のステート変更 UI は後続。データ・ロジックは対応済み）
/// </summary>
public class ConversationEditorController
{
    /// <summary>＜戻るで閉じたとき（一覧側で名前・行数を更新する）。</summary>
    public event Action Closed;

    private readonly VisualElement _overlay;
    private readonly Button _btnBack;
    private readonly TextField _title;
    private readonly VisualElement _lineList;
    private readonly Button _btnAddLine;
    private readonly Label _flash;

    private readonly ConversationLibraryLogic _library;
    private ConversationJson _conversation;
    private ConversationEditLogic _edit;
    private IVisualElementScheduledItem _flashHide;

    public ConversationEditorController(VisualElement root, ConversationLibraryLogic library = null)
    {
        if (root == null)
            throw new ArgumentNullException(nameof(root));
        _library = library;

        _overlay = root.Q("conv-editor");
        _btnBack = root.Q<Button>("conv-editor-back");
        _title = root.Q<TextField>("conv-editor-title");
        _lineList = root.Q("conv-editor-lines");
        _btnAddLine = root.Q<Button>("conv-editor-add-line");
        _flash = root.Q<Label>("conv-editor-flash");

        if (_btnBack != null) _btnBack.clicked += Close;
        if (_btnAddLine != null) _btnAddLine.clicked += OnAddLine;
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
            ShowFlash($"この会話のセリフ行は最大 {ConversationEditLogic.MaxLines} 行までです");
        else
            RefreshLines();
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
            return;
        }

        for (int i = 0; i < _edit.Lines.Count; i++)
            _lineList.Add(BuildLineCard(i, _edit.Lines[i]));
    }

    private VisualElement BuildLineCard(int index, ConversationLineJson line)
    {
        string lineId = line.lineId;
        var card = new VisualElement();
        card.AddToClassList("conv-line-card");

        // ヘッダー: 番号・話者・操作
        var head = new VisualElement();
        head.AddToClassList("conv-line-head");

        var idx = new Label($"{index + 1}.");
        idx.AddToClassList("conv-line-index");
        head.Add(idx);

        var speaker = new TextField { maxLength = ConversationEditLogic.SpeakerMaxLength };
        speaker.AddToClassList("conv-line-speaker");
        speaker.SetValueWithoutNotify(DefaultText(line.speakers));
        speaker.RegisterValueChangedCallback(e =>
        {
            if (string.IsNullOrEmpty(e.newValue))
                _edit.RemoveLineSpeaker(lineId, ""); // 話者は空にできる（任意項目）
            else
                _edit.SetLineSpeaker(lineId, "", e.newValue);
        });
        head.Add(speaker);

        head.Add(IconButton("gimmick-icon-btn--up", "上へ", () => MoveLine(lineId, -1)));
        head.Add(IconButton("gimmick-icon-btn--down", "下へ", () => MoveLine(lineId, +1)));
        head.Add(IconButton("gimmick-icon-btn--close", "削除", () =>
        {
            if (_edit.RemoveLine(lineId))
                RefreshLines();
        }));
        card.Add(head);

        // 本文
        var body = new TextField { multiline = true, maxLength = ConversationEditLogic.TextMaxLength };
        body.AddToClassList("conv-line-text");
        body.SetValueWithoutNotify(DefaultText(line.texts));
        body.RegisterValueChangedCallback(e => _edit.SetLineText(lineId, "", e.newValue));
        card.Add(body);

        // 分岐（選択肢が無いときの「次へ / 終了 / 行」）
        bool hasChoices = (line.choices?.Length ?? 0) > 0;
        if (!hasChoices)
        {
            card.Add(RowLabel("この後の進行"));
            card.Add(BuildGoto(line.gotoLineId, g => _edit.SetLineGoto(lineId, g)));
        }

        // 選択肢
        card.Add(RowLabel("選択肢"));
        var choices = line.choices ?? Array.Empty<ConversationChoiceJson>();
        for (int c = 0; c < choices.Length; c++)
            card.Add(BuildChoiceRow(lineId, c, choices[c]));

        var addChoice = new Button(() =>
        {
            if (_edit.AddChoice(lineId) == null)
                ShowFlash($"選択肢は 1 行に最大 {ConversationEditLogic.MaxChoices} 個までです");
            else
                RefreshLines();
        })
        {
            text = "＋ 選択肢を追加",
        };
        addChoice.AddToClassList("conv-add-choice-btn");
        card.Add(addChoice);

        return card;
    }

    private VisualElement BuildChoiceRow(string lineId, int choiceIndex, ConversationChoiceJson choice)
    {
        var row = new VisualElement();
        row.AddToClassList("conv-choice-row");

        var text = new TextField { maxLength = ConversationEditLogic.ChoiceTextMaxLength };
        text.AddToClassList("conv-choice-text");
        text.SetValueWithoutNotify(DefaultText(choice.texts));
        text.RegisterValueChangedCallback(e => _edit.SetChoiceText(lineId, choiceIndex, "", e.newValue));
        row.Add(text);

        row.Add(BuildGoto(choice.gotoLineId, g => _edit.SetChoiceGoto(lineId, choiceIndex, g)));

        row.Add(IconButton("gimmick-icon-btn--close", "削除", () =>
        {
            if (_edit.RemoveChoice(lineId, choiceIndex))
                RefreshLines();
        }));

        return row;
    }

    // 分岐先ドロップダウン: 次へ / 会話終了 / 各行。
    private DropdownField BuildGoto(string current, Action<string> onSet)
    {
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
        dd.RegisterValueChangedCallback(_ =>
        {
            int i = dd.index;
            if (i >= 0 && i < targets.Count)
                onSet(targets[i]);
        });
        return dd;
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

    private static string DefaultText(GimmickTextJson[] texts)
    {
        if (texts == null)
            return "";
        foreach (var t in texts)
            if (t != null && t.lang == "")
                return t.text ?? "";
        return "";
    }

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
