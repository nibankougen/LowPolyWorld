using System;
using UnityEngine;

// ワールド定義 JSON のデータ構造 (world-creation.md セクション 12.2 / シリアライズ規約はセクション 16)。
// JsonUtility.FromJson/ToJson で直列化する。
//
// シリアライズ規約:
// - version 不一致の JSON は読み込み拒否（WorldDefinition.FromJson が null を返す）
// - 座標はグリッド整数で保存: 位置 = 0.5m 単位（原点中心）・サイズ = 0.25m 単位・回転 = 45° 単位
// - 配列順が正: objects = 描画順 / gimmicks = 実行順（エディタはグループツリーの深さ優先順で書き出す）
// - 動的キーを持つ worldObjectCustomizations / atlasUVMap はリスト形式で保持し、
//   アトラスシステム実装時に変換ユーティリティで対応する
// - JsonUtility は null のクラス型フィールドを往復できないため「null = 未設定」は使わない
//   （オブジェクトサイズはゼロベクトルをセンチネルとする）

[Serializable]
public class WorldDefinitionJson
{
    public int version = WorldDefinition.CurrentVersion;
    public string worldName = "";
    public string[] tags = Array.Empty<string>();
    public int maxPlayers = 6;
    public BackgroundData background = new();
    public WorldBgmData worldBgm = new();
    public string ambientColor = "#FFFFFF";
    public FogData fog = new();
    public ScreenEffectData screenEffect = new();
    public WorldObjectInstance[] objects = Array.Empty<WorldObjectInstance>();
    public GroupJson[] objectGroups = Array.Empty<GroupJson>(); // オブジェクトタブのグループツリー
    // 動的キーマップはリスト形式 (JsonUtility の制約回避)
    public ObjectCustomizationEntry[] worldObjectCustomizations = Array.Empty<ObjectCustomizationEntry>();
    public AtlasUVEntry[] atlasUVMap = Array.Empty<AtlasUVEntry>();
    public SpecialObjectsData specialObjects = new();
    public NumberObjectJson[] numberObjects = Array.Empty<NumberObjectJson>(); // 数字オブジェクト設定（3.9）
    public GimmickRule[] gimmicks = Array.Empty<GimmickRule>();
    public GroupJson[] gimmickGroups = Array.Empty<GroupJson>(); // ギミックタブのグループツリー
    public WorldStateData[] worldStates = Array.Empty<WorldStateData>();
    public WorldStateData[] playerStates = Array.Empty<WorldStateData>(); // プレイヤーステート 0〜3 の名前・初期値
    public TimerData[] timers = Array.Empty<TimerData>();
    public ConversationJson[] conversations = Array.Empty<ConversationJson>(); // 会話定義（9.13）
    public SpeakerJson[] speakers = Array.Empty<SpeakerJson>(); // 話者定義（9.13・会話行から speakerId で参照）
    public TerrainData terrain = new();
}

/// <summary>
/// オブジェクトタブ / ギミックタブのグループ（編集ツリー復元用メタデータ。
/// 最大 4 段ネスト。実行順・描画順は objects / gimmicks の配列順が正）。
/// </summary>
[Serializable]
public class GroupJson
{
    public string groupId = "";
    public string name = "";
    public string parentGroupId = ""; // 空 = ルート直下
    public int sortOrder = 0;         // 親内での表示位置
}

/// <summary>数字オブジェクトの設定（world-creation.md 3.9。配置オブジェクトに紐づく）。</summary>
[Serializable]
public class NumberObjectJson
{
    public string instanceId = "";    // 対応する WorldObjectInstance
    public string source = "fixed";   // worldState | playerState | timer | fixed
    public int stateIndex = 0;
    public int playerNumber = 1;      // playerState 用（ルーム参加順・1 起点）
    public int timerIndex = 0;        // timer 用
    public int countdownFromSeconds = 0; // timer 用（0 = カウントアップ表示）
    public int fixedValue = 0;        // fixed 用（0〜999）
}

[Serializable]
public class WorldBgmData
{
    public string soundId = "none";
    public int volume = 100; // 0–100
}

