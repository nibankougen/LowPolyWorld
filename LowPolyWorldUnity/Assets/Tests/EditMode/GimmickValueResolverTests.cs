using NUnit.Framework;
using System.Collections.Generic;

public class GimmickValueResolverTests
{
    private GimmickStateManager _state;
    private GimmickValueResolver _resolver;
    private GimmickEventContext _ctx;
    private List<string> _allPlayers;

    [SetUp]
    public void SetUp()
    {
        _state = new GimmickStateManager();
        // 乱数は決定的なシード（常に min を返す）でテスト
        _resolver = new GimmickValueResolver(_state, (min, max) => min);
        _ctx = GimmickEventContext.RoomStart();
        _allPlayers = new List<string> { "p1", "p2" };
    }

    // ── Fixed ─────────────────────────────────────────────────────────────────

    [Test]
    public void Resolve_Fixed_ReturnsValue()
    {
        Assert.AreEqual(42, _resolver.Resolve(ValueRef.Fixed(42), _ctx, _allPlayers));
    }

    // ── WorldState ────────────────────────────────────────────────────────────

    [Test]
    public void Resolve_WorldState_ReturnsCurrentValue()
    {
        _state.SetWorldState(3, 77);
        Assert.AreEqual(77, _resolver.Resolve(ValueRef.World(3), _ctx, _allPlayers));
    }

    // ── PlayerState ───────────────────────────────────────────────────────────

    [Test]
    public void Resolve_PlayerState_InputPlayer_ReturnsCorrect()
    {
        var ctx = GimmickEventContext.ActionButton("p1");
        _state.SetPlayerState("p1", 1, 55);
        Assert.AreEqual(55, _resolver.Resolve(
            ValueRef.Player(PlayerTarget.InputPlayer, 1), ctx, _allPlayers));
    }

    [Test]
    public void Resolve_PlayerState_OpponentPlayer_ReturnsOpponentValue()
    {
        var ctx = GimmickEventContext.PlayerTouchPlayer("p1", "p2");
        _state.SetPlayerState("p2", 0, 99);
        Assert.AreEqual(99, _resolver.Resolve(
            ValueRef.Player(PlayerTarget.OpponentPlayer, 0), ctx, _allPlayers));
    }

    [Test]
    public void Resolve_PlayerState_NoOpponent_FallsBackToInputPlayer()
    {
        var ctx = GimmickEventContext.RoomStart();
        _state.SetPlayerState("", 0, 10);
        // 相手なし → 入力プレイヤー（空 ID）にフォールバック
        var result = _resolver.Resolve(
            ValueRef.Player(PlayerTarget.OpponentPlayer, 0), ctx, _allPlayers);
        Assert.AreEqual(10, result);
    }

    // ── AllPlayersStateSum ────────────────────────────────────────────────────

    [Test]
    public void Resolve_AllPlayersStateSum_ReturnsTotalAcrossPlayers()
    {
        _state.SetPlayerState("p1", 0, 30);
        _state.SetPlayerState("p2", 0, 70);
        Assert.AreEqual(100, _resolver.Resolve(ValueRef.AllPlayersSum(0), _ctx, _allPlayers));
    }

    // ── RandomRange ───────────────────────────────────────────────────────────

    [Test]
    public void Resolve_RandomRange_UsesInjectedProvider()
    {
        // シードで min を返す
        Assert.AreEqual(5, _resolver.Resolve(ValueRef.Random(5, 10), _ctx, _allPlayers));
    }

    // ── Evaluate ─────────────────────────────────────────────────────────────

    [TestCase(5, CompareOp.Equal, 5, true)]
    [TestCase(5, CompareOp.NotEqual, 3, true)]
    [TestCase(5, CompareOp.GreaterThan, 3, true)]
    [TestCase(5, CompareOp.GreaterThan, 5, false)]
    [TestCase(5, CompareOp.LessThan, 10, true)]
    [TestCase(5, CompareOp.LessThan, 5, false)]
    [TestCase(5, CompareOp.GreaterOrEqual, 5, true)]
    [TestCase(5, CompareOp.LessOrEqual, 5, true)]
    public void Evaluate_BasicOps(int lhs, CompareOp op, int rhs, bool expected)
    {
        Assert.AreEqual(expected, GimmickValueResolver.Evaluate(lhs, op, rhs));
    }

    [Test]
    public void Evaluate_ModEquals_TrueWhenRemainder()
    {
        // 10 % 3 == 1
        Assert.IsTrue(GimmickValueResolver.Evaluate(10, CompareOp.ModEquals, 0, modBy: 3, modResult: 1));
    }

    [Test]
    public void Evaluate_ModEquals_FalseWhenMismatch()
    {
        // 10 % 3 == 2 → false
        Assert.IsFalse(GimmickValueResolver.Evaluate(10, CompareOp.ModEquals, 0, modBy: 3, modResult: 2));
    }

    [Test]
    public void Evaluate_ModEquals_ModByLessThan2_ReturnsFalse()
    {
        Assert.IsFalse(GimmickValueResolver.Evaluate(5, CompareOp.ModEquals, 0, modBy: 1, modResult: 0));
    }
}
