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

    private readonly VisualElement _overlay;
    private readonly Button _btnBack;
    private readonly Button _btnAdd;
    private readonly VisualElement _list;
    private readonly Label _flash;

    private IVisualElementScheduledItem _flashHide;

    public ConversationLibraryController(VisualElement root, ConversationLibraryLogic logic)
    {
        if (root == null)
            throw new ArgumentNullException(nameof(root));
        _logic = logic ?? throw new ArgumentNullException(nameof(logic));

        _overlay = root.Q("conv-library");
        _btnBack = root.Q<Button>("conv-library-back");
        _btnAdd = root.Q<Button>("conv-library-add");
        _list = root.Q("conv-library-list");
        _flash = root.Q<Label>("conv-library-flash");

        if (_btnBack != null) _btnBack.clicked += Close;
        if (_btnAdd != null) _btnAdd.clicked += OnAdd;
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

    /// <summary>一覧を再構築する（エディタから戻ったとき等）。</summary>
    public void Refresh()
    {
        if (_list == null)
            return;
        _list.Clear();

        if (_logic.Count == 0)
        {
            var empty = new Label("会話がありません。＋ で追加します");
            empty.AddToClassList("conv-empty");
            _list.Add(empty);
            return;
        }

        foreach (var conv in _logic.Conversations)
            _list.Add(BuildRow(conv));
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
        var row = new VisualElement();
        row.AddToClassList("conv-row");

        var name = new Label(conv.name);
        name.AddToClassList("conv-name");
        name.RegisterCallback<ClickEvent>(_ => EditRequested?.Invoke(conv.conversationId));
        row.Add(name);

        var meta = new Label($"{conv.lines?.Length ?? 0} 行");
        meta.AddToClassList("conv-meta");
        row.Add(meta);

        row.Add(IconButton("gimmick-icon-btn--up", "上へ", () => Move(conv.conversationId, -1)));
        row.Add(IconButton("gimmick-icon-btn--down", "下へ", () => Move(conv.conversationId, +1)));
        row.Add(IconButton("gimmick-icon-btn--close", "削除", () =>
        {
            if (_logic.Remove(conv.conversationId))
                Refresh();
        }));

        return row;
    }

    private void Move(string id, int delta)
    {
        int idx = IndexOf(id);
        if (idx < 0)
            return;
        if (_logic.Move(id, idx + delta))
            Refresh();
    }

    private int IndexOf(string id)
    {
        for (int i = 0; i < _logic.Conversations.Count; i++)
            if (_logic.Conversations[i].conversationId == id)
                return i;
        return -1;
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
