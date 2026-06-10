using NUnit.Framework;
using System.Collections.Generic;

public class GimmickInventoryLogicTests
{
    private GimmickInventoryLogic _inventory;

    [SetUp]
    public void SetUp()
    {
        _inventory = new GimmickInventoryLogic();
    }

    // ── 「持つ」（配置オブジェクト） ──────────────────────────────────────────

    [Test]
    public void TryPickup_Succeeds_HoldsTypeAndHidesInstance()
    {
        var result = _inventory.TryPickup("p1", "inst_key_01", "type_key");

        Assert.IsTrue(result.Success);
        Assert.IsNull(result.ReturnedInstanceId, "初回保有: 返却なし");
        Assert.IsTrue(_inventory.HasObject("p1", "type_key"), "種別 ID で保有判定");
        Assert.IsTrue(_inventory.IsInstanceHeld("inst_key_01"), "インスタンスはワールドから消えた扱い");
        Assert.AreEqual("inst_key_01", _inventory.GetHeldItem("p1")?.SourceInstanceId);
    }

    [Test]
    public void TryPickup_DifferentType_ReturnsExistingInstanceToWorld()
    {
        _inventory.TryPickup("p1", "inst_key_01", "type_key");

        var result = _inventory.TryPickup("p1", "inst_gem_01", "type_gem");

        Assert.IsTrue(result.Success);
        Assert.AreEqual("inst_key_01", result.ReturnedInstanceId, "既存アイテムを元の位置に返す");
        Assert.IsFalse(_inventory.IsInstanceHeld("inst_key_01"), "返したインスタンスはワールドに戻る");
        Assert.IsTrue(_inventory.HasObject("p1", "type_gem"));
        Assert.IsFalse(_inventory.HasObject("p1", "type_key"), "同時保有は 1 種別のみ");
    }

    [Test]
    public void TryPickup_InstanceHeldByOtherPlayer_Fails()
    {
        _inventory.TryPickup("p1", "inst_key_01", "type_key");

        var result = _inventory.TryPickup("p2", "inst_key_01", "type_key");

        Assert.IsFalse(result.Success, "保有中のインスタンスは取得できない");
        Assert.IsTrue(_inventory.HasObject("p1", "type_key"), "p1 の保有は維持");
    }

    [Test]
    public void TryPickup_SameTypeDifferentInstance_BothPlayersCanHold()
    {
        // 同一種別の別インスタンスは取得できる（複数プレイヤーが同じ種別を保有できる）
        Assert.IsTrue(_inventory.TryPickup("p1", "inst_key_01", "type_key").Success);
        Assert.IsTrue(_inventory.TryPickup("p2", "inst_key_02", "type_key").Success);

        Assert.IsTrue(_inventory.HasObject("p1", "type_key"));
        Assert.IsTrue(_inventory.HasObject("p2", "type_key"));
    }

    [Test]
    public void TryPickup_SameTypeAlreadyHeld_Fails()
    {
        _inventory.TryPickup("p1", "inst_key_01", "type_key");

        var result = _inventory.TryPickup("p1", "inst_key_02", "type_key");

        Assert.IsFalse(result.Success, "既に同一種別を保有しているときは何もしない");
        Assert.IsFalse(_inventory.IsInstanceHeld("inst_key_02"), "2 個目のインスタンスは消えない");
    }

    [Test]
    public void TryPickup_NullOrEmptyArgs_Fails()
    {
        Assert.IsFalse(_inventory.TryPickup(null, "inst", "type").Success);
        Assert.IsFalse(_inventory.TryPickup("p1", null, "type").Success);
        Assert.IsFalse(_inventory.TryPickup("p1", "inst", null).Success);
        Assert.IsFalse(_inventory.TryPickup("", "inst", "type").Success);
    }

    // ── 「付与する」（種別直接） ──────────────────────────────────────────────

    [Test]
    public void TryGrant_Succeeds_WithoutConsumingInstance()
    {
        var result = _inventory.TryGrant("p1", "type_sword");

        Assert.IsTrue(result.Success);
        Assert.IsNull(result.ReturnedInstanceId);
        Assert.IsTrue(_inventory.HasObject("p1", "type_sword"));
        Assert.IsTrue(_inventory.GetHeldItem("p1")?.IsGranted, "付与品はインスタンスを持たない");
    }

