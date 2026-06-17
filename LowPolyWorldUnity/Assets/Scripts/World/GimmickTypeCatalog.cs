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
        new Category("変数・タイマー", "worldState", "playerState", "playerStateRank", "timerCompare"),
        new Category("人数・番号", "playerCount", "playerNumber"),
        new Category("インベントリ", "hasObject"),
        new Category("物理判定", "playersOverlapping", "playerDistance", "playerLineOfSight"),
    };

    // ── アクション ────────────────────────────────────────────────────────────

    public static readonly IReadOnlyList<Category> ActionCategories = new[]
    {
        new Category("変数・タイマー", "setWorldState", "setPlayerState", "timerStart", "timerStop", "timerReset"),
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

    // 一言説明（種類選択リストで表示・初心者の理解を助ける）。未登録は "".
    public static string TriggerDesc(string id) => TriggerDescs.TryGetValue(id, out var d) ? d : "";
    public static string ConditionDesc(string id) => ConditionDescs.TryGetValue(id, out var d) ? d : "";
    public static string ActionDesc(string id) => ActionDescs.TryGetValue(id, out var d) ? d : "";

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
        { "worldState", "ワールド変数を比べる" },
        { "playerState", "プレイヤー変数を比べる" },
        { "playerStateRank", "プレイヤー変数の順位" },
        { "playerCount", "現在の人数を比べる" },
        { "playerNumber", "プレイヤー番号を比べる" },
        { "timerCompare", "タイマーを比べる" },
        { "hasObject", "オブジェクトを持っている" },
        { "playersOverlapping", "プレイヤーが重なっている" },
        { "playerDistance", "プレイヤーとの距離" },
        { "playerLineOfSight", "プレイヤーが視線上にいる" },
    };

    private static readonly Dictionary<string, string> ActionLabels = new()
    {
        { "setWorldState", "ワールド変数を変更" },
        { "setPlayerState", "プレイヤー変数を変更" },
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

    // ── 一言説明 ──────────────────────────────────────────────────────────────

    private static readonly Dictionary<string, string> TriggerDescs = new()
    {
        { "roomStart", "ルームに入って準備ができたとき（最初に1回）" },
        { "playerCountChanged", "誰かが入室・退室したとき" },
        { "playerTouchObject", "プレイヤーがオブジェクトに触れたとき" },
        { "objectTap", "プレイヤーがオブジェクトをタップしたとき" },
        { "areaEnter", "プレイヤーがエリアに入ったとき" },
        { "areaExit", "プレイヤーがエリアから出たとき" },
        { "timerReached", "タイマーが指定の秒数になったとき" },
        { "actionButton", "画面のアクションボタンを押したとき" },
        { "playerTouchPlayer", "プレイヤー同士が触れ合ったとき" },
        { "respawn", "スポーン地点の外に出てやり直したとき" },
        { "inRoomPortalUsed", "ルーム内のポータルを通ったとき" },
        { "called", "他のルールから「サブルーチンを呼ぶ」で呼ばれたとき" },
    };

    private static readonly Dictionary<string, string> ConditionDescs = new()
    {
        { "worldState", "全員共通のワールド変数の値を比べる" },
        { "playerState", "プレイヤー個人の変数の値を比べる" },
        { "playerStateRank", "プレイヤー変数が全員中で上位/下位 X 位以内か" },
        { "playerCount", "今のルーム人数を比べる" },
        { "playerNumber", "プレイヤーの参加順（1人目・2人目…）を比べる" },
        { "timerCompare", "タイマーの経過秒を比べる" },
        { "hasObject", "指定の種類のオブジェクトを持っているか" },
        { "playersOverlapping", "他のプレイヤーと重なっているか" },
        { "playerDistance", "他のプレイヤーが一定の距離以内にいるか" },
        { "playerLineOfSight", "正面の一定距離に他のプレイヤーが見えるか" },
    };

    private static readonly Dictionary<string, string> ActionDescs = new()
    {
        { "setWorldState", "全員共通のワールド変数を増減・代入する" },
        { "setPlayerState", "プレイヤー個人の変数を増減・代入する" },
        { "timerStart", "タイマーを開始する" },
        { "timerStop", "タイマーを止める" },
        { "timerReset", "タイマーを0に戻す" },
        { "showHideObject", "オブジェクトを表示/非表示にする" },
        { "changeObjectType", "オブジェクトを別の種類に入れ替える" },
        { "showMessage", "画面に文字メッセージを出す" },
        { "pickupObject", "配置オブジェクトをプレイヤーに持たせる" },
        { "grantObject", "指定種類のオブジェクトをプレイヤーに渡す" },
        { "playSound", "効果音を鳴らす" },
        { "switchBgm", "BGM を切り替える（none で停止）" },
        { "moveObject", "オブジェクトを指定位置へ動かす" },
        { "teleportPlayer", "プレイヤーを出口ポータルへワープさせる" },
        { "resetState", "変数やオブジェクトを初期状態に戻す" },
        { "playEffect", "発光などのエフェクトを再生する" },
        { "setMoveSpeed", "プレイヤーの移動速度を変える（0で移動不可）" },
        { "setPlayerMarker", "プレイヤーの頭上にマーカーを表示する" },
        { "startConversation", "会話（セリフ・選択肢）を再生する" },
        { "wait", "指定秒だけ待ってから以降を続ける" },
        { "callSubroutine", "共通ルール（サブルーチン）を呼び出す" },
    };
}
