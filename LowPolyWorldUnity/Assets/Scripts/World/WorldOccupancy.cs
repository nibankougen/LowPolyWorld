using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// <see cref="IWorldOccupancyQuery"/> の純粋 C# 実装群（地形ボクセル占有 + 配置オブジェクトのコライダー）。
/// <see cref="SpecialObjectOverlap"/> がスポーン/ポータルの重複判定（world-creation.md セクション 7）で使う。
///
/// すべてワールド座標（メートル）の軸平行 AABB [min, max) を扱う。面で接するだけ（半開区間）は
/// 重ならない扱い。座標系の差異（地形ボクセルは 0 起点・オブジェクト/特殊は原点中心グリッド）は
/// 地形側の <c>worldOrigin</c>（ボクセル (0,0,0) の最小角のワールド座標）で吸収する。
/// </summary>
public static class WorldOccupancy
{
    /// <summary>
    /// 配置オブジェクト 1 個のコライダー AABB（ワールド・メートル）を求める。
    /// 装飾オブジェクト（コライダー寸法が 0）は false を返す。
    ///
    /// - 配置基準点 = 底面中心（world-creation.md 3.4）。位置は 0.5m グリッド（原点中心）。
    /// - コライダー寸法はサイズグリッド（0.25m 単位・W=x, D=y, H=z）。size がセンチネル (0,0,0) の
    ///   ときは <paramref name="defaultSize"/>（種別デフォルト）で解決する。両方 0 なら装飾＝コライダーなし。
    /// - コライダーは軸平行のまま回転に追従しない（world-creation.md 3.3）。rotationY は無視する。
    /// </summary>
    public static bool TryGetObjectBox(
        WorldObjectInstance obj, IntVec3Json defaultSize, out Vector3 min, out Vector3 max)
    {
        min = default;
        max = default;
        if (obj == null)
            return false;

        // コライダー寸法: 明示サイズ優先・センチネル時は種別デフォルト。両方 0/null は装飾（コライダーなし）。
        IntVec3Json colliderSize = obj.size != null && !obj.size.IsZero ? obj.size : defaultSize;
        if (colliderSize == null || colliderSize.IsZero)
            return false;

        float pos = ObjectGridSnap.PositionUnit;       // 0.5m
        float unit = ObjectPlaceholderTransform.SizeUnit; // 0.25m
        float halfW = colliderSize.x * unit * 0.5f; // W → X
        float halfD = colliderSize.y * unit * 0.5f; // D → Z
        float height = colliderSize.z * unit;       // H → Y

        float cx = obj.position.x * pos;
        float by = obj.position.y * pos; // 底面（Y 最下部）
        float cz = obj.position.z * pos;

        min = new Vector3(cx - halfW, by, cz - halfD);
        max = new Vector3(cx + halfW, by + height, cz + halfD);
        return true;
    }
}

/// <summary>
/// 地形ボクセルの占有を問い合わせる <see cref="IWorldOccupancyQuery"/> 実装（純粋 C#）。
/// 非 empty のボクセルはすべて 0.5m の立方体として占有しているとみなす保守的判定
/// （坂・斜め・角・凹角の欠けた部分も占有扱い。地形へのめり込み警告は過剰側に倒す）。
/// </summary>
public class TerrainOccupancyQuery : IWorldOccupancyQuery
{
    private readonly TerrainVoxelStore _store;
    private readonly Vector3 _worldOrigin; // ボクセル (0,0,0) の最小角のワールド座標

