using System;
using UnityEngine;

/// <summary>
/// チャンク単位の地形メッシュ生成ロジック（world-creation.md セクション 15.8〜15.16）。
///
/// - 露出面のみ生成する（15.14。隣接判定は TerrainNeighborRules = 15.12 テーブル準拠。
///   チャンク境界・ワールド境界は ITerrainVoxelSampler が吸収する）
/// - 下面も他の面と同じルールで常時生成する（ワールド下端の下は「地形あり」扱い）
/// - 面ごとのテクスチャ領域選択（15.8）+ バリアント選択ハッシュ（15.9）
/// - UV は領域内 [0.005, 0.995] を使用し、坂の三角形側面は領域左下の直角三角形を使う（15.10）
/// - 頂点カラーへ簡易 AO を焼き込む（15.16 グループ1〜3）
/// - ramp / diag は North 基準の形状を XZ 回転して導出する
/// - 出力座標は store グリッド基準の Unity 単位（1 ブロック = 0.5m）。ワールド中心への
///   平行移動は上位レイヤー（MonoBehaviour）が transform で行う
/// </summary>
public class TerrainMeshBuilder
{
    public const float BlockSize = 0.5f;

    // 15.10: 領域内の使用 UV 範囲 [0.005, 0.995]
    private const float UvMin = 0.005f;
    private const float UvRange = 0.99f;

    private ITerrainVoxelSampler _sampler;
    private ITerrainAtlasMap _atlas;
    private TerrainMeshData _data;

    /// <summary>指定チャンクのメッシュデータを生成する（隣接変化時は該当チャンクのみ再生成する）。</summary>
    public TerrainMeshData BuildChunk(ITerrainVoxelSampler sampler, ITerrainAtlasMap atlasMap, int cx, int cy, int cz)
    {
        if (sampler == null)
            throw new ArgumentNullException(nameof(sampler));
        if (atlasMap == null)
            throw new ArgumentNullException(nameof(atlasMap));
        if (!TerrainVoxelStore.ChunkInBounds(cx, cy, cz))
            throw new ArgumentOutOfRangeException(nameof(cx), $"チャンク座標が範囲外です: ({cx}, {cy}, {cz})");

        _sampler = sampler;
        _atlas = atlasMap;
        _data = new TerrainMeshData();

        int ox = cx * TerrainChunk.Size;
        int oy = cy * TerrainChunk.Size;
        int oz = cz * TerrainChunk.Size;
        for (int ly = 0; ly < TerrainChunk.Size; ly++)
        {
            for (int lz = 0; lz < TerrainChunk.Size; lz++)
            {
                for (int lx = 0; lx < TerrainChunk.Size; lx++)
                {
                    int x = ox + lx;
                    int y = oy + ly;
                    int z = oz + lz;
                    if (!TerrainVoxelStore.InBounds(x, y, z))
                        continue; // 端のチャンクのパディング領域
                    byte voxel = sampler.GetVoxel(x, y, z);
                    if (TerrainVoxel.IsEmpty(voxel))
                        continue;
                    EmitBlock(x, y, z, voxel);
                }
            }
        }

        var result = _data;
        _sampler = null;
        _atlas = null;
        _data = null;
        return result;
    }

    // ── 形状ごとの面生成 ──────────────────────────────────────────────────────

    private void EmitBlock(int x, int y, int z, byte voxel)
    {
        switch (TerrainVoxel.GetShape(voxel))
        {
            case TerrainShape.Cube:
                EmitCube(x, y, z, voxel);
                break;
            case TerrainShape.RampN:
                EmitRamp(x, y, z, voxel, 0);
                break;
            case TerrainShape.RampE:
                EmitRamp(x, y, z, voxel, 1);
                break;
            case TerrainShape.RampS:
                EmitRamp(x, y, z, voxel, 2);
                break;
            case TerrainShape.RampW:
                EmitRamp(x, y, z, voxel, 3);
                break;
            case TerrainShape.DiagNW:
                EmitDiag(x, y, z, voxel, 0);
                break;
            case TerrainShape.DiagNE:
                EmitDiag(x, y, z, voxel, 1);
                break;
            case TerrainShape.DiagSE:
                EmitDiag(x, y, z, voxel, 2);
                break;
            case TerrainShape.DiagSW:
                EmitDiag(x, y, z, voxel, 3);
                break;
        }
    }

