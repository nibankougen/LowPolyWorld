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

    /// <summary>話者エリアをタップして話者編集を開きたいとき。</summary>
    public event Action EditSpeakersRequested;

    // 左スワイプで現れる削除ボタン領域の幅(px)。
    private const float SwipeRevealWidth = 64f;

    private readonly ConversationLibraryLogic _logic;
    private readonly SpeakerLibraryLogic _speakers; // 話者一覧・各会話の登場話者表示用

    private readonly VisualElement _overlay;
    private readonly Button _btnBack;
    private readonly VisualElement _list;
    private readonly VisualElement _speakerSummary;
    private readonly Label _flash;
    private readonly UiListDragReorder _reorder;
    private readonly UiPopupMenu _popup; // ⋯（会話を削除）のはみ出しメニュー

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

        // 話者エリア全体をタップで話者編集へ（編集ボタンは廃止）。
        var speakerArea = root.Q("conv-speaker-area");
        speakerArea?.RegisterCallback<ClickEvent>(_ => EditSpeakersRequested?.Invoke());

        if (_list != null)
            _reorder = UiListDragReorder.For(_list, OnReorder);

        _popup = new UiPopupMenu(_overlay);

        // 開いているスワイプ行の外側をタップしたら閉じる（他の部分をいじると閉じる）。
        _overlay?.RegisterCallback<PointerDownEvent>(OnOverlayPointerDown, TrickleDown.TrickleDown);

        if (_btnBack != null) _btnBack.clicked += Close;
    }

    // 開いているスワイプ行の外側をタップしたら閉じる（その行の内側＝削除ボタン等は閉じない）。
    private void OnOverlayPointerDown(PointerDownEvent e)
    {
        var cur = UiSwipeReveal.Current;
        if (cur != null && e.target is VisualElement ve && !cur.ContainsTarget(ve))
            UiSwipeReveal.CloseCurrent();
    }

    public bool IsOpen => _overlay != null && !_overlay.ClassListContains("overlay-hidden");

    public void Open()
    {
        if (_overlay == null)
            return;
        Refresh();
        _overlay.EnableInClassList("overlay-hidden", false);
    }

    public void Close()
    {
        _popup?.Close();
        UiSwipeReveal.CloseCurrent();
        _overlay?.EnableInClassList("overlay-hidden", true);
    }

    /// <summary>一覧を再構築する（エディタ / 話者編集から戻ったとき等）。</summary>
    public void Refresh()
    {
        RefreshSpeakerSummary();

        if (_list == null)
            return;
        _popup?.Close();
        UiSwipeReveal.CloseCurrent();
        _list.Clear();
        _reorder?.Reset();

        if (_logic.Count == 0)
        {
            var empty = new Label("会話がありません");
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
            var empty = new Label("タップして編集");
            empty.AddToClassList("conv-speaker-empty");
            _speakerSummary.Add(empty);
            return;
        }

        string app = DeviceLanguage.CurrentCode();
        foreach (var s in _speakers.Speakers)
        {
            string name = SpeakerLibraryLogic.ResolveName(s, app);
            _speakerSummary.Add(SpeakerChip(string.IsNullOrEmpty(name) ? "（名称なし）" : name, s.colorIndex));
        }
    }

    // 左に話者色のドットを付けたチップ（colorIndex 無効時はドットなし）。
    private static VisualElement SpeakerChip(string text, int colorIndex = -1)
    {
        var chip = new VisualElement();
        chip.AddToClassList("conv-speaker-chip");
        if (SpeakerPalette.IsValidIndex(colorIndex))
        {
            var dot = new VisualElement { pickingMode = PickingMode.Ignore };
            dot.AddToClassList("conv-speaker-dot");
            dot.style.backgroundColor = SpeakerPalette.ColorOf(colorIndex);
            chip.Add(dot);
        }
        var label = new Label(text) { pickingMode = PickingMode.Ignore };
        chip.Add(label);
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
        string id = conv.conversationId;

        // 行 = 背後の丸い削除ボタン + 前景カード（左スワイプで削除ボタンが出る・セリフ行と同じ）。
        var rowWrap = new VisualElement();
        rowWrap.AddToClassList("conv-line-row");

        var swipeDelete = new VisualElement();
        swipeDelete.AddToClassList("conv-swipe-delete");
        var swipeBtn = new Button(() =>
        {
            UiSwipeReveal.CloseCurrent();
            if (_logic.Remove(id))
                Refresh();
        }) { text = "", tooltip = "会話を削除" };
        swipeBtn.AddToClassList("conv-swipe-delete-btn");
        swipeDelete.Add(swipeBtn);
        rowWrap.Add(swipeDelete);

        // カードは [縦長ドラッグハンドル | 内容列]（前景・不透明）
        var card = new VisualElement();
        card.AddToClassList("conv-line-card");
        rowWrap.Add(card);

        if (_reorder != null)
        {
            // 並べ替えの行はラッパー（リスト直下の子）を登録する。
            var handle = _reorder.CreateHandle(rowWrap, string.IsNullOrEmpty(conv.name) ? "（名称なし）" : conv.name);
            handle.AddToClassList("conv-line-handle");
            card.Add(handle);
        }

        // 前景カードを左スワイプで開ける（背後の削除ボタンが出る）。
        _ = new UiSwipeReveal(card, SwipeRevealWidth);

        var content = new VisualElement();
        content.AddToClassList("conv-line-content");
        card.Add(content);

        // ヘッダー: 情報（名前 + 行数・タップで編集）+ 右に「⋯」（この会話の操作）
        var header = new VisualElement();
        header.AddToClassList("conv-row-top");

        var info = new VisualElement();
        info.AddToClassList("conv-row-info");
        var name = new Label(conv.name);
        name.AddToClassList("conv-name");
        info.Add(name);
        var meta = new Label($"{conv.lines?.Length ?? 0} 行");
        meta.AddToClassList("conv-meta");
        info.Add(meta);
        info.RegisterCallback<ClickEvent>(_ => EditRequested?.Invoke(id)); // ハンドル/ボタン以外をタップで編集
        header.Add(info);

        var moreBtn = UiLangText.ToolButton("conv-line-tool-btn--more", "この会話の操作");
        moreBtn.clicked += () => _popup.Open(moreBtn, new[]
        {
            new UiPopupMenu.Item("会話を削除", () =>
            {
                if (_logic.Remove(id))
                    Refresh();
            }, "ui-popup-item-icon--trash", "ui-popup-item--danger"),
        }, _list as ScrollView);
        header.Add(moreBtn);
        content.Add(header);

        // 下段: この会話に登場する話者（タップで編集）
        var speakers = BuildConversationSpeakers(conv);
        speakers.RegisterCallback<ClickEvent>(_ => EditRequested?.Invoke(id));
        content.Add(speakers);

        return rowWrap;
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
            var sp = _speakers?.Find(id);
            string name = SpeakerLibraryLogic.ResolveName(sp, app);
            wrap.Add(SpeakerChip(string.IsNullOrEmpty(name) ? "（不明な話者）" : name, sp?.colorIndex ?? -1));
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
