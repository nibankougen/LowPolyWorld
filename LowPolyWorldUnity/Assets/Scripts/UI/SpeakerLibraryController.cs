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

    private readonly VisualElement _overlay;
    private readonly Button _btnBack;
    private readonly VisualElement _list;
    private readonly Label _flash;

    private readonly SpeakerLibraryLogic _library;
    private readonly UiListDragReorder _reorder;
    private IVisualElementScheduledItem _flashHide;

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

    public void Close()
    {
        if (_overlay == null)
            return;
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
        _list.Clear();

        _reorder?.Reset();
        if (_library.Count == 0)
        {
            var empty = new Label("話者がいません。下のボタンで追加します");
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
        var card = new VisualElement();
        card.AddToClassList("conv-line-card");

        var head = new VisualElement();
        head.AddToClassList("conv-line-head");
        if (_reorder != null)
        {
            string name = SpeakerLibraryLogic.ResolveName(speaker, DeviceLanguage.CurrentCode());
            head.Add(_reorder.CreateHandle(card, string.IsNullOrEmpty(name) ? "（名前なし）" : name));
        }
        var spacer = new VisualElement();
        spacer.style.flexGrow = 1;
        head.Add(spacer);
        head.Add(IconButton("gimmick-icon-btn--close", "削除", () =>
        {
            if (_library.Remove(id))
                Refresh();
        }));
        card.Add(head);

        // 名前（既定 = アプリの設定言語・詳細でそれ以外の言語）
        card.Add(MultilangName(id, speaker));
        return card;
    }

    private VisualElement MultilangName(string speakerId, SpeakerJson speaker)
    {
        var box = new VisualElement();
        box.AddToClassList("conv-ml");

        string app = DeviceLanguage.CurrentCode();
        box.Add(NameField($"名前（{SupportedLanguages.LabelOf(app)}）", app, TextForLang(speaker.names, app), speakerId));

        var detail = new Foldout { text = "詳細（言語別）", value = false };
        detail.AddToClassList("conv-ml-detail");
        foreach (var lang in SupportedLanguages.All)
            if (lang.Code != app)
                detail.Add(NameField(lang.Label, lang.Code, TextForLang(speaker.names, lang.Code), speakerId));
        box.Add(detail);
        return box;
    }

    private TextField NameField(string label, string lang, string initial, string speakerId)
    {
        var f = new TextField(label) { maxLength = SpeakerLibraryLogic.NameMaxLength };
        f.AddToClassList("conv-ml-field");
        f.SetValueWithoutNotify(initial);
        f.RegisterValueChangedCallback(e =>
        {
            if (string.IsNullOrEmpty(e.newValue))
                _library.RemoveName(speakerId, lang);
            else
                _library.SetName(speakerId, lang, e.newValue);
        });
        return f;
    }

    // ドラッグ＆ドロップ並べ替え（from の話者を to の位置へ）。
    private void OnReorder(int from, int to)
    {
        if ((uint)from >= (uint)_library.Speakers.Count)
            return;
        if (_library.Move(_library.Speakers[from].speakerId, to))
            Refresh();
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

    private static Button IconButton(string iconClass, string tooltip, Action onClick)
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
