using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 地形コライダー 1 個分の直方体（BoxCollider の center / size に対応）。
/// 座標は store グリッド基準の Unity 単位（1 ブロック = 0.5m）。
/// </summary>
public readonly struct TerrainColliderBox
{
    public readonly Vector3 Center;
    public readonly Vector3 Size;

    public TerrainColliderBox(Vector3 center, Vector3 size)
    {
        Center = center;
        Size = size;
    }

    public static TerrainColliderBox FromMinSize(Vector3 min, Vector3 size) =>
        new TerrainColliderBox(min + size * 0.5f, size);

    public Vector3 Min => Center - Size * 0.5f;
    public Vector3 Max => Center + Size * 0.5f;
}

/// <summary>
/// チャンク単位の地形コライダー生成ロジック（world-creation.md セクション 15.15）。
///
/// - cube はグリーディーメッシュ法で結合する（Y 層ごとに X → Z の順で XZ 矩形に結合し、
///   同一 XZ 矩形が連続する Y 層で共通なら Y 方向にも結合。厳密な最適解は求めない）
/// - ramp は高さ方向の階段近似（薄い水平 BoxCollider 4 段・段差 0.125m）
/// - diag は XZ 平面の階段近似（solid 領域に内接する全高 BoxCollider 3 個・phantom wall 回避の内側近似）
/// - コライダーはチャンク内完結（チャンクをまたぐ結合は行わない）
/// - BoxCollider への適用と CharacterController.stepOffset (0.26m) 設定は MonoBehaviour 層が行う
/// </summary>
public class TerrainColliderBuilder
{
    public const float BlockSize = 0.5f;

    /// <summary>坂の階段近似の段数（段差 0.125m &lt; stepOffset 0.26m）。</summary>
    public const int RampStepCount = 4;

    /// <summary>diag の XZ 階段近似の分割数（最後の段は奥行き 0 のため box は 3 個）。</summary>
    public const int DiagStepCount = 4;

    /// <summary>角（四面体）の高さ段数（最上段は断面 0 のため box は 3 個）。</summary>
    public const int CornerStepCount = 4;

    /// <summary>各段差を乗り越えるために必要な CharacterController.stepOffset。</summary>
    public const float RequiredStepOffset = 0.26f;

    /// <summary>指定チャンクのコライダーボックス一覧を生成する。</summary>
    public List<TerrainColliderBox> BuildChunk(ITerrainVoxelSampler sampler, int cx, int cy, int cz)
    {
        if (sampler == null)
            throw new ArgumentNullException(nameof(sampler));
        if (!TerrainVoxelStore.ChunkInBounds(cx, cy, cz))
            throw new ArgumentOutOfRangeException(nameof(cx), $"チャンク座標が範囲外です: ({cx}, {cy}, {cz})");

        var result = new List<TerrainColliderBox>();
        int ox = cx * TerrainChunk.Size;
        int oy = cy * TerrainChunk.Size;
        int oz = cz * TerrainChunk.Size;

        AddMergedCubeBoxes(result, sampler, ox, oy, oz);
        AddStairBoxes(result, sampler, ox, oy, oz);
        return result;
    }

    // ── cube のグリーディー結合 ───────────────────────────────────────────────

    private static void AddMergedCubeBoxes(
        List<TerrainColliderBox> result, ITerrainVoxelSampler sampler, int ox, int oy, int oz)
    {
        // (x0, z0, w, d) の XZ 矩形 → (yStart, height)
        var open = new Dictionary<(int x0, int z0, int w, int d), (int yStart, int height)>();
        var cube = new bool[TerrainChunk.Size, TerrainChunk.Size];

        for (int ly = 0; ly < TerrainChunk.Size; ly++)
        {
            for (int lz = 0; lz < TerrainChunk.Size; lz++)
                for (int lx = 0; lx < TerrainChunk.Size; lx++)
                    cube[lx, lz] =
                        TerrainVoxel.GetShape(sampler.GetVoxel(ox + lx, oy + ly, oz + lz)) == TerrainShape.Cube;

            var next = new Dictionary<(int, int, int, int), (int, int)>();
            foreach (var rect in GreedyRects(cube))
            {
                if (open.TryGetValue(rect, out var box))
                {
                    next[rect] = (box.yStart, box.height + 1); // 同一矩形が連続する Y 層 → Y 結合
                    open.Remove(rect);
                }
                else
                {
                    next[rect] = (ly, 1);
                }
            }
            foreach (var kv in open)
                result.Add(MakeCubeBox(ox, oy, oz, kv.Key, kv.Value));
            open = next;
        }
        foreach (var kv in open)
            result.Add(MakeCubeBox(ox, oy, oz, kv.Key, kv.Value));
    }

