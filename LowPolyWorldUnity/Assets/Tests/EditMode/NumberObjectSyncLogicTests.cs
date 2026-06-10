using NUnit.Framework;
using System.Collections.Generic;

public class NumberObjectSyncLogicTests
{
    private NumberObjectSyncLogic _sync;
    private GimmickStateManager _state;
    private List<string> _players;

    [SetUp]
    public void SetUp()
    {
        _sync = new NumberObjectSyncLogic();
        _state = new GimmickStateManager();
        _players = new List<string> { "p1", "p2" };
    }

    private static NumberObjectSyncLogic.NumberObjectDefinition WorldRef(
        string id, int stateIndex) =>
        new NumberObjectSyncLogic.NumberObjectDefinition(
            id, NumberObjectSyncLogic.SourceKind.WorldState, stateIndex: stateIndex);

    private static NumberObjectSyncLogic.NumberObjectDefinition PlayerRef(
        string id, int playerNumber, int stateIndex) =>
        new NumberObjectSyncLogic.NumberObjectDefinition(
            id, NumberObjectSyncLogic.SourceKind.PlayerState,
            stateIndex: stateIndex, playerNumber: playerNumber);

    // ── 登録・上限 ────────────────────────────────────────────────────────────

    [Test]
    public void TryAdd_WithinLimit_Succeeds()
    {
        Assert.IsTrue(_sync.TryAdd(WorldRef("num_1", 0)));
        Assert.AreEqual(1, _sync.Objects.Count);
    }

    [Test]
    public void TryAdd_ExceedsLimit30_Fails()
    {
        for (int i = 0; i < NumberObjectSyncLogic.MaxNumberObjects; i++)
            Assert.IsTrue(_sync.TryAdd(WorldRef($"num_{i}", 0)));

        Assert.IsFalse(_sync.TryAdd(WorldRef("num_over", 0)), "31 個目は追加不可");
        Assert.AreEqual(NumberObjectSyncLogic.MaxNumberObjects, _sync.Objects.Count);
    }

    [Test]
    public void TryAdd_DuplicateId_Fails()
    {
        _sync.TryAdd(WorldRef("num_1", 0));
        Assert.IsFalse(_sync.TryAdd(WorldRef("num_1", 1)));
    }

    [Test]
    public void TryAdd_WorldStateIndexOutOfRange_Fails()
    {
        Assert.IsFalse(_sync.TryAdd(WorldRef("num_a", GimmickStateManager.MaxWorldStates)),
            "ワールドステートは 0〜9 のみ");
        Assert.IsFalse(_sync.TryAdd(WorldRef("num_b", -1)));
        Assert.AreEqual(0, _sync.Objects.Count, "不正な定義は登録されない");
    }

    [Test]
    public void TryAdd_PlayerStateIndexOutOfRange_Fails()
    {
        Assert.IsFalse(_sync.TryAdd(PlayerRef("num_a", 1, GimmickStateManager.MaxPlayerStates)),
            "プレイヤーステートは 0〜3 のみ");
        Assert.IsFalse(_sync.TryAdd(PlayerRef("num_b", 1, -1)));
    }

    [Test]
    public void TryAdd_PlayerNumberLessThanOne_Fails()
    {
        Assert.IsFalse(_sync.TryAdd(PlayerRef("num_a", playerNumber: 0, stateIndex: 0)),
            "参加順番号は 1 起点");
        Assert.IsFalse(_sync.TryAdd(PlayerRef("num_b", playerNumber: -1, stateIndex: 0)));
    }

    [Test]
    public void Remove_RegisteredObject_Succeeds()
    {
        _sync.TryAdd(WorldRef("num_1", 0));

        Assert.IsTrue(_sync.Remove("num_1"));
        Assert.AreEqual(0, _sync.Objects.Count);
        Assert.IsFalse(_sync.Remove("num_1"), "削除済みは false");
    }

