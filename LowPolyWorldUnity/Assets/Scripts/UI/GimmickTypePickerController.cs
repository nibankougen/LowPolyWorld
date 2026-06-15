using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

/// <summary>
/// ギミックの種類（入力イベント / 条件 / アクション）を選ぶオーバーレイ選択リスト。
///
/// 全カテゴリの選択肢を 1 画面に縦に並べ、カテゴリごとに見出しを表示して区切りを分かりやすくする
/// （<see cref="GimmickTypeCatalog"/> のジャンル分けを使用）。項目タップで確定し閉じる。
/// <see cref="RuleEditController"/> から開かれる。
/// </summary>
public class GimmickTypePickerController
{
    private readonly VisualElement _overlay;
    private readonly Button _btnBack;
    private readonly Label _title;
    private readonly VisualElement _list;

    public GimmickTypePickerController(VisualElement root)
    {
        if (root == null)
            throw new ArgumentNullException(nameof(root));

        _overlay = root.Q("gimmick-type-picker");
        _btnBack = root.Q<Button>("type-picker-back");
        _title = root.Q<Label>("type-picker-title");
        _list = root.Q("type-picker-list");

        if (_btnBack != null) _btnBack.clicked += Close;
    }

    public bool IsOpen => _overlay != null && !_overlay.ClassListContains("overlay-hidden");

    /// <summary>
    /// 種類選択リストを開く。category 見出し + 各種別ボタンを縦に並べ、
    /// 選択された種別 ID で <paramref name="onSelected"/> を呼んで閉じる。
    /// </summary>
    public void Open(
        string title,
        IReadOnlyList<GimmickTypeCatalog.Category> categories,
        Func<string, string> labelOf,
        string currentType,
        Action<string> onSelected)
    {
        if (_overlay == null || _list == null)
            return;

        if (_title != null)
            _title.text = title;

        _list.Clear();
        foreach (var category in categories)
        {
            var header = new Label(category.Label);
            header.AddToClassList("gimmick-picker-category");
            _list.Add(header);

            foreach (var id in category.TypeIds)
            {
                string typeId = id;
                var item = new Button(() =>
                {
                    Close();
                    onSelected?.Invoke(typeId);
                })
                {
                    text = labelOf(id),
                };
                item.AddToClassList("gimmick-picker-item");
                if (id == currentType)
                    item.AddToClassList("gimmick-picker-item--selected");
                _list.Add(item);
            }
        }

        _overlay.EnableInClassList("overlay-hidden", false);
    }

    public void Close() => _overlay?.EnableInClassList("overlay-hidden", true);
}
