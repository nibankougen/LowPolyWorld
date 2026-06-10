using System.Collections.Generic;

/// <summary>
/// ギミックのインベントリ（オブジェクト保有）ロジッククラス（world-creation.md セクション 9.3）。
///
/// - プレイヤーは同時に 1 個のオブジェクトのみ保有できる
/// - 「持つ」反応時: ワールド上の対象オブジェクトが消え、プレイヤーのインベントリに入る
/// - 別オブジェクトを持つと既存アイテムはワールド初期配置に戻る
/// - 保有中のオブジェクトは他プレイヤーは取得できない
/// - ルーム終了時またはプレイヤー退出時: 保有オブジェクトはワールド初期配置に戻る
///
/// GimmickEngine の HasInventoryObject 条件に IInventoryQuery として注入する。
/// ワールド側の表示反映（消す / 戻す）は PickupObjectEffect を受けた上位レイヤーが行う。
/// </summary>
public class GimmickInventoryLogic : IInventoryQuery
{
    private readonly Dictionary<string, string> _heldByPlayer = new(); // playerId → objectId
    private readonly Dictionary<string, string> _holderByObject = new(); // objectId → playerId

    /// <summary>「持つ」反応の結果。</summary>
    public readonly struct PickupResult
    {
        /// <summary>保有に成功したか（保有済み・他プレイヤー保有中は false）。</summary>
        public bool Success { get; }

        /// <summary>持ち替えで初期配置に返したオブジェクト ID（なければ null）。</summary>
        public string ReturnedObjectId { get; }

        public PickupResult(bool success, string returnedObjectId)
        {
            Success = success;
            ReturnedObjectId = returnedObjectId;
        }
    }

    // ── IInventoryQuery ───────────────────────────────────────────────────────

    public bool HasObject(string playerId, string objectTypeId) =>
        !string.IsNullOrEmpty(playerId)
        && _heldByPlayer.TryGetValue(playerId, out var held)
        && held == objectTypeId;

    // ── 参照 ──────────────────────────────────────────────────────────────────

    /// <summary>プレイヤーが保有中のオブジェクト ID を返す（未保有なら null）。</summary>
    public string GetHeldObject(string playerId) =>
        playerId != null && _heldByPlayer.TryGetValue(playerId, out var held) ? held : null;

    /// <summary>オブジェクトが誰かに保有されている（= ワールドから消えている）か。</summary>
    public bool IsObjectHeld(string objectId) =>
        objectId != null && _holderByObject.ContainsKey(objectId);

    // ── 「持つ」反応 ──────────────────────────────────────────────────────────

    /// <summary>
    /// プレイヤーがオブジェクトを持つ。別オブジェクト保有中なら既存アイテムを
    /// 初期配置に返してから持つ（ReturnedObjectId に返したオブジェクトが入る）。
    /// 同一オブジェクトを保有済み、または他プレイヤーが保有中の場合は失敗する。
    /// </summary>
    public PickupResult TryPickup(string playerId, string objectId)
    {
        if (string.IsNullOrEmpty(playerId) || string.IsNullOrEmpty(objectId))
            return new PickupResult(false, null);

        // 保有中のオブジェクトはワールドに存在しないため取得できない
        if (_holderByObject.ContainsKey(objectId))
            return new PickupResult(false, null);

        // 既存アイテムは初期配置に返す（同時保有は 1 個のみ）
        string returned = null;
        if (_heldByPlayer.TryGetValue(playerId, out var current))
        {
            returned = current;
            _holderByObject.Remove(current);
        }

        _heldByPlayer[playerId] = objectId;
        _holderByObject[objectId] = playerId;
        return new PickupResult(true, returned);
    }

    // ── リセット ──────────────────────────────────────────────────────────────

    /// <summary>
    /// プレイヤー退出時: 保有オブジェクトを初期配置に返す。
    /// 返したオブジェクト ID を返す（未保有なら null）。
    /// </summary>
    public string ReleasePlayer(string playerId)
    {
        if (playerId == null || !_heldByPlayer.TryGetValue(playerId, out var held))
            return null;

        _heldByPlayer.Remove(playerId);
        _holderByObject.Remove(held);
        return held;
    }

    /// <summary>
    /// ルーム終了時: 全プレイヤーの保有オブジェクトを初期配置に返す。
    /// 返したオブジェクト ID の一覧を返す。
    /// </summary>
    public IReadOnlyList<string> ReleaseAll()
    {
        var returned = new List<string>(_holderByObject.Keys);
        _heldByPlayer.Clear();
        _holderByObject.Clear();
        return returned;
    }

    // ── 入室時同期（world-creation.md セクション 9.9 と同方式） ────────────────

    /// <summary>入室同期用スナップショット（playerId → objectId）を取得する。</summary>
    public IReadOnlyDictionary<string, string> GetSnapshot() =>
        new Dictionary<string, string>(_heldByPlayer);

    /// <summary>オーナーから受信したスナップショットで保有状態を置き換える。</summary>
    public void ApplySnapshot(IReadOnlyDictionary<string, string> heldByPlayer)
    {
        _heldByPlayer.Clear();
        _holderByObject.Clear();
        if (heldByPlayer == null)
            return;

        foreach (var pair in heldByPlayer)
        {
            if (string.IsNullOrEmpty(pair.Key) || string.IsNullOrEmpty(pair.Value))
                continue;
            // 同一オブジェクトを複数プレイヤーが保有する壊れたデータは先勝ちで無視し、
            // _heldByPlayer と _holderByObject の 1:1 整合を維持する
            if (_holderByObject.ContainsKey(pair.Value))
                continue;
            _heldByPlayer[pair.Key] = pair.Value;
            _holderByObject[pair.Value] = pair.Key;
        }
    }
}
