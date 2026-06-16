using System;
using System.Collections.Generic;

/// <summary>
/// ギミックタブの「テンプレートから追加」（world-creation.md 9.12）の純粋 C# ロジック。
///
/// チーム分けなどの頻出パターンを、定型のルール一式として <see cref="GimmickTabLogic"/> へ挿入する。
/// ランタイムに新しい概念は追加しない — 挿入されたルールは手作りルールと完全に同じ扱いになる。
///
/// 仕組み:
/// - テンプレートが使用するワールド / プレイヤーステート・タイマーには**空き番号を自動割り当て**し、
///   名前ラベルも自動設定する（例: ワールドステート 0 = 「鬼番号」）。
/// - ステート / タイマーの空きが足りない、またはルール合計が 100 を超える場合は挿入を**拒否**する
///   （<see cref="TemplateInsertResult.Error"/> にフラッシュ用の理由を返す。挿入は一切行わない = 原子的）。
/// - テンプレートはアプリ内蔵（UGC ではない）。アプリ更新で追加できる。
///
/// 注: オブジェクト・効果音・頭上マーカーの**対象 ID はテンプレートでは空のまま**にする
/// （対象選択は 3D ビュータップ / 一覧から行うエディタ操作のため、テンプレートには分からない）。
/// その分のルールは対象を割り当てるまで公開時バリデーション（9.11）で不完全と判定される — 想定どおり。
/// </summary>
public static class GimmickTemplateLogic
{
    /// <summary>テンプレートのパラメータ仕様（UI のパラメータ入力ダイアログ用）。</summary>
    public sealed class TemplateParam
    {
        public string Key;     // 内部キー
        public string Label;   // 表示名（日本語）
        public int Default;
        public int Min;
        public int Max;
    }

    /// <summary>テンプレート定義（一覧表示 + 容量チェック用）。</summary>
    public sealed class Template
    {
        public string Id;            // 安定 ID（"twoTeams" 等）
        public string Name;          // 表示名
        public string Description;   // 説明
        public TemplateParam[] Params = Array.Empty<TemplateParam>();

        // 自動割り当てに必要な空きスロット数
        public int WorldStatesNeeded;
        public int PlayerStatesNeeded;
        public int TimersNeeded;

        // 挿入されるルール数（容量事前チェック用）
        public int RuleCount;

        internal Func<BuildContext, List<RuleSpec>> Build;
    }

    /// <summary>挿入結果。失敗時は <see cref="Error"/> に理由（フラッシュメッセージ用）、挿入は 0 件。</summary>
    public sealed class TemplateInsertResult
    {
        public bool Success;
        public string Error;                          // 成功時 null
        public IReadOnlyList<GimmickRule> Rules;      // 挿入されたルール（失敗時は空）
    }

    // ── テンプレート一覧 ────────────────────────────────────────────────────────

    private static readonly Template[] _all =
    {
        new()
        {
            Id = "twoTeams",
            Name = "チーム分け（2 チーム）",
            Description = "入室時にプレイヤー番号の偶奇でチームを設定し、チーム別マーカーを表示する。",
            PlayerStatesNeeded = 1,
            RuleCount = 2,
            Build = BuildTwoTeams,
        },
        new()
        {
            Id = "tagBasic",
            Name = "鬼ごっこ基本",
            Description = "開始時に乱数で鬼を選び、鬼マーカーを表示。接触で鬼が交代する。",
            WorldStatesNeeded = 1,
            RuleCount = 3,
            Build = BuildTagBasic,
        },
        new()
        {
            Id = "countdown",
            Name = "カウントダウン",
            Description = "タイマーを開始し、終了時にメッセージと効果音を再生する。",
            TimersNeeded = 1,
            RuleCount = 2,
            Params = new[]
            {
                new TemplateParam { Key = "seconds", Label = "秒数", Default = 60, Min = 1, Max = 3600 },
            },
            Build = BuildCountdown,
        },
        new()
        {
            Id = "periodic",
            Name = "周期処理",
            Description = "一定秒ごとにアクションを実行する（タイマー到達 → 実行 → リセット → 再開）。",
            TimersNeeded = 1,
            RuleCount = 2,
            Params = new[]
            {
                new TemplateParam { Key = "seconds", Label = "間隔（秒）", Default = 10, Min = 1, Max = 3600 },
            },
            Build = BuildPeriodic,
        },
        new()
        {
            Id = "comboLock",
            Name = "コンビネーションロック",
            Description = "タップ順をステートで記録し、正解で扉を非表示・不正解でリセットする（3 手順）。",
            WorldStatesNeeded = 1,
            RuleCount = 4,
            Build = BuildComboLock,
        },
        new()
        {
            Id = "raceTiming",
            Name = "レース計測",
            Description = "スタートエリア退出でタイマー開始、ゴール侵入で着順を記録して結果を表示する。",
            WorldStatesNeeded = 1,
            TimersNeeded = 1,
            RuleCount = 2,
            Build = BuildRaceTiming,
        },
    };

