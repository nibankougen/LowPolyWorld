using System.Collections.Generic;
using NUnit.Framework;

public class WorldPublishValidatorTests
{
    private WorldPublishValidator _validator;

    [SetUp]
    public void SetUp() => _validator = new WorldPublishValidator();

    // ── ヘルパー ─────────────────────────────────────────────────────────────

    private static WorldDefinitionJson ValidDef() =>
        new()
        {
            worldName = "テストワールド",
            specialObjects = new SpecialObjectsData
            {
                spawn = new SpawnPointData { isSet = true, position = new IntVec3Json(0, 0, 0) },
            },
        };

    private IReadOnlyList<PublishError> Run(
        WorldDefinitionJson def = null,
        int textureCost = 0,
        int objectCount = 0,
        bool hasThumbnail = true,
        int publishedVersion = 0) =>
        _validator.Validate(def ?? ValidDef(), textureCost, objectCount, hasThumbnail, publishedVersion);

    // ── 正常ケース ────────────────────────────────────────────────────────────

    [Test]
    public void ValidWorld_NoErrors()
    {
        var errors = Run();
        Assert.AreEqual(0, errors.Count);
    }

    // ── ワールド名 ────────────────────────────────────────────────────────────

    [Test]
    public void WorldNameEmpty_ReturnsWorldNameEmpty()
    {
        var def = ValidDef();
        def.worldName = "";
        var errors = Run(def);
        Assert.Contains(PublishError.WorldNameEmpty, (System.Collections.ICollection)errors);
    }

    [Test]
    public void WorldNameWhitespace_ReturnsWorldNameEmpty()
    {
        var def = ValidDef();
        def.worldName = "   ";
        var errors = Run(def);
        Assert.Contains(PublishError.WorldNameEmpty, (System.Collections.ICollection)errors);
    }

    [Test]
    public void NullDef_ReturnsWorldNameEmpty()
    {
        var errors = _validator.Validate(null, 0, 0, true, 0);
        Assert.Contains(PublishError.WorldNameEmpty, (System.Collections.ICollection)errors);
    }

    // ── サムネイル ────────────────────────────────────────────────────────────

    [Test]
    public void NoThumbnail_ReturnsThumbnailMissing()
    {
        var errors = Run(hasThumbnail: false);
        Assert.Contains(PublishError.ThumbnailMissing, (System.Collections.ICollection)errors);
    }

    // ── スポーン ─────────────────────────────────────────────────────────────

    [Test]
    public void SpawnNotSet_ReturnsSpawnNotSet()
    {
        var def = ValidDef();
        def.specialObjects.spawn.isSet = false;
        var errors = Run(def);
        Assert.Contains(PublishError.SpawnNotSet, (System.Collections.ICollection)errors);
    }

    [Test]
    public void SpawnAtOriginWithIsSet_NoSpawnError()
    {
        // (0,0,0) は isSet=true なら有効なスポーン位置
        var def = ValidDef();
        def.specialObjects.spawn = new SpawnPointData { isSet = true, position = new IntVec3Json(0, 0, 0) };
        var errors = Run(def);
        CollectionAssert.DoesNotContain(errors, PublishError.SpawnNotSet);
    }

    // ── テクスチャコスト ─────────────────────────────────────────────────────

    [Test]
    public void TextureCostAtLimit_NoError()
    {
        var errors = Run(textureCost: TextureCostCalculator.CostLimit);
        CollectionAssert.DoesNotContain(errors, PublishError.TextureCostExceeded);
    }

    [Test]
    public void TextureCostExceedsLimit_ReturnsError()
    {
        var errors = Run(textureCost: TextureCostCalculator.CostLimit + 1);
        Assert.Contains(PublishError.TextureCostExceeded, (System.Collections.ICollection)errors);
    }

    // ── オブジェクト数 ────────────────────────────────────────────────────────

