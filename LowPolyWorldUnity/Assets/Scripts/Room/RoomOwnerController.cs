using Unity.Netcode;
using UnityEngine;

/// <summary>
/// ルームオーナー移譲を管理する NetworkBehaviour（サーバー/ホスト上で動作）。
/// RoomOwnerLogic を所有し、クライアント接続・切断時に移譲処理を行う。
/// 新オーナー決定後は全クライアントへ ClientRpc で通知する。
/// </summary>
public class RoomOwnerController : NetworkBehaviour
{
    private RoomOwnerLogic _ownerLogic;

    /// <summary>現在のオーナー clientId（全クライアントで参照可能）。</summary>
    public readonly NetworkVariable<ulong> CurrentOwnerId = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        // ホスト自身が最初のオーナー兼作成者
        _ownerLogic = new RoomOwnerLogic(NetworkManager.LocalClientId);
        _ownerLogic.AddMember(NetworkManager.LocalClientId);
        _ownerLogic.OnOwnerChanged += ApplyOwnerChange;

        CurrentOwnerId.Value = NetworkManager.LocalClientId;

        NetworkManager.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
    }

    public override void OnNetworkDespawn()
    {
        if (!IsServer) return;

        NetworkManager.OnClientConnectedCallback -= OnClientConnected;
        NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
    }

    private void Update()
    {
        if (IsServer) _ownerLogic?.Tick(Time.deltaTime);
    }

    private void OnClientConnected(ulong clientId)
    {
        if (!IsServer) return;
        // ホスト自身は OnNetworkSpawn で登録済みなのでスキップ
        if (clientId == NetworkManager.LocalClientId) return;
        _ownerLogic.AddMember(clientId);
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (!IsServer) return;
        _ownerLogic.RemoveMember(clientId);
    }

    private void ApplyOwnerChange(ulong newOwnerId)
    {
        CurrentOwnerId.Value = newOwnerId;
        NotifyOwnerChangedClientRpc(newOwnerId);
    }

    [ClientRpc]
    private void NotifyOwnerChangedClientRpc(ulong newOwnerId)
    {
        Debug.Log($"[RoomOwner] Owner transferred to client {newOwnerId}");
    }

    /// <summary>現在のクライアントがオーナーかどうか。</summary>
    public bool IsLocalOwner => IsSpawned && CurrentOwnerId.Value == NetworkManager.LocalClientId;
}
