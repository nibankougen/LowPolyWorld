using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

/// <summary>
/// ギミックタブの「テンプレートから追加」（world-creation.md 9.12）のオーバーレイ UI。
///
/// 2 段構成: (1) テンプレート一覧（名前 + 説明）→ (2) パラメータ入力フォーム（秒数等）。
/// パラメータの無いテンプレートは一覧タップで即挿入する。確定すると
/// <c>onInsert(templateId, values)</c> を呼ぶ（実際の挿入・容量判定は呼び出し側 =
/// <see cref="GimmickTabController"/> が <see cref="GimmickTemplateLogic.Insert"/> で行う）。
/// </summary>
public class GimmickTemplatePickerController
{
    private readonly VisualElement _overlay;
    private readonly Button _btnBack;
    private readonly ScrollView _list;
    private readonly VisualElement _paramsBox;
    private readonly Label _paramTitle;
    private readonly Label _paramDesc;
    private readonly VisualElement _paramFields;
    private readonly Button _insertBtn;

    private Action<string, IReadOnlyDictionary<string, int>> _onInsert;
    private readonly Dictionary<string, IntegerField> _fieldByKey = new();
    private GimmickTemplateLogic.Template _selected;

    public GimmickTemplatePickerController(VisualElement root)
    {
        if (root == null)
            throw new ArgumentNullException(nameof(root));

        _overlay = root.Q("gimmick-template-picker");
        _btnBack = root.Q<Button>("template-picker-back");
        _list = root.Q<ScrollView>("template-picker-list");
        _paramsBox = root.Q("template-picker-params");
        _paramTitle = root.Q<Label>("template-picker-param-title");
        _paramDesc = root.Q<Label>("template-picker-param-desc");
        _paramFields = root.Q("template-picker-param-fields");
        _insertBtn = root.Q<Button>("template-picker-insert");

        if (_btnBack != null) _btnBack.clicked += OnBack;
        if (_insertBtn != null) _insertBtn.clicked += OnConfirmParams;
    }

    public bool IsOpen => _overlay != null && !_overlay.ClassListContains("overlay-hidden");

    /// <summary>テンプレート一覧を開く。確定時に <paramref name="onInsert"/> を呼ぶ。</summary>
    public void Open(Action<string, IReadOnlyDictionary<string, int>> onInsert)
    {
        if (_overlay == null)
            return;
        _onInsert = onInsert;
        BuildList();
        ShowList();
        _overlay.EnableInClassList("overlay-hidden", false);
    }

    public void Close()
    {
        _overlay?.EnableInClassList("overlay-hidden", true);
        _selected = null;
    }

    // ── 一覧 ────────────────────────────────────────────────────────────────────

    private void BuildList()
    {
        if (_list == null)
            return;
        _list.Clear();
        foreach (var template in GimmickTemplateLogic.All)
        {
            var t = template; // クロージャ用にコピー
            var item = new VisualElement();
            item.AddToClassList("gimmick-template-item");

            var name = new Label(t.Name);
            name.AddToClassList("gimmick-template-item-name");
            item.Add(name);

            var desc = new Label(t.Description);
            desc.AddToClassList("gimmick-template-item-desc");
            item.Add(desc);

            item.RegisterCallback<ClickEvent>(_ => OnSelectTemplate(t));
            _list.Add(item);
        }
    }

    private void OnSelectTemplate(GimmickTemplateLogic.Template template)
    {
        // パラメータが無ければ即挿入する。
        if (template.Params.Length == 0)
        {
            Close();
            _onInsert?.Invoke(template.Id, null);
            return;
        }

        // パラメータ入力フォームへ。
        _selected = template;
        BuildParamFields(template);
        ShowParams();
    }

    // ── パラメータ入力 ─────────────────────────────────────────────────────────

    private void BuildParamFields(GimmickTemplateLogic.Template template)
    {
        if (_paramTitle != null) _paramTitle.text = template.Name;
        if (_paramDesc != null) _paramDesc.text = template.Description;

        _fieldByKey.Clear();
        _paramFields?.Clear();
        foreach (var p in template.Params)
        {
            var row = new VisualElement();
            row.AddToClassList("gimmick-template-param-row");

            var label = new Label($"{p.Label}（{p.Min}〜{p.Max}）");
            label.AddToClassList("gimmick-template-param-label");
            row.Add(label);

            var field = new IntegerField { value = p.Default };
            field.AddToClassList("gimmick-template-param-field");
            // 入力中も範囲内へクランプして見た目を正す（最終クランプは Insert でも行う）。
            field.RegisterValueChangedCallback(e =>
            {
                int clamped = e.newValue < p.Min ? p.Min : e.newValue > p.Max ? p.Max : e.newValue;
                if (clamped != e.newValue)
                    field.SetValueWithoutNotify(clamped);
            });
            row.Add(field);

            _paramFields?.Add(row);
            _fieldByKey[p.Key] = field;
        }
    }

    private void OnConfirmParams()
    {
        if (_selected == null)
            return;
        var values = new Dictionary<string, int>();
        foreach (var kv in _fieldByKey)
            values[kv.Key] = kv.Value.value;

        string id = _selected.Id;
        Close();
        _onInsert?.Invoke(id, values);
    }

    // ── 表示切り替え ───────────────────────────────────────────────────────────

    private void OnBack()
    {
        // パラメータ入力中なら一覧へ戻る。一覧表示中なら閉じる。
        if (_paramsBox != null && !_paramsBox.ClassListContains("overlay-hidden"))
            ShowList();
        else
            Close();
    }

    private void ShowList()
    {
        _selected = null;
        _list?.EnableInClassList("overlay-hidden", false);
        _paramsBox?.EnableInClassList("overlay-hidden", true);
    }

    private void ShowParams()
    {
        _list?.EnableInClassList("overlay-hidden", true);
        _paramsBox?.EnableInClassList("overlay-hidden", false);
    }
}
