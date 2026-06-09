using NUnit.Framework;

public class WorldSettingsPanelLogicTests
{
    // ── MaxPlayers ────────────────────────────────────────────────────────────

    [Test]
    public void MaxPlayers_Default_Is6()
    {
        var logic = new WorldSettingsPanelLogic(isPremium: false);
        Assert.AreEqual(6, logic.MaxPlayers);
    }

    [Test]
    public void SetMaxPlayers_Normal_ClampedTo2_6()
    {
        var logic = new WorldSettingsPanelLogic(isPremium: false);
        logic.SetMaxPlayers(1);
        Assert.AreEqual(2, logic.MaxPlayers, "下限クランプ");
        logic.SetMaxPlayers(100);
        Assert.AreEqual(6, logic.MaxPlayers, "通常上限クランプ");
    }

    [Test]
    public void SetMaxPlayers_Premium_ClampedTo2_24()
    {
        var logic = new WorldSettingsPanelLogic(isPremium: true);
        logic.SetMaxPlayers(1);
        Assert.AreEqual(2, logic.MaxPlayers, "下限クランプ");
        logic.SetMaxPlayers(100);
        Assert.AreEqual(24, logic.MaxPlayers, "プレミアム上限クランプ");
    }

    [Test]
    public void MaxPlayersUpperBound_Normal_Is6_Premium_Is24()
    {
        Assert.AreEqual(6, new WorldSettingsPanelLogic(false).MaxPlayersUpperBound);
        Assert.AreEqual(24, new WorldSettingsPanelLogic(true).MaxPlayersUpperBound);
    }

    // ── BgmVolume ─────────────────────────────────────────────────────────────

    [Test]
    public void BgmVolume_Default_Is100()
    {
        var logic = new WorldSettingsPanelLogic(false);
        Assert.AreEqual(100, logic.BgmVolume);
    }

    [Test]
    public void SetBgmVolume_ClampedTo0_100()
    {
        var logic = new WorldSettingsPanelLogic(false);
        logic.SetBgmVolume(-1);
        Assert.AreEqual(0, logic.BgmVolume);
        logic.SetBgmVolume(101);
        Assert.AreEqual(100, logic.BgmVolume);
    }

    // ── BgmSoundId ────────────────────────────────────────────────────────────

    [Test]
    public void SetBgmSoundId_Empty_DefaultsToNone()
    {
        var logic = new WorldSettingsPanelLogic(false);
        logic.SetBgmSoundId("");
        Assert.AreEqual("none", logic.BgmSoundId);
        logic.SetBgmSoundId(null);
        Assert.AreEqual("none", logic.BgmSoundId);
    }

    [Test]
    public void SetBgmSoundId_ValidId_Stored()
    {
        var logic = new WorldSettingsPanelLogic(false);
        logic.SetBgmSoundId("bgmFunNightStage");
        Assert.AreEqual("bgmFunNightStage", logic.BgmSoundId);
    }

    // ── WorldName ─────────────────────────────────────────────────────────────

    [Test]
    public void IsWorldNameEmpty_EmptyAndWhitespace_ReturnsTrue()
    {
        var logic = new WorldSettingsPanelLogic(false);
        logic.SetWorldName("");
        Assert.IsTrue(logic.IsWorldNameEmpty);
        logic.SetWorldName("   ");
        Assert.IsTrue(logic.IsWorldNameEmpty);
    }

    [Test]
    public void IsWorldNameEmpty_NonEmpty_ReturnsFalse()
    {
        var logic = new WorldSettingsPanelLogic(false);
        logic.SetWorldName("テストワールド");
        Assert.IsFalse(logic.IsWorldNameEmpty);
    }

    // ── LoadFrom / ApplyTo ────────────────────────────────────────────────────

    [Test]
    public void LoadFrom_SetsAllSimpleFields()
    {
        var def = new WorldDefinitionJson
        {
            worldName = "テスト",
            tags = new[] { "A", "B" },
            maxPlayers = 4,
            worldBgm = new WorldBgmData { soundId = "rain", volume = 80 },
            ambientColor = "#FF0000",
        };
        var logic = new WorldSettingsPanelLogic(false);
        logic.LoadFrom(def);

        Assert.AreEqual("テスト", logic.WorldName);
        Assert.AreEqual(2, logic.Tags.Count);
        Assert.AreEqual(4, logic.MaxPlayers);
        Assert.AreEqual("rain", logic.BgmSoundId);
        Assert.AreEqual(80, logic.BgmVolume);
        Assert.AreEqual("#FF0000", logic.AmbientColor);
    }

    [Test]
    public void ApplyTo_WritesFieldsBack()
    {
        var logic = new WorldSettingsPanelLogic(false);
        logic.SetWorldName("書き戻しテスト");
        logic.Tags.TryAdd("タグ1");
        logic.SetMaxPlayers(3);
        logic.SetBgmSoundId("ocean");
        logic.SetBgmVolume(50);

        var def = new WorldDefinitionJson();
        logic.ApplyTo(def);

        Assert.AreEqual("書き戻しテスト", def.worldName);
        Assert.AreEqual(1, def.tags.Length);
        Assert.AreEqual("タグ1", def.tags[0]);
        Assert.AreEqual(3, def.maxPlayers);
        Assert.AreEqual("ocean", def.worldBgm.soundId);
        Assert.AreEqual(50, def.worldBgm.volume);
    }

    [Test]
    public void LoadFrom_Null_DoesNotThrow()
    {
        var logic = new WorldSettingsPanelLogic(false);
        Assert.DoesNotThrow(() => logic.LoadFrom(null));
    }

    [Test]
    public void ApplyTo_Null_DoesNotThrow()
    {
        var logic = new WorldSettingsPanelLogic(false);
        Assert.DoesNotThrow(() => logic.ApplyTo(null));
    }

    // ── AmbientColor ─────────────────────────────────────────────────────────

    [Test]
    public void SetAmbientColor_Null_DefaultsToWhite()
    {
        var logic = new WorldSettingsPanelLogic(false);
        logic.SetAmbientColor(null);
        Assert.AreEqual("#FFFFFF", logic.AmbientColor);
    }

    [Test]
    public void LoadFrom_NullWorldBgm_KeepsConstructorDefaults()
    {
        // JsonUtility は [Serializable] フィールドを null にしないが、
        // コードで手動構築された場合のフォールバックを保証する
        var def = new WorldDefinitionJson { worldBgm = null };
        var logic = new WorldSettingsPanelLogic(false);
        logic.LoadFrom(def);
        Assert.AreEqual("none", logic.BgmSoundId, "worldBgm が null でもデフォルト soundId を維持");
        Assert.AreEqual(100, logic.BgmVolume, "worldBgm が null でもデフォルト volume を維持");
    }

    [Test]
    public void IsLocked_ForeignEntry_ReturnsTrue()
    {
        // 別のロジックインスタンスのエントリを渡すと IsLocked = true
        var logic = new WorldSlotLogic(new System.Collections.Generic.List<WorldSlotEntry>(), isPremium: false);
        var foreignEntry = new WorldSlotEntry("foreign_id", "外部ワールド");
        Assert.IsTrue(logic.IsLocked(foreignEntry), "リストに存在しないエントリはロック扱い");
    }
}