    /// <summary>内蔵テンプレート一覧（UI の選択リスト用）。</summary>
    public static IReadOnlyList<Template> All => _all;

    /// <summary>ID からテンプレートを取得する。未知の ID は null。</summary>
    public static Template Get(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;
        foreach (var t in _all)
            if (t.Id == id)
                return t;
        return null;
    }

    // ── 挿入 ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// テンプレートを <paramref name="tab"/> へ挿入する。容量不足時は何も変更せず失敗を返す。
    /// <paramref name="values"/> はパラメータの上書き値（省略時は既定値・範囲外はクランプ）。
    /// </summary>
    public static TemplateInsertResult Insert(
        GimmickTabLogic tab,
        string templateId,
        IReadOnlyDictionary<string, int> values = null
    )
    {
        if (tab == null)
            throw new ArgumentNullException(nameof(tab));

        var template = Get(templateId);
        if (template == null)
            return Fail("テンプレートが見つかりません");

        // 1. 空きスロットを（変更せずに）確保できるか確認する。
        var worldSlots = FindFreeWorldStates(tab, template.WorldStatesNeeded);
        var playerSlots = FindFreePlayerStates(tab, template.PlayerStatesNeeded);
        var timerSlots = FindFreeTimers(tab, template.TimersNeeded);

        if (worldSlots.Count < template.WorldStatesNeeded || playerSlots.Count < template.PlayerStatesNeeded)
            return Fail("ステートの空きが足りません");
        if (timerSlots.Count < template.TimersNeeded)
            return Fail("タイマーの空きが足りません");

        // 2. ルール数の上限（ルール + グループ合計 100）を確認する。
        if (tab.TotalCount + template.RuleCount > GimmickTabLogic.MaxRulesAndGroups)
            return Fail("ルール数が上限（100）を超えます");

        // 3. パラメータを解決（既定値マージ + クランプ）。
        var resolved = ResolveParams(template, values);

        // 4. ここから変更を確定する（検証は完了済みなので原子的に適用できる）。
        //    Build 内でステート / タイマーのラベルを自動設定し、ルール仕様を構築する。
        var ctx = new BuildContext
        {
            Tab = tab,
            WorldStates = worldSlots,
            PlayerStates = playerSlots,
            Timers = timerSlots,
            Params = resolved,
        };
        var specs = template.Build(ctx);

        var inserted = new List<GimmickRule>(specs.Count);
        foreach (var spec in specs)
        {
            var rule = tab.AddRule(spec.Label);
            if (rule == null) // 事前チェック済みのため通常は起こらないが防御的に中断
                break;
            rule.triggers = spec.Triggers ?? Array.Empty<GimmickTrigger>();
            rule.conditions = spec.Conditions ?? Array.Empty<GimmickCondition>();
            rule.actions = spec.Actions ?? Array.Empty<GimmickAction>();
            inserted.Add(rule);
        }

        return new TemplateInsertResult { Success = true, Error = null, Rules = inserted };
    }

    private static TemplateInsertResult Fail(string reason) =>
        new() { Success = false, Error = reason, Rules = Array.Empty<GimmickRule>() };

    private static Dictionary<string, int> ResolveParams(
        Template template,
        IReadOnlyDictionary<string, int> values
    )
    {
        var result = new Dictionary<string, int>();
        foreach (var p in template.Params)
        {
            int v = p.Default;
            if (values != null && values.TryGetValue(p.Key, out int provided))
                v = provided;
            if (v < p.Min)
                v = p.Min;
            else if (v > p.Max)
                v = p.Max;
            result[p.Key] = v;
        }
        return result;
    }

    // ── 空きスロット探索（GimmickTabLogic を変更せず読み取りのみ）─────────────────

    // ワールドステートはラベルが空かつ初期値 0 のスロットを未使用とみなす。
    private static List<int> FindFreeWorldStates(GimmickTabLogic tab, int n)
    {
        var free = new List<int>();
        for (int i = 0; i < GimmickTabLogic.MaxWorldStates && free.Count < n; i++)
            if (string.IsNullOrEmpty(tab.GetWorldStateLabel(i)) && tab.GetWorldStateInitial(i) == 0)
                free.Add(i);
        return free;
    }

    private static List<int> FindFreePlayerStates(GimmickTabLogic tab, int n)
    {
        var free = new List<int>();
        for (int i = 0; i < GimmickTabLogic.MaxPlayerStates && free.Count < n; i++)
            if (string.IsNullOrEmpty(tab.GetPlayerStateLabel(i)) && tab.GetPlayerStateInitial(i) == 0)
                free.Add(i);
        return free;
    }

