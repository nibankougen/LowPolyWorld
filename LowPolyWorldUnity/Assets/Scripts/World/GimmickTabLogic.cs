using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

/// <summary>
/// ギミックタブ（ギミックエディタ）の編集状態を管理する純粋 C# ロジック
/// （screens-and-modes.md 11.7.4 / world-creation.md 9.1・9.2）。
///
/// 担当: ステート定義（ワールド 0〜9・プレイヤー 0〜3・タイマー 0〜4 の名前 + 初期値）と
/// ルール一覧（追加・改名・削除・並び替え）。ルール・グループ合計は最大 100。
/// グループツリー編集とルール内容（入力イベント / 条件 / アクション）の編集は別レイヤー（後続）。
///
/// <see cref="WorldDefinitionJson"/> とは <see cref="LoadFrom"/> / <see cref="WriteTo"/> で往復する。
/// </summary>
public class GimmickTabLogic
{
    public const int MaxWorldStates = 10;
    public const int MaxPlayerStates = 4;
    public const int MaxTimers = 5;
    public const int LabelMaxLength = 20;
    public const int StateValueMax = 255;
    public const int MaxRulesAndGroups = 100;

    private static readonly Regex DefaultRuleNamePattern = new(@"^ルール(\d+)$", RegexOptions.Compiled);

    // ステート定義は「追加 / 削除」式（最小 0・最大は上記定数）。
    // インデックスは識別子としてルールから参照されるため安定させる（削除しても他の番号は詰めない・
    // 追加は最小の空き番号を再利用する）。「定義済み」は _xDefined で明示管理する
    // （ラベル空・初期値 0 でも定義済みなら保持する）。JSON の配列に存在する = 定義済み。
    private readonly bool[] _worldDefined = new bool[MaxWorldStates];
    private readonly string[] _worldLabels = new string[MaxWorldStates];
    private readonly int[] _worldInitials = new int[MaxWorldStates];
    private readonly bool[] _playerDefined = new bool[MaxPlayerStates];
    private readonly string[] _playerLabels = new string[MaxPlayerStates];
    private readonly int[] _playerInitials = new int[MaxPlayerStates];
    private readonly bool[] _timerDefined = new bool[MaxTimers];
    private readonly string[] _timerLabels = new string[MaxTimers];

    private readonly List<GimmickRule> _rules = new();
    private readonly List<GroupJson> _groups = new();

    public GimmickTabLogic()
    {
        ResetStates();
    }

    // ── ステート定義 ───────────────────────────────────────────────────────────

    /// <summary>ラベルを 20 文字以内に整形する（前後空白除去・超過は切り詰め）。</summary>
    public static string SanitizeLabel(string label)
    {
        if (string.IsNullOrEmpty(label))
            return "";
        var trimmed = label.Trim();
        return trimmed.Length > LabelMaxLength ? trimmed.Substring(0, LabelMaxLength) : trimmed;
    }

    /// <summary>初期値を 0〜255 にクランプする。</summary>
    public static int ClampStateValue(int value) => value < 0 ? 0 : value > StateValueMax ? StateValueMax : value;

    // 定義済みステートの番号一覧（昇順）。UI はこれを行に展開する。
    public IReadOnlyList<int> WorldStateIndices => DefinedIndices(_worldDefined);
    public IReadOnlyList<int> PlayerStateIndices => DefinedIndices(_playerDefined);
    public IReadOnlyList<int> TimerIndices => DefinedIndices(_timerDefined);

    public int WorldStateCount => CountDefined(_worldDefined);
    public int PlayerStateCount => CountDefined(_playerDefined);
    public int TimerCount => CountDefined(_timerDefined);

    public bool CanAddWorldState => WorldStateCount < MaxWorldStates;
    public bool CanAddPlayerState => PlayerStateCount < MaxPlayerStates;
    public bool CanAddTimer => TimerCount < MaxTimers;

    public bool IsWorldStateDefined(int index) => InRange(index, MaxWorldStates) && _worldDefined[index];
    public bool IsPlayerStateDefined(int index) => InRange(index, MaxPlayerStates) && _playerDefined[index];
    public bool IsTimerDefined(int index) => InRange(index, MaxTimers) && _timerDefined[index];

    /// <summary>ワールドステートを 1 つ追加し、割り当てた番号を返す（最小の空き番号を再利用）。満杯時は -1。</summary>
    public int AddWorldState(string label = "", int initial = 0) =>
        AddState(_worldDefined, _worldLabels, _worldInitials, label, initial);

