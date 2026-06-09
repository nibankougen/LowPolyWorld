using System;
using UnityEngine;

// ワールド定義 JSON のデータ構造 (world-creation.md セクション 12.2)。
// JsonUtility.FromJson/ToJson で直列化する。
// 動的キーを持つ worldObjectCustomizations / atlasUVMap はリスト形式で保持し、
// アトラスシステム実装時に変換ユーティリティで対応する。

[Serializable]
public class WorldDefinitionJson
{
    public int version = 2;
    public string worldName = "";
    public string[] tags = Array.Empty<string>();
    public int maxPlayers = 6;
    public BackgroundData background = new();
    public WorldBgmData worldBgm = new();
    public string ambientColor = "#FFFFFF";
    public FogData fog = new();
    public ScreenEffectData screenEffect = new();
    public WorldObjectInstance[] objects = Array.Empty<WorldObjectInstance>();
    // 動的キーマップはリスト形式 (JsonUtility の制約回避)
    public ObjectCustomizationEntry[] worldObjectCustomizations = Array.Empty<ObjectCustomizationEntry>();
    public AtlasUVEntry[] atlasUVMap = Array.Empty<AtlasUVEntry>();
    public SpecialObjectsData specialObjects = new();
    public GimmickRule[] gimmicks = Array.Empty<GimmickRule>();
    public WorldStateData[] worldStates = Array.Empty<WorldStateData>();
    public TimerData[] timers = Array.Empty<TimerData>();
    public TerrainData terrain = new();
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
    public Vec3Json position = new();
    public int rotationY = 0;
    public bool visible = true;
    public Vec3Json size = null; // null = use object type default
}

[Serializable]
public class Vec3Json
{
    public float x, y, z;

    public Vec3Json() { }

    public Vec3Json(float x, float y, float z)
    {
        this.x = x;
        this.y = y;
        this.z = z;
    }

    public Vector3 ToVector3() => new(x, y, z);
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
    public Vec3Json position = new();
    public int rotationY = 0;
}

[Serializable]
public class PortalInstance
{
    public string entryId = "";
    public string exitId = "";
    public Vec3Json entryPosition = new();
    public int entryRotationY = 0;
    public Vec3Json exitPosition = new();
    public int exitRotationY = 0;
}

[Serializable]
public class WorldPortalInstance
{
    public string instanceId = "";
    public string targetWorldId = "";
    public Vec3Json position = new();
    public int rotationY = 0;
}

[Serializable]
public class AreaInstance
{
    public string instanceId = "";
    public Vec3Json position = new();
    public Vec3Json size = new();
    public int areaIndex = 0;
}

[Serializable]
public class GimmickRule
{
    public string ruleId = "";
    public string label = "";
    public GimmickTrigger[] triggers = Array.Empty<GimmickTrigger>();
    public GimmickCondition[] conditions = Array.Empty<GimmickCondition>();
    public GimmickAction[] actions = Array.Empty<GimmickAction>();
}

[Serializable]
public class GimmickTrigger
{
    public string type = "";
    public string targetId = "";
}

[Serializable]
public class GimmickCondition
{
    public string type = "";
    public int stateIndex = 0;
    public string op = "eq"; // "eq" | "ne" | "gt" | "lt" | "gte" | "lte" | "mod_eq"
    public int value = 0;
    public int modBy = 0;
}

[Serializable]
public class GimmickAction
{
    public string type = "";
    public string targetId = "";
    public string paramsJson = "{}"; // ネストした JSON を raw 文字列で保持
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
    /// <summary>
    /// 空白テンプレートからワールド定義を生成する。
    /// </summary>
    public static WorldDefinitionJson CreateBlank(string worldName = "新しいワールド") =>
        new() { worldName = worldName };

    /// <summary>
    /// JSON 文字列から WorldDefinitionJson をデシリアライズする。
    /// </summary>
    public static WorldDefinitionJson FromJson(string json) =>
        JsonUtility.FromJson<WorldDefinitionJson>(json) ?? new WorldDefinitionJson();

    /// <summary>
    /// WorldDefinitionJson を JSON 文字列にシリアライズする。
    /// </summary>
    public static string ToJson(WorldDefinitionJson def) =>
        JsonUtility.ToJson(def, prettyPrint: false);
}
