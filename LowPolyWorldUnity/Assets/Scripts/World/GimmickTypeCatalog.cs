using System.Collections.Generic;

/// <summary>
/// ギミックの入力イベント / 条件 / アクションの種別を**ジャンル（カテゴリ）**に分類し、
/// 表示用の日本語ラベルを提供する純粋 C# カタログ。
///
/// 種別が増えても選びやすいよう、ルール編集 UI ではカテゴリ → 種別の 2 段階で選択する。
/// 各カテゴリの <see cref="Category.TypeIds"/> は <see cref="GimmickRuleEditLogic"/> の
/// 正規 ID 一覧と同じ文字列を使う（全 ID が必ずいずれか 1 つのカテゴリに属する。
/// 整合は GimmickTypeCatalogTests で検証）。
/// </summary>
public static class GimmickTypeCatalog
{
    /// <summary>1 カテゴリ。表示名と、それに属する種別 ID（定義順）。</summary>
    public class Category
    {
        public string Label { get; }
        public string[] TypeIds { get; }

        public Category(string label, params string[] typeIds)
        {
            Label = label;
            TypeIds = typeIds;
        }
    }

    // ── 入力イベント ──────────────────────────────────────────────────────────

    public static readonly IReadOnlyList<Category> TriggerCategories = new[]
    {
        new Category("基本", "roomStart", "playerCountChanged", "respawn", "actionButton"),
        new Category("オブジェクト", "playerTouchObject", "objectTap", "inRoomPortalUsed"),
        new Category("エリア", "areaEnter", "areaExit"),
        new Category("タイマー", "timerReached"),
        new Category("プレイヤー", "playerTouchPlayer"),
        new Category("サブルーチン", "called"),
    };

    // ── 条件 ──────────────────────────────────────────────────────────────────

    public static readonly IReadOnlyList<Category> ConditionCategories = new[]
    {
        new Category("ステート・タイマー", "worldState", "playerState", "timerCompare"),
        new Category("人数・番号", "playerCount", "playerNumber"),
        new Category("インベントリ", "hasObject"),
        new Category("物理判定", "playersOverlapping", "playerDistance", "playerLineOfSight"),
    };

    // ── アクション ────────────────────────────────────────────────────────────

    public static readonly IReadOnlyList<Category> ActionCategories = new[]
    {
        new Category("ステート・タイマー", "setWorldState", "setPlayerState", "timerStart", "timerStop", "timerReset"),
        new Category("オブジェクト", "showHideObject", "changeObjectType", "moveObject"),
        new Category("インベントリ", "pickupObject", "grantObject"),
        new Category("プレイヤー", "teleportPlayer", "setMoveSpeed", "setPlayerMarker"),
        new Category("演出・会話", "showMessage", "playSound", "switchBgm", "playEffect", "startConversation"),
        new Category("進行・制御", "wait", "callSubroutine"),
        new Category("その他", "resetState"),
    };

    // ── カテゴリ検索 ──────────────────────────────────────────────────────────

    /// <summary>typeId が属するカテゴリのインデックス（見つからなければ 0）。</summary>
    public static int CategoryIndexOf(IReadOnlyList<Category> categories, string typeId)
    {
        for (int i = 0; i < categories.Count; i++)
            if (System.Array.IndexOf(categories[i].TypeIds, typeId) >= 0)
                return i;
        return 0;
    }

    // ── 表示ラベル ────────────────────────────────────────────────────────────

    public static string TriggerLabel(string id) => TriggerLabels.TryGetValue(id, out var l) ? l : id;
    public static string ConditionLabel(string id) => ConditionLabels.TryGetValue(id, out var l) ? l : id;
    public static string ActionLabel(string id) => ActionLabels.TryGetValue(id, out var l) ? l : id;

    private static readonly Dictionary<string, string> TriggerLabels = new()
    {
        { "roomStart", "ルーム開始時" },
        { "playerCountChanged", "人数が変化したとき" },
        { "playerTouchObject", "オブジェクトに接触したとき" },
        { "objectTap", "オブジェクトをタップしたとき" },
        { "areaEnter", "エリアに入ったとき" },
        { "areaExit", "エリアから出たとき" },
        { "timerReached", "タイマーが到達したとき" },
        { "actionButton", "アクションボタンを押したとき" },
        { "playerTouchPlayer", "プレイヤー同士が接触したとき" },
        { "respawn", "リスポーンしたとき" },
        { "inRoomPortalUsed", "ルーム内ポータルを使ったとき" },
        { "called", "サブルーチンが呼ばれたとき" },
    };

    private static readonly Dictionary<string, string> ConditionLabels = new()
    {
        { "worldState", "ワールドステート比較" },
        { "playerState", "プレイヤーステート比較" },
        { "playerCount", "現在人数比較" },
        { "playerNumber", "プレイヤー番号比較" },
        { "timerCompare", "タイマー値比較" },
        { "hasObject", "オブジェクトを持っている" },
        { "playersOverlapping", "プレイヤーが重なっている" },
        { "playerDistance", "プレイヤーとの距離" },
        { "playerLineOfSight", "プレイヤーが視線上にいる" },
    };

    private static readonly Dictionary<string, string> ActionLabels = new()
    {
        { "setWorldState", "ワールドステートを変更" },
        { "setPlayerState", "プレイヤーステートを変更" },
        { "timerStart", "タイマーを開始" },
        { "timerStop", "タイマーを停止" },
        { "timerReset", "タイマーをリセット" },
        { "showHideObject", "オブジェクトの表示を切替" },
        { "changeObjectType", "オブジェクトの種類を変更" },
        { "showMessage", "文字メッセージを表示" },
        { "pickupObject", "オブジェクトを持つ" },
        { "grantObject", "オブジェクトを付与" },
        { "playSound", "効果音を鳴らす" },
        { "switchBgm", "BGM を切り替える" },
        { "moveObject", "オブジェクトを移動" },
        { "teleportPlayer", "プレイヤーをワープ" },
        { "resetState", "状態をリセット" },
        { "playEffect", "エフェクトを再生" },
        { "setMoveSpeed", "移動速度を変更" },
        { "setPlayerMarker", "頭上マーカーを表示" },
        { "startConversation", "会話を開始" },
        { "wait", "待機（以降を遅延）" },
        { "callSubroutine", "サブルーチンを呼ぶ" },
    };
}