    private void EmitCube(int x, int y, int z, byte voxel)
    {
        EmitTopFace(x, y, z, voxel, CubeTopQuad, CubeTopUv);
        EmitBottomFace(x, y, z, voxel, CubeBottomQuad, CubeBottomUv);
        EmitSideFace(x, y, z, voxel, TerrainFaceDir.North, CubeNorthQuad, SideQuadUv);
        EmitSideFace(x, y, z, voxel, TerrainFaceDir.South, CubeSouthQuad, SideQuadUv);
        EmitSideFace(x, y, z, voxel, TerrainFaceDir.East, CubeEastQuad, SideQuadUv);
        EmitSideFace(x, y, z, voxel, TerrainFaceDir.West, CubeWestQuad, SideQuadUv);
    }

    private void EmitRamp(int x, int y, int z, byte voxel, int k)
    {
        EmitBottomFace(x, y, z, voxel, CubeBottomQuad, CubeBottomUv);
        EmitSideFace(x, y, z, voxel, RotSideDir(TerrainFaceDir.North, k), Rot(CubeNorthQuad, k), SideQuadUv);
        EmitRampTriangle(x, y, z, voxel, RotSideDir(TerrainFaceDir.East, k), Rot(RampEastTri, k), RampEastTriUv);
        EmitRampTriangle(x, y, z, voxel, RotSideDir(TerrainFaceDir.West, k), Rot(RampWestTri, k), RampWestTriUv);
        EmitRampSlope(x, y, z, voxel, k);
    }

    private void EmitDiag(int x, int y, int z, byte voxel, int k)
    {
        EmitTopFace(x, y, z, voxel, Rot(DiagTopTri, k), DiagTopUv);
        EmitBottomFace(x, y, z, voxel, Rot(DiagBottomTri, k), DiagBottomUv);
        EmitSideFace(x, y, z, voxel, RotSideDir(TerrainFaceDir.North, k), Rot(CubeNorthQuad, k), SideQuadUv);
        EmitSideFace(x, y, z, voxel, RotSideDir(TerrainFaceDir.West, k), Rot(CubeWestQuad, k), SideQuadUv);
        EmitDiagHypotenuse(x, y, z, voxel, k);
    }

    // ── 面種別ごとのカリング + 領域選択 + 発行 ────────────────────────────────

    private void EmitTopFace(int x, int y, int z, byte voxel, Vector3[] verts, Vector2[] uvs)
    {
        byte above = _sampler.GetVoxel(x, y + 1, z);
        if (TerrainNeighborRules.HidesTopFace(above))
            return;
        var region = TerrainNeighborRules.IsSameKind(voxel, above)
            ? TerrainFaceRegion.TopMiddle
            : TerrainFaceRegion.Top;
        EmitGroup1Face(x, y, z, voxel, TerrainFaceDir.Up, region, verts, uvs);
    }

    private void EmitBottomFace(int x, int y, int z, byte voxel, Vector3[] verts, Vector2[] uvs)
    {
        byte below = _sampler.GetVoxel(x, y - 1, z);
        if (TerrainNeighborRules.HidesBottomFace(below))
            return;
        EmitGroup1Face(x, y, z, voxel, TerrainFaceDir.Down, TerrainFaceRegion.Bottom, verts, uvs);
    }

    private void EmitSideFace(int x, int y, int z, byte voxel, TerrainFaceDir dir, Vector3[] verts, Vector2[] uvs)
    {
        var (dx, _, dz) = TerrainFaceDirUtil.Offset(dir);
        byte neighbor = _sampler.GetVoxel(x + dx, y, z + dz);
        if (TerrainNeighborRules.HidesSideFace(neighbor, dir))
            return;
        EmitGroup1Face(x, y, z, voxel, dir, SideRegion(x, y, z, voxel), verts, uvs);
    }

    private void EmitRampTriangle(int x, int y, int z, byte voxel, TerrainFaceDir dir, Vector3[] verts, Vector2[] uvs)
    {
        var (dx, _, dz) = TerrainFaceDirUtil.Offset(dir);
        byte neighbor = _sampler.GetVoxel(x + dx, y, z + dz);
        if (TerrainNeighborRules.HidesSideFace(neighbor, dir))
            return;
        var region = IsSameKindBelow(x, y, z, voxel) ? TerrainFaceRegion.RampSide : TerrainFaceRegion.RampSideBottom;
        EmitGroup1Face(x, y, z, voxel, dir, region, verts, uvs);
    }