    [Test]
    public void TryGrant_SameTypeToMultiplePlayers_AllSucceed()
    {
        // 付与は配置物を消費しないため、全員に同じ種別を持たせられる（入室時に剣を持たせる例）
        Assert.IsTrue(_inventory.TryGrant("p1", "type_sword").Success);
        Assert.IsTrue(_inventory.TryGrant("p2", "type_sword").Success);
        Assert.IsTrue(_inventory.TryGrant("p3", "type_sword").Success);

        Assert.IsTrue(_inventory.HasObject("p1", "type_sword"));
        Assert.IsTrue(_inventory.HasObject("p2", "type_sword"));
        Assert.IsTrue(_inventory.HasObject("p3", "type_sword"));
    }

    [Test]
    public void TryGrant_SameTypeAlreadyHeld_Fails()
    {
        _inventory.TryGrant("p1", "type_sword");

        Assert.IsFalse(_inventory.TryGrant("p1", "type_sword").Success);
    }

    [Test]
    public void TryGrant_WhileHoldingPickedInstance_ReturnsInstanceToWorld()
    {
        _inventory.TryPickup("p1", "inst_key_01", "type_key");

        var result = _inventory.TryGrant("p1", "type_sword");

        Assert.IsTrue(result.Success);
        Assert.AreEqual("inst_key_01", result.ReturnedInstanceId, "配置由来の既存アイテムは元の位置へ");
        Assert.IsTrue(_inventory.HasObject("p1", "type_sword"));
    }

    [Test]
    public void TryPickup_WhileHoldingGrantedItem_GrantedItemVanishes()
    {
        _inventory.TryGrant("p1", "type_sword");

        var result = _inventory.TryPickup("p1", "inst_key_01", "type_key");

        Assert.IsTrue(result.Success);
        Assert.IsNull(result.ReturnedInstanceId, "付与品の返却はワールドに戻すものがない");
        Assert.IsTrue(_inventory.HasObject("p1", "type_key"));
        Assert.IsFalse(_inventory.HasObject("p1", "type_sword"));
    }

    // ── HasObject（IInventoryQuery） ──────────────────────────────────────────

    [Test]
    public void HasObject_DifferentTypeOrPlayer_ReturnsFalse()
    {
        _inventory.TryPickup("p1", "inst_key_01", "type_key");

        Assert.IsFalse(_inventory.HasObject("p1", "type_gem"));
        Assert.IsFalse(_inventory.HasObject("p2", "type_key"));
        Assert.IsFalse(_inventory.HasObject(null, "type_key"));
    }

    // ── 退出・ルーム終了リセット ──────────────────────────────────────────────

    [Test]
    public void ReleasePlayer_PickedInstance_ReturnsInstanceToWorld()
    {
        _inventory.TryPickup("p1", "inst_key_01", "type_key");

        string returned = _inventory.ReleasePlayer("p1");

        Assert.AreEqual("inst_key_01", returned, "退出時: 配置由来は初期配置に返す");
        Assert.IsFalse(_inventory.IsInstanceHeld("inst_key_01"));
        Assert.IsNull(_inventory.GetHeldItem("p1"));
    }

    [Test]
    public void ReleasePlayer_GrantedItem_ReturnsNullAndVanishes()
    {
        _inventory.TryGrant("p1", "type_sword");

        string returned = _inventory.ReleasePlayer("p1");

        Assert.IsNull(returned, "付与品はワールドに戻すものがない");
        Assert.IsNull(_inventory.GetHeldItem("p1"));
    }

    [Test]
    public void ReleasePlayer_NoHeldItem_ReturnsNull()
    {
        Assert.IsNull(_inventory.ReleasePlayer("p1"));
        Assert.IsNull(_inventory.ReleasePlayer(null));
    }

    [Test]
    public void ReleaseAll_ReturnsOnlyPickedInstances()
    {
        _inventory.TryPickup("p1", "inst_key_01", "type_key");
        _inventory.TryGrant("p2", "type_sword");

        var returned = _inventory.ReleaseAll();

        CollectionAssert.AreEquivalent(new[] { "inst_key_01" }, returned,
            "ワールドに戻すのは配置由来のみ（付与品は含まない）");
        Assert.IsNull(_inventory.GetHeldItem("p1"));
        Assert.IsNull(_inventory.GetHeldItem("p2"));
    }

    // ── スナップショット同期 ──────────────────────────────────────────────────

    [Test]
    public void Snapshot_RoundTrip_RestoresHeldState()
    {
        _inventory.TryPickup("p1", "inst_key_01", "type_key");
        _inventory.TryGrant("p2", "type_sword");

        var restored = new GimmickInventoryLogic();
        restored.ApplySnapshot(_inventory.GetSnapshot());

        Assert.IsTrue(restored.HasObject("p1", "type_key"));
        Assert.IsTrue(restored.HasObject("p2", "type_sword"));
        Assert.IsTrue(restored.IsInstanceHeld("inst_key_01"));
        Assert.IsTrue(restored.GetHeldItem("p2")?.IsGranted, "付与品として復元される");
        Assert.IsFalse(restored.TryPickup("p3", "inst_key_01", "type_key").Success,
            "復元後も保有中インスタンスは取得不可");
    }

