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

    // ── 「持つ」反応 ──────────────────────────────────────────────────────────

    [Test]
    public void TryPickup_Succeeds_ObjectBecomesHeld()
    {
        var result = _inventory.TryPickup("p1", "obj_key");

        Assert.IsTrue(result.Success);
        Assert.IsNull(result.ReturnedObjectId, "初回保有: 返却なし");
        Assert.IsTrue(_inventory.HasObject("p1", "obj_key"), "保有状態になる");
        Assert.IsTrue(_inventory.IsObjectHeld("obj_key"), "ワールドから消えた扱いになる");
        Assert.AreEqual("obj_key", _inventory.GetHeldObject("p1"));
    }

    [Test]
    public void TryPickup_AnotherObject_ReturnsExistingToWorld()
    {
        _inventory.TryPickup("p1", "obj_key");

        var result = _inventory.TryPickup("p1", "obj_gem");

        Assert.IsTrue(result.Success);
        Assert.AreEqual("obj_key", result.ReturnedObjectId, "既存アイテムを元の位置に返す");
        Assert.IsFalse(_inventory.IsObjectHeld("obj_key"), "返したオブジェクトはワールドに戻る");
        Assert.IsTrue(_inventory.HasObject("p1", "obj_gem"), "新しいオブジェクトを保有");
        Assert.IsFalse(_inventory.HasObject("p1", "obj_key"), "同時保有は 1 個のみ");
    }

    [Test]
    public void TryPickup_ObjectHeldByOtherPlayer_Fails()
    {
        _inventory.TryPickup("p1", "obj_key");

        var result = _inventory.TryPickup("p2", "obj_key");

        Assert.IsFalse(result.Success, "保有中のオブジェクトは取得できない");
        Assert.IsTrue(_inventory.HasObject("p1", "obj_key"), "p1 の保有は維持");
        Assert.IsNull(_inventory.GetHeldObject("p2"));
    }

    [Test]
    public void TryPickup_SameObjectAlreadyHeld_Fails()
    {
        _inventory.TryPickup("p1", "obj_key");

        var result = _inventory.TryPickup("p1", "obj_key");

        Assert.IsFalse(result.Success, "保有済みオブジェクトの再取得は失敗");
        Assert.IsTrue(_inventory.HasObject("p1", "obj_key"), "保有は維持");
    }

    [Test]
    public void TryPickup_NullOrEmptyArgs_Fails()
    {
        Assert.IsFalse(_inventory.TryPickup(null, "obj_key").Success);
        Assert.IsFalse(_inventory.TryPickup("p1", null).Success);
        Assert.IsFalse(_inventory.TryPickup("", "obj_key").Success);
        Assert.IsFalse(_inventory.TryPickup("p1", "").Success);
    }

    // ── HasObject（IInventoryQuery） ──────────────────────────────────────────

    [Test]
    public void HasObject_DifferentObject_ReturnsFalse()
    {
        _inventory.TryPickup("p1", "obj_key");

        Assert.IsFalse(_inventory.HasObject("p1", "obj_gem"));
        Assert.IsFalse(_inventory.HasObject("p2", "obj_key"));
        Assert.IsFalse(_inventory.HasObject(null, "obj_key"));
    }

    // ── 退出・ルーム終了リセット ──────────────────────────────────────────────

    [Test]
    public void ReleasePlayer_ReturnsHeldObjectToWorld()
    {
        _inventory.TryPickup("p1", "obj_key");

        string returned = _inventory.ReleasePlayer("p1");

        Assert.AreEqual("obj_key", returned, "退出時: 保有オブジェクトを初期配置に返す");
        Assert.IsFalse(_inventory.IsObjectHeld("obj_key"));
        Assert.IsNull(_inventory.GetHeldObject("p1"));
    }

    [Test]
    public void ReleasePlayer_NoHeldObject_ReturnsNull()
    {
        Assert.IsNull(_inventory.ReleasePlayer("p1"));
        Assert.IsNull(_inventory.ReleasePlayer(null));
    }

    [Test]
    public void ReleaseAll_ReturnsAllHeldObjects()
    {
        _inventory.TryPickup("p1", "obj_key");
        _inventory.TryPickup("p2", "obj_gem");

        var returned = _inventory.ReleaseAll();

        CollectionAssert.AreEquivalent(new[] { "obj_key", "obj_gem" }, returned);
        Assert.IsFalse(_inventory.IsObjectHeld("obj_key"));
        Assert.IsFalse(_inventory.IsObjectHeld("obj_gem"));
        Assert.IsNull(_inventory.GetHeldObject("p1"));
        Assert.IsNull(_inventory.GetHeldObject("p2"));
    }

    // ── スナップショット同期 ──────────────────────────────────────────────────

    [Test]
    public void Snapshot_RoundTrip_RestoresHeldState()
    {
        _inventory.TryPickup("p1", "obj_key");
        _inventory.TryPickup("p2", "obj_gem");

        var snapshot = _inventory.GetSnapshot();
        var restored = new GimmickInventoryLogic();
        restored.ApplySnapshot(snapshot);

        Assert.IsTrue(restored.HasObject("p1", "obj_key"));
        Assert.IsTrue(restored.HasObject("p2", "obj_gem"));
        Assert.IsTrue(restored.IsObjectHeld("obj_key"));
        Assert.IsFalse(restored.TryPickup("p3", "obj_key").Success,
            "復元後も保有中オブジェクトは取得不可");
    }

    [Test]
    public void ApplySnapshot_ReplacesExistingState()
    {
        _inventory.TryPickup("p1", "obj_key");

        _inventory.ApplySnapshot(new Dictionary<string, string> { ["p2"] = "obj_gem" });

        Assert.IsFalse(_inventory.HasObject("p1", "obj_key"), "旧状態は破棄される");
        Assert.IsFalse(_inventory.IsObjectHeld("obj_key"));
        Assert.IsTrue(_inventory.HasObject("p2", "obj_gem"));
    }

    [Test]
    public void ApplySnapshot_DuplicateObjectId_KeepsFirstHolderOnly()
    {
        // 同一オブジェクトを複数プレイヤーが保有する壊れたスナップショットは先勝ち
        _inventory.ApplySnapshot(new Dictionary<string, string>
        {
            ["p1"] = "obj_key",
            ["p2"] = "obj_key",
        });

        int holderCount = 0;
        if (_inventory.HasObject("p1", "obj_key")) holderCount++;
        if (_inventory.HasObject("p2", "obj_key")) holderCount++;

        Assert.AreEqual(1, holderCount, "保有者は 1 人だけになる");
        Assert.IsTrue(_inventory.IsObjectHeld("obj_key"));
    }

    [Test]
    public void ApplySnapshot_Null_ClearsState()
    {
        _inventory.TryPickup("p1", "obj_key");

        _inventory.ApplySnapshot(null);

        Assert.IsNull(_inventory.GetHeldObject("p1"));
        Assert.IsFalse(_inventory.IsObjectHeld("obj_key"));
    }

    // ── GimmickEngine 統合 ────────────────────────────────────────────────────

    [Test]
    public void GimmickEngine_HasInventoryCondition_UsesInventoryLogic()
    {
        _inventory.TryPickup("p1", "obj_key");

        var state = new GimmickStateManager();
        var rule = new RuntimeGimmickRule("r1", "",
            new[] { new RuntimeGimmickTrigger(GimmickEventType.ActionButton) },
            new[] { new RuntimeGimmickCondition(
                GimmickConditionType.HasInventoryObject, objectId: "obj_key") },
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
}