[Serializable]
public class BackgroundData
{
    public string type = "solid"; // "solid" | "gradient" | "texture"
    public string[] colors = { "#111111" };
}

[Serializable]
public class FogData
{
    public bool enabled = false;
    public string color = "#E6E6E6";
    public float startDistance = 10f;
    public float endDistance = 50f;
}

[Serializable]
public class ScreenEffectData
{
    public string type = "none"; // "none" | "rain"
    public int intensity = 100;  // 0–100
}

[Serializable]
public class WorldObjectInstance
{
    public string instanceId = "";
    public string objectTypeId = "";
    public string savedVariantId = null;
    public string groupId = "";        // 所属グループ（空 = ルート直下）
    public IntVec3Json position = new(); // 0.5m グリッド単位（原点中心）
    public int rotationY = 0;            // 45° 単位
    public IntVec3Json size = new();     // 0.25m 単位。(0,0,0) = 種別デフォルトサイズを使用（センチネル）
}

/// <summary>
/// グリッド整数の 3 次元ベクトル。単位は用途ごとに異なる
/// （位置: 0.5m = WorldDefinition.PositionUnit / サイズ: 0.25m = WorldDefinition.SizeUnit）。
/// float ではなく整数で保存することで浮動小数誤差を排除し、範囲検証を単純化する。
/// </summary>
[Serializable]
public class IntVec3Json
{
    public int x, y, z;

    public IntVec3Json() { }

    public IntVec3Json(int x, int y, int z)
    {
        this.x = x;
        this.y = y;
        this.z = z;
    }

    public bool IsZero => x == 0 && y == 0 && z == 0;

    /// <summary>グリッド単位（unit メートル/グリッド）を掛けてワールド座標に変換する。</summary>
    public Vector3 ToVector3(float unit) => new(x * unit, y * unit, z * unit);
}

[Serializable]
public class ObjectCustomizationEntry
{
    public string objectTypeId = "";
    public string layerJsonUrl = "";
    public string integratedTextureUrl = "";
    public string textureHash = "";
}

[Serializable]
public class AtlasUVEntry
{
    public string key = ""; // objectTypeId または savedVariantId
    public float u,
        v,
        w,
        h;
}

[Serializable]
public class SpecialObjectsData
{
    public SpawnPointData spawn = new();
    public PortalInstance[] portals = Array.Empty<PortalInstance>();
    public WorldPortalInstance[] worldPortals = Array.Empty<WorldPortalInstance>();
    public AreaInstance[] areas = Array.Empty<AreaInstance>();
}

[Serializable]
public class SpawnPointData
{
    /// <summary>スポーン位置が設定済みかどうか。false = 未設定（position の値は無効）。</summary>
    public bool isSet = false;
    public IntVec3Json position = new(); // 0.5m グリッド単位
    public int rotationY = 0;
}

[Serializable]
public class PortalInstance
{
    public string entryId = "";
    public string exitId = "";
    public IntVec3Json entryPosition = new(); // 0.5m グリッド単位
    public int entryRotationY = 0;
    public IntVec3Json exitPosition = new();
    public int exitRotationY = 0;
}

[Serializable]
public class WorldPortalInstance
{
    public string instanceId = "";
    public string targetWorldId = "";
    public IntVec3Json position = new(); // 0.5m グリッド単位
    public int rotationY = 0;
}

[Serializable]
public class AreaInstance
{
    public string instanceId = "";
    public IntVec3Json position = new(); // 0.5m グリッド単位
    public IntVec3Json size = new();     // 0.25m 単位
    public int areaIndex = 0;
}

// ── ギミックルール (world-creation.md セクション 9.4〜9.8) ─────────────────────
// JSON → ランタイム変換とバリデーション（9.11）は GimmickRuleConverter が行う。

[Serializable]
public class GimmickRule
{
    public string ruleId = "";
    public string label = "";
    public string groupId = ""; // 所属グループ（空 = ルート直下。実行順は配列順が正）
    public GimmickTrigger[] triggers = Array.Empty<GimmickTrigger>();
    public GimmickCondition[] conditions = Array.Empty<GimmickCondition>();
    public GimmickAction[] actions = Array.Empty<GimmickAction>();
}