    public int AddPlayerState(string label = "", int initial = 0) =>
        AddState(_playerDefined, _playerLabels, _playerInitials, label, initial);

    public int AddTimer(string label = "") => AddState(_timerDefined, _timerLabels, null, label, 0);

    /// <summary>指定番号のステートを削除する。未定義なら false。</summary>
    public bool RemoveWorldState(int index) => RemoveState(_worldDefined, _worldLabels, _worldInitials, index);

    public bool RemovePlayerState(int index) => RemoveState(_playerDefined, _playerLabels, _playerInitials, index);

    public bool RemoveTimer(int index) => RemoveState(_timerDefined, _timerLabels, null, index);

    public string GetWorldStateLabel(int index) => _worldLabels[index] ?? "";
    public int GetWorldStateInitial(int index) => _worldInitials[index];
    public string GetPlayerStateLabel(int index) => _playerLabels[index] ?? "";
    public int GetPlayerStateInitial(int index) => _playerInitials[index];
    public string GetTimerLabel(int index) => _timerLabels[index] ?? "";

    // セッターは定義済みの番号にのみ作用する（未定義スロットを誤って有効化しない）。
    public void SetWorldStateLabel(int index, string label)
    {
        if (IsWorldStateDefined(index)) _worldLabels[index] = SanitizeLabel(label);
    }

    public void SetWorldStateInitial(int index, int value)
    {
        if (IsWorldStateDefined(index)) _worldInitials[index] = ClampStateValue(value);
    }

    public void SetPlayerStateLabel(int index, string label)
    {
        if (IsPlayerStateDefined(index)) _playerLabels[index] = SanitizeLabel(label);
    }

    public void SetPlayerStateInitial(int index, int value)
    {
        if (IsPlayerStateDefined(index)) _playerInitials[index] = ClampStateValue(value);
    }

    public void SetTimerLabel(int index, string label)
    {
        if (IsTimerDefined(index)) _timerLabels[index] = SanitizeLabel(label);
    }

    private static bool InRange(int index, int max) => (uint)index < (uint)max;

    private static int CountDefined(bool[] defined)
    {
        int count = 0;
        foreach (var b in defined)
            if (b) count++;
        return count;
    }

    private static List<int> DefinedIndices(bool[] defined)
    {
        var list = new List<int>();
        for (int i = 0; i < defined.Length; i++)
            if (defined[i]) list.Add(i);
        return list;
    }

    // 最小の空き番号に追加して番号を返す（initials が null = タイマー）。満杯なら -1。
    private static int AddState(bool[] defined, string[] labels, int[] initials, string label, int initial)
    {
        for (int i = 0; i < defined.Length; i++)
            if (!defined[i])
            {
                defined[i] = true;
                labels[i] = SanitizeLabel(label);
                if (initials != null) initials[i] = ClampStateValue(initial);
                return i;
            }
        return -1;
    }

    private static bool RemoveState(bool[] defined, string[] labels, int[] initials, int index)
    {
        if (!InRange(index, defined.Length) || !defined[index])
            return false;
        defined[index] = false;
        labels[index] = "";
        if (initials != null) initials[index] = 0;
        return true;
    }

    private void ResetStates()
    {
        for (int i = 0; i < MaxWorldStates; i++) { _worldDefined[i] = false; _worldLabels[i] = ""; _worldInitials[i] = 0; }
        for (int i = 0; i < MaxPlayerStates; i++) { _playerDefined[i] = false; _playerLabels[i] = ""; _playerInitials[i] = 0; }
        for (int i = 0; i < MaxTimers; i++) { _timerDefined[i] = false; _timerLabels[i] = ""; }
    }

    // ── ルール一覧 ─────────────────────────────────────────────────────────────

    public IReadOnlyList<GimmickRule> Rules => _rules;
    public IReadOnlyList<GroupJson> Groups => _groups;

    /// <summary>ルール + グループの合計数（最大 100 判定用）。</summary>
    public int TotalCount => _rules.Count + _groups.Count;

    public bool CanAddRule => TotalCount < MaxRulesAndGroups;

    /// <summary>
    /// 新規ルールを末尾に追加して返す。空き（100 未満）が無ければ null。
    /// label 省略時は「ルールN」を自動採番する。
    /// </summary>
    public GimmickRule AddRule(string label = null)
    {
        if (!CanAddRule)
            return null;

        var rule = new GimmickRule
        {
            ruleId = NewRuleId(),
            label = string.IsNullOrWhiteSpace(label) ? NextDefaultName() : SanitizeLabel(label),
            groupId = "",
        };
        _rules.Add(rule);
        return rule;
    }

