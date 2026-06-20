using System;
using UnityEngine.UIElements;

/// <summary>
/// 多言語テキスト編集 UI の再利用ヘルパー（会話エディタ・話者編集で共有）。
///
/// 既定言語の入力欄（<see cref="DefaultField"/>）と、既定言語以外を並べる言語別欄
/// （<see cref="FillLangFields"/>）、タイトル + 「⋯」削除メニュー付きの詳細セクション枠
/// （<see cref="OptionalSection"/>）、枠なしツールボタン（＋ / ⋯）などを提供する。
/// USS は WorldEditor.uss を共有（conv-ml-field / conv-optional / conv-line-tool-btn 等）。
/// </summary>
public static class UiLangText
{
    // 既定言語のテキスト入力欄（ラベルなし）。空入力で削除・旧 "" 既定はアプリ設定言語へ移行。
    public static TextField DefaultField(
        GimmickTextJson[] texts, string app, int maxLen, bool multiline,
        Action<string, string> set, Action<string> remove)
    {
        string init = TextForLang(texts, app);
        if (string.IsNullOrEmpty(init))
            init = TextForLang(texts, ""); // 旧 "" 既定を初期表示で引き継ぐ
        return Field(null, maxLen, multiline, init, v =>
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
        });
    }

    // box に、既定言語以外の言語別入力欄を並べる。
    public static void FillLangFields(
        VisualElement box, GimmickTextJson[] texts, string app, int maxLen, bool multiline,
        Action<string, string> set, Action<string> remove)
    {
        foreach (var lang in SupportedLanguages.All)
        {
            if (lang.Code == app)
                continue;
            string code = lang.Code;
            box.Add(Field(lang.Label, maxLen, multiline, TextForLang(texts, code), v =>
            {
                if (string.IsNullOrEmpty(v))
                    remove(code);
                else
                    set(code, v);
            }));
        }
    }

    // 既定言語以外にテキストがあるか。
    public static bool HasOtherLang(GimmickTextJson[] texts, string app)
    {
        if (texts == null)
            return false;
        foreach (var t in texts)
            if (t != null && !string.IsNullOrEmpty(t.text) && t.lang != app && t.lang != "")
                return true;
        return false;
    }

    public static string TextForLang(GimmickTextJson[] texts, string lang)
    {
        if (texts == null)
            return "";
        foreach (var t in texts)
            if (t != null && t.lang == lang)
                return t.text ?? "";
        return "";
    }

    public static TextField Field(string label, int maxLen, bool multiline, string initial, Action<string> onChange)
    {
        var f = new TextField(label) { multiline = multiline, maxLength = maxLen };
        f.AddToClassList("conv-ml-field");
        f.SetValueWithoutNotify(initial);
        f.RegisterValueChangedCallback(e => onChange(e.newValue));
        return f;
    }

    public static Label RowLabel(string text)
    {
        var l = new Label(text);
        l.AddToClassList("conv-row-label");
        return l;
    }

    // 下部ツール行・セクションヘッダーの枠なし小アイコンボタン（＋ / ⋯）。
    public static Button ToolButton(string iconClass, string tooltip)
    {
        var btn = new Button { text = "", tooltip = tooltip };
        btn.AddToClassList("conv-line-tool-btn");
        btn.AddToClassList(iconClass);
        return btn;
    }

    // タイトル + 任意の「⋯」削除メニューを持つ詳細セクション枠。本文は呼び出し側が追加する。
    // 削除はセリフ行・選択肢と同じ経路（⋯ → 「削除」trash・danger）。
    public static VisualElement OptionalSection(string title, Action onRemove, UiPopupMenu popup, ScrollView closeOnScroll)
    {
        var box = new VisualElement();
        box.AddToClassList("conv-optional");

        var head = new VisualElement();
        head.AddToClassList("conv-optional-head");
        head.Add(RowLabel(title));
        if (onRemove != null && popup != null)
        {
            var sp = new VisualElement();
            sp.style.flexGrow = 1;
            head.Add(sp);
            var moreBtn = ToolButton("conv-line-tool-btn--more", "操作");
            moreBtn.clicked += () => popup.Open(moreBtn, new[]
            {
                new UiPopupMenu.Item("削除", onRemove, "ui-popup-item-icon--trash", "ui-popup-item--danger"),
            }, closeOnScroll);
            head.Add(moreBtn);
        }
        box.Add(head);
        return box;
    }
}
