using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

/// <summary>
/// ワールド BGM をオーバーレイの選択リストから選ぶ（screens-and-modes.md 11.7.5 / world-creation.md 14）。
///
/// ギミック種類ピッカー（<see cref="GimmickTypePickerController"/>）と同じ操作感で、
/// 全トラックを 1 画面に縦に並べ、種別（環境音 / BGM / 購入済み BGM）ごとに見出しを付けて区切る。
/// 項目タップで soundId を確定して閉じる。
/// </summary>
public class BgmPickerController
{
    private readonly VisualElement _overlay;
    private readonly Button _btnBack;
    private readonly VisualElement _list;

    public BgmPickerController(VisualElement root)
    {
        if (root == null)
            throw new ArgumentNullException(nameof(root));

        _overlay = root.Q("bgm-picker");
        _btnBack = root.Q<Button>("bgm-picker-back");
        _list = root.Q("bgm-picker-list");

        if (_btnBack != null) _btnBack.clicked += Close;
    }

    public bool IsOpen => _overlay != null && !_overlay.ClassListContains("overlay-hidden");

    /// <summary>トラック一覧を種別ごとに見出し付きで表示する。項目タップで onSelected(soundId)。</summary>
    public void Open(IReadOnlyList<WorldMusicTrack> tracks, string currentSoundId, Action<string> onSelected)
    {
        if (_overlay == null || _list == null)
            return;

        _list.Clear();
        // 種別順に見出し + 項目を並べる（その種別のトラックが無ければ見出しごと省略）
        AddKind(tracks, TrackKind.None, "BGM なし", currentSoundId, onSelected);
        AddKind(tracks, TrackKind.Ambient, "環境音", currentSoundId, onSelected);
        AddKind(tracks, TrackKind.Bgm, "BGM", currentSoundId, onSelected);
        AddKind(tracks, TrackKind.Shop, "購入済み BGM", currentSoundId, onSelected);

        _overlay.EnableInClassList("overlay-hidden", false);
    }

    public void Close() => _overlay?.EnableInClassList("overlay-hidden", true);

    private void AddKind(
        IReadOnlyList<WorldMusicTrack> tracks, TrackKind kind, string header,
        string currentSoundId, Action<string> onSelected)
    {
        bool headerAdded = false;
        foreach (var track in tracks)
        {
            if (track.Kind != kind)
                continue;

            if (!headerAdded)
            {
                var headerLabel = new Label(header);
                headerLabel.AddToClassList("gimmick-picker-category");
                _list.Add(headerLabel);
                headerAdded = true;
            }

            _list.Add(BuildItem(track, currentSoundId, onSelected));
        }
    }

    private VisualElement BuildItem(WorldMusicTrack track, string currentSoundId, Action<string> onSelected)
    {
        string soundId = track.SoundId;
        var item = new Button(() =>
        {
            Close();
            onSelected?.Invoke(soundId);
        });
        item.AddToClassList("gimmick-picker-item");
        if (track.SoundId == currentSoundId)
            item.AddToClassList("gimmick-picker-item--selected");

        var name = new Label(track.DisplayName) { pickingMode = PickingMode.Ignore };
        name.AddToClassList("bgm-picker-name");
        item.Add(name);

        if (!string.IsNullOrEmpty(track.AuthorName))
        {
            var author = new Label(track.AuthorName) { pickingMode = PickingMode.Ignore };
            author.AddToClassList("bgm-picker-author");
            item.Add(author);
        }

        return item;
    }
}