    /// <summary>ルール名を変更する（1〜20 文字・空不可）。</summary>
    public bool RenameRule(string ruleId, string label)
    {
        var sanitized = SanitizeLabel(label);
        if (string.IsNullOrEmpty(sanitized))
            return false;
        var rule = FindRule(ruleId);
        if (rule == null)
            return false;
        rule.label = sanitized;
        return true;
    }

    public bool DeleteRule(string ruleId)
    {
        int idx = IndexOfRule(ruleId);
        if (idx < 0)
            return false;
        _rules.RemoveAt(idx);
        return true;
    }

    /// <summary>ルールを newIndex の位置へ移動する（実行順 = 並び順）。範囲外はクランプ。</summary>
    public bool MoveRule(string ruleId, int newIndex)
    {
        int idx = IndexOfRule(ruleId);
        if (idx < 0)
            return false;
        newIndex = newIndex < 0 ? 0 : newIndex >= _rules.Count ? _rules.Count - 1 : newIndex;
        if (newIndex == idx)
            return true;
        var rule = _rules[idx];
        _rules.RemoveAt(idx);
        _rules.Insert(newIndex, rule);
        return true;
    }

    private GimmickRule FindRule(string ruleId)
    {
        foreach (var r in _rules)
            if (r.ruleId == ruleId)
                return r;
        return null;
    }

    private int IndexOfRule(string ruleId)
    {
        for (int i = 0; i < _rules.Count; i++)
            if (_rules[i].ruleId == ruleId)
                return i;
        return -1;
    }

    private static string NewRuleId() => "rule_" + Guid.NewGuid().ToString("N").Substring(0, 8);

    // 既存の「ルールN」の最大連番 + 1。
    private string NextDefaultName()
    {
        int max = 0;
        foreach (var r in _rules)
        {
            var m = DefaultRuleNamePattern.Match(r.label ?? "");
            if (m.Success && int.TryParse(m.Groups[1].Value, out int n) && n > max)
                max = n;
        }
        return $"ルール{max + 1}";
    }

    // ── ワールド定義との往復 ───────────────────────────────────────────────────

    public void LoadFrom(WorldDefinitionJson def)
    {
        ResetStates();
        _rules.Clear();
        _groups.Clear();
        if (def == null)
            return;

        LoadStateArray(def.worldStates, _worldDefined, _worldLabels, _worldInitials);
        LoadStateArray(def.playerStates, _playerDefined, _playerLabels, _playerInitials);
        if (def.timers != null)
            foreach (var t in def.timers)
                if (t != null && (uint)t.index < MaxTimers)
                {
                    _timerDefined[t.index] = true;
                    _timerLabels[t.index] = SanitizeLabel(t.label);
                }

        if (def.gimmicks != null)
            _rules.AddRange(def.gimmicks);
        if (def.gimmickGroups != null)
            _groups.AddRange(def.gimmickGroups);
    }

    public void WriteTo(WorldDefinitionJson def)
    {
        if (def == null)
            return;
        def.worldStates = BuildStateArray(_worldDefined, _worldLabels, _worldInitials);
        def.playerStates = BuildStateArray(_playerDefined, _playerLabels, _playerInitials);
        def.timers = BuildTimerArray(_timerDefined, _timerLabels);
        def.gimmicks = _rules.ToArray();
        def.gimmickGroups = _groups.ToArray();
    }

    private static void LoadStateArray(WorldStateData[] src, bool[] defined, string[] labels, int[] initials)
    {
        if (src == null)
            return;
        foreach (var s in src)
            if (s != null && (uint)s.index < labels.Length)
            {
                defined[s.index] = true;
                labels[s.index] = SanitizeLabel(s.label);
                initials[s.index] = ClampStateValue(s.initialValue);
            }
    }

    // 定義済みのステートをすべて書き出す（ラベル空・初期値 0 でも定義済みなら保持する）。
    private static WorldStateData[] BuildStateArray(bool[] defined, string[] labels, int[] initials)
    {
        var list = new List<WorldStateData>();
        for (int i = 0; i < labels.Length; i++)
            if (defined[i])
                list.Add(new WorldStateData { index = i, label = labels[i] ?? "", initialValue = initials[i] });
        return list.ToArray();
    }

    private static TimerData[] BuildTimerArray(bool[] defined, string[] labels)
    {
        var list = new List<TimerData>();
        for (int i = 0; i < labels.Length; i++)
            if (defined[i])
                list.Add(new TimerData { index = i, label = labels[i] ?? "" });
        return list.ToArray();
    }
}