    /// <param name="store">地形ボクセルストア。</param>
    /// <param name="worldOrigin">
    /// ボクセル (0,0,0) の最小角（local 原点）が置かれるワールド座標。
    /// ボクセル (x,y,z) は world [origin + (x,y,z)·0.5, origin + (x+1,y+1,z+1)·0.5) を占有する。
    /// </param>
    public TerrainOccupancyQuery(TerrainVoxelStore store, Vector3 worldOrigin)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _worldOrigin = worldOrigin;
    }

    public bool OverlapsSolid(Vector3 min, Vector3 max)
    {
        const float b = TerrainMeshBuilder.BlockSize; // 0.5m

        // ワールド AABB → 地形 local → ボクセルインデックス範囲（半開区間で接面を除外）。
        var lo = (min - _worldOrigin) / b;
        var hi = (max - _worldOrigin) / b;

        int x0 = Mathf.Max(0, Mathf.FloorToInt(lo.x));
        int y0 = Mathf.Max(0, Mathf.FloorToInt(lo.y));
        int z0 = Mathf.Max(0, Mathf.FloorToInt(lo.z));
        // 上端は半開: ちょうど境界に接するインデックスは含めない（CeilToInt - 1）。
        int x1 = Mathf.Min(TerrainVoxelStore.SizeX - 1, Mathf.CeilToInt(hi.x) - 1);
        int y1 = Mathf.Min(TerrainVoxelStore.SizeY - 1, Mathf.CeilToInt(hi.y) - 1);
        int z1 = Mathf.Min(TerrainVoxelStore.SizeZ - 1, Mathf.CeilToInt(hi.z) - 1);

        for (int y = y0; y <= y1; y++)
            for (int z = z0; z <= z1; z++)
                for (int x = x0; x <= x1; x++)
                    if (!TerrainVoxel.IsEmpty(_store.GetVoxel(x, y, z)))
                        return true;
        return false;
    }
}

/// <summary>
/// 配置オブジェクトのコライダー占有を問い合わせる <see cref="IWorldOccupancyQuery"/> 実装（純粋 C#）。
/// 装飾オブジェクト（コライダーなし）は除外し、各オブジェクトの軸平行 AABB を事前計算して保持する。
/// </summary>
public class WorldObjectOccupancyQuery : IWorldOccupancyQuery
{
    private readonly List<(Vector3 min, Vector3 max)> _boxes = new List<(Vector3, Vector3)>();

    /// <summary>事前計算済みのコライダー AABB 一覧から構築する。</summary>
    public WorldObjectOccupancyQuery(IEnumerable<(Vector3 min, Vector3 max)> boxes)
    {
        if (boxes != null)
            _boxes.AddRange(boxes);
    }

    /// <summary>
    /// ワールド定義の配置オブジェクトから構築する。
    /// <paramref name="defaultSizeOf"/> は objectTypeId → 種別デフォルトサイズ（0.25m 単位・
    /// 未知/装飾は null または (0,0,0)）を返す。装飾オブジェクトは自動的に除外される。
    /// </summary>
    public static WorldObjectOccupancyQuery FromDefinition(
        WorldDefinitionJson def, Func<string, IntVec3Json> defaultSizeOf)
    {
        var boxes = new List<(Vector3, Vector3)>();
        if (def?.objects != null)
        {
            foreach (var obj in def.objects)
            {
                var defaultSize = defaultSizeOf?.Invoke(obj.objectTypeId);
                if (WorldOccupancy.TryGetObjectBox(obj, defaultSize, out var mn, out var mx))
                    boxes.Add((mn, mx));
            }
        }
        return new WorldObjectOccupancyQuery(boxes);
    }

    public bool OverlapsSolid(Vector3 min, Vector3 max)
    {
        foreach (var (bmin, bmax) in _boxes)
            if (AabbOverlap(min, max, bmin, bmax))
                return true;
        return false;
    }

    // 半開区間 AABB 交差（面で接するだけは重ならない）。
    private static bool AabbOverlap(Vector3 amin, Vector3 amax, Vector3 bmin, Vector3 bmax) =>
        amin.x < bmax.x && amax.x > bmin.x &&
        amin.y < bmax.y && amax.y > bmin.y &&
        amin.z < bmax.z && amax.z > bmin.z;
}

/// <summary>
/// 複数の <see cref="IWorldOccupancyQuery"/> を OR で合成する（いずれかが重なれば重なり）。
/// 地形 + オブジェクトをまとめて 1 つのクエリとして <see cref="SpecialObjectOverlap"/> に渡すのに使う。
/// </summary>
public class CompositeOccupancyQuery : IWorldOccupancyQuery
{
    private readonly IWorldOccupancyQuery[] _queries;

    public CompositeOccupancyQuery(params IWorldOccupancyQuery[] queries)
    {
        _queries = queries ?? Array.Empty<IWorldOccupancyQuery>();
    }

    public bool OverlapsSolid(Vector3 min, Vector3 max)
    {
        foreach (var q in _queries)
            if (q != null && q.OverlapsSolid(min, max))
                return true;
        return false;
    }
}