    [Test]
    public void ObjectCountAtLimit_NoError()
    {
        var errors = Run(objectCount: TextureCostCalculator.ObjectCountLimit);
        CollectionAssert.DoesNotContain(errors, PublishError.ObjectCountExceeded);
    }

    [Test]
    public void ObjectCountExceedsLimit_ReturnsError()
    {
        var errors = Run(objectCount: TextureCostCalculator.ObjectCountLimit + 1);
        Assert.Contains(PublishError.ObjectCountExceeded, (System.Collections.ICollection)errors);
    }

    // ── バージョン番号オーバーフロー ──────────────────────────────────────────

    [Test]
    public void PublishedVersionAtIntMax_ReturnsVersionNumberOverflow()
    {
        var errors = Run(publishedVersion: int.MaxValue);
        Assert.Contains(PublishError.VersionNumberOverflow, (System.Collections.ICollection)errors);
    }

    [Test]
    public void PublishedVersionNormal_NoOverflowError()
    {
        var errors = Run(publishedVersion: 100);
        CollectionAssert.DoesNotContain(errors, PublishError.VersionNumberOverflow);
    }

    // ── ポータル ─────────────────────────────────────────────────────────────

    [Test]
    public void PortalWithoutExit_ReturnsPortalExitMissing()
    {
        var def = ValidDef();
        def.specialObjects.portals = new[]
        {
            new PortalInstance { entryId = "e1", exitId = "" },
        };
        var errors = Run(def);
        Assert.Contains(PublishError.PortalExitMissing, (System.Collections.ICollection)errors);
    }

    [Test]
    public void PortalWithExit_NoPortalError()
    {
        var def = ValidDef();
        def.specialObjects.portals = new[]
        {
            new PortalInstance { entryId = "e1", exitId = "x1" },
        };
        var errors = Run(def);
        CollectionAssert.DoesNotContain(errors, PublishError.PortalExitMissing);
    }

    // ── スポーン/ポータルの重複 ──────────────────────────────────────────────

    [Test]
    public void SpawnPortalOverlap_ReturnsError()
    {
        var errors = _validator.Validate(ValidDef(), 0, 0, true, 0, spawnPortalOverlap: true);
        Assert.Contains(PublishError.SpawnPortalOverlap, (System.Collections.ICollection)errors);
    }

    [Test]
    public void NoSpawnPortalOverlap_NoError()
    {
        var errors = _validator.Validate(ValidDef(), 0, 0, true, 0, spawnPortalOverlap: false);
        CollectionAssert.DoesNotContain(errors, PublishError.SpawnPortalOverlap);
    }

    // ── ギミック無限ループ ────────────────────────────────────────────────────

    [Test]
    public void GimmickLoopRuleId_ReturnsGimmickLoopDetected()
    {
        var errors = _validator.Validate(ValidDef(), 0, 0, true, 0, gimmickLoopRuleId: "loop_rule");
        Assert.Contains(PublishError.GimmickLoopDetected, (System.Collections.ICollection)errors);
    }

    [Test]
    public void NoGimmickLoop_NoError()
    {
        var emptyId = _validator.Validate(ValidDef(), 0, 0, true, 0, gimmickLoopRuleId: "");
        CollectionAssert.DoesNotContain(emptyId, PublishError.GimmickLoopDetected);
        var nullId = _validator.Validate(ValidDef(), 0, 0, true, 0, gimmickLoopRuleId: null);
        CollectionAssert.DoesNotContain(nullId, PublishError.GimmickLoopDetected);
    }

    // ── 複数エラー ────────────────────────────────────────────────────────────

    [Test]
    public void MultipleErrors_AllReported()
    {
        var def = new WorldDefinitionJson { worldName = "" };
        var errors = _validator.Validate(def, 0, 0, hasThumbnail: false, publishedVersion: 0);
        Assert.GreaterOrEqual(errors.Count, 2, "名前なし + サムネイルなし + スポーンなしで複数エラー");
    }
}
