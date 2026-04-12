using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

/// <summary>
/// プレイヤーの位置・回転・アニメーション状態を Netcode で同期する NetworkBehaviour。
/// 送信レート制御: 最大 20Hz・変化量が閾値を超えた場合のみ送信。
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class PlayerNetworkSync : NetworkBehaviour
{
    private const float SendIntervalSeconds = 1f / 20f; // 20Hz
    private const float PositionThreshold = 0.01f;
    private const float RotationThreshold = 1f;

    // サーバー権威の NetworkVariable（オーナーが書き込み、全員が読み取り）
    private readonly NetworkVariable<Vector3> _netPosition = new(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    private readonly NetworkVariable<float> _netYaw = new(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    private readonly NetworkVariable<int> _netAnimState = new(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    private readonly NetworkVariable<NetworkedString> _netAvatarId = new(
        new NetworkedString(""),
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    // 送信レート制御
    private float _sendTimer;
    private Vector3 _lastSentPosition;
    private float _lastSentYaw;
    private int _lastSentAnimState;

    // 補間用
    private Vector3 _targetPosition;
    private float _targetYaw;

    private Animator _animator;
    private static readonly int AnimSpeed = Animator.StringToHash("Speed");
    private static readonly int AnimIsGrounded = Animator.StringToHash("IsGrounded");

    public override void OnNetworkSpawn()
    {
        _animator = GetComponentInChildren<Animator>();

        if (!IsOwner)
        {
            _netPosition.OnValueChanged += OnPositionChanged;
            _netYaw.OnValueChanged += OnYawChanged;
            _netAnimState.OnValueChanged += OnAnimStateChanged;
            _netAvatarId.OnValueChanged += OnAvatarIdChanged;

            // 初期値を補間ターゲットに設定
            _targetPosition = _netPosition.Value;
            _targetYaw = _netYaw.Value;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (!IsOwner)
        {
            _netPosition.OnValueChanged -= OnPositionChanged;
            _netYaw.OnValueChanged -= OnYawChanged;
            _netAnimState.OnValueChanged -= OnAnimStateChanged;
            _netAvatarId.OnValueChanged -= OnAvatarIdChanged;
        }
    }

    private void Update()
    {
        if (!IsSpawned) return;
        if (IsOwner)
            UpdateOwner();
        else
            UpdateRemote();
    }

    // ---- オーナー側: 送信レート制御 ----

    private void UpdateOwner()
    {
        _sendTimer += Time.deltaTime;
        if (_sendTimer < SendIntervalSeconds) return;
        _sendTimer = 0f;

        var pos = transform.position;
        var yaw = transform.eulerAngles.y;
        int animState = GetCurrentAnimState();

        bool posChanged = Vector3.Distance(pos, _lastSentPosition) > PositionThreshold;
        bool rotChanged = Mathf.Abs(Mathf.DeltaAngle(yaw, _lastSentYaw)) > RotationThreshold;
        bool animChanged = animState != _lastSentAnimState;

        if (posChanged || rotChanged || animChanged)
        {
            if (posChanged || rotChanged)
            {
                _netPosition.Value = pos;
                _netYaw.Value = yaw;
                _lastSentPosition = pos;
                _lastSentYaw = yaw;
            }

            if (animChanged)
            {
                _netAnimState.Value = animState;
                _lastSentAnimState = animState;
            }
        }
    }

    // ---- リモート側: 線形補間 ----

    private void UpdateRemote()
    {
        float lerpSpeed = 10f;
        transform.position = Vector3.Lerp(transform.position, _targetPosition, Time.deltaTime * lerpSpeed);

        var currentRot = transform.eulerAngles;
        float lerpedYaw = Mathf.LerpAngle(currentRot.y, _targetYaw, Time.deltaTime * lerpSpeed);
        transform.eulerAngles = new Vector3(currentRot.x, lerpedYaw, currentRot.z);
    }

    private void OnPositionChanged(Vector3 prev, Vector3 next) => _targetPosition = next;
    private void OnYawChanged(float prev, float next) => _targetYaw = next;

    private void OnAnimStateChanged(int prev, int next)
    {
        if (_animator == null) return;
        float speed = (next & 0xFF) / 255f;
        bool isGrounded = (next & 0x100) != 0;
        bool jumped = (next & 0x200) != 0;

        _animator.SetFloat(AnimSpeed, speed);
        _animator.SetBool(AnimIsGrounded, isGrounded);
        if (jumped) _animator.SetTrigger("Jump");
    }

    private void OnAvatarIdChanged(NetworkedString prev, NetworkedString next)
    {
        Debug.Log($"[PlayerNetworkSync] Avatar changed: {next.Value}");
        // Phase 5 で AvatarManager 連携
    }

    /// <summary>アバター変更をネットワークに通知する。</summary>
    public void NotifyAvatarChanged(string avatarId)
    {
        if (!IsOwner) return;
        _netAvatarId.Value = new NetworkedString(avatarId);
    }

    private int GetCurrentAnimState()
    {
        if (_animator == null) return 0;
        float speed = _animator.GetFloat(AnimSpeed);
        bool isGrounded = _animator.GetBool(AnimIsGrounded);

        int state = Mathf.RoundToInt(speed * 255f) & 0xFF;
        if (isGrounded) state |= 0x100;
        return state;
    }
}

/// <summary>NetworkVariable で文字列を扱うためのラッパー。</summary>
public struct NetworkedString : INetworkSerializable, System.IEquatable<NetworkedString>
{
    public string Value;

    public NetworkedString(string value) => Value = value ?? "";

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Value);
    }

    public bool Equals(NetworkedString other) => Value == other.Value;
    public override string ToString() => Value;
}