    private void EmitRampSlope(int x, int y, int z, byte voxel, int k)
    {
        // 斜面はどの隣接平面とも接しないためカリングしない（真上に cube があっても生成する。
        // カリングすると側面方向から内部が見えてしまう — 15.12）
        byte above = _sampler.GetVoxel(x, y + 1, z);
        var region = TerrainNeighborRules.IsSameKind(voxel, above)
            ? TerrainFaceRegion.TopMiddle
            : TerrainFaceRegion.Top;
        Rect rect = GetUvRect(x, y, z, voxel, region, TerrainFaceDir.Slope);

        var verts = Rot(RampSlopeQuad, k);
        var (hx, hz) = RotDirXZ(0, 1, k); // 高い側の方向
        var brightness = new float[4];
        for (int i = 0; i < 4; i++)
        {
            var (sx, sz) = RotDirXZ(RampSlopeSideX[i], 0, k); // 側方向（canonical では ±X）
            brightness[i] = SlopeBrightness(x, y, z, hx, hz, sx, sz, RampSlopeIsTop[i]);
        }
        AddFace(x, y, z, verts, RampSlopeUv, brightness, rect);
    }

    private void EmitDiagHypotenuse(int x, int y, int z, byte voxel, int k)
    {
        // 斜辺垂直面はどの隣接平面とも接しないためカリングしない
        Rect rect = GetUvRect(x, y, z, voxel, SideRegion(x, y, z, voxel), TerrainFaceDir.Hypotenuse);

        var verts = Rot(DiagHypotenuseQuad, k);
        var (nx, nz) = RotDirXZ(1, -1, k); // 法線の XZ 成分（canonical diag_NW は (+1,−1)）
        var brightness = new float[4];
        for (int i = 0; i < 4; i++)
        {
            int dy = DiagHypotenuseIsTop[i] ? 1 : -1;
            float darkness = 0f;
            if (PresentForAo(x + nx, y + dy, z))
                darkness += TerrainAo.WeightStandard;
            if (PresentForAo(x, y + dy, z + nz))
                darkness += TerrainAo.WeightStandard;
            brightness[i] = TerrainAo.Brightness(darkness);
        }
        AddFace(x, y, z, verts, DiagHypotenuseUv, brightness, rect);
    }

    private void EmitGroup1Face(
        int x, int y, int z, byte voxel,
        TerrainFaceDir dir, TerrainFaceRegion region,
        Vector3[] verts, Vector2[] uvs)
    {
        Rect rect = GetUvRect(x, y, z, voxel, region, dir);
        var (nx, ny, nz) = TerrainFaceDirUtil.Offset(dir);
        var brightness = new float[verts.Length];
        for (int i = 0; i < verts.Length; i++)
            brightness[i] = Group1Brightness(x, y, z, nx, ny, nz, verts[i]);
        AddFace(x, y, z, verts, uvs, brightness, rect);
    }

    // ── テクスチャ領域選択（15.8）・UV ────────────────────────────────────────

    private TerrainFaceRegion SideRegion(int x, int y, int z, byte voxel)
    {
        bool aboveSame = TerrainNeighborRules.IsSameKind(voxel, _sampler.GetVoxel(x, y + 1, z));
        bool belowSame = IsSameKindBelow(x, y, z, voxel);
        if (aboveSame && belowSame)
            return TerrainFaceRegion.Side;
        if (aboveSame)
            return TerrainFaceRegion.SideBottom;
        if (belowSame)
            return TerrainFaceRegion.SideTop;
        return TerrainFaceRegion.SideTopBottom;
    }

    // ワールド下端の下の仮想地形（カリング用）は「同種」とは見なさない
    private bool IsSameKindBelow(int x, int y, int z, byte voxel) =>
        y > 0 && TerrainNeighborRules.IsSameKind(voxel, _sampler.GetVoxel(x, y - 1, z));

    private Rect GetUvRect(int x, int y, int z, byte voxel, TerrainFaceRegion region, TerrainFaceDir dir)
    {
        int palette = TerrainVoxel.GetPaletteIndex(voxel);
        int count = _atlas.GetVariantCount(palette, region);
        int variant = TerrainTextureHash.SelectIndex(x, y, z, TerrainFaceDirUtil.DirectionIndex(dir), count);
        return _atlas.GetUvRect(palette, region, variant);
    }

    // ── 頂点 AO（15.16） ──────────────────────────────────────────────────────

