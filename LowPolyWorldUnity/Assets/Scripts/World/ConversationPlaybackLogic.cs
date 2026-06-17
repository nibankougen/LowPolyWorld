using System;
using System.Collections.Generic;

/// <summary>
/// 会話（<see cref="ConversationJson"/>）の再生を駆動する純粋 C# ランタイムロジック
/// （world-creation.md 9.13）。ギミックアクション「会話を開始」（<see cref="StartConversationEffect"/>）
/// を受けた上位レイヤー（オーナー権威）が、対象プレイヤーごとに 1 インスタンス生成して進行を制御する。
///
/// 担当:
/// - 現在のセリフ行の解決（話者・本文・選択肢を閲覧者の言語へ解決。未設定言語は英語優先でフォールバック）
/// - 「次へ」/ 選択肢による分岐（ジャンプ先 "" = 次の行・"end" = 終了・行 ID = 同一会話内へジャンプ）
/// - 行到達時 / 選択時のステート変更要求（<see cref="StateChangeRequest"/>）の発行
///
/// ステート変更の**適用そのものは行わない**。9.13 の権威モデルに従い、上位レイヤー（オーナー）が
/// 返却された要求を会話定義と照合して検証・適用し、全プレイヤーへ同期する。連鎖の無限ループ防止
/// （9.10）のカウントも上位レイヤーの責務。表示 UI・選択待ち・同期は MonoBehaviour / Netcode 層が担う。
/// </summary>
public class ConversationPlaybackLogic
{
    public const string GotoNext = "";   // 次の行へ（最終行なら終了）
    public const string GotoEnd = "end"; // 会話終了
    public const string FallbackLang = "en"; // 未設定言語のフォールバック（英語優先）

    /// <summary>
    /// オーナーへ送るステート変更要求（9.13 行到達 / 選択時のステート変更・値は固定値のみ）。
    /// オーナーが定義と照合のうえ権威的に適用する。
    /// </summary>
    public class StateChangeRequest
    {
        public string Kind { get; }         // worldState | playerState
        public int StateIndex { get; }
        public string StateOp { get; }      // set | add | sub
        public int Value { get; }
        public string PlayerTarget { get; } // playerState 用: input | opponent | all

        public StateChangeRequest(string kind, int stateIndex, string stateOp, int value, string playerTarget)
        {
            Kind = kind;
            StateIndex = stateIndex;
            StateOp = stateOp;
            Value = value;
            PlayerTarget = playerTarget;
        }
    }

    /// <summary>現在表示すべきセリフ行（閲覧者言語へ解決済み）。</summary>
    public class DisplayLine
    {
        public string LineId { get; }
        public string Speaker { get; }                 // 解決済み話者名（未設定なら ""）
        public string Text { get; }                    // 解決済み本文
        public IReadOnlyList<string> Choices { get; }  // 解決済み選択肢テキスト（空 = 選択肢なし）

        public DisplayLine(string lineId, string speaker, string text, IReadOnlyList<string> choices)
        {
            LineId = lineId;
            Speaker = speaker;
            Text = text;
            Choices = choices;
        }

        public bool HasChoices => Choices != null && Choices.Count > 0;
    }

    private static readonly IReadOnlyList<StateChangeRequest> NoChanges = Array.Empty<StateChangeRequest>();

    private readonly ConversationLineJson[] _lines;
    private readonly Dictionary<string, int> _lineIndexById;
    private readonly string _viewerLang;

    private int _currentIndex = -1;
    private bool _started;
    private bool _finished;

    private readonly Dictionary<string, SpeakerJson> _speakersById;

    public ConversationPlaybackLogic(
        ConversationJson conversation, string viewerLang = "", IReadOnlyList<SpeakerJson> speakers = null)
    {
        _viewerLang = viewerLang ?? "";
        ConversationId = conversation?.conversationId ?? "";
        _lines = conversation?.lines ?? Array.Empty<ConversationLineJson>();
        _lineIndexById = new Dictionary<string, int>();
        for (int i = 0; i < _lines.Length; i++)
        {
            var l = _lines[i];
            if (l != null && !string.IsNullOrEmpty(l.lineId) && !_lineIndexById.ContainsKey(l.lineId))
                _lineIndexById[l.lineId] = i;
        }

        _speakersById = new Dictionary<string, SpeakerJson>();
        if (speakers != null)
            foreach (var s in speakers)
                if (s != null && !string.IsNullOrEmpty(s.speakerId))
                    _speakersById[s.speakerId] = s;
    }

    /// <summary>この再生が対象とする会話 ID（上位レイヤーの管理用）。</summary>
    public string ConversationId { get; }

    /// <summary>会話が終了したか（最終行通過 / "end" 到達 / 空の会話）。</summary>
    public bool IsFinished => _finished;

    /// <summary>現在表示すべき行。未開始 / 終了 / 空の会話では null。</summary>
    public DisplayLine Current =>
        _finished || !_started || !IsValid(_currentIndex) ? null : Resolve(_lines[_currentIndex]);

    /// <summary>「次へ」で進められるか（開始済み・未終了・選択肢のない行）。</summary>
    public bool CanAdvance
    {
        get
        {
            if (_finished || !_started || !IsValid(_currentIndex))
                return false;
            var line = _lines[_currentIndex];
            return line != null && (line.choices == null || line.choices.Length == 0);
        }
    }

    /// <summary>
    /// 会話を開始し、先頭行へ入る。先頭行の到達時ステート変更要求を返す。
    /// 空の会話 / 先頭が無効な場合は即終了（空リスト）。複数回呼んでも先頭再入はしない。
    /// </summary>
    public IReadOnlyList<StateChangeRequest> Start()
    {
        if (_started)
            return NoChanges;
        _started = true;
        return EnterLine(0);
    }

