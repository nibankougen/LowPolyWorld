using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 特殊オブジェクト（スポーン位置・各種ポータル）のコライダー AABB を地形/オブジェクト/相互に
/// 重ねたまま配置していないか判定する純粋 C# ロジック
/// （world-creation.md セクション 7「スポーン位置・ポータルの重複ルール」/ screens-and-modes.md 11.7.3）。
///
/// - 全特殊オブジェクトのコライダーは 1m × 1.5m × 1m・原点 (0.5, 0, 0.5)（XZ 中央・Y 最下部）
/// - 位置は 0.5m グリッド単位（IntVec3Json）。回転は無視する（1×1 の正方形フットプリント近似）
/// - 重複していると警告フラッシュ表示＋プレイ/公開不可（公開判定は WorldPublishValidator が使用）
/// - 地形/通常オブジェクトとの重複は <see cref="IWorldOccupancyQuery"/> 経由で問い合わせる
///   （Unity 実装＝地形ボクセル占有＋配置オブジェクトのコライダー は別途。null なら特殊同士のみ判定）
/// </summary>
public static class SpecialObjectOverlap
{
    public const float FootprintXZ = 1f;   // コライダー XZ 幅（m）
    public const float Height = 1.5f;      // コライダー高さ（m）
    public const float GridSize = 0.5f;    // 1 グリッド = 0.5m

    private const float HalfXZ = FootprintXZ * 0.5f;

    public enum Kind
    {
        Spawn,        // スポーン位置
        PortalEntry,  // ルーム内入口ポータル（白）
        PortalExit,   // ルーム内出口ポータル（黒）
        WorldPortal,  // ワールドポータル
    }

    /// <summary>1 つの特殊オブジェクトのコライダー（種別・ID・ワールド AABB）。</summary>
    public readonly struct Box
    {
        public readonly Kind Kind;
        public readonly string Id;   // PortalEntry/Exit = entryId/exitId・WorldPortal = instanceId・Spawn = "spawn"
        public readonly Vector3 Min;
        public readonly Vector3 Max;

        public Box(Kind kind, string id, Vector3 min, Vector3 max)
        {
            Kind = kind;
            Id = id;
            Min = min;
            Max = max;
        }

        /// <summary>AABB 同士が重なるか（半開区間 [min, max)。面で接するだけは重ならない扱い）。</summary>
        public bool Overlaps(in Box o) =>
            Min.x < o.Max.x && Max.x > o.Min.x &&
            Min.y < o.Max.y && Max.y > o.Min.y &&
            Min.z < o.Max.z && Max.z > o.Min.z;
    }

    /// <summary>グリッド位置（XZ 中央・Y 最下部のアンカー）からワールド AABB（m）を求める。</summary>
    public static void BoxFromGrid(IntVec3Json pos, out Vector3 min, out Vector3 max)
    {
        float cx = pos.x * GridSize;
        float by = pos.y * GridSize;
        float cz = pos.z * GridSize;
        min = new Vector3(cx - HalfXZ, by, cz - HalfXZ);
        max = new Vector3(cx + HalfXZ, by + Height, cz + HalfXZ);
    }

    /// <summary>
    /// ワールド定義から全特殊オブジェクトのコライダーを列挙する（配置順。spawn は isSet のときのみ）。
    /// ルーム内ポータルは入口・出口の 2 つを別々のコライダーとして数える。エリアは重複ルール対象外。
    /// </summary>
    public static List<Box> Collect(WorldDefinitionJson def)
    {
        var boxes = new List<Box>();
        var so = def?.specialObjects;
        if (so == null)
            return boxes;

        if (so.spawn != null && so.spawn.isSet)
        {
            BoxFromGrid(so.spawn.position, out var mn, out var mx);
            boxes.Add(new Box(Kind.Spawn, "spawn", mn, mx));
        }
        if (so.portals != null)
        {
            foreach (var p in so.portals)
            {
                BoxFromGrid(p.entryPosition, out var emn, out var emx);
                boxes.Add(new Box(Kind.PortalEntry, p.entryId, emn, emx));
                BoxFromGrid(p.exitPosition, out var xmn, out var xmx);
                boxes.Add(new Box(Kind.PortalExit, p.exitId, xmn, xmx));
            }
        }
        if (so.worldPortals != null)
        {
            foreach (var wp in so.worldPortals)
            {
                BoxFromGrid(wp.position, out var mn, out var mx);
                boxes.Add(new Box(Kind.WorldPortal, wp.instanceId, mn, mx));
            }
        }
        return boxes;
    }

    /// <summary>
    /// 重複している特殊オブジェクトのコライダー一覧を配置順で返す（空なら重複なし）。
    /// 特殊オブジェクト同士の重複に加え、<paramref name="occupancy"/> が指定されていれば
    /// 地形・通常オブジェクトとの重複も判定する。
    /// </summary>
    public static List<Box> FindOverlapping(WorldDefinitionJson def, IWorldOccupancyQuery occupancy = null)
    {
        var boxes = Collect(def);
        var flagged = new bool[boxes.Count];

        for (int i = 0; i < boxes.Count; i++)
            for (int j = i + 1; j < boxes.Count; j++)
                if (boxes[i].Overlaps(boxes[j]))
                {
                    flagged[i] = true;
                    flagged[j] = true;
                }

        if (occupancy != null)
            for (int i = 0; i < boxes.Count; i++)
                if (!flagged[i] && occupancy.OverlapsSolid(boxes[i].Min, boxes[i].Max))
                    flagged[i] = true;

        var result = new List<Box>();
        for (int i = 0; i < boxes.Count; i++)
            if (flagged[i])
                result.Add(boxes[i]);
        return result;
    }

    /// <summary>重複している特殊オブジェクトが 1 つでもあるか（公開/プレイ可否判定用）。</summary>
    public static bool HasOverlap(WorldDefinitionJson def, IWorldOccupancyQuery occupancy = null) =>
        FindOverlapping(def, occupancy).Count > 0;
}

/// <summary>
/// 特殊オブジェクトのコライダー（ワールド AABB・m）が地形ボクセルまたは通常オブジェクトの
/// コライダーと重なるかを問い合わせるインターフェース。
/// Unity 実装（地形ボクセル占有 + 配置オブジェクトのコライダー）は別レイヤーで行う。
/// </summary>
public interface IWorldOccupancyQuery
{
    /// <summary>ワールド AABB [min, max)（m）が地形または通常オブジェクトと重なるか。</summary>
    bool OverlapsSolid(Vector3 min, Vector3 max);
}
