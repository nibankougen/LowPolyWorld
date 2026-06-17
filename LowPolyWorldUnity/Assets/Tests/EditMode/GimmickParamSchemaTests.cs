using System.Collections.Generic;
using NUnit.Framework;
using P = GimmickParamSchema.Param;

public class GimmickParamSchemaTests
{
    private static void AssertSame(IReadOnlyList<P> actual, params P[] expected)
    {
        CollectionAssert.AreEqual(expected, actual);
    }

    // ── 入力イベント ──────────────────────────────────────────────────────────

    [Test]
    public void Trigger_ObjectEvents_HaveObjectId()
    {
        AssertSame(GimmickParamSchema.ForTrigger("playerTouchObject"), P.TrigObjectId);
        AssertSame(GimmickParamSchema.ForTrigger("objectTap"), P.TrigObjectId);
        AssertSame(GimmickParamSchema.ForTrigger("inRoomPortalUsed"), P.TrigObjectId);
    }

    [Test]
    public void Trigger_AreaEvents_HaveAreaId()
    {
        AssertSame(GimmickParamSchema.ForTrigger("areaEnter"), P.TrigAreaId);
        AssertSame(GimmickParamSchema.ForTrigger("areaExit"), P.TrigAreaId);
    }

    [Test]
    public void Trigger_TimerReached_HasTimerAndSeconds()
    {
        AssertSame(GimmickParamSchema.ForTrigger("timerReached"), P.TrigTimerIndex, P.TrigTimerSeconds);
    }

    [Test]
    public void Trigger_Called_HasSubroutineId()
    {
        AssertSame(GimmickParamSchema.ForTrigger("called"), P.TrigSubroutineId);
    }

    [Test]
    public void Trigger_Parameterless_HaveNoParams()
    {
        foreach (var t in new[] { "roomStart", "playerCountChanged", "respawn", "actionButton", "playerTouchPlayer" })
            Assert.AreEqual(0, GimmickParamSchema.ForTrigger(t).Count, t);
    }

    // ── 条件 ──────────────────────────────────────────────────────────────────

    [Test]
    public void Condition_WorldState_Fields()
    {
        AssertSame(GimmickParamSchema.ForCondition("worldState"),
            P.CondWorldStateIndex, P.CondCompareOp, P.CondThreshold);
    }

    [Test]
    public void Condition_PlayerState_HasTargetAndIndex()
    {
        AssertSame(GimmickParamSchema.ForCondition("playerState"),
            P.CondPlayerStateIndex, P.CondPlayerTarget, P.CondCompareOp, P.CondThreshold);
    }

    [Test]
    public void Condition_PlayerStateRank_Fields()
    {
        AssertSame(GimmickParamSchema.ForCondition("playerStateRank"),
            P.CondPlayerStateIndex, P.CondPlayerTarget, P.CondRankOrder, P.CondRankWithin);
    }

    [Test]
    public void Condition_HasObject_Fields()
    {
        AssertSame(GimmickParamSchema.ForCondition("hasObject"),
            P.CondInventoryType, P.CondPlayerTarget);
    }

    [Test]
    public void Condition_PhysicsDistance_HasDistance()
    {
        AssertSame(GimmickParamSchema.ForCondition("playerDistance"),
            P.CondPlayerTarget, P.CondDistanceGrid);
        AssertSame(GimmickParamSchema.ForCondition("playerLineOfSight"),
            P.CondPlayerTarget, P.CondDistanceGrid);
    }

    [Test]
    public void Condition_Overlapping_HasNoParams()
    {
        Assert.AreEqual(0, GimmickParamSchema.ForCondition("playersOverlapping").Count);
    }

    [Test]
    public void SupportsModParams_OnlyForStateAndTimer()
    {
        Assert.IsTrue(GimmickParamSchema.SupportsModParams("worldState"));
        Assert.IsTrue(GimmickParamSchema.SupportsModParams("playerState"));
        Assert.IsTrue(GimmickParamSchema.SupportsModParams("timerCompare"));
        Assert.IsFalse(GimmickParamSchema.SupportsModParams("playerCount"));
        Assert.IsFalse(GimmickParamSchema.SupportsModParams("playerNumber"));
    }

    // ── アクション ────────────────────────────────────────────────────────────

    [Test]
    public void Action_SetWorldState_Fields()
    {
        AssertSame(GimmickParamSchema.ForAction("setWorldState"),
            P.ActWorldStateIndex, P.ActStateOp, P.ActValue);
    }

    [Test]
    public void Action_SetPlayerState_HasTarget()
    {
        AssertSame(GimmickParamSchema.ForAction("setPlayerState"),
            P.ActPlayerStateIndex, P.ActPlayerTarget, P.ActStateOp, P.ActValue);
    }

    [Test]
    public void Action_PlaySound_HasVolumePitchRate()
    {
        AssertSame(GimmickParamSchema.ForAction("playSound"),
            P.ActSoundId, P.ActVolume, P.ActPitch, P.ActPlaybackRate);
    }

    [Test]
    public void Action_MoveObject_HasPositionAndSpeed()
    {
        AssertSame(GimmickParamSchema.ForAction("moveObject"),
            P.ActObjectId, P.ActMovePosition, P.ActMoveSpeed);
    }

    [Test]
    public void Action_Wait_HasSeconds()
    {
        AssertSame(GimmickParamSchema.ForAction("wait"), P.ActWaitSeconds);
    }

    [Test]
    public void Action_Conversation_HasIdAndTarget()
    {
        AssertSame(GimmickParamSchema.ForAction("startConversation"),
            P.ActConversationId, P.ActPlayerTarget);
    }

    [Test]
    public void Action_ShowMessage_HasMessage()
    {
        AssertSame(GimmickParamSchema.ForAction("showMessage"),
            P.ActPlayerTarget, P.ActMessage);
    }

    [Test]
    public void Action_Timers_HaveTimerIndexOnly()
    {
        foreach (var t in new[] { "timerStart", "timerStop", "timerReset" })
            AssertSame(GimmickParamSchema.ForAction(t), P.ActTimerIndex);
    }

    // ── ラベル網羅（全正規 ID に日本語ラベルがある）──────────────────────────

    [Test]
    public void AllCompareOps_HaveLabels()
    {
        foreach (var id in GimmickRuleEditLogic.CompareOps)
            Assert.AreNotEqual(id, GimmickParamSchema.CompareOpLabel(id), id);
    }

    [Test]
    public void AllPlayerTargets_HaveLabels()
    {
        foreach (var id in GimmickRuleEditLogic.PlayerTargets)
            Assert.AreNotEqual(id, GimmickParamSchema.PlayerTargetLabel(id), id);
    }

    [Test]
    public void AllStateOps_HaveLabels()
    {
        foreach (var id in GimmickRuleEditLogic.StateOps)
            Assert.AreNotEqual(id, GimmickParamSchema.StateOpLabel(id), id);
    }

    [Test]
    public void AllResetTargets_HaveLabels()
    {
        foreach (var id in GimmickRuleEditLogic.ResetTargets)
            Assert.AreNotEqual(id, GimmickParamSchema.ResetTargetLabel(id), id);
    }

    [Test]
    public void AllValueKinds_HaveLabels()
    {
        foreach (var id in GimmickRuleEditLogic.ValueKinds)
            Assert.AreNotEqual(id, GimmickParamSchema.ValueKindLabel(id), id);
    }

    [Test]
    public void AllRankOrders_HaveLabels()
    {
        foreach (var id in GimmickRuleEditLogic.RankOrders)
            Assert.AreNotEqual(id, GimmickParamSchema.RankOrderLabel(id), id);
    }
}