    /// <summary>1 層分の cube セルを X 方向優先 → Z 方向の順で矩形に結合する。</summary>
    private static List<(int x0, int z0, int w, int d)> GreedyRects(bool[,] cube)
    {
        var rects = new List<(int, int, int, int)>();
        var consumed = new bool[TerrainChunk.Size, TerrainChunk.Size];

        for (int z = 0; z < TerrainChunk.Size; z++)
        {
            for (int x = 0; x < TerrainChunk.Size; x++)
            {
                if (!cube[x, z] || consumed[x, z])
                    continue;

                int w = 1;
                while (x + w < TerrainChunk.Size && cube[x + w, z] && !consumed[x + w, z])
                    w++;

                int d = 1;
                while (z + d < TerrainChunk.Size && RowIsFree(cube, consumed, x, w, z + d))
                    d++;

                for (int dz = 0; dz < d; dz++)
                    for (int dx = 0; dx < w; dx++)
                        consumed[x + dx, z + dz] = true;

                rects.Add((x, z, w, d));
            }
        }
        return rects;
    }

    private static bool RowIsFree(bool[,] cube, bool[,] consumed, int x, int w, int z)
    {
        for (int dx = 0; dx < w; dx++)
            if (!cube[x + dx, z] || consumed[x + dx, z])
                return false;
        return true;
    }

    private static TerrainColliderBox MakeCubeBox(
        int ox, int oy, int oz, (int x0, int z0, int w, int d) rect, (int yStart, int height) span)
    {
        var min = new Vector3(ox + rect.x0, oy + span.yStart, oz + rect.z0) * BlockSize;
        var size = new Vector3(rect.w, span.height, rect.d) * BlockSize;
        return TerrainColliderBox.FromMinSize(min, size);
    }

    // ── ramp / diag の階段近似 ────────────────────────────────────────────────

    private static void AddStairBoxes(
        List<TerrainColliderBox> result, ITerrainVoxelSampler sampler, int ox, int oy, int oz)
    {
        for (int ly = 0; ly < TerrainChunk.Size; ly++)
        {
            for (int lz = 0; lz < TerrainChunk.Size; lz++)
            {
                for (int lx = 0; lx < TerrainChunk.Size; lx++)
                {
                    int x = ox + lx;
                    int y = oy + ly;
                    int z = oz + lz;
                    switch (TerrainVoxel.GetShape(sampler.GetVoxel(x, y, z)))
                    {
                        case TerrainShape.RampN:
                            AddRampStairs(result, x, y, z, 0);
                            break;
                        case TerrainShape.RampE:
                            AddRampStairs(result, x, y, z, 1);
                            break;
                        case TerrainShape.RampS:
                            AddRampStairs(result, x, y, z, 2);
                            break;
                        case TerrainShape.RampW:
                            AddRampStairs(result, x, y, z, 3);
                            break;
                        case TerrainShape.DiagNW:
                            AddDiagStairs(result, x, y, z, 0);
                            break;
                        case TerrainShape.DiagNE:
                            AddDiagStairs(result, x, y, z, 1);
                            break;
                        case TerrainShape.DiagSE:
                            AddDiagStairs(result, x, y, z, 2);
                            break;
                        case TerrainShape.DiagSW:
                            AddDiagStairs(result, x, y, z, 3);
                            break;
                        case TerrainShape.CornerNW:
                            AddCornerStairs(result, x, y, z, 0);
                            break;
                        case TerrainShape.CornerNE:
                            AddCornerStairs(result, x, y, z, 1);
                            break;
                        case TerrainShape.CornerSE:
                            AddCornerStairs(result, x, y, z, 2);
                            break;
                        case TerrainShape.CornerSW:
                            AddCornerStairs(result, x, y, z, 3);
                            break;
                        case TerrainShape.ConcaveNW:
                            AddConcaveStairs(result, x, y, z, 0);
                            break;
                        case TerrainShape.ConcaveNE:
                            AddConcaveStairs(result, x, y, z, 1);
                            break;
                        case TerrainShape.ConcaveSE:
                            AddConcaveStairs(result, x, y, z, 2);
                            break;
                        case TerrainShape.ConcaveSW:
                            AddConcaveStairs(result, x, y, z, 3);
                            break;
                    }
                }
            }
        }
    }

    /// <summary>
    /// 坂の階段近似。canonical（ramp_N = North 側が高い）では段 i（低い側から）が
    /// z [i/4, (i+1)/4]・y [i/4, (i+1)/4]・x 全幅の薄い水平ボックスになる。
    /// 段の上面はその区間の斜面の高い側の高さに一致する。
    /// </summary>
    private static void AddRampStairs(List<TerrainColliderBox> result, int x, int y, int z, int k)
    {
        for (int i = 0; i < RampStepCount; i++)
        {
            float f0 = (float)i / RampStepCount;
            float f1 = (float)(i + 1) / RampStepCount;
            var (rx0, rz0, rx1, rz1) = RotRect(0f, f0, 1f, f1, k);
            result.Add(MakeLocalBox(x, y, z, rx0, f0, rz0, rx1, f1, rz1));
        }
    }