[Serializable]
public class GimmickTrigger
{
    // roomStart | playerCountChanged | playerTouchObject | objectTap | areaEnter | areaExit |
    // timerReached | actionButton | playerTouchPlayer | respawn | inRoomPortalUsed
    public string type = "";

    public string targetId = "";    // オブジェクト系イベントの対象 ID（空 = 全対象）
    public int timerIndex = 0;      // timerReached 用（0〜4）
    public float timerSeconds = 0f; // timerReached 用（到達秒）
}

[Serializable]
public class GimmickCondition
{
    // worldState | playerState | playerCount | playerNumber | timerCompare |
    // hasObject | playersOverlapping | playerDistance | playerLineOfSight
    public string type = "";

    public int stateIndex = 0;
    public int timerIndex = 0;                    // timerCompare 用
    public string op = "eq";                      // eq | ne | gt | lt | gte | lte | mod_eq
    public GimmickValueJson threshold = new();    // 比較閾値（9.6 比較値の参照種別）
    public int modBy = 2;                         // mod_eq 用（2 以上）
    public int modResult = 0;                     // mod_eq 用
    public string playerTarget = "input";         // input | opponent | all
    public string objectId = "";                  // hasObject 用（種別 ID）
    public float distanceGrid = 0f;               // playerDistance / playerLineOfSight 用（1 グリッド = 0.5m）
}

[Serializable]
public class GimmickAction
{
    // setWorldState | setPlayerState | timerStart | timerStop | timerReset |
    // showHideObject | changeObjectType | showMessage | pickupObject | grantObject |
    // playSound | switchBgm | moveObject | teleportPlayer | resetState | playEffect |
    // setMoveSpeed | setPlayerMarker
    public string type = "";

    public int stateIndex = 0;
    public string stateOp = "set";                // set | add | sub
    public GimmickValueJson value = new();        // ステート変更の値（9.7 値の参照種別）
    public string playerTarget = "input";         // input | opponent | all
    public string targetId = "";                  // 対象 ID（オブジェクト / ポータル / サウンド / マーカー等）
    public string stringParam = "";               // changeObjectType の切り替え先種別 等
    public GimmickTextJson[] texts = Array.Empty<GimmickTextJson>(); // showMessage 用（言語別・各 80 文字以内）
    public bool visible = true;                   // showHideObject / setPlayerMarker 用
    public int timerIndex = 0;                    // タイマー操作用（0〜4）
    public float floatParam = 0f;                 // moveObject: 速度 / playSound: 音量 0〜100 / setMoveSpeed: 0〜200%
    public float pitch = 1f;                      // playSound 用（0.5〜2.0）
    public float playbackRate = 1f;               // playSound 用（0.5〜2.0）
    public IntVec3Json position = new();          // moveObject の目標座標（0.5m グリッド単位）
    public string resetTarget = "all";            // resetState 用: input | opponent | allPlayers | world | all
}

/// <summary>値の参照（world-creation.md 9.6 / 9.7）。</summary>
[Serializable]
public class GimmickValueJson
{
    public string kind = "fixed"; // fixed | worldState | playerState | allPlayersSum | random
    public int value = 0;         // fixed 用
    public int stateIndex = 0;    // worldState / playerState / allPlayersSum 用
    public string playerTarget = "input"; // playerState 用: input | opponent
    public int min = 0;           // random 用
    public int max = 0;           // random 用
    public bool maxIsPlayerCount = false; // random 用（最大値 = 現在人数）
}

[Serializable]
public class GimmickTextJson
{
    public string lang = ""; // 言語コード（空 = デフォルト言語）
    public string text = "";
}

[Serializable]
public class WorldStateData
{
    public int index;
    public string label = "";
    public int initialValue = 0;
}

[Serializable]
public class TimerData
{
    public int index;
    public string label = "";
}

// ── 会話システム（world-creation.md 9.13）─────────────────────────────────────

/// <summary>分岐・選択肢付きの会話定義（9.13）。アクション startConversation の targetId が参照する。</summary>
[Serializable]
public class ConversationJson
{
    public string conversationId = "";
    public string name = "";
    public ConversationLineJson[] lines = Array.Empty<ConversationLineJson>();
}

