using System.Collections.Generic;

/// <summary>
/// ギミックのインベントリ（オブジェクト保有）ロジッククラス（world-creation.md セクション 9.3）。
///
/// インベントリは**オブジェクト種別 ID 単位**で保持し、プレイヤーは同時に 1 種別・1 個のみ保有できる。
/// - 「持つ」（TryPickup）: 配置オブジェクト（インスタンス）がワールドから消え、その種別を保有する。
///   元のインスタンスは記録され、返却時に元の位置へ戻る
/// - 「付与する」（TryGrant）: 配置物を消費せず種別を直接保有する。返却時は単に消える
/// - 既に同一種別を保有しているときは何もしない。別種別保有中は既存を返却してから保有する
/// - 保有中の配置インスタンスは他プレイヤーは取得できないが、同一種別の別インスタンスは取得できる
/// - ルーム終了時またはプレイヤー退出時: 配置由来はワールド初期配置に戻り、付与品は消える
///
/// GimmickEngine の HasInventoryObject 条件に IInventoryQuery として注入する。
/// ワールド側の表示反映（消す / 戻す）は PickupObjectEffect を受けた上位レイヤーが行う。
/// </summary>
public class GimmickInventoryLogic : IInventoryQuery
{
    /// <summary>保有アイテム。SourceInstanceId が null の場合は付与品。</summary>
    public readonly struct HeldItem
    {
        public string TypeId { get; }
        public string SourceInstanceId { get; }
        public bool IsGranted => SourceInstanceId == null;

        public HeldItem(string typeId, string sourceInstanceId)
        {
            TypeId = typeId;
            SourceInstanceId = sourceInstanceId;
        }
    }

    /// <summary>「持つ」「付与する」反応の結果。</summary>
    public readonly struct PickupResult
    {
        /// <summary>保有に成功したか（同一種別保有済み・他プレイヤー保有中インスタンスは false）。</summary>
        public bool Success { get; }

        /// <summary>持ち替えで初期配置に返した配置インスタンス ID（付与品の返却・返却なしは null）。</summary>
        public string ReturnedInstanceId { get; }

        public PickupResult(bool success, string returnedInstanceId)
        {
            Success = success;
            ReturnedInstanceId = returnedInstanceId;
        }
    }

    private readonly Dictionary<string, HeldItem> _heldByPlayer = new(); // playerId → 保有アイテム
    private readonly Dictionary<string, string> _holderByInstance = new(); // 配置インスタンスID → playerId

    // ── IInventoryQuery ───────────────────────────────────────────────────────

    public bool HasObject(string playerId, string objectTypeId) =>
        !string.IsNullOrEmpty(playerId)
        && _heldByPlayer.TryGetValue(playerId, out var held)
        && held.TypeId == objectTypeId;

    // ── 参照 ──────────────────────────────────────────────────────────────────

    /// <summary>プレイヤーの保有アイテムを返す（未保有なら null）。</summary>
    public HeldItem? GetHeldItem(string playerId) =>
        playerId != null && _heldByPlayer.TryGetValue(playerId, out var held)
            ? held
            : (HeldItem?)null;

    /// <summary>配置インスタンスが誰かに保有されている（= ワールドから消えている）か。</summary>
    public bool IsInstanceHeld(string instanceId) =>
        instanceId != null && _holderByInstance.ContainsKey(instanceId);

    // ── 「持つ」（配置オブジェクト） ──────────────────────────────────────────

    /// <summary>
    /// プレイヤーが配置オブジェクトを持つ。インスタンスはワールドから消え、その種別を保有する。
    /// 同一種別を保有済み・インスタンスが保有中の場合は失敗する。
    /// </summary>
    public PickupResult TryPickup(string playerId, string instanceId, string typeId)
    {
        if (string.IsNullOrEmpty(playerId)
            || string.IsNullOrEmpty(instanceId)
            || string.IsNullOrEmpty(typeId))
            return new PickupResult(false, null);

        // 保有中のインスタンスはワールドに存在しないため取得できない
        if (_holderByInstance.ContainsKey(instanceId))
            return new PickupResult(false, null);

        // 既に同一種別を保有しているときは何もしない
        if (HasObject(playerId, typeId))
            return new PickupResult(false, null);

        string returned = ReleaseCurrent(playerId);
        _heldByPlayer[playerId] = new HeldItem(typeId, instanceId);
        _holderByInstance[instanceId] = playerId;
        return new PickupResult(true, returned);
    }

