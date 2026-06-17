using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

/// <summary>
/// ワールド単位の話者定義（<see cref="SpeakerJson"/>）を管理する純粋 C# ロジック（world-creation.md 9.13）。
///
/// 担当: 話者の追加 / 削除 / 並び替え（最大 <see cref="MaxSpeakers"/>）と言語別名前の編集、
/// <see cref="WorldDefinitionJson"/> との往復。会話行は名前を直接持たず <see cref="ConversationLineJson.speakerId"/>
/// で話者を参照する。名前の表示解決は <see cref="ResolveName"/>（未設定言語は英語優先でフォールバック）。
/// </summary>
public class SpeakerLibraryLogic
{
    public const int MaxSpeakers = 30;
    public const int NameMaxLength = 40;

    private static readonly Regex DefaultNamePattern = new(@"^話者(\d+)$", RegexOptions.Compiled);

    private readonly List<SpeakerJson> _speakers = new();

    public IReadOnlyList<SpeakerJson> Speakers => _speakers;
    public int Count => _speakers.Count;
    public bool CanAdd => Count < MaxSpeakers;

    public static string SanitizeName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return "";
        var trimmed = name.Trim();
        return trimmed.Length > NameMaxLength ? trimmed.Substring(0, NameMaxLength) : trimmed;
    }

    /// <summary>話者を 1 つ追加して返す。上限到達時は null。name 省略時は「話者N」を lang で自動採番。</summary>
    public SpeakerJson Add(string lang = "", string name = null)
    {
        if (!CanAdd)
            return null;
        var speaker = new SpeakerJson { speakerId = NewId() };
        var label = string.IsNullOrWhiteSpace(name) ? NextDefaultName() : SanitizeName(name);
        speaker.names = new[] { new GimmickTextJson { lang = lang ?? "", text = label } };
        _speakers.Add(speaker);
        return speaker;
    }

    public bool Remove(string speakerId)
    {
        int idx = IndexOf(speakerId);
        if (idx < 0)
            return false;
        _speakers.RemoveAt(idx);
        return true;
    }

    /// <summary>話者を newIndex の位置へ移動する（範囲外はクランプ）。</summary>
    public bool Move(string speakerId, int newIndex)
    {
        int idx = IndexOf(speakerId);
        if (idx < 0)
            return false;
        newIndex = newIndex < 0 ? 0 : newIndex >= _speakers.Count ? _speakers.Count - 1 : newIndex;
        if (newIndex == idx)
            return true;
        var s = _speakers[idx];
        _speakers.RemoveAt(idx);
        _speakers.Insert(newIndex, s);
        return true;
    }

    /// <summary>言語別の名前を設定する（40 文字に切り詰め・空テキストは拒否＝<see cref="RemoveName"/> を使う）。</summary>
    public bool SetName(string speakerId, string lang, string name)
    {
        var speaker = Find(speakerId);
        if (speaker == null || string.IsNullOrEmpty(name))
            return false;
        lang ??= "";
        var clamped = SanitizeName(name);
        if (string.IsNullOrEmpty(clamped))
            return false;
        var list = new List<GimmickTextJson>(speaker.names ?? Array.Empty<GimmickTextJson>());
        var existing = list.Find(t => t != null && t.lang == lang);
        if (existing != null)
            existing.text = clamped;
        else
            list.Add(new GimmickTextJson { lang = lang, text = clamped });
        speaker.names = list.ToArray();
        return true;
    }

    public bool RemoveName(string speakerId, string lang)
    {
        var speaker = Find(speakerId);
        if (speaker == null)
            return false;
        lang ??= "";
        var list = new List<GimmickTextJson>(speaker.names ?? Array.Empty<GimmickTextJson>());
        int removed = list.RemoveAll(t => t != null && t.lang == lang);
        if (removed == 0)
            return false;
        speaker.names = list.ToArray();
        return true;
    }

    public SpeakerJson Find(string speakerId)
    {
        foreach (var s in _speakers)
            if (s.speakerId == speakerId)
                return s;
        return null;
    }

    /// <summary>speakerId の表示名を解決する（未知 ID / 空は ""）。</summary>
    public string DisplayName(string speakerId, string lang) => ResolveName(Find(speakerId), lang);

    /// <summary>
    /// 話者名を閲覧者言語へ解決する。優先: 指定言語 → 英語 → デフォルト("") → 先頭の非空 → ""。
    /// </summary>
    public static string ResolveName(SpeakerJson speaker, string lang)
    {
        var names = speaker?.names;
        if (names == null || names.Length == 0)
            return "";
        lang ??= "";
        string exact = Find(names, lang);
        if (exact != null) return exact;
        if (lang != SupportedLanguages.Fallback)
        {
            string en = Find(names, SupportedLanguages.Fallback);
            if (en != null) return en;
        }
        if (lang != "")
        {
            string def = Find(names, "");
            if (def != null) return def;
        }
        foreach (var t in names)
            if (t != null && !string.IsNullOrEmpty(t.text))
                return t.text;
        return "";
    }

    private static string Find(GimmickTextJson[] names, string lang)
    {
        foreach (var t in names)
            if (t != null && t.lang == lang && !string.IsNullOrEmpty(t.text))
                return t.text;
        return null;
    }

    // ── ワールド定義との往復 ───────────────────────────────────────────────────

    public void LoadFrom(WorldDefinitionJson def)
    {
        _speakers.Clear();
        if (def?.speakers != null)
            foreach (var s in def.speakers)
                if (s != null && !string.IsNullOrEmpty(s.speakerId))
                    _speakers.Add(s);
    }

    public void WriteTo(WorldDefinitionJson def)
    {
        if (def != null)
            def.speakers = _speakers.ToArray();
    }

    private int IndexOf(string speakerId)
    {
        for (int i = 0; i < _speakers.Count; i++)
            if (_speakers[i].speakerId == speakerId)
                return i;
        return -1;
    }

    private static string NewId() => "spk_" + Guid.NewGuid().ToString("N").Substring(0, 8);

    // 既存の「話者N」名（いずれかの言語）の最大連番 + 1。
    private string NextDefaultName()
    {
        int max = 0;
        foreach (var s in _speakers)
        {
            if (s.names == null)
                continue;
            foreach (var t in s.names)
            {
                if (t == null)
                    continue;
                var m = DefaultNamePattern.Match(t.text ?? "");
                if (m.Success && int.TryParse(m.Groups[1].Value, out int n) && n > max)
                    max = n;
            }
        }
        return $"話者{max + 1}";
    }
}