    /// <summary>
    /// diag の XZ 階段近似。canonical（diag_NW = solid 領域 z ≥ x）では段 i が
    /// x [i/4, (i+1)/4]・z [(i+1)/4, 1]・全高のボックスになる（solid に内接する内側近似）。
    /// 最後の段は奥行き 0 になるためスキップする。
    /// </summary>
    private static void AddDiagStairs(List<TerrainColliderBox> result, int x, int y, int z, int k)
    {
        for (int i = 0; i < DiagStepCount; i++)
        {
            float f0 = (float)i / DiagStepCount;
            float f1 = (float)(i + 1) / DiagStepCount;
            if (f1 >= 1f)
                continue;
            var (rx0, rz0, rx1, rz1) = RotRect(f0, f1, f1, 1f, k);
            result.Add(MakeLocalBox(x, y, z, rx0, 0f, rz0, rx1, 1f, rz1));
        }
    }

    /// <summary>
    /// 角（四面体）の階段近似。ramp の高さ階段と diag の内側近似の合成。
    /// canonical（CornerNW = 高頂点 NW 上角・solid 領域は z ≥ x + y）では、高さ段 i（薄い水平ボックス・
    /// 厚さ 0.125m）の上端 y = (i+1)/N における断面（高い NW 角へ縮む直角三角形）に内接する
    /// 軸平行ボックスを配置する。段上端の小さい断面を使うため段全体が solid に収まり phantom wall を作らない。
    /// 上面が斜面に沿って NW へ昇る階段になる。最上段は断面 0 のためスキップする（box は 3 個）。
    /// </summary>
    private static void AddCornerStairs(List<TerrainColliderBox> result, int x, int y, int z, int k)
    {
        for (int i = 0; i < CornerStepCount; i++)
        {
            float f0 = (float)i / CornerStepCount;
            float f1 = (float)(i + 1) / CornerStepCount;
            float len = 1f - f1; // 段上端での三角形断面の脚の長さ
            if (len <= 0f)
                continue;
            float half = len * 0.5f; // 直角三角形に内接する軸平行ボックスの辺（遠端が斜辺に接する）
            // canonical 断面: x ∈ [0, half], z ∈ [1−half, 1]（NW 角に内接）
            var (rx0, rz0, rx1, rz1) = RotRect(0f, 1f - half, half, 1f, k);
            result.Add(MakeLocalBox(x, y, z, rx0, f0, rz0, rx1, f1, rz1));
        }
    }

    /// <summary>
    /// 凹角（内角）の階段近似。canonical（ConcaveNW = NW 上角を切り落とし・solid 領域は y ≤ x − z + 1）では、
    /// 高さ段 i の上端 y = (i+1)/N における断面（z ≤ x + g、g = 1 − y）に内接する 2 個の軸平行ボックス
    /// （South 帯 x[0,1]×z[0,g] と East 帯 x[1−g,1]×z[0,1]）を配置する。段上端の最小断面を使うため
    /// solid に収まり phantom wall を作らない。最上段は g = 0 で断面が消えるためスキップする（box は最大 6 個）。
    /// </summary>
    private static void AddConcaveStairs(List<TerrainColliderBox> result, int x, int y, int z, int k)
    {
        for (int i = 0; i < CornerStepCount; i++)
        {
            float f0 = (float)i / CornerStepCount;
            float f1 = (float)(i + 1) / CornerStepCount;
            float g = 1f - f1; // 段上端での solid 断面の余裕（z ≤ x + g）
            if (g <= 0f)
                continue;

            var (sx0, sz0, sx1, sz1) = RotRect(0f, 0f, 1f, g, k);     // South 帯
            result.Add(MakeLocalBox(x, y, z, sx0, f0, sz0, sx1, f1, sz1));
            var (ex0, ez0, ex1, ez1) = RotRect(1f - g, 0f, 1f, 1f, k); // East 帯
            result.Add(MakeLocalBox(x, y, z, ex0, f0, ez0, ex1, f1, ez1));
        }
    }

    /// <summary>XZ 矩形をブロック中心まわりに 90° × k 回転する（点 (x,z) → (z, 1−x)）。</summary>
    private static (float x0, float z0, float x1, float z1) RotRect(float x0, float z0, float x1, float z1, int k)
    {
        for (int r = 0; r < k; r++)
            (x0, z0, x1, z1) = (z0, 1f - x1, z1, 1f - x0);
        return (x0, z0, x1, z1);
    }

    private static TerrainColliderBox MakeLocalBox(
        int x, int y, int z, float lx0, float ly0, float lz0, float lx1, float ly1, float lz1)
    {
        var min = new Vector3(x + lx0, y + ly0, z + lz0) * BlockSize;
        var size = new Vector3(lx1 - lx0, ly1 - ly0, lz1 - lz0) * BlockSize;
        return TerrainColliderBox.FromMinSize(min, size);
    }
}