    [Test]
    public void ApplySnapshot_DuplicateInstance_KeepsFirstHolderOnly()
    {
        // 同一インスタンスを複数プレイヤーが保有する壊れたスナップショットは先勝ち
        _inventory.ApplySnapshot(new Dictionary<string, GimmickInventoryLogic.HeldItem>
        {
            ["p1"] = new GimmickInventoryLogic.HeldItem("type_key", "inst_key_01"),
            ["p2"] = new GimmickInventoryLogic.HeldItem("type_key", "inst_key_01"),
        });

        int holderCount = 0;
        if (_inventory.HasObject("p1", "type_key")) holderCount++;
        if (_inventory.HasObject("p2", "type_key")) holderCount++;

        Assert.AreEqual(1, holderCount, "保有者は 1 人だけになる");
        Assert.IsTrue(_inventory.IsInstanceHeld("inst_key_01"));
    }

    [Test]
    public void ApplySnapshot_Null_ClearsState()
    {
        _inventory.TryPickup("p1", "inst_key_01", "type_key");

        _inventory.ApplySnapshot(null);

        Assert.IsNull(_inventory.GetHeldItem("p1"));
        Assert.IsFalse(_inventory.IsInstanceHeld("inst_key_01"));
    }

    // ── GimmickEngine 統合 ────────────────────────────────────────────────────

    [Test]
    public void GimmickEngine_HasInventoryCondition_ChecksTypeId()
    {
        _inventory.TryGrant("p1", "type_key");

        var state = new GimmickStateManager();
        var rule = new RuntimeGimmickRule("r1", "",
            new[] { new RuntimeGimmickTrigger(GimmickEventType.ActionButton) },
            new[] { new RuntimeGimmickCondition(
                GimmickConditionType.HasInventoryObject, objectId: "type_key") },
            new[] { new RuntimeGimmickAction(GimmickActionType.SetWorldState,
                stateIndex: 0, stateOp: StateOp.Set, valueRef: ValueRef.Fixed(1)) });

        var engine = new GimmickEngine(
            new[] { rule }, state, new GimmickTimerLogic(),
            new List<string> { "p1", "p2" }, inventory: _inventory);

        engine.Fire(GimmickEventContext.ActionButton("p1"));
        Assert.AreEqual(1, state.GetWorldState(0), "保有プレイヤー: 条件成立");

        state.SetWorldState(0, 0);
        engine.Fire(GimmickEventContext.ActionButton("p2"));
        Assert.AreEqual(0, state.GetWorldState(0), "未保有プレイヤー: 条件不成立");
    }

    [Test]
    public void GimmickEngine_GrantOnPlayerJoin_EmitsGrantEffectForJoiner()
    {
        // 仕様 9.3 の例: 入室したプレイヤーに剣を付与する
        var rule = new RuntimeGimmickRule("r1", "入室時に剣を渡す",
            new[] { new RuntimeGimmickTrigger(GimmickEventType.PlayerCountChanged) },
            System.Array.Empty<RuntimeGimmickCondition>(),
            new[] { new RuntimeGimmickAction(GimmickActionType.GrantObject,
                targetId: "type_sword", playerTarget: PlayerTarget.InputPlayer) });

        var engine = new GimmickEngine(
            new[] { rule }, new GimmickStateManager(), new GimmickTimerLogic(),
            new List<string> { "p1", "p2" });

        var result = engine.Fire(GimmickEventContext.PlayerCountChanged("p2"));

        Assert.AreEqual(1, result.Effects.Count);
        var effect = result.Effects[0] as PickupObjectEffect;
        Assert.IsNotNull(effect);
        Assert.AreEqual("p2", effect.PlayerId, "入室者が入力プレイヤー");
        Assert.AreEqual("type_sword", effect.ObjectId);
        Assert.IsTrue(effect.IsGrant, "付与モード（配置物を消費しない）");

        // 上位レイヤーがエフェクトを適用 → 保有状態になる
        var apply = _inventory.TryGrant(effect.PlayerId, effect.ObjectId);
        Assert.IsTrue(apply.Success);
        Assert.IsTrue(_inventory.HasObject("p2", "type_sword"));
    }
}
