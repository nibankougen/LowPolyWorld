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
    public const int MaxNestDepth = 4;

    private static readonly Regex DefaultRuleNamePattern = new(@"^ルール(\d+)$", RegexOptions.Compiled);
    private static readonly Regex DefaultGroupNamePattern = new(@"^グループ(\d+)$", RegexOptions.Compiled);

    // ステート定義は「追加 / 削除」式（最小 0・最大は上記定数）。
    // インデックスは識別子としてルール・会話から参照されるため安定させる（削除しても他の番号は
    // 詰めない・追加は最小の空き番号を再利用する）。「定義済み」は _xDefined で明示管理する
    // （ラベル空・初期値 0 でも定義済みなら保持する）。JSON の配列に存在する = 定義済み。
    //
    // 表示順は番号（index）とは独立に _xOrder（定義済み index を表示順に並べたリスト）で管理する。
    // これにより並べ替えてもインデックスは変わらず、ルール・会話の参照（stateIndex）が壊れない。
    // JSON 配列の並びが表示順を表す（各要素は自身の index を持つ）。
    private readonly bool[] _worldDefined = new bool[MaxWorldStates];
    private readonly string[] _worldLabels = new string[MaxWorldStates];
    private readonly int[] _worldInitials = new int[MaxWorldStates];
    private readonly List<int> _worldOrder = new();
    private readonly bool[] _playerDefined = new bool[MaxPlayerStates];
    private readonly string[] _playerLabels = new string[MaxPlayerStates];
    private readonly int[] _playerInitials = new int[MaxPlayerStates];
    private readonly List<int> _playerOrder = new();
    private readonly bool[] _timerDefined = new bool[MaxTimers];
    private readonly string[] _timerLabels = new string[MaxTimers];
    private readonly List<int> _timerOrder = new();

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

    // 定義済みステートの番号一覧（表示順）。UI はこれを行に展開する。
    public IReadOnlyList<int> WorldStateIndices => new List<int>(_worldOrder);
    public IReadOnlyList<int> PlayerStateIndices => new List<int>(_playerOrder);
    public IReadOnlyList<int> TimerIndices => new List<int>(_timerOrder);

    public int WorldStateCount => _worldOrder.Count;
    public int PlayerStateCount => _playerOrder.Count;
    public int TimerCount => _timerOrder.Count;

    public bool CanAddWorldState => WorldStateCount < MaxWorldStates;
    public bool CanAddPlayerState => PlayerStateCount < MaxPlayerStates;
    public bool CanAddTimer => TimerCount < MaxTimers;

    public bool IsWorldStateDefined(int index) => InRange(index, MaxWorldStates) && _worldDefined[index];
    public bool IsPlayerStateDefined(int index) => InRange(index, MaxPlayerStates) && _playerDefined[index];
    public bool IsTimerDefined(int index) => InRange(index, MaxTimers) && _timerDefined[index];

    /// <summary>ワールドステートを 1 つ追加し、割り当てた番号を返す（最小の空き番号を再利用・表示は末尾）。満杯時は -1。</summary>
    public int AddWorldState(string label = "", int initial = 0) =>
        AddState(_worldDefined, _worldLabels, _worldInitials, _worldOrder, label, initial);

    public int AddPlayerState(string label = "", int initial = 0) =>
        AddState(_playerDefined, _playerLabels, _playerInitials, _playerOrder, label, initial);

    public int AddTimer(string label = "") => AddState(_timerDefined, _timerLabels, null, _timerOrder, label, 0);

    /// <summary>指定番号のステートを削除する。未定義なら false。番号は詰めない（参照は安定）。</summary>
    public bool RemoveWorldState(int index) => RemoveState(_worldDefined, _worldLabels, _worldInitials, _worldOrder, index);

    public bool RemovePlayerState(int index) => RemoveState(_playerDefined, _playerLabels, _playerInitials, _playerOrder, index);

    public bool RemoveTimer(int index) => RemoveState(_timerDefined, _timerLabels, null, _timerOrder, index);

    /// <summary>
    /// ステートを表示順で newPos の位置へ移動する（番号 = 参照 ID は変えないので、ルール・会話の
    /// 参照は壊れない）。範囲外はクランプ。未定義番号は false。
    /// </summary>
    public bool MoveWorldState(int index, int newPos) => MoveState(_worldOrder, index, newPos);

    public bool MovePlayerState(int index, int newPos) => MoveState(_playerOrder, index, newPos);

    public bool MoveTimer(int index, int newPos) => MoveState(_timerOrder, index, newPos);

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

    // 最小の空き番号に追加して番号を返す（表示順リスト末尾に追加・initials が null = タイマー）。満杯なら -1。
    private static int AddState(bool[] defined, string[] labels, int[] initials, List<int> order, string label, int initial)
    {
        for (int i = 0; i < defined.Length; i++)
            if (!defined[i])
            {
                defined[i] = true;
                labels[i] = SanitizeLabel(label);
                if (initials != null) initials[i] = ClampStateValue(initial);
                order.Add(i);
                return i;
            }
        return -1;
    }

    private static bool RemoveState(bool[] defined, string[] labels, int[] initials, List<int> order, int index)
    {
        if (!InRange(index, defined.Length) || !defined[index])
            return false;
        defined[index] = false;
        labels[index] = "";
        if (initials != null) initials[index] = 0;
        order.Remove(index);
        return true;
    }

    // 表示順リスト内で index を newPos へ移動する（番号は不変）。
    private static bool MoveState(List<int> order, int index, int newPos)
    {
        int cur = order.IndexOf(index);
        if (cur < 0)
            return false;
        newPos = newPos < 0 ? 0 : newPos >= order.Count ? order.Count - 1 : newPos;
        if (newPos == cur)
            return true;
        order.RemoveAt(cur);
        order.Insert(newPos, index);
        return true;
    }

    private void ResetStates()
    {
        for (int i = 0; i < MaxWorldStates; i++) { _worldDefined[i] = false; _worldLabels[i] = ""; _worldInitials[i] = 0; }
        for (int i = 0; i < MaxPlayerStates; i++) { _playerDefined[i] = false; _playerLabels[i] = ""; _playerInitials[i] = 0; }
        for (int i = 0; i < MaxTimers; i++) { _timerDefined[i] = false; _timerLabels[i] = ""; }
        _worldOrder.Clear();
        _playerOrder.Clear();
        _timerOrder.Clear();
    }

    // ── ルール一覧 ─────────────────────────────────────────────────────────────

    public IReadOnlyList<GimmickRule> Rules => _rules;
    public IReadOnlyList<GroupJson> Groups => _groups;

    public int RuleCount => _rules.Count;
    public int GroupCount => _groups.Count;

    /// <summary>ルール + グループの合計数（最大 100 判定用）。</summary>
    public int TotalCount => _rules.Count + _groups.Count;

    public bool CanAddRule => TotalCount < MaxRulesAndGroups;

    /// <summary>
    /// 新規ルールを末尾に追加して返す。空き（100 未満）が無ければ null。
    /// label 省略時は「ルールN」を自動採番する。groupId 指定時はそのグループに所属させる
    /// （存在しないグループ ID はルート扱い）。
    /// </summary>
    public GimmickRule AddRule(string label = null, string groupId = "")
    {
        if (!CanAddRule)
            return null;

        var rule = new GimmickRule
        {
            ruleId = NewRuleId(),
            label = string.IsNullOrWhiteSpace(label) ? NextDefaultName() : SanitizeLabel(label),
            groupId = GroupExists(groupId) ? groupId : "",
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

    // ── グループ操作 ──────────────────────────────────────────────────────────
    // ルールのグループ化（screens-and-modes.md 11.7.4・オブジェクトの ObjectPlacementStore 相当）。
    // グループはルール・グループ合計 100（MaxRulesAndGroups）に含まれ、最大 4 段ネスト。
    // 実行順はルール配列順が正であり、グループは編集ツリー復元用メタデータにすぎない。

    /// <summary>グループを作成できるか（ルール + グループ合計が 100 未満）。</summary>
    public bool CanCreateGroup => TotalCount < MaxRulesAndGroups;

    /// <summary>
    /// グループを作成し groupId を返す。合計 100 未満かつ親の深さが 4 段未満
    /// （新グループ深さ ≤ 4）のときのみ成功（失敗時 null）。
    /// name が空のときは「グループN」を自動採番する。
    /// </summary>
    public string CreateGroup(string parentGroupId = "", string name = null)
    {
        if (!CanCreateGroup)
            return null;
        if (!string.IsNullOrEmpty(parentGroupId) && !GroupExists(parentGroupId))
            return null;
        if (GroupDepth(parentGroupId) + 1 > MaxNestDepth)
            return null;

        var group = new GroupJson
        {
            groupId = NewGroupId(),
            name = NormalizeGroupName(name),
            parentGroupId = parentGroupId ?? "",
            sortOrder = CountChildGroups(parentGroupId),
        };
        _groups.Add(group);
        return group.groupId;
    }

    /// <summary>グループ名を変更する（1〜20 文字・空不可）。</summary>
    public bool RenameGroup(string groupId, string name)
    {
        var group = FindGroup(groupId);
        if (group == null)
            return false;
        var sanitized = SanitizeLabel(name);
        if (string.IsNullOrEmpty(sanitized))
            return false;
        group.name = sanitized;
        return true;
    }

    /// <summary>
    /// グループを削除する。直下の子ルール・子グループは、削除するグループの親へ繰り上げる
    /// （ツリーを 1 段詰める）。
    /// </summary>
    public bool DeleteGroup(string groupId)
    {
        var group = FindGroup(groupId);
        if (group == null)
            return false;

        string newParent = group.parentGroupId;
        foreach (var rule in _rules)
            if (rule.groupId == groupId)
                rule.groupId = newParent;
        foreach (var g in _groups)
            if (g.parentGroupId == groupId)
                g.parentGroupId = newParent;

        _groups.Remove(group);
        return true;
    }

    /// <summary>ルールの所属グループを設定する（"" = ルート直下）。グループが存在しなければ false。</summary>
    public bool SetRuleGroup(string ruleId, string groupId)
    {
        var rule = FindRule(ruleId);
        if (rule == null)
            return false;
        if (!string.IsNullOrEmpty(groupId) && !GroupExists(groupId))
            return false;
        rule.groupId = groupId ?? "";
        return true;
    }

    /// <summary>
    /// ルールを anchorRuleId の直前へ移動し、同じコンテナ（グループ / ルート）に所属させる。
    /// D&D の「行間に落とす（並べ替え）」用。実行順 = 配列順。
    /// </summary>
    public bool MoveRuleBefore(string ruleId, string anchorRuleId)
    {
        if (ruleId == anchorRuleId)
            return true;
        int from = IndexOfRule(ruleId);
        var anchor = FindRule(anchorRuleId);
        if (from < 0 || anchor == null)
            return false;
        var rule = _rules[from];
        rule.groupId = anchor.groupId ?? "";
        _rules.RemoveAt(from);
        int to = IndexOfRule(anchorRuleId); // 取り除いた後に取り直す（方向に依存しない）
        _rules.Insert(to, rule);
        return true;
    }

    /// <summary>
    /// ルールを container（"" = ルート）内の末尾へ移動する。
    /// D&D の「グループ本体に落とす（中へ入れる）」用。存在しないグループは false。
    /// </summary>
    public bool MoveRuleToContainerEnd(string ruleId, string containerId)
    {
        int from = IndexOfRule(ruleId);
        if (from < 0)
            return false;
        containerId ??= "";
        if (containerId.Length > 0 && !GroupExists(containerId))
            return false;
        var rule = _rules[from];
        rule.groupId = containerId;
        _rules.RemoveAt(from);
        // container 内の最後の兄弟の直後（兄弟が無ければ配列末尾）。
        int insertAt = _rules.Count;
        for (int i = _rules.Count - 1; i >= 0; i--)
            if ((_rules[i].groupId ?? "") == containerId)
            {
                insertAt = i + 1;
                break;
            }
        _rules.Insert(insertAt, rule);
        return true;
    }

    /// <summary>
    /// グループの親を変更する。自己・自身の子孫への移動は不可（循環防止）。
    /// 移動後のサブツリーの最深部が 4 段を超える場合も不可。
    /// </summary>
    public bool SetGroupParent(string groupId, string newParentId)
    {
        var group = FindGroup(groupId);
        if (group == null)
            return false;
        if (!string.IsNullOrEmpty(newParentId) && !GroupExists(newParentId))
            return false;
        if (groupId == newParentId || IsDescendantOf(newParentId, groupId))
            return false;
        if (GroupDepth(newParentId) + SubtreeHeight(groupId) > MaxNestDepth)
            return false;

        group.parentGroupId = newParentId ?? "";
        return true;
    }

    /// <summary>
    /// グループを anchorGroupId の直前（同じ親・同階層）へ並べ替える。
    /// 親が違う場合は anchor の親へ付け替える（循環 / 深さは <see cref="SetGroupParent"/> で検証）。
    /// D&D の「グループ行間に落とす」用。表示順 = _groups リスト順。
    /// </summary>
    public bool MoveGroupBefore(string groupId, string anchorGroupId)
    {
        if (groupId == anchorGroupId)
            return false;
        var group = FindGroup(groupId);
        var anchor = FindGroup(anchorGroupId);
        if (group == null || anchor == null)
            return false;
        if (!SetGroupParent(groupId, anchor.parentGroupId)) // 同親なら何もしないが検証は通す
            return false;

        _groups.Remove(group);
        int to = _groups.IndexOf(anchor);
        _groups.Insert(to, group);
        ResequenceSiblingSortOrders(group.parentGroupId);
        return true;
    }

    /// <summary>
    /// グループを parentId（"" = ルート）の末尾へ移動する。
    /// D&D の「別グループ本体 / ルート余白に落とす」用。
    /// </summary>
    public bool MoveGroupToParentEnd(string groupId, string parentId)
    {
        var group = FindGroup(groupId);
        if (group == null)
            return false;
        if (!SetGroupParent(groupId, parentId))
            return false;

        _groups.Remove(group);
        _groups.Add(group); // 表示は親フィルタ後の _groups 順 → 末尾追加で同親内の最後になる
        ResequenceSiblingSortOrders(group.parentGroupId);
        return true;
    }

    // 指定親の子グループの sortOrder を _groups リスト順に振り直す（JSON メタデータの整合用）。
    private void ResequenceSiblingSortOrders(string parentId)
    {
        int order = 0;
        foreach (var g in _groups)
            if (g.parentGroupId == (parentId ?? ""))
                g.sortOrder = order++;
    }

    /// <summary>グループの深さ（ルート直下 = 1）。"" / 不明は 0。</summary>
    public int GroupDepth(string groupId)
    {
        int depth = 0;
        string current = groupId;
        while (!string.IsNullOrEmpty(current))
        {
            var g = FindGroup(current);
            if (g == null)
                break;
            depth++;
            current = g.parentGroupId;
            if (depth > MaxRulesAndGroups)
                break; // 万一の循環ガード
        }
        return depth;
    }

    /// <summary>サブツリーの高さ（自身のみ = 1）。</summary>
    public int SubtreeHeight(string groupId)
    {
        int max = 0;
        foreach (var g in _groups)
            if (g.parentGroupId == groupId)
                max = Math.Max(max, SubtreeHeight(g.groupId));
        return max + 1;
    }

    private GroupJson FindGroup(string groupId) =>
        string.IsNullOrEmpty(groupId) ? null : _groups.Find(g => g.groupId == groupId);

    private bool GroupExists(string groupId) => FindGroup(groupId) != null;

    private int CountChildGroups(string parentGroupId)
    {
        int count = 0;
        foreach (var g in _groups)
            if (g.parentGroupId == (parentGroupId ?? ""))
                count++;
        return count;
    }

    /// <summary>candidate が ancestor の子孫（自身を含む）か。</summary>
    private bool IsDescendantOf(string candidate, string ancestor)
    {
        string current = candidate;
        while (!string.IsNullOrEmpty(current))
        {
            if (current == ancestor)
                return true;
            var g = FindGroup(current);
            if (g == null)
                return false;
            current = g.parentGroupId;
        }
        return false;
    }

    private static string NewGroupId() => "grp_" + Guid.NewGuid().ToString("N").Substring(0, 8);

    /// <summary>name が空なら「グループN」を採番（既存の最大連番 + 1）。20 文字超は切り詰める。</summary>
    private string NormalizeGroupName(string name)
    {
        if (!string.IsNullOrEmpty(name))
            return SanitizeLabel(name);

        int max = 0;
        foreach (var g in _groups)
        {
            var m = DefaultGroupNamePattern.Match(g.name ?? "");
            if (m.Success && int.TryParse(m.Groups[1].Value, out int n) && n > max)
                max = n;
        }
        return $"グループ{max + 1}";
    }

    // ── ワールド定義との往復 ───────────────────────────────────────────────────

    public void LoadFrom(WorldDefinitionJson def)
    {
        ResetStates();
        _rules.Clear();
        _groups.Clear();
        if (def == null)
            return;

        LoadStateArray(def.worldStates, _worldDefined, _worldLabels, _worldInitials, _worldOrder);
        LoadStateArray(def.playerStates, _playerDefined, _playerLabels, _playerInitials, _playerOrder);
        if (def.timers != null)
            foreach (var t in def.timers)
                if (t != null && (uint)t.index < MaxTimers && !_timerDefined[t.index])
                {
                    _timerDefined[t.index] = true;
                    _timerLabels[t.index] = SanitizeLabel(t.label);
                    _timerOrder.Add(t.index); // 配列の並び = 表示順
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
        def.worldStates = BuildStateArray(_worldOrder, _worldLabels, _worldInitials);
        def.playerStates = BuildStateArray(_playerOrder, _playerLabels, _playerInitials);
        def.timers = BuildTimerArray(_timerOrder, _timerLabels);
        def.gimmicks = _rules.ToArray();
        def.gimmickGroups = _groups.ToArray();
    }

    private static void LoadStateArray(WorldStateData[] src, bool[] defined, string[] labels, int[] initials, List<int> order)
    {
        if (src == null)
            return;
        foreach (var s in src)
            if (s != null && (uint)s.index < labels.Length && !defined[s.index])
            {
                defined[s.index] = true;
                labels[s.index] = SanitizeLabel(s.label);
                initials[s.index] = ClampStateValue(s.initialValue);
                order.Add(s.index); // 配列の並び = 表示順
            }
    }

    // 定義済みのステートを表示順で書き出す（ラベル空・初期値 0 でも定義済みなら保持する）。
    private static WorldStateData[] BuildStateArray(List<int> order, string[] labels, int[] initials)
    {
        var list = new List<WorldStateData>();
        foreach (int i in order)
            list.Add(new WorldStateData { index = i, label = labels[i] ?? "", initialValue = initials[i] });
        return list.ToArray();
    }

    private static TimerData[] BuildTimerArray(List<int> order, string[] labels)
    {
        var list = new List<TimerData>();
        foreach (int i in order)
            list.Add(new TimerData { index = i, label = labels[i] ?? "" });
        return list.ToArray();
    }
}
