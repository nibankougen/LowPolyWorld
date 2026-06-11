using NUnit.Framework;
using UnityEngine;

public class WorldDefinitionTests
{
    // ── version チェック ──────────────────────────────────────────────────────

    [Test]
    public void FromJson_CurrentVersion_RoundTrips()
    {
        var def = WorldDefinition.CreateBlank("テストワールド");
        def.objects = new[]
        {
            new WorldObjectInstance
            {
                instanceId = "inst_1",
                objectTypeId = "type_desk",
                position = new IntVec3Json(-31, 0, 31),
                rotationY = 2,
                size = new IntVec3Json(4, 8, 4),
            },
        };

        var restored = WorldDefinition.FromJson(WorldDefinition.ToJson(def));

        Assert.IsNotNull(restored);
        Assert.AreEqual("テストワールド", restored.worldName);
        Assert.AreEqual(-31, restored.objects[0].position.x, "グリッド整数が往復する");
        Assert.AreEqual(31, restored.objects[0].position.z);
        Assert.AreEqual(4, restored.objects[0].size.x);
    }

    [Test]
    public void FromJson_VersionMismatch_ReturnsNull()
    {
        var def = WorldDefinition.CreateBlank();
        def.version = WorldDefinition.CurrentVersion - 1;

        Assert.IsNull(WorldDefinition.FromJson(WorldDefinition.ToJson(def)),
            "version 不一致は読み込み拒否（後方互換なし）");
    }

    [Test]
    public void FromJson_InvalidInput_ReturnsNull()
    {
        Assert.IsNull(WorldDefinition.FromJson(null));
        Assert.IsNull(WorldDefinition.FromJson(""));
        Assert.IsNull(WorldDefinition.FromJson("not json at all"));
    }

    // ── サイズセンチネル ──────────────────────────────────────────────────────

    [Test]
    public void Size_ZeroSentinel_SurvivesRoundTrip()
    {
        // (0,0,0) = 種別デフォルトサイズを使用。JsonUtility は null を往復できないため
        // ゼロベクトルをセンチネルとして扱う（シリアライズ規約）
        var def = WorldDefinition.CreateBlank();
        def.objects = new[]
        {
            new WorldObjectInstance { instanceId = "inst_1", objectTypeId = "type_desk" },
        };

        var restored = WorldDefinition.FromJson(WorldDefinition.ToJson(def));

        Assert.IsTrue(restored.objects[0].size.IsZero, "デフォルトサイズ指定が維持される");
    }

    // ── グリッド単位変換 ──────────────────────────────────────────────────────

    [Test]
    public void IntVec3_ToVector3_AppliesUnit()
    {
        var pos = new IntVec3Json(2, -4, 6);

        Assert.AreEqual(new Vector3(1f, -2f, 3f), pos.ToVector3(WorldDefinition.PositionUnit),
            "位置は 0.5m/グリッド");
        Assert.AreEqual(new Vector3(0.5f, -1f, 1.5f), pos.ToVector3(WorldDefinition.SizeUnit),
            "サイズは 0.25m/グリッド");
    }

    // ── 追加フィールドの往復 ──────────────────────────────────────────────────

    [Test]
    public void GroupsAndPlayerStatesAndNumberObjects_RoundTrip()
    {
        var def = WorldDefinition.CreateBlank();
        def.objectGroups = new[]
        {
            new GroupJson { groupId = "g1", name = "家具", parentGroupId = "", sortOrder = 0 },
        };
        def.gimmickGroups = new[]
        {
            new GroupJson { groupId = "gg1", name = "ドア制御", parentGroupId = "", sortOrder = 1 },
        };
        def.playerStates = new[]
        {
            new WorldStateData { index = 0, label = "HP", initialValue = 100 },
        };
        def.numberObjects = new[]
        {
            new NumberObjectJson
            {
                instanceId = "inst_num1", source = "timer",
                timerIndex = 2, countdownFromSeconds = 60,
            },
        };
        def.gimmicks = new[] { new GimmickRule { ruleId = "r1", groupId = "gg1" } };

        var restored = WorldDefinition.FromJson(WorldDefinition.ToJson(def));

        Assert.AreEqual("家具", restored.objectGroups[0].name);
        Assert.AreEqual("ドア制御", restored.gimmickGroups[0].name);
        Assert.AreEqual(100, restored.playerStates[0].initialValue);
        Assert.AreEqual("timer", restored.numberObjects[0].source);
        Assert.AreEqual(60, restored.numberObjects[0].countdownFromSeconds);
        Assert.AreEqual("gg1", restored.gimmicks[0].groupId, "ルールの所属グループが保存される");
    }
}
