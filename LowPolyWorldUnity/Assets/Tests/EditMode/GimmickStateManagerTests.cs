using NUnit.Framework;

public class GimmickStateManagerTests
{
    private GimmickStateManager _mgr;

    [SetUp]
    public void SetUp() => _mgr = new GimmickStateManager();

    // ── ワールドステート ───────────────────────────────────────────────────────

    [Test]
    public void WorldState_InitialValue_IsZero()
    {
        for (int i = 0; i < GimmickStateManager.MaxWorldStates; i++)
            Assert.AreEqual(0, _mgr.GetWorldState(i));
    }

    [Test]
    public void WorldState_SetAndGet_ReturnsValue()
    {
        _mgr.SetWorldState(0, 100);
        Assert.AreEqual(100, _mgr.GetWorldState(0));
    }

    [Test]
    public void WorldState_Set_ClampsAbove255()
    {
        _mgr.SetWorldState(0, 300);
        Assert.AreEqual(255, _mgr.GetWorldState(0));
    }

    [Test]
    public void WorldState_Set_ClampsBelow0()
    {
        _mgr.SetWorldState(0, -10);
        Assert.AreEqual(0, _mgr.GetWorldState(0));
    }

    [Test]
    public void WorldState_Apply_Add()
    {
        _mgr.SetWorldState(0, 100);
        _mgr.ApplyWorldState(0, StateOp.Add, 50);
        Assert.AreEqual(150, _mgr.GetWorldState(0));
    }

    [Test]
    public void WorldState_Apply_Subtract()
    {
        _mgr.SetWorldState(0, 100);
        _mgr.ApplyWorldState(0, StateOp.Subtract, 30);
        Assert.AreEqual(70, _mgr.GetWorldState(0));
    }

    [Test]
    public void WorldState_Apply_AddOverflow_Clamps()
    {
        _mgr.SetWorldState(0, 250);
        _mgr.ApplyWorldState(0, StateOp.Add, 20);
        Assert.AreEqual(255, _mgr.GetWorldState(0));
    }

    [Test]
    public void WorldState_Apply_SubtractUnderflow_Clamps()
    {
        _mgr.SetWorldState(0, 5);
        _mgr.ApplyWorldState(0, StateOp.Subtract, 10);
        Assert.AreEqual(0, _mgr.GetWorldState(0));
    }

    [Test]
    public void WorldState_InvalidIndex_Throws()
    {
        Assert.Throws<System.ArgumentOutOfRangeException>(() => _mgr.GetWorldState(-1));
        Assert.Throws<System.ArgumentOutOfRangeException>(
            () => _mgr.GetWorldState(GimmickStateManager.MaxWorldStates));
    }

    [Test]
    public void WorldState_CustomInitials_AppliedOnReset()
    {
        var mgr = new GimmickStateManager(new[] { 10, 20, 30 });
        mgr.SetWorldState(0, 99);
        mgr.ResetWorldStates();
        Assert.AreEqual(10, mgr.GetWorldState(0));
        Assert.AreEqual(20, mgr.GetWorldState(1));
        Assert.AreEqual(30, mgr.GetWorldState(2));
    }

    // ── プレイヤーステート ────────────────────────────────────────────────────

    [Test]
    public void PlayerState_InitialValue_IsZero()
    {
        Assert.AreEqual(0, _mgr.GetPlayerState("p1", 0));
    }

    [Test]
    public void PlayerState_SetAndGet()
    {
        _mgr.SetPlayerState("p1", 2, 77);
        Assert.AreEqual(77, _mgr.GetPlayerState("p1", 2));
    }

    [Test]
    public void PlayerState_DifferentPlayers_IndependentValues()
    {
        _mgr.SetPlayerState("p1", 0, 50);
        _mgr.SetPlayerState("p2", 0, 100);
        Assert.AreEqual(50, _mgr.GetPlayerState("p1", 0));
        Assert.AreEqual(100, _mgr.GetPlayerState("p2", 0));
    }

    [Test]
    public void PlayerState_Apply_Add()
    {
        _mgr.SetPlayerState("p1", 0, 100);
        _mgr.ApplyPlayerState("p1", 0, StateOp.Add, 55);
        Assert.AreEqual(155, _mgr.GetPlayerState("p1", 0));
    }

    [Test]
    public void PlayerState_InvalidIndex_Throws()
    {
        Assert.Throws<System.ArgumentOutOfRangeException>(
            () => _mgr.GetPlayerState("p1", GimmickStateManager.MaxPlayerStates));
    }

    // ── SumAllPlayersState ────────────────────────────────────────────────────

    [Test]
    public void SumAllPlayersState_ReturnsTotal()
    {
        _mgr.SetPlayerState("p1", 0, 30);
        _mgr.SetPlayerState("p2", 0, 70);
        Assert.AreEqual(100, _mgr.SumAllPlayersState(0));
    }

    // ── リセット ──────────────────────────────────────────────────────────────

    [Test]
    public void ResetWorldStates_ClearsAllToInitial()
    {
        _mgr.SetWorldState(0, 200);
        _mgr.ResetWorldStates();
        Assert.AreEqual(0, _mgr.GetWorldState(0));
    }

    [Test]
    public void ResetPlayerStates_ClearsTargetPlayer()
    {
        _mgr.SetPlayerState("p1", 0, 100);
        _mgr.SetPlayerState("p2", 0, 200);
        _mgr.ResetPlayerStates("p1");
        Assert.AreEqual(0, _mgr.GetPlayerState("p1", 0));
        Assert.AreEqual(200, _mgr.GetPlayerState("p2", 0));
    }

    [Test]
    public void ResetAll_ClearsWorldAndAllPlayers()
    {
        _mgr.SetWorldState(0, 100);
        _mgr.SetPlayerState("p1", 0, 50);
        _mgr.ResetAll();
        Assert.AreEqual(0, _mgr.GetWorldState(0));
        Assert.AreEqual(0, _mgr.GetPlayerState("p1", 0));
    }

    // ── プレイヤーステート初期値（world-creation.md 9.1 / 11.7.4） ─────────────

    [Test]
    public void PlayerInitials_NewPlayerStartsAtInitialValues()
    {
        var mgr = new GimmickStateManager(playerInitials: new[] { 100, 50 });

        Assert.AreEqual(100, mgr.GetPlayerState("p1", 0), "入場時点で初期値");
        Assert.AreEqual(50, mgr.GetPlayerState("p1", 1));
        Assert.AreEqual(0, mgr.GetPlayerState("p1", 2), "未指定分は 0");
    }

    [Test]
    public void PlayerInitials_ResetRestoresInitialValues()
    {
        var mgr = new GimmickStateManager(playerInitials: new[] { 100 });
        mgr.SetPlayerState("p1", 0, 10);

        mgr.ResetPlayerStates("p1");
        Assert.AreEqual(100, mgr.GetPlayerState("p1", 0), "リセットは 0 ではなく初期値に戻す");

        mgr.SetPlayerState("p1", 0, 10);
        mgr.ResetAll();
        Assert.AreEqual(100, mgr.GetPlayerState("p1", 0), "ResetAll も初期値に戻す");
    }

    [Test]
    public void PlayerInitials_ClampedTo0To255()
    {
        var mgr = new GimmickStateManager(playerInitials: new[] { 300, -5 });

        Assert.AreEqual(255, mgr.GetPlayerState("p1", 0));
        Assert.AreEqual(0, mgr.GetPlayerState("p1", 1));
    }
}