    // タイマーはラベルが空のスロットを未使用とみなす。
    private static List<int> FindFreeTimers(GimmickTabLogic tab, int n)
    {
        var free = new List<int>();
        for (int i = 0; i < GimmickTabLogic.MaxTimers && free.Count < n; i++)
            if (string.IsNullOrEmpty(tab.GetTimerLabel(i)))
                free.Add(i);
        return free;
    }

    // ── テンプレート別のルール構築 ──────────────────────────────────────────────

    internal sealed class BuildContext
    {
        public GimmickTabLogic Tab;
        public List<int> WorldStates;
        public List<int> PlayerStates;
        public List<int> Timers;
        public Dictionary<string, int> Params;

        public int Param(string key) => Params.TryGetValue(key, out int v) ? v : 0;

        // 割り当て済みスロットにラベル（と初期値）を設定し、実スロット番号を返す。
        public int NameWorld(int slot, string label, int initial = 0)
        {
            int idx = WorldStates[slot];
            Tab.SetWorldStateLabel(idx, label);
            if (initial != 0)
                Tab.SetWorldStateInitial(idx, initial);
            return idx;
        }

        public int NamePlayer(int slot, string label, int initial = 0)
        {
            int idx = PlayerStates[slot];
            Tab.SetPlayerStateLabel(idx, label);
            if (initial != 0)
                Tab.SetPlayerStateInitial(idx, initial);
            return idx;
        }

        public int NameTimer(int slot, string label)
        {
            int idx = Timers[slot];
            Tab.SetTimerLabel(idx, label);
            return idx;
        }
    }

    internal sealed class RuleSpec
    {
        public string Label;
        public GimmickTrigger[] Triggers;
        public GimmickCondition[] Conditions;
        public GimmickAction[] Actions;
    }

    // 1. チーム分け（2 チーム）
    private static List<RuleSpec> BuildTwoTeams(BuildContext ctx)
    {
        int team = ctx.NamePlayer(0, "チーム");
        return new List<RuleSpec>
        {
            new()
            {
                Label = "チームA振り分け",
                Triggers = new[] { Trigger("playerCountChanged") },
                Conditions = new[] { ConditionMod("playerNumber", modBy: 2, modResult: 0) },
                Actions = new[]
                {
                    SetPlayerState(team, "set", ValFixed(1)),
                    Marker(visible: true),
                },
            },
            new()
            {
                Label = "チームB振り分け",
                Triggers = new[] { Trigger("playerCountChanged") },
                Conditions = new[] { ConditionMod("playerNumber", modBy: 2, modResult: 1) },
                Actions = new[]
                {
                    SetPlayerState(team, "set", ValFixed(2)),
                    Marker(visible: true),
                },
            },
        };
    }

    // 2. 鬼ごっこ基本
    private static List<RuleSpec> BuildTagBasic(BuildContext ctx)
    {
        int oni = ctx.NameWorld(0, "鬼番号"); // 鬼番号
        return new List<RuleSpec>
        {
            new()
            {
                Label = "鬼を選出",
                Triggers = new[] { Trigger("roomStart") },
                Actions = new[]
                {
                    SetWorldState(oni, "set", ValRandom(1, max: 0, maxIsPlayerCount: true)),
                },
            },
            new()
            {
                Label = "鬼マーカー表示",
                Triggers = new[] { Trigger("playerCountChanged") },
                Conditions = new[] { ConditionCompare("playerNumber", "eq", ValWorldState(oni)) },
                Actions = new[] { Marker(visible: true) },
            },
            new()
            {
                Label = "鬼交代",
                Triggers = new[] { Trigger("playerTouchPlayer") },
                Actions = new[]
                {
                    Message("鬼が交代した！"),
                },
            },
        };
    }

    // 3. カウントダウン
    private static List<RuleSpec> BuildCountdown(BuildContext ctx)
    {
        int timer = ctx.NameTimer(0, "カウントダウン");
        int seconds = ctx.Param("seconds");
        return new List<RuleSpec>
        {
            new()
            {
                Label = "カウント開始",
                Triggers = new[] { Trigger("roomStart") },
                Actions = new[] { TimerAction("timerStart", timer) },
            },
            new()
            {
                Label = "終了処理",
                Triggers = new[] { TriggerTimer(timer, seconds) },
                Actions = new[]
                {
                    Message("終了！"),
                    PlaySound(),
                },
            },
        };
    }