/// <summary>
/// ワールド単位で定義する話者（9.13）。会話行（<see cref="ConversationLineJson.speakerId"/>）から参照する。
/// 名前は言語別（各 40 文字以内・未設定言語は英語優先でフォールバック）。
/// </summary>
[Serializable]
public class SpeakerJson
{
    public string speakerId = "";
    public GimmickTextJson[] names = Array.Empty<GimmickTextJson>();
}

/// <summary>会話の 1 セリフ行。</summary>
[Serializable]
public class ConversationLineJson
{
    public string lineId = "";
    public string speakerId = ""; // 話者定義（SpeakerJson）への参照（"" = 話者なし / 地の文）
    public GimmickTextJson[] texts = Array.Empty<GimmickTextJson>(); // 本文（言語別・各 80 文字）
    public ConversationEffectJson onReach = new(); // 行到達時のステート変更（kind="none" = なし）

    // 分岐先: "" = 次の行へ（最終行なら終了）/ "end" = 会話終了 / それ以外 = 同一会話内の lineId
    public string gotoLineId = "";
    public ConversationChoiceJson[] choices = Array.Empty<ConversationChoiceJson>(); // 選択肢（最大 4・空 = 選択肢なし）
}

/// <summary>セリフ行の選択肢。</summary>
[Serializable]
public class ConversationChoiceJson
{
    public GimmickTextJson[] texts = Array.Empty<GimmickTextJson>(); // 選択肢テキスト（言語別・各 40 文字）
    public string gotoLineId = ""; // "" = 次の行へ / "end" = 終了 / それ以外 = lineId
    public ConversationEffectJson effect = new(); // 選択時のステート変更（kind="none" = なし）
}

/// <summary>会話の行到達 / 選択時に適用するステート変更（値は固定値のみ）。</summary>
[Serializable]
public class ConversationEffectJson
{
    public string kind = "none"; // none | worldState | playerState
    public int stateIndex = 0;
    public string stateOp = "set"; // set | add | sub
    public int value = 0; // 固定値（0〜255）
    public string playerTarget = "input"; // playerState 用: input | opponent | all
}

[Serializable]
public class TerrainData
{
    public string[] palette = Array.Empty<string>(); // uid 文字列 (最大 16 要素)
    public string voxelDataUrl = "";
    public string terrainAtlasUrl = "";
    public AtlasUVEntry[] terrainAtlasUVMap = Array.Empty<AtlasUVEntry>();
}

/// <summary>
/// WorldDefinitionJson のファクトリ / ユーティリティ。
/// </summary>
public static class WorldDefinition
{
    /// <summary>現在のワールド定義フォーマットバージョン。不一致は読み込み拒否する。</summary>
    public const int CurrentVersion = 3;

    /// <summary>位置のグリッド単位（メートル/グリッド）。</summary>
    public const float PositionUnit = 0.5f;

    /// <summary>サイズのグリッド単位（メートル/グリッド）。</summary>
    public const float SizeUnit = 0.25f;

    /// <summary>
    /// 空白テンプレートからワールド定義を生成する。
    /// </summary>
    public static WorldDefinitionJson CreateBlank(string worldName = "新しいワールド") =>
        new() { worldName = worldName };

    /// <summary>
    /// JSON 文字列から WorldDefinitionJson をデシリアライズする。
    /// 解析失敗・version 不一致の場合は null を返す（呼び出し側でエラー処理する）。
    /// </summary>
    public static WorldDefinitionJson FromJson(string json)
    {
        if (string.IsNullOrEmpty(json))
            return null;

        WorldDefinitionJson def;
        try
        {
            def = JsonUtility.FromJson<WorldDefinitionJson>(json);
        }
        catch (Exception)
        {
            return null;
        }

        if (def == null || def.version != CurrentVersion)
            return null;

        return def;
    }

    /// <summary>
    /// WorldDefinitionJson を JSON 文字列にシリアライズする。
    /// </summary>
    public static string ToJson(WorldDefinitionJson def) =>
        JsonUtility.ToJson(def, prettyPrint: false);
}
