using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

/// <summary>
/// ワールド単位の話者定義（<see cref="SpeakerLibraryLogic"/>）を編集するオーバーレイ UI
/// （world-creation.md 9.13）。会話一覧から「話者を編集」で開く。
///
/// 話者の追加 / 削除 / 並び替えと、言語別の名前入力（既定 = アプリの設定言語・「詳細」でそれ以外）を扱う。
/// 会話行はここで定義した話者を <see cref="ConversationEditorController"/> のドロップダウンから選ぶ。
/// </summary>
public class SpeakerLibraryController
{
    /// <summary>＜戻るで閉じたとき。</summary>
    public event Action Closed;

    // 左スワイプで現れる削除ボタン領域の幅(px)。
    private const float SwipeRevealWidth = 64f;

    private readonly VisualElement _overlay;
    private readonly Button _btnBack;
    private readonly VisualElement _list;
    private readonly Label _flash;

    private readonly SpeakerLibraryLogic _library;
    private readonly UiListDragReorder _reorder;
    private readonly UiPopupMenu _popup; // ＋（言語別名前を追加）/ ⋯（削除）のはみ出しメニュー
    private IVisualElementScheduledItem _flashHide;

    // 言語別名前を開いている話者 ID（"開いた" UI 状態を覚える・データがあるものは常に表示）。
    private readonly System.Collections.Generic.HashSet<string> _revealed = new();

    public SpeakerLibraryController(VisualElement root, SpeakerLibraryLogic library)
    {
        if (root == null)
            throw new ArgumentNullException(nameof(root));
        _library = library;

        _overlay = root.Q("speaker-library");
        _btnBack = root.Q<Button>("speaker-library-back");
        _list = root.Q("speaker-library-list");
        _flash = root.Q<Label>("speaker-library-flash");

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
        _revealed.Clear();
        Refresh();
        _overlay.EnableInClassList("overlay-hidden", false);
    }

    public void Close()
    {
        if (_overlay == null)
            return;
        _popup?.Close();
        UiSwipeReveal.CloseCurrent();
        _overlay.EnableInClassList("overlay-hidden", true);
        Closed?.Invoke();
    }

    private void OnAdd()
    {
        if (_library == null)
            return;
        string app = DeviceLanguage.CurrentCode();
        if (_library.Add(app) == null)
        {
            ShowFlash($"話者は最大 {SpeakerLibraryLogic.MaxSpeakers} 人までです");
            return;
        }
        Refresh();
        ScrollListToBottom(_list);
    }

    // 一覧（ScrollView）を最下部までスクロールする（要素追加直後に末尾を見せる）。
    private static void ScrollListToBottom(VisualElement listEl)
    {
        if (listEl is not ScrollView sv || sv.childCount == 0)
            return;
        var last = sv[sv.childCount - 1];
        sv.schedule.Execute(() => sv.ScrollTo(last)).StartingIn(0);
    }

    private void Refresh()
    {
        if (_list == null || _library == null)
            return;
        _popup?.Close();
        UiSwipeReveal.CloseCurrent();
        _list.Clear();

        _reorder?.Reset();
        if (_library.Count == 0)
        {
            var empty = new Label("話者がいません");
            empty.AddToClassList("conv-empty");
            _list.Add(empty);
        }
        else
        {
            foreach (var s in _library.Speakers)
                _list.Add(BuildCard(s));
        }

        // 一覧の最下部に追加ボタン
        var add = new Button(OnAdd) { text = "＋ 話者を追加" };
        add.AddToClassList("gimmick-template-top-btn");
        add.AddToClassList("gimmick-list-add--inset");
        _list.Add(add);
    }