    // 4. 周期処理
    private static List<RuleSpec> BuildPeriodic(BuildContext ctx)
    {
        int timer = ctx.NameTimer(0, "周期");
        int seconds = ctx.Param("seconds");
        return new List<RuleSpec>
        {
            new()
            {
                Label = "周期開始",
                Triggers = new[] { Trigger("roomStart") },
                Actions = new[] { TimerAction("timerStart", timer) },
            },
            new()
            {
                Label = "周期処理",
                Triggers = new[] { TriggerTimer(timer, seconds) },
                Actions = new[]
                {
                    Message("周期処理が発火しました"),
                    TimerAction("timerReset", timer),
                    TimerAction("timerStart", timer),
                },
            },
        };
    }

    // 5. コンビネーションロック（3 手順）
    private static List<RuleSpec> BuildComboLock(BuildContext ctx)
    {
        int progress = ctx.NameWorld(0, "入力進捗"); // 入力進捗
        return new List<RuleSpec>
        {
            new()
            {
                Label = "正解 1 番目",
                Triggers = new[] { Trigger("objectTap") },
                Conditions = new[] { ConditionCompare("worldState", "eq", ValFixed(0), progress) },
                Actions = new[] { SetWorldState(progress, "set", ValFixed(1)) },
            },
            new()
            {
                Label = "正解 2 番目",
                Triggers = new[] { Trigger("objectTap") },
                Conditions = new[] { ConditionCompare("worldState", "eq", ValFixed(1), progress) },
                Actions = new[] { SetWorldState(progress, "set", ValFixed(2)) },
            },
            new()
            {
                Label = "解錠",
                Triggers = new[] { Trigger("objectTap") },
                Conditions = new[] { ConditionCompare("worldState", "eq", ValFixed(2), progress) },
                Actions = new[]
                {
                    ShowHide(visible: false),
                    Message("開いた！"),
                },
            },
            new()
            {
                Label = "間違えたらリセット",
                Triggers = new[] { Trigger("objectTap") },
                Actions = new[] { SetWorldState(progress, "set", ValFixed(0)) },
            },
        };
    }

    // 6. レース計測
    private static List<RuleSpec> BuildRaceTiming(BuildContext ctx)
    {
        int rank = ctx.NameWorld(0, "着順"); // 着順
        int timer = ctx.NameTimer(0, "タイム"); // タイム
        return new List<RuleSpec>
        {
            new()
            {
                Label = "計測開始",
                Triggers = new[] { Trigger("areaExit") },
                Actions = new[] { TimerAction("timerStart", timer) },
            },
            new()
            {
                Label = "ゴール記録",
                Triggers = new[] { Trigger("areaEnter") },
                Actions = new[]
                {
                    SetWorldState(rank, "add", ValFixed(1)),
                    Message("ゴール！"),
                },
            },
        };
    }

    // ── 構築ヘルパー ────────────────────────────────────────────────────────────

    private static GimmickTrigger Trigger(string type) => new() { type = type };

    private static GimmickTrigger TriggerTimer(int timerIndex, float seconds) =>
        new() { type = "timerReached", timerIndex = timerIndex, timerSeconds = seconds };

    private static GimmickCondition ConditionCompare(string type, string op, GimmickValueJson threshold, int stateIndex = 0) =>
        new() { type = type, op = op, threshold = threshold, stateIndex = stateIndex };

    private static GimmickCondition ConditionMod(string type, int modBy, int modResult) =>
        new() { type = type, op = "mod_eq", modBy = modBy, modResult = modResult };

    private static GimmickAction SetWorldState(int stateIndex, string op, GimmickValueJson value) =>
        new() { type = "setWorldState", stateIndex = stateIndex, stateOp = op, value = value };

    private static GimmickAction SetPlayerState(int stateIndex, string op, GimmickValueJson value) =>
        new() { type = "setPlayerState", stateIndex = stateIndex, stateOp = op, value = value, playerTarget = "input" };

    private static GimmickAction TimerAction(string type, int timerIndex) =>
        new() { type = type, timerIndex = timerIndex };

    private static GimmickAction Marker(bool visible) =>
        new() { type = "setPlayerMarker", visible = visible, playerTarget = "input" };

    private static GimmickAction ShowHide(bool visible) =>
        new() { type = "showHideObject", visible = visible };

    private static GimmickAction PlaySound() =>
        new() { type = "playSound", floatParam = 100f };

    private static GimmickAction Message(string text) =>
        new()
        {
            type = "showMessage",
            texts = new[] { new GimmickTextJson { lang = "", text = text } },
        };

    private static GimmickValueJson ValFixed(int v) => new() { kind = "fixed", value = v };

    private static GimmickValueJson ValWorldState(int index) =>
        new() { kind = "worldState", stateIndex = index };

    private static GimmickValueJson ValRandom(int min, int max, bool maxIsPlayerCount) =>
        new() { kind = "random", min = min, max = max, maxIsPlayerCount = maxIsPlayerCount };
}
