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
/// - 直上ブロックでカリングされた上面は HiddenTops（上面中間フェイス）として別バッファに生成し、
///   UV2.x にブロック上面の Y グリッドインデックスを焼き込む（Height Culling 用 — 15.11）
/// - 出力座標は store グリッド基準の Unity 単位（1 ブロック = 0.5m）。ワールド中心への
///   平行移動は上位レイヤー（MonoBehaviour）が transform で行う
/// </summary>
public class TerrainMeshBuilder
{
    public const float BlockSize = 0.5f;

    // 15.10: 領域内の使用 UV 範囲 [0.005, 0.995]
    private const float UvMin = 0.005f;
    private const float UvRange = 0.99f;

    private const float NoUv2 = -1f;

    private ITerrainVoxelSampler _sampler;
    private ITerrainAtlasMap _atlas;
    private TerrainChunkMeshes _meshes;

    /// <summary>指定チャンクのメッシュデータを生成する（隣接変化時は該当チャンクのみ再生成する）。</summary>
    public TerrainChunkMeshes BuildChunk(ITerrainVoxelSampler sampler, ITerrainAtlasMap atlasMap, int cx, int cy, int cz)
    {
        if (sampler == null)
            throw new ArgumentNullException(nameof(sampler));
        if (atlasMap == null)
            throw new ArgumentNullException(nameof(atlasMap));
        if (!TerrainVoxelStore.ChunkInBounds(cx, cy, cz))
            throw new ArgumentOutOfRangeException(nameof(cx), $"チャンク座標が範囲外です: ({cx}, {cy}, {cz})");

        _sampler = sampler;
        _atlas = atlasMap;
        _meshes = new TerrainChunkMeshes();

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

        var result = _meshes;
        _sampler = null;
        _atlas = null;
        _meshes = null;
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
        {
            // 直上ブロックでカリングされた上面 → Height Culling で直上が消えたときだけ表示する
            // 上面中間フェイス。UV2.x = 上面の Y グリッドインデックス（シェーダーが閾値と比較）。
            // AO の参照先（y+1 レイヤー）は表示時に必ず全部非表示になっているため、
            // AO は焼き込まずベース明度固定にする（15.16）
            var region = TerrainNeighborRules.IsSameKind(voxel, above)
                ? TerrainFaceRegion.TopMiddle
                : TerrainFaceRegion.Top;
            Rect rect = GetUvRect(x, y, z, voxel, region, TerrainFaceDir.Up);
            var brightness = new float[verts.Length];
            for (int i = 0; i < verts.Length; i++)
                brightness[i] = TerrainAo.BaseBrightness;
            AddFace(_meshes.HiddenTops, x, y, z, verts, uvs, brightness, rect, y + 1, 1f);
        }
        else
        {
            // 露出している上面は直上が同種（diag など）でも通常の上面領域を使う
            //（上面中間は「隠されている面」専用 — 15.8）
            EmitGroup1Face(_meshes.Solid, x, y, z, voxel, TerrainFaceDir.Up, TerrainFaceRegion.Top, verts, uvs);
        }
    }

    private void EmitBottomFace(int x, int y, int z, byte voxel, Vector3[] verts, Vector2[] uvs)
    {
        byte below = _sampler.GetVoxel(x, y - 1, z);
        if (TerrainNeighborRules.HidesBottomFace(below))
            return;
        EmitGroup1Face(_meshes.Solid, x, y, z, voxel, TerrainFaceDir.Down, TerrainFaceRegion.Bottom, verts, uvs);
    }

    private void EmitSideFace(int x, int y, int z, byte voxel, TerrainFaceDir dir, Vector3[] verts, Vector2[] uvs)
    {
        var (dx, _, dz) = TerrainFaceDirUtil.Offset(dir);
        byte neighbor = _sampler.GetVoxel(x + dx, y, z + dz);
        if (TerrainNeighborRules.HidesSideFace(neighbor, dir))
            return;
        EmitGroup1Face(_meshes.Solid, x, y, z, voxel, dir, SideRegion(x, y, z, voxel), verts, uvs);
    }