    // ── 表示値の解決 ──────────────────────────────────────────────────────────

    [Test]
    public void ResolveValue_WorldState_ReturnsCurrentValue()
    {
        _state.SetWorldState(3, 42);
        _sync.TryAdd(WorldRef("num_1", 3));

        Assert.AreEqual(42, _sync.ResolveValue("num_1", _state, _players));
    }

    [Test]
    public void ResolveValue_PlayerState_UsesJoinOrderNumber()
    {
        _state.SetPlayerState("p2", 1, 77);
        _sync.TryAdd(PlayerRef("num_1", playerNumber: 2, stateIndex: 1));

        Assert.AreEqual(77, _sync.ResolveValue("num_1", _state, _players),
            "参加 2 番目のプレイヤー (p2) のステートを表示");
    }

    [Test]
    public void ResolveValue_PlayerNumberOutOfRange_ReturnsZero()
    {
        _sync.TryAdd(PlayerRef("num_1", playerNumber: 5, stateIndex: 0));

        Assert.AreEqual(0, _sync.ResolveValue("num_1", _state, _players),
            "参照先プレイヤー不在: 0 を表示");
    }

    [Test]
    public void ResolveValue_Fixed_ReturnsFixedValue()
    {
        _sync.TryAdd(new NumberObjectSyncLogic.NumberObjectDefinition(
            "num_1", NumberObjectSyncLogic.SourceKind.Fixed, fixedValue: 123));

        Assert.AreEqual(123, _sync.ResolveValue("num_1", _state, _players));
    }

    [Test]
    public void ResolveValue_UnknownObjectId_ReturnsZero()
    {
        Assert.AreEqual(0, _sync.ResolveValue("unknown", _state, _players));
    }

    // ── ステート更新時の影響特定 ──────────────────────────────────────────────

    [Test]
    public void GetAffectedByWorldState_ReturnsReferencingObjects()
    {
        _sync.TryAdd(WorldRef("num_a", 0));
        _sync.TryAdd(WorldRef("num_b", 0));
        _sync.TryAdd(WorldRef("num_c", 1));
        _sync.TryAdd(PlayerRef("num_d", 1, 0));

        var affected = _sync.GetAffectedByWorldState(0);

        CollectionAssert.AreEquivalent(new[] { "num_a", "num_b" }, affected,
            "ワールドステート 0 を参照する数字オブジェクトのみ即時更新対象");
    }

    [Test]
    public void GetAffectedByPlayerState_MatchesPlayerAndIndex()
    {
        _sync.TryAdd(PlayerRef("num_a", playerNumber: 1, stateIndex: 0)); // p1 / state0
        _sync.TryAdd(PlayerRef("num_b", playerNumber: 2, stateIndex: 0)); // p2 / state0
        _sync.TryAdd(PlayerRef("num_c", playerNumber: 2, stateIndex: 1)); // p2 / state1
        _sync.TryAdd(WorldRef("num_d", 0));

        var affected = _sync.GetAffectedByPlayerState("p2", 0, _players);

        CollectionAssert.AreEquivalent(new[] { "num_b" }, affected,
            "p2 のステート 0 を参照する数字オブジェクトのみ即時更新対象");
    }

    [Test]
    public void GetAffectedByPlayerState_PlayerNotInList_ReturnsEmpty()
    {
        _sync.TryAdd(PlayerRef("num_a", playerNumber: 1, stateIndex: 0));

        var affected = _sync.GetAffectedByPlayerState("p_unknown", 0, _players);

        Assert.IsEmpty(affected);
    }

    [Test]
    public void GetAllObjectIds_ReturnsAllRegistered()
    {
        _sync.TryAdd(WorldRef("num_a", 0));
        _sync.TryAdd(PlayerRef("num_b", 1, 0));

        CollectionAssert.AreEquivalent(new[] { "num_a", "num_b" }, _sync.GetAllObjectIds());
    }
}
