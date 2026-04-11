using UnityEngine;

public enum PlayerMoveState
{
    Idle,
    Walk,
    Sprint,
    InAir,
}

/// <summary>
/// Pure C# logic for player movement state and velocity calculation.
/// MonoBehaviour への依存なし。
/// </summary>
public class PlayerMovementLogic
{
    public const float WalkSpeed = 3.0f;
    public const float SprintSpeed = 6.0f;

    // v = sqrt(2 * g * h), g = 9.81, h = 0.55m → ≈ 3.28 m/s
    public const float JumpSpeed = 3.28f;
    public const float Gravity = -9.81f;

    public PlayerMoveState State { get; private set; }
    public Vector3 HorizontalVelocity { get; private set; }
    public float VerticalVelocity { get; private set; }
    public bool IsGrounded { get; private set; }

    public bool CanJump => IsGrounded && State != PlayerMoveState.InAir;

    public void SetGrounded(bool grounded)
    {
        IsGrounded = grounded;
    }

    /// <summary>ジャンプを試みる。接地中のみ成功し true を返す。</summary>
    public bool TryJump()
    {
        if (!CanJump)
            return false;

        VerticalVelocity = JumpSpeed;
        State = PlayerMoveState.InAir;
        return true;
    }

    /// <summary>
    /// フレームごとの移動更新。
    /// </summary>
    /// <param name="deltaTime">前フレームからの経過秒数。</param>
    /// <param name="moveInput">XZ 入力 (X=右, Y=前)。大きさが 1 を超える場合は正規化する。</param>
    /// <param name="sprint">スプリント入力が有効かどうか。</param>
    public void Update(float deltaTime, Vector2 moveInput, bool sprint)
    {
        // 重力
        if (!IsGrounded)
        {
            VerticalVelocity += Gravity * deltaTime;
        }
        else if (VerticalVelocity < 0f)
        {
            VerticalVelocity = 0f;
            if (State == PlayerMoveState.InAir)
                State = PlayerMoveState.Idle;
        }

        bool hasInput = moveInput.sqrMagnitude > 0.01f;
        var clamped = moveInput.sqrMagnitude > 1f ? moveInput.normalized : moveInput;
        float speed = hasInput ? (sprint ? SprintSpeed : WalkSpeed) : 0f;
        HorizontalVelocity = new Vector3(clamped.x, 0f, clamped.y) * speed;

        if (State != PlayerMoveState.InAir)
        {
            if (!hasInput)
                State = PlayerMoveState.Idle;
            else
                State = sprint ? PlayerMoveState.Sprint : PlayerMoveState.Walk;
        }
    }
}