    /// <summary>
    /// 選択肢のない行で「次へ」を押す。現在行のジャンプ先へ移動し、到達先の到達時ステート変更要求を返す。
    /// 選択肢のある行 / 未開始 / 終了済みでは何もしない（空リスト）。
    /// </summary>
    public IReadOnlyList<StateChangeRequest> Advance()
    {
        if (!CanAdvance)
            return NoChanges;
        return GotoFrom(_lines[_currentIndex].gotoLineId, null);
    }

    /// <summary>
    /// 選択肢を選ぶ。選択時ステート変更 → ジャンプ先到達時ステート変更 の順で要求を返す。
    /// 選択肢のない行 / 範囲外 / 未開始 / 終了済みでは何もしない（空リスト）。
    /// </summary>
    public IReadOnlyList<StateChangeRequest> Select(int choiceIndex)
    {
        if (_finished || !_started || !IsValid(_currentIndex))
            return NoChanges;
        var line = _lines[_currentIndex];
        var choices = line?.choices;
        if (choices == null || (uint)choiceIndex >= (uint)choices.Length)
            return NoChanges;
        var choice = choices[choiceIndex];
        if (choice == null)
            return NoChanges;
        // 選択時のステート変更を先に、続けてジャンプ先の到達時ステート変更を発行する。
        return GotoFrom(choice.gotoLineId, ToRequest(choice.effect));
    }

    // ── 内部 ──────────────────────────────────────────────────────────────────

    // gotoLineId に従って遷移し、到達行の onReach を含むステート変更要求を返す。
    // leading が非 null の場合は先頭に加える（選択肢の effect）。
    private IReadOnlyList<StateChangeRequest> GotoFrom(string gotoLineId, StateChangeRequest leading)
    {
        int target = ResolveGoto(gotoLineId);
        var onReach = target >= 0 ? EnterLine(target) : Finish();
        if (leading == null)
            return onReach;
        var list = new List<StateChangeRequest>(onReach.Count + 1) { leading };
        list.AddRange(onReach);
        return list;
    }

    // 行 index へ入り、その行の到達時ステート変更要求を返す（無効 index は終了）。
    private IReadOnlyList<StateChangeRequest> EnterLine(int index)
    {
        if (!IsValid(index) || _lines[index] == null)
            return Finish();
        _currentIndex = index;
        var req = ToRequest(_lines[index].onReach);
        return req == null ? NoChanges : new[] { req };
    }

    private IReadOnlyList<StateChangeRequest> Finish()
    {
        _finished = true;
        _currentIndex = -1;
        return NoChanges;
    }

    // gotoLineId を解決: "" = 次の行（無ければ -1）/ "end" = -1（終了）/ 行 ID = その index（無ければ -1）。
    private int ResolveGoto(string gotoLineId)
    {
        if (string.IsNullOrEmpty(gotoLineId))
            return _currentIndex + 1 < _lines.Length ? _currentIndex + 1 : -1;
        if (gotoLineId == GotoEnd)
            return -1;
        return _lineIndexById.TryGetValue(gotoLineId, out int idx) ? idx : -1;
    }

    private static StateChangeRequest ToRequest(ConversationEffectJson effect)
    {
        if (effect == null || string.IsNullOrEmpty(effect.kind) || effect.kind == "none")
            return null;
        return new StateChangeRequest(
            effect.kind, effect.stateIndex, effect.stateOp ?? "set", effect.value, effect.playerTarget ?? "input");
    }

    private DisplayLine Resolve(ConversationLineJson line)
    {
        string speaker = ResolveSpeaker(line.speakerId);
        string text = ResolveText(line.texts);
        string[] choiceTexts;
        var choices = line.choices;
        if (choices == null || choices.Length == 0)
        {
            choiceTexts = Array.Empty<string>();
        }
        else
        {
            choiceTexts = new string[choices.Length];
            for (int i = 0; i < choices.Length; i++)
                choiceTexts[i] = choices[i] == null ? "" : ResolveText(choices[i].texts);
        }
        return new DisplayLine(line.lineId, speaker, text, choiceTexts);
    }

    // speakerId から話者名を閲覧者言語で解決する（未指定 / 未知 ID は ""）。
    private string ResolveSpeaker(string speakerId)
    {
        if (string.IsNullOrEmpty(speakerId) || !_speakersById.TryGetValue(speakerId, out var s))
            return "";
        return SpeakerLibraryLogic.ResolveName(s, _viewerLang);
    }

    // 閲覧者言語 → 英語 → デフォルト（"") → 先頭の非空 の優先で解決（9.8 / 9.13 のフォールバック）。
    private string ResolveText(GimmickTextJson[] texts)
    {
        if (texts == null || texts.Length == 0)
            return "";
        string exact = Find(texts, _viewerLang);
        if (exact != null) return exact;
        if (_viewerLang != FallbackLang)
        {
            string en = Find(texts, FallbackLang);
            if (en != null) return en;
        }
        if (_viewerLang != "")
        {
            string def = Find(texts, "");
            if (def != null) return def;
        }
        foreach (var t in texts)
            if (t != null && !string.IsNullOrEmpty(t.text))
                return t.text;
        return "";
    }

    // lang に一致し非空テキストを持つエントリを返す（無ければ null）。
    private static string Find(GimmickTextJson[] texts, string lang)
    {
        foreach (var t in texts)
            if (t != null && t.lang == lang && !string.IsNullOrEmpty(t.text))
                return t.text;
        return null;
    }

    private bool IsValid(int index) => (uint)index < (uint)_lines.Length;
}
