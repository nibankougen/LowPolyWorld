using System.Collections.Generic;

/// <summary>
/// 数字オブジェクトの値参照を解決するロジッククラス（world-creation.md セクション 3.9）。
///
/// - 配置上限: 1 ワールドにつき 30 個
/// - 参照元: ワールドステート / ルーム参加 X 番目のプレイヤーのステート / 固定値
/// - ステート更新時、そのステートを参照している数字オブジェクトを特定して即座に表示を更新する
///
/// プレミアム限定の配置制限はワールドエディタ側（PlanCapabilities）で行う。
/// メッシュ・表示の更新は本クラスが返す影響オブジェクト一覧を受けた上位レイヤーが行う。
/// </summary>
public class NumberObjectSyncLogic
{
    public const int MaxNumberObjects = 30;

    public enum SourceKind
    {
        WorldState,
        PlayerState,
        Fixed,
    }

    public class NumberObjectDefinition
    {
        public string ObjectId { get; }
        public SourceKind Source { get; }
        public int StateIndex { get; }    // WorldState / PlayerState 用
        public int PlayerNumber { get; }  // PlayerState 用（ルーム参加順・1 起点）
        public int FixedValue { get; }    // Fixed 用

        public NumberObjectDefinition(
            string objectId,
            SourceKind source,
            int stateIndex = 0,
            int playerNumber = 1,
            int fixedValue = 0)
        {
            ObjectId = objectId ?? "";
            Source = source;
            StateIndex = stateIndex;
            PlayerNumber = playerNumber;
            FixedValue = fixedValue;
        }
    }

    private readonly List<NumberObjectDefinition> _objects = new();
    private readonly Dictionary<string, NumberObjectDefinition> _byId = new();

    public IReadOnlyList<NumberObjectDefinition> Objects => _objects;

    // ── 登録・削除 ────────────────────────────────────────────────────────────

    /// <summary>
    /// 数字オブジェクトを登録する。上限 30 個超過・ID 重複・空 ID・
    /// 参照インデックス範囲外（不正なワールド定義 JSON）の場合は false。
    /// </summary>
    public bool TryAdd(NumberObjectDefinition def)
    {
        if (def == null || string.IsNullOrEmpty(def.ObjectId))
            return false;
        if (_objects.Count >= MaxNumberObjects)
            return false;
        if (_byId.ContainsKey(def.ObjectId))
            return false;
        if (!IsValidSource(def))
            return false;

        _objects.Add(def);
        _byId[def.ObjectId] = def;
        return true;
    }

    // 参照インデックスを登録時に検証する。ワールド定義 JSON は UGC 由来のため、
    // 不正な定義を弾いて ResolveValue が GimmickStateManager の範囲外例外を
    // 起こさないようにする。
    private static bool IsValidSource(NumberObjectDefinition def) =>
        def.Source switch
        {
            SourceKind.WorldState =>
                (uint)def.StateIndex < GimmickStateManager.MaxWorldStates,
            SourceKind.PlayerState =>
                (uint)def.StateIndex < GimmickStateManager.MaxPlayerStates && def.PlayerNumber >= 1,
            SourceKind.Fixed => true,
            _ => false,
        };

    public bool Remove(string objectId)
    {
        if (objectId == null || !_byId.TryGetValue(objectId, out var def))
            return false;
        _byId.Remove(objectId);
        _objects.Remove(def);
        return true;
    }

    // ── 表示値の解決 ──────────────────────────────────────────────────────────

    /// <summary>
    /// 数字オブジェクトの現在の表示値を解決する。
    /// 参照先プレイヤーが不在（参加順番号超過）の場合は 0 を返す。
    /// </summary>
    public int ResolveValue(
        string objectId, GimmickStateManager state, IReadOnlyList<string> playerIds)
    {
        if (objectId == null || !_byId.TryGetValue(objectId, out var def))
            return 0;

        switch (def.Source)
        {
            case SourceKind.WorldState:
                return state.GetWorldState(def.StateIndex);

            case SourceKind.PlayerState:
            {
                int index = def.PlayerNumber - 1; // 1 起点 → 0 起点
                if (playerIds == null || index < 0 || index >= playerIds.Count)
                    return 0;
                return state.GetPlayerState(playerIds[index], def.StateIndex);
            }

            case SourceKind.Fixed:
                return def.FixedValue;

            default:
                return 0;
        }
    }

    // ── ステート更新時の影響オブジェクト特定 ──────────────────────────────────

    /// <summary>ワールドステート更新時: そのステートを参照する数字オブジェクト ID を返す。</summary>
    public IReadOnlyList<string> GetAffectedByWorldState(int stateIndex)
    {
        var affected = new List<string>();
        foreach (var def in _objects)
        {
            if (def.Source == SourceKind.WorldState && def.StateIndex == stateIndex)
                affected.Add(def.ObjectId);
        }
        return affected;
    }

    /// <summary>
    /// プレイヤーステート更新時: そのプレイヤー・ステートを参照する数字オブジェクト ID を返す。
    /// </summary>
    public IReadOnlyList<string> GetAffectedByPlayerState(
        string playerId, int stateIndex, IReadOnlyList<string> playerIds)
    {
        var affected = new List<string>();
        if (playerId == null || playerIds == null)
            return affected;

        foreach (var def in _objects)
        {
            if (def.Source != SourceKind.PlayerState || def.StateIndex != stateIndex)
                continue;

            int index = def.PlayerNumber - 1;
            if (index >= 0 && index < playerIds.Count && playerIds[index] == playerId)
                affected.Add(def.ObjectId);
        }
        return affected;
    }

    /// <summary>
    /// 状態リセットや入室時スナップショット適用後など、全数字オブジェクトの
    /// 再表示が必要なときに使う ID 一覧。
    /// </summary>
    public IReadOnlyList<string> GetAllObjectIds()
    {
        var ids = new List<string>(_objects.Count);
        foreach (var def in _objects)
            ids.Add(def.ObjectId);
        return ids;
    }
}