    // ── 「付与する」（種別直接） ──────────────────────────────────────────────

    /// <summary>
    /// プレイヤーに指定種別のオブジェクトを付与する。ワールドの配置物は消費しない。
    /// 同一種別を保有済みの場合は失敗する。
    /// </summary>
    public PickupResult TryGrant(string playerId, string typeId)
    {
        if (string.IsNullOrEmpty(playerId) || string.IsNullOrEmpty(typeId))
            return new PickupResult(false, null);

        if (HasObject(playerId, typeId))
            return new PickupResult(false, null);

        string returned = ReleaseCurrent(playerId);
        _heldByPlayer[playerId] = new HeldItem(typeId, null);
        return new PickupResult(true, returned);
    }

    // ── リセット ──────────────────────────────────────────────────────────────

    /// <summary>
    /// プレイヤー退出時: 保有アイテムを返却する。
    /// ワールドに戻すべき配置インスタンス ID を返す（付与品・未保有は null）。
    /// </summary>
    public string ReleasePlayer(string playerId)
    {
        if (string.IsNullOrEmpty(playerId))
            return null;
        return ReleaseCurrent(playerId);
    }

    /// <summary>
    /// ルーム終了時: 全プレイヤーの保有アイテムを返却する。
    /// ワールドに戻すべき配置インスタンス ID の一覧を返す（付与品は含まない）。
    /// </summary>
    public IReadOnlyList<string> ReleaseAll()
    {
        var returned = new List<string>(_holderByInstance.Keys);
        _heldByPlayer.Clear();
        _holderByInstance.Clear();
        return returned;
    }

    // ── 入室時同期（world-creation.md セクション 9.9） ─────────────────────────

    /// <summary>入室同期用スナップショット（playerId → 保有アイテム）を取得する。</summary>
    public IReadOnlyDictionary<string, HeldItem> GetSnapshot() =>
        new Dictionary<string, HeldItem>(_heldByPlayer);

    /// <summary>オーナーから受信したスナップショットで保有状態を置き換える。</summary>
    public void ApplySnapshot(IReadOnlyDictionary<string, HeldItem> heldByPlayer)
    {
        _heldByPlayer.Clear();
        _holderByInstance.Clear();
        if (heldByPlayer == null)
            return;

        foreach (var pair in heldByPlayer)
        {
            if (string.IsNullOrEmpty(pair.Key) || string.IsNullOrEmpty(pair.Value.TypeId))
                continue;

            string instanceId = pair.Value.SourceInstanceId;
            if (instanceId != null)
            {
                // 同一インスタンスを複数プレイヤーが保有する壊れたデータは先勝ちで無視し、
                // _heldByPlayer と _holderByInstance の整合を維持する
                if (_holderByInstance.ContainsKey(instanceId))
                    continue;
                _holderByInstance[instanceId] = pair.Key;
            }
            _heldByPlayer[pair.Key] = pair.Value;
        }
    }

    // ── Private ───────────────────────────────────────────────────────────────

    // 現在の保有アイテムを返却し、戻すべき配置インスタンス ID を返す（付与品・未保有は null）
    private string ReleaseCurrent(string playerId)
    {
        if (!_heldByPlayer.TryGetValue(playerId, out var held))
            return null;

        _heldByPlayer.Remove(playerId);
        if (held.SourceInstanceId != null)
            _holderByInstance.Remove(held.SourceInstanceId);
        return held.SourceInstanceId;
    }
}
