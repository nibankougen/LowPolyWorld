using NUnit.Framework;
using UnityEngine;

public class PlayerMovementLogicTests
{
    // ---- 初期状態 ----

    [Test]
    public void InitialState_IsIdle()
    {
        var logic = new PlayerMovementLogic();
        Assert.AreEqual(PlayerMoveState.Idle, logic.State);
    }

    [Test]
    public void InitialState_VerticalVelocityIsZero()
    {
        var logic = new PlayerMovementLogic();
        Assert.AreEqual(0f, logic.VerticalVelocity);
    }

    // ---- 状態遷移: 待機 → 歩行 → 走り ----

    [Test]
    public void Update_WithMoveInput_TransitionsToWalk()
    {
        var logic = new PlayerMovementLogic();
        logic.SetGrounded(true);

        logic.Update(0.016f, new Vector2(0f, 1f), sprint: false);

        Assert.AreEqual(PlayerMoveState.Walk, logic.State);
    }

    [Test]
    public void Update_WithMoveInputAndSprint_TransitionsToSprint()
    {
        var logic = new PlayerMovementLogic();
        logic.SetGrounded(true);

        logic.Update(0.016f, new Vector2(0f, 1f), sprint: true);

        Assert.AreEqual(PlayerMoveState.Sprint, logic.State);
    }

    [Test]
    public void Update_NoInput_StaysIdle()
    {
        var logic = new PlayerMovementLogic();
        logic.SetGrounded(true);

        logic.Update(0.016f, Vector2.zero, sprint: false);

        Assert.AreEqual(PlayerMoveState.Idle, logic.State);
    }

    [Test]
    public void Update_MoveInputThenNoInput_TransitionsBackToIdle()
    {
        var logic = new PlayerMovementLogic();
        logic.SetGrounded(true);
        logic.Update(0.016f, new Vector2(1f, 0f), sprint: false);
        Assert.AreEqual(PlayerMoveState.Walk, logic.State);

        logic.Update(0.016f, Vector2.zero, sprint: false);

        Assert.AreEqual(PlayerMoveState.Idle, logic.State);
    }

    // ---- 水平速度 ----

    [Test]
    public void Update_Walk_HorizontalSpeedEqualsWalkSpeed()
    {
        var logic = new PlayerMovementLogic();
        logic.SetGrounded(true);

        logic.Update(0.016f, new Vector2(0f, 1f), sprint: false);

        Assert.AreEqual(PlayerMovementLogic.WalkSpeed, logic.HorizontalVelocity.magnitude, 0.001f);
    }

    [Test]
    public void Update_Sprint_HorizontalSpeedEqualsSprintSpeed()
    {
        var logic = new PlayerMovementLogic();
        logic.SetGrounded(true);

        logic.Update(0.016f, new Vector2(0f, 1f), sprint: true);

        Assert.AreEqual(PlayerMovementLogic.SprintSpeed, logic.HorizontalVelocity.magnitude, 0.001f);
    }

    [Test]
    public void Update_InputMagnitudeExceedsOne_IsClamped()
    {
        var logic = new PlayerMovementLogic();
        logic.SetGrounded(true);

        logic.Update(0.016f, new Vector2(2f, 2f), sprint: false);

        Assert.AreEqual(PlayerMovementLogic.WalkSpeed, logic.HorizontalVelocity.magnitude, 0.001f);
    }

    // ---- ジャンプ可否判定 ----

    [Test]
    public void TryJump_WhenGrounded_ReturnsTrue_AndTransitionsToInAir()
    {
        var logic = new PlayerMovementLogic();
        logic.SetGrounded(true);

        bool result = logic.TryJump();

        Assert.IsTrue(result);
        Assert.AreEqual(PlayerMoveState.InAir, logic.State);
    }

    [Test]
    public void TryJump_WhenGrounded_SetsVerticalVelocity()
    {
        var logic = new PlayerMovementLogic();
        logic.SetGrounded(true);

        logic.TryJump();

        Assert.AreEqual(PlayerMovementLogic.JumpSpeed, logic.VerticalVelocity, 0.001f);
    }

    [Test]
    public void TryJump_WhenNotGrounded_ReturnsFalse()
    {
        var logic = new PlayerMovementLogic();
        logic.SetGrounded(false);

        bool result = logic.TryJump();

        Assert.IsFalse(result);
    }

    [Test]
    public void TryJump_WhenAlreadyInAir_ReturnsFalse()
    {
        var logic = new PlayerMovementLogic();
        logic.SetGrounded(true);
        logic.TryJump(); // 1回目は成功 → InAir

        // 接地フラグはまだ true のままだが State == InAir なので CanJump = false
        bool result = logic.TryJump();

        Assert.IsFalse(result);
    }

    // ---- 着地後の状態復帰 ----

    [Test]
    public void Update_AfterLanding_TransitionsFromInAirToIdle()
    {
        var logic = new PlayerMovementLogic();
        logic.SetGrounded(true);
        logic.TryJump();
        Assert.AreEqual(PlayerMoveState.InAir, logic.State);

        // 落下して地面に戻る: VerticalVelocity を負にして接地させる
        logic.SetGrounded(false);
        logic.Update(0.5f, Vector2.zero, sprint: false); // 重力で下降

        logic.SetGrounded(true);
        logic.Update(0.016f, Vector2.zero, sprint: false);

        Assert.AreEqual(PlayerMoveState.Idle, logic.State);
    }

    // ---- 重力 ----

    [Test]
    public void Update_WhileInAir_GravityDecreasesVerticalVelocity()
    {
        var logic = new PlayerMovementLogic();
        logic.SetGrounded(true);
        logic.TryJump();
        logic.SetGrounded(false);

        float before = logic.VerticalVelocity;
        logic.Update(0.1f, Vector2.zero, sprint: false);

        Assert.Less(logic.VerticalVelocity, before);
    }
}
