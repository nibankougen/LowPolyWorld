using UnityEngine;

/// <summary>
/// 特殊オブジェクト（スポーン位置・ポータル）の配置グリッド → プレイヤー出現位置・向きの解決
/// （world-creation.md セクション 8）。純粋 C#。
///
/// 「いつ転送するか」（入口ポータルへの接触検知・リスポーン判定）は物理/ランタイム層の責務。
/// ここは「どこへ・どの向きで出現するか」の解決のみを担う。次の 2 経路から使う:
/// - 組み込みポータル転送: 入口ポータルに触れたプレイヤーを、ペアの出口へ（<see cref="TryGetEntryExitTarget"/>）
/// - ギミック <c>TeleportPlayerEffect(playerId, exitPortalId)</c>: 出口 ID で転送先を解決（<see cref="TryResolveExitPortal"/>）
///
/// 出現点 = コライダー原点 (0.5, 0, 0.5)（XZ 中央・Y 最下部 — 8 章）。位置は 0.5m グリッド（原点中心）
/// なので world = grid · 0.5m。向き（rotationY）は配置共通の 45° 単位段数（0〜7）として度に変換する。
/// </summary>
public static class SpecialObjectTransform
{
    /// <summary>プレイヤーの出現位置と向き（度）。</summary>
    public readonly struct SpawnTarget
    {
        public readonly Vector3 Position;
        public readonly float FacingDegrees;

        public SpawnTarget(Vector3 position, float facingDegrees)
        {
            Position = position;
            FacingDegrees = facingDegrees;
        }
    }

    /// <summary>配置グリッド位置 → 出現ワールド座標（コライダー原点 = XZ 中央・Y 最下部）。</summary>
    public static Vector3 ResolveWorldPosition(IntVec3Json gridPos) =>
        gridPos == null ? Vector3.zero : gridPos.ToVector3(ObjectGridSnap.PositionUnit);

    /// <summary>向き段数（0〜7・45° 単位） → 度。</summary>
    public static float ResolveFacingDegrees(int rotationY) => ObjectGridSnap.RotationToDegrees(rotationY);

    /// <summary>
    /// スポーン位置（入場時・リスポーン時のプレイヤー出現先）を解決する。
    /// 未設定（isSet=false）なら false。
    /// </summary>
    public static bool TryGetSpawn(WorldDefinitionJson def, out SpawnTarget target)
    {
        target = default;
        var spawn = def?.specialObjects?.spawn;
        if (spawn == null || !spawn.isSet)
            return false;
        target = new SpawnTarget(ResolveWorldPosition(spawn.position), ResolveFacingDegrees(spawn.rotationY));
        return true;
    }

    /// <summary>
    /// 出口ポータル ID から転送先を解決する（ギミック <c>TeleportPlayerEffect</c> 用）。
    /// 該当する出口を持つポータルが無い・出口未設定なら false。
    /// </summary>
    public static bool TryResolveExitPortal(WorldDefinitionJson def, string exitId, out SpawnTarget target)
    {
        target = default;
        if (string.IsNullOrEmpty(exitId))
            return false;
        var portals = def?.specialObjects?.portals;
        if (portals == null)
            return false;
        foreach (var p in portals)
        {
            if (p != null && p.exitId == exitId)
            {
                target = new SpawnTarget(
                    ResolveWorldPosition(p.exitPosition), ResolveFacingDegrees(p.exitRotationY));
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 入口ポータル ID から、ペアの出口への転送先を解決する（組み込みポータル転送用）。
    /// 該当する入口が無い・その入口に出口が未設定なら false。
    /// </summary>
    public static bool TryGetEntryExitTarget(WorldDefinitionJson def, string entryId, out SpawnTarget target)
    {
        target = default;
        if (string.IsNullOrEmpty(entryId))
            return false;
        var portals = def?.specialObjects?.portals;
        if (portals == null)
            return false;
        foreach (var p in portals)
        {
            if (p != null && p.entryId == entryId)
            {
                if (string.IsNullOrEmpty(p.exitId))
                    return false; // 出口未設定（公開バリデーションで弾かれる状態）
                target = new SpawnTarget(
                    ResolveWorldPosition(p.exitPosition), ResolveFacingDegrees(p.exitRotationY));
                return true;
            }
        }
        return false;
    }
}