    private VisualElement BuildCard(SpeakerJson speaker)
    {
        string id = speaker.speakerId;
        string app = DeviceLanguage.CurrentCode();

        // 行 = 背後の丸い削除ボタン + 前景カード（左スワイプで削除ボタンが出る・セリフ行と同じ）。
        var rowWrap = new VisualElement();
        rowWrap.AddToClassList("conv-line-row");

        var swipeDelete = new VisualElement();
        swipeDelete.AddToClassList("conv-swipe-delete");
        var swipeBtn = new Button(() =>
        {
            UiSwipeReveal.CloseCurrent();
            if (_library.Remove(id))
                Refresh();
        }) { text = "", tooltip = "話者を削除" };
        swipeBtn.AddToClassList("conv-swipe-delete-btn");
        swipeDelete.Add(swipeBtn);
        rowWrap.Add(swipeDelete);

        // カードは [縦長ドラッグハンドル | 内容列]（会話のセリフ行と同じ構成・前景）
        var card = new VisualElement();
        card.AddToClassList("conv-line-card");
        rowWrap.Add(card);

        if (_reorder != null)
        {
            // 並べ替えの行はラッパー（リスト直下の子）を登録する。
            string name = SpeakerLibraryLogic.ResolveName(speaker, app);
            var handle = _reorder.CreateHandle(rowWrap, string.IsNullOrEmpty(name) ? "（名前なし）" : name);
            handle.AddToClassList("conv-line-handle");
            card.Add(handle);
        }

        // 前景カードを左スワイプで開ける（背後の削除ボタンが出る）。
        _ = new UiSwipeReveal(card, SwipeRevealWidth);

        var content = new VisualElement();
        content.AddToClassList("conv-line-content");
        card.Add(content);

        Action<string, string> setName = (lang, text) => _library.SetName(id, lang, text);
        Action<string> removeName = lang => _library.RemoveName(id, lang);

        // 1 行目: 色スウォッチ + 名前（既定言語）。削除は下部の「⋯」メニューへ（会話と同じ）。
        var head = new VisualElement();
        head.AddToClassList("conv-line-head");
        head.Add(BuildColorSwatch(speaker));
        var nameField = UiLangText.DefaultField(
            speaker.names, app, SpeakerLibraryLogic.NameMaxLength, false, setName, removeName);
        nameField.style.flexGrow = 1;
        head.Add(nameField);
        content.Add(head);

        // 色パレット（常に表示）
        content.Add(BuildColorPalette(speaker));

        // 言語別の名前（他言語データあり or 開いたときだけ・⋯ メニューで閉じられる）
        bool hasOtherLang = UiLangText.HasOtherLang(speaker.names, app);
        string keyLang = id + ":langname";
        if (hasOtherLang || _revealed.Contains(keyLang))
        {
            var box = UiLangText.OptionalSection("言語別名前", hasOtherLang ? (Action)null : () =>
            {
                _revealed.Remove(keyLang);
                Refresh();
            }, _popup, _list as ScrollView);
            UiLangText.FillLangFields(box, speaker.names, app, SpeakerLibraryLogic.NameMaxLength, false, setName, removeName);
            content.Add(box);
        }

        // ── 下部ツール行: 左に「＋」（言語別名前を追加）・右に「⋯」（この話者の操作）──
        bool canLang = !hasOtherLang && !_revealed.Contains(keyLang);
        var tools = new VisualElement();
        tools.AddToClassList("conv-line-tools");

        if (canLang)
        {
            var addBtn = UiLangText.ToolButton("conv-line-tool-btn--plus", "追加");
            addBtn.clicked += () => _popup.Open(addBtn, new[]
            {
                new UiPopupMenu.Item("言語別名前", () => { _revealed.Add(keyLang); Refresh(); }),
            }, _list as ScrollView);
            tools.Add(addBtn);
        }

        var spacer = new VisualElement { pickingMode = PickingMode.Ignore };
        spacer.style.flexGrow = 1;
        tools.Add(spacer);

        var moreBtn = UiLangText.ToolButton("conv-line-tool-btn--more", "この話者の操作");
        moreBtn.clicked += () => _popup.Open(moreBtn, new[]
        {
            new UiPopupMenu.Item("話者を削除", () =>
            {
                if (_library.Remove(id))
                    Refresh();
            }, "ui-popup-item-icon--trash", "ui-popup-item--danger"),
        }, _list as ScrollView);
        tools.Add(moreBtn);

        content.Add(tools);
        return rowWrap;
    }

    // 話者の現在色を表すアイコン（icon_speaker を話者色でティント・枠なし）。
    private VisualElement BuildColorSwatch(SpeakerJson speaker)
    {
        var icon = new VisualElement { pickingMode = PickingMode.Ignore };
        icon.AddToClassList("speaker-color-swatch");
        if (SpeakerPalette.IsValidIndex(speaker.colorIndex))
            icon.style.unityBackgroundImageTintColor = SpeakerPalette.ColorOf(speaker.colorIndex);
        return icon;
    }

    // プリセット色のグリッド（常に表示）。選んだ色を話者に設定する。
    private VisualElement BuildColorPalette(SpeakerJson speaker)
    {
        string id = speaker.speakerId;
        var grid = new VisualElement();
        grid.AddToClassList("speaker-color-palette");
        for (int i = 0; i < SpeakerPalette.Count; i++)
        {
            int index = i;
            var opt = new Button(() =>
            {
                _library.SetColorIndex(id, index);
                Refresh();
            }) { text = "", tooltip = $"色 {index + 1}" };
            opt.AddToClassList("speaker-color-option");
            opt.style.backgroundColor = SpeakerPalette.ColorOf(index);
            if (speaker.colorIndex == index)
                opt.AddToClassList("speaker-color-option--selected");
            grid.Add(opt);
        }
        return grid;
    }

    // ドラッグ＆ドロップ並べ替え（from の話者を to の位置へ）。
    private void OnReorder(int from, int to)
    {
        if ((uint)from >= (uint)_library.Speakers.Count)
            return;
        if (_library.Move(_library.Speakers[from].speakerId, to))
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