    /// <summary>
    /// グループ1（通常面）: 各頂点について「面の 2 辺方向それぞれ + 法線方向」の
    /// 合成オフセット先のブロックをウェイト 1.0 で加算する。
    /// </summary>
    private float Group1Brightness(int x, int y, int z, int nx, int ny, int nz, Vector3 local)
    {
        float darkness = 0f;
        if (nx == 0)
        {
            int sx = local.x > 0.5f ? 1 : -1;
            if (PresentForAo(x + sx, y + ny, z + nz))
                darkness += TerrainAo.WeightStandard;
        }
        if (ny == 0)
        {
            int sy = local.y > 0.5f ? 1 : -1;
            if (PresentForAo(x + nx, y + sy, z + nz))
                darkness += TerrainAo.WeightStandard;
        }
        if (nz == 0)
        {
            int sz = local.z > 0.5f ? 1 : -1;
            if (PresentForAo(x + nx, y + ny, z + sz))
                darkness += TerrainAo.WeightStandard;
        }
        return TerrainAo.Brightness(darkness);
    }

    /// <summary>
    /// グループ2（坂の斜め面）: グループ A（主方向）は存在ブロックの最大ウェイトを採用し、
    /// グループ B（側方向）の存在ウェイトを加算する。(hx, hz) = 高い側、(sx, sz) = 側方向。
    /// </summary>
    private float SlopeBrightness(int x, int y, int z, int hx, int hz, int sx, int sz, bool isTop)
    {
        float groupA = 0f;
        if (isTop)
        {
            if (PresentForAo(x, y + 1, z))
                groupA = TerrainAo.RampHighPrimary;
            if (PresentForAo(x + hx, y + 1, z + hz))
                groupA = Math.Max(groupA, TerrainAo.RampHighSecondary);
            float darkness = groupA + (PresentForAo(x + sx, y + 1, z + sz) ? TerrainAo.RampHighSide : 0f);
            return TerrainAo.Brightness(darkness);
        }
        else
        {
            if (PresentForAo(x - hx, y, z - hz))
                groupA = TerrainAo.RampLowPrimary;
            if (PresentForAo(x - hx + sx, y, z - hz + sz))
                groupA = Math.Max(groupA, TerrainAo.RampLowSecondary);
            float darkness = groupA + (PresentForAo(x + sx, y, z + sz) ? TerrainAo.RampLowSide : 0f);
            return TerrainAo.Brightness(darkness);
        }
    }

    // AO の隣接参照。ワールド下端の下の仮想地形は「ブロックなし」として扱う
    //（存在しない床との接地影を作らないため。面カリングの「地形あり」扱いとは別）
    private bool PresentForAo(int x, int y, int z) =>
        y >= 0 && !TerrainVoxel.IsEmpty(_sampler.GetVoxel(x, y, z));

    // ── 頂点バッファへの発行 ──────────────────────────────────────────────────

    private void AddFace(int x, int y, int z, Vector3[] verts, Vector2[] uvs, float[] brightness, Rect rect)
    {
        int baseIndex = _data.Vertices.Count;
        for (int i = 0; i < verts.Length; i++)
        {
            _data.Vertices.Add(new Vector3(x + verts[i].x, y + verts[i].y, z + verts[i].z) * BlockSize);
            _data.Uvs.Add(new Vector2(
                rect.x + (UvMin + uvs[i].x * UvRange) * rect.width,
                rect.y + (UvMin + uvs[i].y * UvRange) * rect.height));
            float b = brightness[i];
            _data.Colors.Add(new Color(b, b, b, 1f));
        }

        _data.Triangles.Add(baseIndex);
        _data.Triangles.Add(baseIndex + 1);
        _data.Triangles.Add(baseIndex + 2);
        if (verts.Length == 4)
        {
            _data.Triangles.Add(baseIndex);
            _data.Triangles.Add(baseIndex + 2);
            _data.Triangles.Add(baseIndex + 3);
        }
    }

    // ── XZ 回転（North 基準形状 → E / S / W、diag_NW 基準 → NE / SE / SW） ──────

    private static Vector3[] Rot(Vector3[] src, int k)
    {
        if (k == 0)
            return src; // 共有配列をそのまま返す（呼び出し側は変更しない）
        var dst = new Vector3[src.Length];
        for (int i = 0; i < src.Length; i++)
        {
            Vector3 p = src[i];
            for (int r = 0; r < k; r++)
                p = new Vector3(p.z, p.y, 1f - p.x);
            dst[i] = p;
        }
        return dst;
    }

    private static (int dx, int dz) RotDirXZ(int dx, int dz, int k)
    {
        for (int r = 0; r < k; r++)
            (dx, dz) = (dz, -dx);
        return (dx, dz);
    }