    private void EmitRampTriangle(int x, int y, int z, byte voxel, TerrainFaceDir dir, Vector3[] verts, Vector2[] uvs)
    {
        var (dx, _, dz) = TerrainFaceDirUtil.Offset(dir);
        byte neighbor = _sampler.GetVoxel(x + dx, y, z + dz);
        if (TerrainNeighborRules.HidesSideFace(neighbor, dir))
            return;
        var region = IsSameKindBelow(x, y, z, voxel) ? TerrainFaceRegion.RampSide : TerrainFaceRegion.RampSideBottom;
        EmitGroup1Face(_meshes.Solid, x, y, z, voxel, dir, region, verts, uvs);
    }

    private void EmitRampSlope(int x, int y, int z, byte voxel, int k)
    {
        // 斜面はどの隣接平面とも接しないためカリングしない（真上に cube があっても生成する。
        // カリングすると側面方向から内部が見えてしまう — 15.12）。
        // 常に露出するため領域も常に上面（上面中間は「隠されている面」専用 — 15.8）
        Rect rect = GetUvRect(x, y, z, voxel, TerrainFaceRegion.Top, TerrainFaceDir.Slope);

        var verts = Rot(RampSlopeQuad, k);
        var (hx, hz) = RotDirXZ(0, 1, k); // 高い側の方向
        var rampShape = TerrainVoxel.GetShape(voxel);
        var brightness = new float[4];
        for (int i = 0; i < 4; i++)
        {
            var (sx, sz) = RotDirXZ(RampSlopeSideX[i], 0, k); // 側方向（canonical では ±X）
            int px = x + (verts[i].x > 0.5f ? 1 : 0);
            int pz = z + (verts[i].z > 0.5f ? 1 : 0);
            brightness[i] = SlopeBrightness(x, y, z, hx, hz, sx, sz, RampSlopeIsTop[i], px, pz, rampShape);
        }
        AddFace(_meshes.Solid, x, y, z, verts, RampSlopeUv, brightness, rect, NoUv2, 1f);
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
            int px = x + (verts[i].x > 0.5f ? 1 : 0);
            int pz = z + (verts[i].z > 0.5f ? 1 : 0);
            float darkness =
                TerrainAo.WeightStandard * OcclusionAt(x + nx, y + dy, z, px, pz)
                + TerrainAo.WeightStandard * OcclusionAt(x, y + dy, z + nz, px, pz);
            brightness[i] = TerrainAo.Brightness(darkness);
        }
        AddFace(_meshes.Solid, x, y, z, verts, DiagHypotenuseUv, brightness, rect, NoUv2, 0f);
    }

    private void EmitGroup1Face(
        TerrainMeshData target,
        int x, int y, int z, byte voxel,
        TerrainFaceDir dir, TerrainFaceRegion region,
        Vector3[] verts, Vector2[] uvs,
        float uv2X = NoUv2)
    {
        Rect rect = GetUvRect(x, y, z, voxel, region, dir);
        var (nx, ny, nz) = TerrainFaceDirUtil.Offset(dir);
        var brightness = new float[verts.Length];
        for (int i = 0; i < verts.Length; i++)
            brightness[i] = Group1Brightness(x, y, z, nx, ny, nz, verts[i]);
        AddFace(target, x, y, z, verts, uvs, brightness, rect, uv2X, dir == TerrainFaceDir.Up ? 1f : 0f);
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
    /// グループ1（通常面）: 各頂点（格子点）について、面の法線方向に 1 つ進んだレイヤーのうち
    /// 頂点に接する 4 ブロック（正面・辺方向 2・斜め角）を等ウェイト 1.0 で加算する。
    /// 4 ブロックの等ウェイト和は対称なため、同一平面上で頂点を共有する面同士の明度が必ず一致し、
    /// 面の継ぎ目でグラデーションが連続になる（15.16）。
    /// </summary>
    private float Group1Brightness(int x, int y, int z, int nx, int ny, int nz, Vector3 local)
    {
        int sx = local.x > 0.5f ? 1 : -1;
        int sy = local.y > 0.5f ? 1 : -1;
        int sz = local.z > 0.5f ? 1 : -1;
        int px = x + (local.x > 0.5f ? 1 : 0); // 頂点の格子点 XZ（占有判定用）
        int pz = z + (local.z > 0.5f ? 1 : 0);

        float darkness = 0f;
        for (int i = 0; i <= 1; i++)
        {
            for (int j = 0; j <= 1; j++)
            {
                int ox, oy, oz;
                if (nx != 0)
                {
                    ox = nx; oy = i * sy; oz = j * sz;
                }
                else if (ny != 0)
                {
                    ox = i * sx; oy = ny; oz = j * sz;
                }
                else
                {
                    ox = i * sx; oy = j * sy; oz = nz;
                }
                darkness += TerrainAo.WeightStandard * OcclusionAt(x + ox, y + oy, z + oz, px, pz);
            }
        }
        return TerrainAo.Brightness(darkness);
    }

    /// <summary>
    /// グループ2（坂の斜め面）: グループ A（主方向）は存在ブロックの最大ウェイトを採用し、
    /// グループ B（側方向）の存在ウェイトを加算する。(hx, hz) = 高い側、(sx, sz) = 側方向。
    /// </summary>
    private float SlopeBrightness(
        int x, int y, int z, int hx, int hz, int sx, int sz, bool isTop, int px, int pz, TerrainShape rampShape)
    {
        float groupA;
        float darkness;
        if (isTop)
        {
            groupA = TerrainAo.RampHighPrimary * OcclusionAt(x, y + 1, z, px, pz);
            // 高い側の上が同方向の ramp なら斜面の連続（solid は斜面の下側で遮蔽しない）。
            // つなぎ目の AO 段差を防ぐため副参照から除外する（15.16）
            if (TerrainVoxel.GetShape(_sampler.GetVoxel(x + hx, y + 1, z + hz)) != rampShape)
                groupA = Math.Max(groupA, TerrainAo.RampHighSecondary * OcclusionAt(x + hx, y + 1, z + hz, px, pz));
            darkness = groupA + TerrainAo.RampHighSide * OcclusionAt(x + sx, y + 1, z + sz, px, pz);
        }
        else
        {
            groupA = TerrainAo.RampLowPrimary * OcclusionAt(x - hx, y, z - hz, px, pz);
            groupA = Math.Max(groupA, TerrainAo.RampLowSecondary * OcclusionAt(x - hx + sx, y, z - hz + sz, px, pz));
            darkness = groupA + TerrainAo.RampLowSide * OcclusionAt(x + sx, y, z + sz, px, pz);
        }
        return TerrainAo.Brightness(darkness);
    }

    /// <summary>
    /// AO の遮蔽判定。参照ブロックの「頂点（格子点）に接する角の占有ウェイト 0〜1」を返す（15.16）。
    /// cube = 全角 1.0 / ramp = 高い側の 2 角 1.0・低い側の 2 角 0.5 /
    /// diag = 直角 1.0・斜辺両端の 2 角 0.5（全高の壁が角を通る）・空き角 0。
    /// 部分占有ウェイトにより、坂・斜め周辺の地面の暗さが面の下端の暗さと連続的につながる。
    /// グループ2・3 で頂点に接しない参照ブロックは最も近い角で判定する。
    /// ワールド下端の下の仮想地形は「ブロックなし」として扱う（面カリングの「地形あり」扱いとは別）。
    /// </summary>
    private float OcclusionAt(int rx, int ry, int rz, int vertexLatticeX, int vertexLatticeZ)
    {
        if (ry < 0)
            return 0f;
        var shape = TerrainVoxel.GetShape(_sampler.GetVoxel(rx, ry, rz));
        if (shape == TerrainShape.Empty)
            return 0f;
        if (shape == TerrainShape.Cube)
            return 1f;

        int dx = vertexLatticeX - rx;
        int dz = vertexLatticeZ - rz;
        dx = dx < 0 ? 0 : (dx > 1 ? 1 : dx); // 0 = West 側の角, 1 = East 側の角
        dz = dz < 0 ? 0 : (dz > 1 ? 1 : dz); // 0 = South 側の角, 1 = North 側の角
        switch (shape)
        {
            case TerrainShape.RampN: return dz == 1 ? 1f : TerrainAo.OccupancyRampLow;
            case TerrainShape.RampE: return dx == 1 ? 1f : TerrainAo.OccupancyRampLow;
            case TerrainShape.RampS: return dz == 0 ? 1f : TerrainAo.OccupancyRampLow;
            case TerrainShape.RampW: return dx == 0 ? 1f : TerrainAo.OccupancyRampLow;
            case TerrainShape.DiagNW: return DiagOcclusion(dx, dz, 0, 1);
            case TerrainShape.DiagNE: return DiagOcclusion(dx, dz, 1, 1);
            case TerrainShape.DiagSE: return DiagOcclusion(dx, dz, 1, 0);
            case TerrainShape.DiagSW: return DiagOcclusion(dx, dz, 0, 0);
            default: return 1f;
        }
    }

    private static float DiagOcclusion(int dx, int dz, int solidDx, int solidDz)
    {
        if (dx == solidDx && dz == solidDz)
            return 1f; // 直角（solid 側）の角
        if (dx == 1 - solidDx && dz == 1 - solidDz)
            return 0f; // 空き側の角
        return TerrainAo.OccupancyDiagTip; // 斜辺の両端
    }

    // ── 頂点バッファへの発行 ──────────────────────────────────────────────────

    private void AddFace(
        TerrainMeshData target, int x, int y, int z,
        Vector3[] verts, Vector2[] uvs, float[] brightness, Rect rect, float uv2X, float upFacing)
    {
        int baseIndex = target.Vertices.Count;
        for (int i = 0; i < verts.Length; i++)
        {
            target.Vertices.Add(new Vector3(x + verts[i].x, y + verts[i].y, z + verts[i].z) * BlockSize);
            target.Uvs.Add(new Vector2(
                rect.x + (UvMin + uvs[i].x * UvRange) * rect.width,
                rect.y + (UvMin + uvs[i].y * UvRange) * rect.height));
            if (uv2X >= 0f)
                target.Uvs2.Add(new Vector2(uv2X, 0f));
            float b = brightness[i];
            // α = 上向きの面フラグ（Height Culling のカット平面と一致する高さでの表示判定に使用 — 15.11）
            target.Colors.Add(new Color(b, b, b, upFacing));
        }

        if (verts.Length == 4)
        {
            // 明度差の大きい方の対角線を分割線に選ぶ（15.16）。
            // 仲間外れの明度の頂点（暗い角・明るい角）を両方の三角形が共有することで、
            // 3 頂点が同色の平坦な三角形が現れて分割線が目立つのを防ぐ
            bool flip = Math.Abs(brightness[1] - brightness[3]) > Math.Abs(brightness[0] - brightness[2]) + 1e-5f;
            int d = flip ? 1 : 0; // 対角線 (1,3) または (0,2)
            target.Triangles.Add(baseIndex + d);
            target.Triangles.Add(baseIndex + d + 1);
            target.Triangles.Add(baseIndex + d + 2);
            target.Triangles.Add(baseIndex + d);
            target.Triangles.Add(baseIndex + d + 2);
            target.Triangles.Add(baseIndex + (d + 3) % 4);
        }
        else
        {
            target.Triangles.Add(baseIndex);
            target.Triangles.Add(baseIndex + 1);
            target.Triangles.Add(baseIndex + 2);
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
