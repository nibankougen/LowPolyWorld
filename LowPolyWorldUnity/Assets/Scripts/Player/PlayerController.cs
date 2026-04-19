using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

/// <summary>
/// Unity エンジン境界のみを担当する MonoBehaviour。
/// ゲームロジックは PlayerMovementLogic / TouchInputLogic に委譲する。
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
[RequireComponent(typeof(Animator))]
public class PlayerController : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private InputActionAsset _inputActions;

    [Header("Ground Check")]
    [SerializeField] private LayerMask _groundLayerMask;
    [SerializeField] private float _groundCheckRadius = 0.22f;
    [SerializeField] private float _groundCheckOffset = 0.05f;

    private Rigidbody _rigidbody;
    private Animator _animator;
    private PlayerMovementLogic _movement;
    private TouchInputLogic _touchInput;

    private static readonly int AnimSpeed = Animator.StringToHash("Speed");
    private static readonly int AnimIsGrounded = Animator.StringToHash("IsGrounded");
    private static readonly int AnimJump = Animator.StringToHash("Jump");

    private InputAction _moveAction;
    private InputAction _lookAction;
    private InputAction _jumpAction;
    private InputAction _sprintAction;

    /// <summary>CameraFollowController が参照するルック入力（度/フレーム）。</summary>
    public Vector2 LookDelta { get; private set; }

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _rigidbody.freezeRotation = true;
        _rigidbody.useGravity = false;
        _animator = GetComponent<Animator>();

        // Player-Player 間の衝突を無効化（アバター同士は重なれる）
        int playerLayer = LayerMask.NameToLayer("Player");
        if (playerLayer >= 0)
            Physics.IgnoreLayerCollision(playerLayer, playerLayer, true);

        gameObject.layer = playerLayer >= 0 ? playerLayer : gameObject.layer;

        _movement = new PlayerMovementLogic();
        _touchInput = new TouchInputLogic();
        _touchInput.SetScreenSize(Screen.width, Screen.height);

        if (_inputActions != null)
        {
            var map = _inputActions.FindActionMap("Player");
            _moveAction = map?.FindAction("Move");
            _lookAction = map?.FindAction("Look");
            _jumpAction = map?.FindAction("Jump");
            _sprintAction = map?.FindAction("Sprint");
        }
    }

    private void OnEnable()
    {
        EnhancedTouchSupport.Enable();
        _inputActions?.FindActionMap("Player")?.Enable();
    }

    private void OnDisable()
    {
        _inputActions?.FindActionMap("Player")?.Disable();
        EnhancedTouchSupport.Disable();
    }

    private void Update()
    {
        _touchInput.SetScreenSize(Screen.width, Screen.height);

        Vector2 moveInput;
        bool sprint;
        bool jumpRequested;
        Vector2 lookDelta;

        if (Application.isMobilePlatform)
        {
            ProcessTouches();
            _touchInput.Tick(Time.deltaTime);

            moveInput = _touchInput.MoveDirection;
            sprint = _touchInput.IsSprinting;
            jumpRequested = _touchInput.JumpRequested;
            if (jumpRequested)
                _touchInput.ConsumeJump();
            lookDelta = _touchInput.LookDelta;
        }
        else
        {
            moveInput = _moveAction?.ReadValue<Vector2>() ?? Vector2.zero;
            sprint = _sprintAction?.IsPressed() ?? false;
            jumpRequested = _jumpAction?.WasPressedThisFrame() ?? false;
            lookDelta = _lookAction?.ReadValue<Vector2>() ?? Vector2.zero;
        }

        LookDelta = lookDelta;

        bool isGrounded = CheckGrounded();
        _movement.SetGrounded(isGrounded);

        bool jumped = false;
        if (jumpRequested)
            jumped = _movement.TryJump();

        _movement.Update(Time.deltaTime, moveInput, sprint);

        // Animator 駆動
        float speed = _movement.HorizontalVelocity.magnitude / PlayerMovementLogic.SprintSpeed;
        _animator.SetFloat(AnimSpeed, speed);
        _animator.SetBool(AnimIsGrounded, isGrounded);
        if (jumped)
            _animator.SetTrigger(AnimJump);

        // Vivox 3D 位置通知
        VoiceManager.Instance?.UpdateListenerPosition(gameObject);
    }

    private void FixedUpdate()
    {
        // 水平速度をカメラの Yaw に合わせて回転させることでカメラ相対移動を実現
        float yaw = 0f;
        var cam = Camera.main;
        if (cam != null)
        {
            var camFollow = cam.GetComponent<CameraFollowController>();
            if (camFollow != null)
                yaw = camFollow.Yaw;
        }

        var rotatedH = Quaternion.Euler(0f, yaw, 0f) * _movement.HorizontalVelocity;
        _rigidbody.linearVelocity = new Vector3(rotatedH.x, _movement.VerticalVelocity, rotatedH.z);
    }

    private bool CheckGrounded()
    {
        var origin = transform.position + Vector3.up * _groundCheckOffset;
        return Physics.CheckSphere(origin, _groundCheckRadius, _groundLayerMask);
    }

    private void ProcessTouches()
    {
        foreach (var touch in Touch.activeTouches)
        {
            switch (touch.phase)
            {
                case UnityEngine.InputSystem.TouchPhase.Began:
                    _touchInput.OnTouchBegan(touch.touchId, touch.screenPosition, Time.realtimeSinceStartup);
                    break;
                case UnityEngine.InputSystem.TouchPhase.Moved:
                    _touchInput.OnTouchMoved(touch.touchId, touch.screenPosition, touch.delta);
                    break;
                case UnityEngine.InputSystem.TouchPhase.Ended:
                case UnityEngine.InputSystem.TouchPhase.Canceled:
                    _touchInput.OnTouchEnded(touch.touchId, touch.screenPosition, Time.realtimeSinceStartup);
                    break;
            }
        }
    }
}
