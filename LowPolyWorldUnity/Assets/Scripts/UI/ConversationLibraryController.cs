using System;
using UnityEngine.UIElements;

/// <summary>
/// 会話ライブラリ（一覧）のオーバーレイ UI（screens-and-modes.md 11.7.4b）。
///
/// 担当: 会話の追加 / 改名 / 削除 / 並び替え。会話名タップ・追加で
/// <see cref="EditRequested"/> を発火し、上位がエディタ（<see cref="ConversationEditorController"/>）を開く。
/// 会話の中身は <see cref="ConversationLibraryLogic"/> が保持する。
/// </summary>
public class ConversationLibraryController
{
    /// <summary>会話を編集したいとき（conversationId）。</summary>
    public event Action<string> EditRequested;

    private readonly ConversationLibraryLogic _logic;
    private readonly SpeakerLibraryLogic _speakers; // 話者一覧・各会話の登場話者表示用

    private readonly VisualElement _overlay;
    private readonly Button _btnBack;
    private readonly VisualElement _list;
    private readonly VisualElement _speakerSummary;
    private readonly Label _flash;
    private readonly UiListDragReorder _reorder;

    private IVisualElementScheduledItem _flashHide;

    public ConversationLibraryController(
        VisualElement root, ConversationLibraryLogic logic, SpeakerLibraryLogic speakers = null)
    {
        if (root == null)
            throw new ArgumentNullException(nameof(root));
        _logic = logic ?? throw new ArgumentNullException(nameof(logic));
        _speakers = speakers;

        _overlay = root.Q("conv-library");
        _btnBack = root.Q<Button>("conv-library-back");
        _list = root.Q("conv-library-list");
        _speakerSummary = root.Q("conv-speaker-summary");
        _flash = root.Q<Label>("conv-library-flash");

        if (_list != null)
            _reorder = UiListDragReorder.For(_list, OnReorder);

        if (_btnBack != null) _btnBack.clicked += Close;
    }

    public bool IsOpen => _overlay != null && !_overlay.ClassListContains("overlay-hidden");

    public void Open()
    {
        if (_overlay == null)
            return;
        Refresh();
        _overlay.EnableInClassList("overlay-hidden", false);
    }

    public void Close() => _overlay?.EnableInClassList("overlay-hidden", true);

    /// <summary>一覧を再構築する（エディタ / 話者編集から戻ったとき等）。</summary>
    public void Refresh()
    {
        RefreshSpeakerSummary();

        if (_list == null)
            return;
        _list.Clear();
        _reorder?.Reset();

        if (_logic.Count == 0)
        {
            var empty = new Label("会話がありません。下の「＋ 会話を追加」で作成します");
            empty.AddToClassList("conv-empty");
            _list.Add(empty);
        }
        else
        {
            foreach (var conv in _logic.Conversations)
                _list.Add(BuildRow(conv));
        }

        // 一覧の最下部に追加ボタン
        var add = new Button(OnAdd) { text = "＋ 会話を追加" };
        add.AddToClassList("gimmick-template-top-btn");
        add.AddToClassList("gimmick-list-add--inset");
        _list.Add(add);
    }

    // 話者一覧（ワールド単位の定義）をチップで表示する。
    private void RefreshSpeakerSummary()
    {
        if (_speakerSummary == null)
            return;
        _speakerSummary.Clear();

        if (_speakers == null || _speakers.Count == 0)
        {
            var empty = new Label("話者が未定義です。「話者を編集」で追加できます");
            empty.AddToClassList("conv-speaker-empty");
            _speakerSummary.Add(empty);
            return;
        }

        string app = DeviceLanguage.CurrentCode();
        foreach (var s in _speakers.Speakers)
        {
            string name = SpeakerLibraryLogic.ResolveName(s, app);
            _speakerSummary.Add(SpeakerChip(string.IsNullOrEmpty(name) ? "（名称未設定）" : name));
        }
    }

    private static Label SpeakerChip(string text)
    {
        var chip = new Label(text);
        chip.AddToClassList("conv-speaker-chip");
        return chip;
    }

    private void OnAdd()
    {
        var conv = _logic.Add();
        if (conv == null)
        {
            ShowFlash($"会話は最大 {ConversationLibraryLogic.MaxConversations} 個までです");
            return;
        }
        Refresh();
        EditRequested?.Invoke(conv.conversationId); // 追加直後に編集画面へ
    }

    private VisualElement BuildRow(ConversationJson conv)
    {
        var card = new VisualElement();
        card.AddToClassList("conv-row");

        // 上段: ハンドル + 会話名 + 行数 + 操作ボタン
        var top = new VisualElement();
        top.AddToClassList("conv-row-top");

        if (_reorder != null)
            top.Add(_reorder.CreateHandle(card, conv.name));

        var name = new Label(conv.name);
        name.AddToClassList("conv-name");
        name.RegisterCallback<ClickEvent>(_ => EditRequested?.Invoke(conv.conversationId));
        top.Add(name);

        var meta = new Label($"{conv.lines?.Length ?? 0} 行");
        meta.AddToClassList("conv-meta");
        top.Add(meta);

        top.Add(IconButton("gimmick-icon-btn--edit", "編集", () => EditRequested?.Invoke(conv.conversationId)));
        top.Add(IconButton("gimmick-icon-btn--close", "削除", () =>
        {
            if (_logic.Remove(conv.conversationId))
                Refresh();
        }));
        card.Add(top);

        // 下段: この会話に登場する話者
        card.Add(BuildConversationSpeakers(conv));

        return card;
    }

    // 会話に登場する話者をチップで表示する（タップで編集に入れるよう行全体は名前タップで遷移）。
    private VisualElement BuildConversationSpeakers(ConversationJson conv)
    {
        var wrap = new VisualElement();
        wrap.AddToClassList("conv-row-speakers");

        var ids = ConversationSpeakers.DistinctSpeakerIds(conv);
        if (ids.Count == 0)
        {
            var none = new Label("登場話者なし");
            none.AddToClassList("conv-row-speakers-empty");
            wrap.Add(none);
            return wrap;
        }

        string app = DeviceLanguage.CurrentCode();
        foreach (var id in ids)
        {
            string name = _speakers != null ? SpeakerLibraryLogic.ResolveName(_speakers.Find(id), app) : "";
            wrap.Add(SpeakerChip(string.IsNullOrEmpty(name) ? "（不明な話者）" : name));
        }
        return wrap;
    }

    // ドラッグ＆ドロップ並べ替え（from の会話を to の位置へ）。
    private void OnReorder(int from, int to)
    {
        if ((uint)from >= (uint)_logic.Conversations.Count)
            return;
        if (_logic.Move(_logic.Conversations[from].conversationId, to))
            Refresh();
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