    private static TerrainFaceDir RotSideDir(TerrainFaceDir dir, int k)
    {
        var (dx, _, dz) = TerrainFaceDirUtil.Offset(dir);
        (dx, dz) = RotDirXZ(dx, dz, k);
        if (dz == 1)
            return TerrainFaceDir.North;
        if (dz == -1)
            return TerrainFaceDir.South;
        return dx == 1 ? TerrainFaceDir.East : TerrainFaceDir.West;
    }

    // ── 形状定義（ブロックローカル座標 0〜1・巻き順は Cross(b−a, c−a) = 外向き法線） ──

    private static readonly Vector3[] CubeTopQuad =
    {
        new Vector3(0, 1, 0), new Vector3(0, 1, 1), new Vector3(1, 1, 1), new Vector3(1, 1, 0),
    };
    private static readonly Vector2[] CubeTopUv =
    {
        new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0),
    };

    private static readonly Vector3[] CubeBottomQuad =
    {
        new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(1, 0, 1), new Vector3(0, 0, 1),
    };
    private static readonly Vector2[] CubeBottomUv =
    {
        new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1),
    };

    private static readonly Vector3[] CubeNorthQuad =
    {
        new Vector3(0, 0, 1), new Vector3(1, 0, 1), new Vector3(1, 1, 1), new Vector3(0, 1, 1),
    };
    private static readonly Vector3[] CubeSouthQuad =
    {
        new Vector3(1, 0, 0), new Vector3(0, 0, 0), new Vector3(0, 1, 0), new Vector3(1, 1, 0),
    };
    private static readonly Vector3[] CubeEastQuad =
    {
        new Vector3(1, 0, 1), new Vector3(1, 0, 0), new Vector3(1, 1, 0), new Vector3(1, 1, 1),
    };
    private static readonly Vector3[] CubeWestQuad =
    {
        new Vector3(0, 0, 0), new Vector3(0, 0, 1), new Vector3(0, 1, 1), new Vector3(0, 1, 0),
    };
    private static readonly Vector2[] SideQuadUv =
    {
        new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1),
    };

    // ramp_N（North 側が高い）。三角形側面の UV は領域左下の直角三角形（直角 = 高い側の下端）
    private static readonly Vector3[] RampEastTri =
    {
        new Vector3(1, 0, 1), new Vector3(1, 0, 0), new Vector3(1, 1, 1),
    };
    private static readonly Vector2[] RampEastTriUv =
    {
        new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1),
    };
    private static readonly Vector3[] RampWestTri =
    {
        new Vector3(0, 0, 1), new Vector3(0, 1, 1), new Vector3(0, 0, 0),
    };
    private static readonly Vector2[] RampWestTriUv =
    {
        new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 0),
    };

    // 斜面の頂点順: SE_bot, SW_bot, NW_top, NE_top（低端 2 つ → 高端 2 つ）
    private static readonly Vector3[] RampSlopeQuad =
    {
        new Vector3(1, 0, 0), new Vector3(0, 0, 0), new Vector3(0, 1, 1), new Vector3(1, 1, 1),
    };
    private static readonly Vector2[] RampSlopeUv =
    {
        new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1),
    };
    private static readonly int[] RampSlopeSideX = { 1, -1, -1, 1 };
    private static readonly bool[] RampSlopeIsTop = { false, false, true, true };

    // diag_NW（solid 三角形の XZ 角 = SW・NE・NW、斜辺法線 = (+1, 0, −1)/√2）
    private static readonly Vector3[] DiagTopTri =
    {
        new Vector3(0, 1, 0), new Vector3(0, 1, 1), new Vector3(1, 1, 1),
    };
    private static readonly Vector2[] DiagTopUv =
    {
        new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1),
    };
    private static readonly Vector3[] DiagBottomTri =
    {
        new Vector3(0, 0, 0), new Vector3(1, 0, 1), new Vector3(0, 0, 1),
    };
    private static readonly Vector2[] DiagBottomUv =
    {
        new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, 1),
    };

    // 斜辺垂直面の頂点順: NE_bot, SW_bot, SW_top, NE_top
    private static readonly Vector3[] DiagHypotenuseQuad =
    {
        new Vector3(1, 0, 1), new Vector3(0, 0, 0), new Vector3(0, 1, 0), new Vector3(1, 1, 1),
    };
    private static readonly Vector2[] DiagHypotenuseUv =
    {
        new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1),
    };
    private static readonly bool[] DiagHypotenuseIsTop = { false, false, true, true };
}
