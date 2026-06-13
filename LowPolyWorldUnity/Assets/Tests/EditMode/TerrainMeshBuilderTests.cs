using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class TerrainMeshBuilderTests
{
    private const float Delta = 1e-4f;

    // AO 明度の期待値（15.16: brightness = 1.0 − darkness / 4）
    private const float AoNone = 1.0f;        // darkness 0（テクスチャ色をそのまま表示）
    private const float AoOne = 0.75f;        // darkness 1（グループ1 参照 1 つ）
    private const float AoHalfOcc = 0.875f;   // darkness 0.5（占有ウェイト 0.5 の角 1 つ）
    private const float AoSlopeAbove = 0.8125f; // darkness 0.75（グループ2 高端・真上のみ）

    private TerrainVoxelStore _store;
    private TerrainMeshBuilder _builder;
    private TestAtlasMap _map;

    [SetUp]
    public void SetUp()
    {
        _store = new TerrainVoxelStore();
        _builder = new TerrainMeshBuilder();
        _map = new TestAtlasMap();
    }

    // ── 基本・引数検証 ────────────────────────────────────────────────────────

    [Test]
    public void BuildChunk_EmptyChunk_ReturnsEmptyMesh()
    {
        var data = Build();
        Assert.IsTrue(data.IsEmpty);
        Assert.AreEqual(0, data.Triangles.Count);
    }

    [Test]
    public void BuildChunk_InvalidArguments_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => _builder.BuildChunk(null, _map, 0, 0, 0));
        Assert.Throws<ArgumentNullException>(
            () => _builder.BuildChunk(new TerrainStoreSampler(_store), null, 0, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => _builder.BuildChunk(new TerrainStoreSampler(_store), _map, 4, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => _builder.BuildChunk(new TerrainStoreSampler(_store), _map, 0, 2, 0));
    }

    // ── cube ──────────────────────────────────────────────────────────────────

    [Test]
    public void BuildChunk_IsolatedCube_SixFacesWithExpectedRegions()
    {
        Set(5, 5, 5, TerrainShape.Cube, 2);
        var data = Build();

        Assert.AreEqual(24, data.Vertices.Count, "6 面 × 4 頂点");
        Assert.AreEqual(36, data.Triangles.Count, "6 面 × 2 三角形 × 3");
        Assert.AreEqual(4, CountRegion(data, TerrainFaceRegion.Top));
        Assert.AreEqual(16, CountRegion(data, TerrainFaceRegion.SideTopBottom), "上下に同種なし → 側面上端下端");
        Assert.AreEqual(4, CountRegion(data, TerrainFaceRegion.Bottom));

        foreach (var p in data.Vertices)
        {
            Assert.GreaterOrEqual(p.x, 2.5f - Delta, "1 ブロック = 0.5m");
            Assert.LessOrEqual(p.x, 3.0f + Delta);
            Assert.GreaterOrEqual(p.y, 2.5f - Delta);
            Assert.LessOrEqual(p.y, 3.0f + Delta);
        }

        for (int i = 0; i < data.Colors.Count; i++)
        {
            Color c = data.Colors[i];
            Assert.AreEqual(AoNone, c.r, Delta, "隣接なし → ベース明度 1.0（テクスチャ色をそのまま表示）");
            Assert.AreEqual(c.r, c.g, Delta, "無彩色");
            Assert.AreEqual(c.r, c.b, Delta);
            float expectedAlpha = (int)Mathf.Floor(data.Uvs[i].x) == (int)TerrainFaceRegion.Top ? 1f : 0f;
            Assert.AreEqual(expectedAlpha, c.a, Delta, "α = 上向きの面フラグ（15.11 カット平面の表示判定）");
        }

        foreach (var uv in data.Uvs)
            Assert.AreEqual(2, (int)Mathf.Floor(uv.y), "テスト用マップはパレットインデックスを v に埋め込む");
    }

    [Test]
    public void BuildChunk_CubeAtWorldBottom_BottomFaceCulled()
    {
        Set(5, 0, 5, TerrainShape.Cube);
        var data = Build();

        Assert.AreEqual(20, data.Vertices.Count, "ワールド下端の下は「地形あり」扱いで底面を生成しない");
        Assert.AreEqual(0, CountRegion(data, TerrainFaceRegion.Bottom));
        Assert.AreEqual(16, CountRegion(data, TerrainFaceRegion.SideTopBottom), "仮想地形は同種扱いしない");

        foreach (var c in data.Colors)
            Assert.AreEqual(AoNone, c.r, Delta, "仮想地形は AO 参照に含めない");
    }

    [Test]
    public void BuildChunk_StackedSameKindCubes_SharedFacesCulledAndSideRegionsChange()
    {
        Set(5, 5, 5, TerrainShape.Cube, 1);
        Set(5, 6, 5, TerrainShape.Cube, 1);
        var data = Build();

        Assert.AreEqual(40, data.Vertices.Count, "接する上面・下面はカリング → 10 面");
        Assert.AreEqual(60, data.Triangles.Count);
        Assert.AreEqual(16, CountRegion(data, TerrainFaceRegion.SideBottom), "下段: 上に同種あり");
        Assert.AreEqual(16, CountRegion(data, TerrainFaceRegion.SideTop), "上段: 下に同種あり");
        Assert.AreEqual(4, CountRegion(data, TerrainFaceRegion.Top));
        Assert.AreEqual(4, CountRegion(data, TerrainFaceRegion.Bottom));
    }

    [Test]
    public void BuildChunk_AdjacentCubes_SharedSideFacesCulled()
    {
        Set(5, 5, 5, TerrainShape.Cube);
        Set(6, 5, 5, TerrainShape.Cube);
        var data = Build();
        Assert.AreEqual(40, data.Vertices.Count, "向かい合う側面はカリング → 各 5 面");
    }

    [Test]
    public void BuildChunk_NeighborInAdjacentChunk_CullsBoundaryFace()
    {
        Set(15, 5, 5, TerrainShape.Cube); // チャンク (0,0,0) の東端
        Set(16, 5, 5, TerrainShape.Cube); // チャンク (1,0,0)
        var data = Build(0, 0, 0);
        Assert.AreEqual(20, data.Vertices.Count, "チャンク境界でも隣チャンクのボクセルを参照して面カリングする");
    }

    // ── 隣接形状ごとのカリングルール ──────────────────────────────────────────

    [Test]
    public void BuildChunk_RampNeighbor_DoesNotHideSideFace()
    {
        Set(5, 5, 5, TerrainShape.Cube);
        Set(6, 5, 5, TerrainShape.RampN);
        var data = Build();
        // cube は 6 面全部（ramp は側面を隠さない）、ramp は西三角形のみ非表示
        Assert.AreEqual(24 + 15, data.Vertices.Count);
    }

    [Test]
    public void BuildChunk_CubeUnderRamp_TopFaceCulled()
    {
        Set(5, 5, 5, TerrainShape.Cube, 1);
        Set(5, 6, 5, TerrainShape.RampN, 1);
        var data = Build();
        // cube: 側面 4 + 下面 = 20 頂点 / ramp: 下面カリングで 14 頂点
        Assert.AreEqual(34, data.Vertices.Count, "ramp の下面は full なので下のブロックの上面を隠す");
        Assert.AreEqual(6, CountRegion(data, TerrainFaceRegion.RampSide), "下に同種あり → 坂側面");
        Assert.AreEqual(4, CountRegion(data, TerrainFaceRegion.SideTop), "ramp の垂直面: 下に同種あり");
        Assert.AreEqual(16, CountRegion(data, TerrainFaceRegion.SideBottom), "cube 側面: 上に同種あり");
    }

    [Test]
    public void BuildChunk_RampSlope_NotCulledByCubeAbove()
    {
        Set(5, 5, 5, TerrainShape.RampN, 1);
        Set(5, 6, 5, TerrainShape.Cube, 1);
        var data = Build();

        // 斜面は境界平面に接しないため真上に cube があってもカリングしない（15.12）。
        // ramp 18 頂点（全面）+ cube 24 頂点（下面も ramp に隠されない）
        Assert.AreEqual(42, data.Vertices.Count);
        Assert.AreEqual(0, CountRegion(data, TerrainFaceRegion.TopMiddle), "露出している斜面は常に上面領域（15.8）");
        Assert.AreEqual(8, CountRegion(data, TerrainFaceRegion.Top), "斜面 4 + cube 上面 4");
    }

    [Test]
    public void BuildChunk_CubeUnderSameKindDiag_ExposedTopUsesTopRegion()
    {
        Set(5, 5, 5, TerrainShape.Cube, 3);
        Set(5, 6, 5, TerrainShape.DiagNW, 3);
        var data = Build();
        // diag は上面を隠さない → 露出している上面は同種でも通常の上面領域（15.8 — 上面中間は隠れ面専用）
        Assert.AreEqual(0, CountRegion(data, TerrainFaceRegion.TopMiddle));
        Assert.AreEqual(4 + 3, CountRegion(data, TerrainFaceRegion.Top), "cube 上面 + diag 上面三角形");
    }

    [Test]
    public void BuildChunk_CubeUnderDifferentKindDiag_TopFaceUsesTop()
    {
        Set(5, 5, 5, TerrainShape.Cube, 3);
        Set(5, 6, 5, TerrainShape.DiagNW, 4);
        var data = Build();
        Assert.AreEqual(0, CountRegion(data, TerrainFaceRegion.TopMiddle));
        Assert.AreEqual(4 + 3, CountRegion(data, TerrainFaceRegion.Top), "cube 上面 + diag 上面三角形");
    }

    // ── ramp ──────────────────────────────────────────────────────────────────

    [Test]
    public void BuildChunk_IsolatedRampN_GeometryAndRegions()
    {
        Set(5, 5, 5, TerrainShape.RampN);
        var data = Build();

        Assert.AreEqual(18, data.Vertices.Count, "下面 4 + 北面 4 + 三角形 3×2 + 斜面 4");
        Assert.AreEqual(24, data.Triangles.Count);
        Assert.AreEqual(4, CountRegion(data, TerrainFaceRegion.Top), "斜面は上面領域");
        Assert.AreEqual(4, CountRegion(data, TerrainFaceRegion.SideTopBottom), "北の垂直面");
        Assert.AreEqual(6, CountRegion(data, TerrainFaceRegion.RampSideBottom), "下に同種なし → 坂側面下端");
        Assert.AreEqual(4, CountRegion(data, TerrainFaceRegion.Bottom));

        // North 側が高い: 上端エッジ (y=3.0) は z=3.0 にのみ存在する
        Assert.IsTrue(data.Vertices.TrueForAll(p => p.y < 3.0f - Delta || p.z > 3.0f - Delta));
    }

    [Test]
    public void BuildChunk_RampE_RotatedGeometry()
    {
        Set(5, 5, 5, TerrainShape.RampE);
        var data = Build();

        Assert.AreEqual(18, data.Vertices.Count);
        // East 側が高い: 上端エッジ (y=3.0) は x=3.0 にのみ存在する
        Assert.IsTrue(data.Vertices.TrueForAll(p => p.y < 3.0f - Delta || p.x > 3.0f - Delta));
        Assert.IsTrue(data.Vertices.Exists(p => SamePos(p, 3.0f, 3.0f, 2.5f)));
        Assert.IsTrue(data.Vertices.Exists(p => SamePos(p, 3.0f, 3.0f, 3.0f)));
    }

    // ── diag ──────────────────────────────────────────────────────────────────

    [Test]
    public void BuildChunk_IsolatedDiagNW_GeometryAndRegions()
    {
        Set(5, 5, 5, TerrainShape.DiagNW);
        var data = Build();

        Assert.AreEqual(18, data.Vertices.Count, "上下三角形 3×2 + 北面 4 + 西面 4 + 斜辺面 4");
        Assert.AreEqual(24, data.Triangles.Count);
        Assert.AreEqual(3, CountRegion(data, TerrainFaceRegion.Top));
        Assert.AreEqual(3, CountRegion(data, TerrainFaceRegion.Bottom));
        Assert.AreEqual(12, CountRegion(data, TerrainFaceRegion.SideTopBottom), "北面 + 西面 + 斜辺面は側面領域");
    }

    [Test]
    public void BuildChunk_DiagNW_WestNeighbor_MutualFaceCulling()
    {
        Set(4, 5, 5, TerrainShape.Cube);
        Set(5, 5, 5, TerrainShape.DiagNW);
        var data = Build();
        // cube の東面は diag_NW の West full 面に隠され、diag の西面は cube に隠される
        Assert.AreEqual(20 + 14, data.Vertices.Count);
    }

    [Test]
    public void BuildChunk_DiagNW_SouthNeighbor_NotCulled()
    {
        Set(5, 5, 4, TerrainShape.Cube);
        Set(5, 5, 5, TerrainShape.DiagNW);
        var data = Build();
        // diag_NW の South 面は partial → cube の北面は表示。diag に南面はないため両者フル生成
        Assert.AreEqual(24 + 18, data.Vertices.Count);
    }

    [Test]
    public void BuildChunk_DiagSE_RotationAndCulling()
    {
        Set(5, 5, 5, TerrainShape.DiagSE);
        var data = Build();
        Assert.AreEqual(18, data.Vertices.Count);
        // solid が S・E を覆う: SW 角は solid に含まれ、NW 角には頂点が存在しない
        Assert.IsTrue(data.Vertices.Exists(p => SamePos(p, 2.5f, 2.5f, 2.5f)));
        Assert.AreEqual(
            0,
            CountVerts(data, p => Mathf.Approximately(p.x, 2.5f) && Mathf.Approximately(p.z, 3.0f)),
            "NW 半分は empty");

        // 南隣の cube の北面は diag_SE の South full 面に隠される
        Set(5, 5, 4, TerrainShape.Cube);
        var data2 = Build();
        Assert.AreEqual(14 + 20, data2.Vertices.Count, "diag の南面と cube の北面が相互カリング");
    }

    // ── corner（外角・四面体） ────────────────────────────────────────────────

    [Test]
    public void BuildChunk_IsolatedCornerNW_GeometryAndRegions()
    {
        Set(5, 5, 5, TerrainShape.CornerNW);
        var data = Build();

        Assert.AreEqual(12, data.Vertices.Count, "底面 3 + 斜面 3 + West 壁 3 + North 壁 3");
        Assert.AreEqual(12, data.Triangles.Count, "4 面 × 1 三角形 × 3");
        Assert.AreEqual(3, CountRegion(data, TerrainFaceRegion.Top), "斜面は上面領域");
        Assert.AreEqual(3, CountRegion(data, TerrainFaceRegion.Bottom));
        Assert.AreEqual(6, CountRegion(data, TerrainFaceRegion.RampSideBottom), "2 壁・下に同種なし → 坂側面下端");
        Assert.AreEqual(0, CountRegion(data, TerrainFaceRegion.TopMiddle));

        // 高頂点は NW 上角 (2.5, 3.0, 3.0) のみ（斜面・2 壁が共有 → 3 回出現）
        Assert.AreEqual(3, CountVerts(data, p => Mathf.Approximately(p.y, 3.0f)), "y=3.0 は高頂点のみ");
        Assert.AreEqual(3, CountVerts(data, p => SamePos(p, 2.5f, 3.0f, 3.0f)));

        foreach (var c in data.Colors)
            Assert.AreEqual(AoNone, c.r, Delta, "孤立した角は AO 参照なし → ベース明度");
        foreach (var c in data.Colors)
            Assert.AreEqual(0f, c.a, Delta, "角に水平な上向き面はない（斜面 α=0・壁/底面 α=0）");
    }

    [Test]
    public void BuildChunk_CornerRotations_HighVertexAtExpectedCorner()
    {
        AssertCornerHighVertex(TerrainShape.CornerNW, 2.5f, 3.0f); // NW
        AssertCornerHighVertex(TerrainShape.CornerNE, 3.0f, 3.0f); // NE
        AssertCornerHighVertex(TerrainShape.CornerSE, 3.0f, 2.5f); // SE
        AssertCornerHighVertex(TerrainShape.CornerSW, 2.5f, 2.5f); // SW
    }

    private void AssertCornerHighVertex(TerrainShape shape, float worldX, float worldZ)
    {
        _store = new TerrainVoxelStore();
        Set(5, 5, 5, shape);
        var data = Build();
        Assert.AreEqual(12, data.Vertices.Count, $"{shape}");
        Assert.AreEqual(3, CountVerts(data, p => Mathf.Approximately(p.y, 3.0f)), $"{shape}: 高頂点は 1 点のみ");
        Assert.AreEqual(
            3, CountVerts(data, p => SamePos(p, worldX, 3.0f, worldZ)), $"{shape}: 高頂点 ({worldX},3.0,{worldZ})");
    }

    [Test]
    public void BuildChunk_CornerBottom_CulledByCubeBelow_SlopeAlwaysShown()
    {
        Set(5, 5, 5, TerrainShape.Cube, 1);
        Set(5, 6, 5, TerrainShape.CornerNW, 1); // cube の上に角
        var data = Build();

        // 角の底面は直下の cube に隠される（9 頂点）。cube の上面は半三角の底面では隠されず表示（24 頂点）
        Assert.AreEqual(24 + 9, data.Vertices.Count, "cube 全面 + 角（底面カリング）");
        // 角の斜面（上面領域）は直上が空なので常に表示。cube 上面 4 + 角斜面 3
        Assert.AreEqual(4 + 3, CountRegion(data, TerrainFaceRegion.Top));
        Assert.AreEqual(6, CountRegion(data, TerrainFaceRegion.RampSide), "下に同種 cube あり → 坂側面（下端でない）");
    }

    [Test]
    public void BuildChunk_CornerSlope_NotCulledByCubeAbove()
    {
        Set(5, 5, 5, TerrainShape.CornerNW);
        Set(5, 6, 5, TerrainShape.Cube); // 真上に cube
        var data = Build();

        // 斜面は境界平面に接しないため真上に cube があってもカリングしない（15.12）
        Assert.AreEqual(0, CountRegion(data, TerrainFaceRegion.TopMiddle), "露出している斜面は常に上面領域");
        Assert.AreEqual(4 + 3, CountRegion(data, TerrainFaceRegion.Top), "cube 上面 4 + 角斜面 3（常に表示）");
    }

    [Test]
    public void BuildChunk_CornerOcclusion_HighCornerFull_AdjacentCornerHalf()
    {
        Set(15, 5, 5, TerrainShape.Cube);       // メッシュ化する cube
        Set(16, 5, 5, TerrainShape.CornerNW);   // 東隣（隣チャンク・非メッシュ化）の角
        var data = Build(0, 0, 0);

        // 角の高角 = NW（占有 1.0）→ cube 東面の北端 (z=3.0) は darkness 1 → 0.75
        AssertAnyVertexWithColor(data, p => SamePos(p, 8.0f, 3.0f, 3.0f), AoOne);
        AssertAnyVertexWithColor(data, p => SamePos(p, 8.0f, 2.5f, 3.0f), AoOne);
        // 角の隣接低角 = SW（占有 0.5）→ cube 東面の南端 (z=2.5) は darkness 0.5 → 0.875
        AssertAnyVertexWithColor(data, p => SamePos(p, 8.0f, 3.0f, 2.5f), AoHalfOcc);
        AssertAnyVertexWithColor(data, p => SamePos(p, 8.0f, 2.5f, 2.5f), AoHalfOcc);
    }

    // ── 頂点 AO ───────────────────────────────────────────────────────────────

    [Test]
    public void BuildChunk_Group1Ao_DarkensCornersNextToNeighbor()
    {
        Set(15, 5, 5, TerrainShape.Cube);
        Set(16, 6, 5, TerrainShape.Cube); // 隣チャンク（メッシュ化対象外）の斜め上ブロック
        var data = Build(0, 0, 0);

        Assert.AreEqual(24, data.Vertices.Count, "AO 参照ブロックは面カリングに影響しない");
        // 斜め上ブロックを参照するのは上面の東端 2 頂点 + 東面の上端 2 頂点のみ
        Assert.AreEqual(4, CountVerts(data, (p, c) => NearColor(c, AoOne)));
        AssertAnyVertexWithColor(data, p => SamePos(p, 8.0f, 3.0f, 2.5f), AoOne);
        AssertAnyVertexWithColor(data, p => SamePos(p, 8.0f, 3.0f, 3.0f), AoOne);
        AssertAllColorAt(data, p => Mathf.Approximately(p.x, 7.5f), AoNone);
    }

    [Test]
    public void BuildChunk_SlopeAo_HighEndDarkenedByBlockAbove()
    {
        Set(5, 15, 5, TerrainShape.RampN, 1);
        Set(5, 16, 5, TerrainShape.Cube, 2); // 真上（隣チャンク）: 斜面はカリングされず常に描画
        var data = Build(0, 0, 0);

        Assert.AreEqual(18, data.Vertices.Count);
        // 高端 2 頂点のみ darkness = AO_RAMP_HIGH_PRIMARY (0.75) → 0.75 × (1 − 0.25)
        Assert.AreEqual(2, CountVerts(data, (p, c) => NearColor(c, AoSlopeAbove)));
    }

    [Test]
    public void BuildChunk_Group1Ao_RampOccupancy_HighSideFullLowSideHalf()
    {
        Set(13, 5, 5, TerrainShape.Cube);  // 坂の低い側の cube
        Set(14, 5, 5, TerrainShape.RampE); // East 側が高い坂
        Set(15, 5, 5, TerrainShape.Cube);  // 坂の高い側の cube
        var data = Build(0, 0, 0);

        // 低い側の cube 東面: ramp の低い側の角 = 占有 0.5 → darkness 0.5
        AssertAnyVertexWithColor(data, p => SamePos(p, 7.0f, 3.0f, 2.5f), AoHalfOcc);
        // 高い側の cube 西面: ramp の高い側の角 = 占有 1.0 → darkness 1
        AssertAnyVertexWithColor(data, p => SamePos(p, 7.5f, 3.0f, 2.5f), AoOne);
        // cube 上面（y+1 レイヤー参照）は同じ高さの ramp の影響を受けない
        AssertAnyVertexWithColor(data, p => SamePos(p, 6.5f, 3.0f, 2.5f), AoNone);
    }

    [Test]
    public void BuildChunk_SlopeAo_ContinuousRampsHaveNoSeam()
    {
        // 連続する坂（階段状）: 下の坂の高端と上の坂の低端は同一平面上の同じ位置
        Set(5, 5, 5, TerrainShape.RampN);
        Set(5, 6, 6, TerrainShape.RampN); // 1 段上の同方向の坂
        Set(5, 5, 6, TerrainShape.Cube);  // 上の坂の支持ブロック
        var data = Build();

        // つなぎ目 (y=3.0, z=3.0): 修正前は下の坂の高端 2 頂点が上の坂を副参照して 0.9375 に暗くなり、
        // 同位置の上の坂の低端 (1.0) と段差が出ていた。同方向 ramp は斜面の連続として遮蔽から除外される
        bool OnSeam(Vector3 p) => Mathf.Approximately(p.y, 3.0f) && Mathf.Approximately(p.z, 3.0f);
        Assert.AreEqual(
            0,
            CountVerts(data, (p, c) => OnSeam(p) && Mathf.Abs(c.r - 0.9375f) < Delta),
            "つなぎ目に AO の明度段差が出ない");
        Assert.GreaterOrEqual(
            CountVerts(data, (p, c) => OnSeam(p) && NearColor(c, AoNone)),
            4,
            "下の坂の高端 2 + 上の坂の低端 2 はベース明度のまま");
    }

    [Test]
    public void BuildChunk_UpFacingAlpha_SlopeExcludedFromMargin()
    {
        // 斜面（上面領域だが非水平）は α = 0、diag の上面三角形（水平）は α = 1
        Set(5, 5, 5, TerrainShape.RampN);
        var rampData = Build();
        for (int i = 0; i < rampData.Colors.Count; i++)
            if ((int)Mathf.Floor(rampData.Uvs[i].x) == (int)TerrainFaceRegion.Top)
                Assert.AreEqual(0f, rampData.Colors[i].a, Delta, "斜面はカット平面より上に伸びるためマージン対象外");

        _store = new TerrainVoxelStore();
        Set(5, 5, 5, TerrainShape.DiagNW);
        var diagData = Build();
        int upCount = 0;
        for (int i = 0; i < diagData.Colors.Count; i++)
        {
            if ((int)Mathf.Floor(diagData.Uvs[i].x) != (int)TerrainFaceRegion.Top)
                continue;
            Assert.AreEqual(1f, diagData.Colors[i].a, Delta, "水平な上面三角形はマージン対象");
            upCount++;
        }
        Assert.AreEqual(3, upCount);
    }

    [Test]
    public void BuildChunk_QuadDiagonal_DefaultSplitWhenUniform()
    {
        Set(15, 5, 5, TerrainShape.Cube);
        var data = Build(0, 0, 0);
        // 明度が均一なら既定の対角線 (0,2) で分割
        CollectionAssert.AreEqual(new[] { 0, 1, 2, 0, 2, 3 }, data.Triangles.GetRange(0, 6));
    }

    [Test]
    public void BuildChunk_QuadDiagonal_FlipsThroughOddBrightnessVertex()
    {
        Set(15, 5, 5, TerrainShape.Cube);
        Set(16, 6, 4, TerrainShape.Cube); // 上面の SE 頂点（index 3）だけを暗くする斜め上ブロック
        var data = Build(0, 0, 0);
        // 仲間外れの暗い頂点を両方の三角形が共有するよう対角線 (1,3) に反転
        //（3 頂点同色の平坦な三角形が出ないように — 15.16）
        CollectionAssert.AreEqual(new[] { 1, 2, 3, 1, 3, 0 }, data.Triangles.GetRange(0, 6));
    }

    [Test]
    public void BuildChunk_Group1Ao_DiagTipOccupancyIsHalf()
    {
        Set(15, 5, 5, TerrainShape.Cube);
        Set(16, 5, 5, TerrainShape.DiagNE); // 東隣の diag（斜辺の端が cube の北東角を通る）
        var data = Build(0, 0, 0);

        // cube 東面の北端 2 頂点: diag_NE の NW 角 = 斜辺の端 → 占有 0.5
        AssertAnyVertexWithColor(data, p => SamePos(p, 8.0f, 3.0f, 3.0f), AoHalfOcc);
        // cube 東面の南端 2 頂点: diag_NE の SW 角 = 空き側 → 占有 0
        AssertAllColorAt(data, p => SamePos(p, 8.0f, 3.0f, 2.5f), AoNone);
    }

    [Test]
    public void BuildChunk_Group1Ao_DiagonalCornerBlockDarkensSharedVertex()
    {
        // 角に斜め接するブロック: 上面の共有頂点がどの面から見ても同じ明度になる（継ぎ目なし）
        Set(15, 5, 5, TerrainShape.Cube);
        Set(16, 6, 6, TerrainShape.Cube); // 斜め上の角ブロック（隣チャンク・メッシュ化対象外）
        var data = Build(0, 0, 0);

        // 上面 NE 頂点 (8.0, 3.0, 3.0) のみ斜め角参照で darkness 1 → 0.5
        AssertAnyVertexWithColor(data, p => SamePos(p, 8.0f, 3.0f, 3.0f), AoOne);
        AssertAllColorAt(data, p => SamePos(p, 7.5f, 3.0f, 2.5f), AoNone);
    }

    [Test]
    public void BuildChunk_HypotenuseAo_UsesNormalComponents()
    {
        Set(15, 5, 5, TerrainShape.DiagNW);
        Set(16, 6, 5, TerrainShape.Cube); // 斜辺法線の +X 成分方向の上ブロック
        var data = Build(0, 0, 0);

        // 斜辺面の上端 2 頂点（SW_top / NE_top）が darkness 1 → 0.375
        Assert.IsTrue(data.Vertices.Exists((p) => SamePos(p, 7.5f, 3.0f, 2.5f)));
        AssertAnyVertexWithColor(data, p => SamePos(p, 7.5f, 3.0f, 2.5f), AoOne);
        AssertAnyVertexWithColor(data, p => SamePos(p, 8.0f, 3.0f, 3.0f), AoOne);
    }

    // ── 上面中間フェイス（Height Culling 用 hidden tops — 15.11） ─────────────

    [Test]
    public void BuildChunk_IsolatedCube_NoHiddenTops()
    {
        Set(5, 5, 5, TerrainShape.Cube);
        Assert.IsTrue(BuildMeshes().HiddenTops.IsEmpty, "直上に何もなければ上面中間フェイスは生成しない");
    }

    [Test]
    public void BuildChunk_StackedSameKindCubes_LowerTopEmittedAsHiddenTop()
    {
        Set(5, 5, 5, TerrainShape.Cube, 1);
        Set(5, 6, 5, TerrainShape.Cube, 1);
        var hidden = BuildMeshes().HiddenTops;

        Assert.AreEqual(4, hidden.Vertices.Count, "カリングされた下段の上面 1 面のみ");
        Assert.AreEqual(4, hidden.Uvs2.Count);
        Assert.AreEqual(4, CountRegion(hidden, TerrainFaceRegion.TopMiddle), "上に同種あり → 上面中間領域");
        foreach (var uv2 in hidden.Uvs2)
            Assert.AreEqual(6f, uv2.x, Delta, "UV2.x = 上面の Y グリッドインデックス（ブロック Y + 1）");
        foreach (var p in hidden.Vertices)
            Assert.AreEqual(3.0f, p.y, Delta, "上面は y = 6 × 0.5m");
        foreach (var c in hidden.Colors)
            Assert.AreEqual(AoNone, c.r, Delta, "hidden tops は AO を焼き込まずベース明度固定（参照先は表示時に必ず非表示）");
    }

    [Test]
    public void BuildChunk_CubeUnderDifferentKindCube_HiddenTopUsesTopRegion()
    {
        Set(5, 5, 5, TerrainShape.Cube, 1);
        Set(5, 6, 5, TerrainShape.Cube, 2);
        var meshes = BuildMeshes();

        // 上段の上面は通常表示（Solid 側）、hidden tops に入るのは下段の上面 1 面のみ
        Assert.AreEqual(4, meshes.HiddenTops.Vertices.Count);
        Assert.AreEqual(4, CountRegion(meshes.HiddenTops, TerrainFaceRegion.Top), "上に同種なし → 上面領域（15.8）");
    }

    [Test]
    public void BuildChunk_SolidMesh_HasNoUv2()
    {
        Set(5, 5, 5, TerrainShape.Cube);
        Assert.AreEqual(0, BuildMeshes().Solid.Uvs2.Count, "UV2 は hidden tops のみ使用");
    }

    // ── UV・バリアント選択 ────────────────────────────────────────────────────

    [Test]
    public void BuildChunk_Uv_StaysWithinInsetRange()
    {
        Set(5, 5, 5, TerrainShape.Cube);
        var data = Build();

        float minFrac = 1f;
        float maxFrac = 0f;
        foreach (var uv in data.Uvs)
        {
            float fx = uv.x - Mathf.Floor(uv.x);
            float fy = uv.y - Mathf.Floor(uv.y);
            minFrac = Mathf.Min(minFrac, Mathf.Min(fx, fy));
            maxFrac = Mathf.Max(maxFrac, Mathf.Max(fx, fy));
        }
        Assert.AreEqual(0.005f, minFrac, Delta, "領域内の使用 UV 範囲は [0.005, 0.995]");
        Assert.AreEqual(0.995f, maxFrac, Delta);
    }

    [Test]
    public void BuildChunk_VariantSelection_MatchesHash()
    {
        Set(5, 5, 5, TerrainShape.Cube, 2);
        var recording = new RecordingAtlasMap();
        _builder.BuildChunk(new TerrainStoreSampler(_store), recording, 0, 0, 0);

        int upIndex = TerrainFaceDirUtil.DirectionIndex(TerrainFaceDir.Up);
        int expectedTop = TerrainTextureHash.SelectIndex(5, 5, 5, upIndex, 8);
        var topCall = recording.Calls.Find(c => c.region == TerrainFaceRegion.Top);
        Assert.AreEqual(2, topCall.palette);
        Assert.AreEqual(expectedTop, topCall.variant);

        int downIndex = TerrainFaceDirUtil.DirectionIndex(TerrainFaceDir.Down);
        int expectedBottom = TerrainTextureHash.SelectIndex(5, 5, 5, downIndex, 8);
        var bottomCall = recording.Calls.Find(c => c.region == TerrainFaceRegion.Bottom);
        Assert.AreEqual(expectedBottom, bottomCall.variant);
    }

    // ── ヘルパー ──────────────────────────────────────────────────────────────

    private TerrainMeshData Build(int cx = 0, int cy = 0, int cz = 0) => BuildMeshes(cx, cy, cz).Solid;

    private TerrainChunkMeshes BuildMeshes(int cx = 0, int cy = 0, int cz = 0) =>
        _builder.BuildChunk(new TerrainStoreSampler(_store), _map, cx, cy, cz);

    private void Set(int x, int y, int z, TerrainShape shape, int palette = 0) =>
        _store.SetVoxel(x, y, z, TerrainVoxel.Encode(shape, palette));

    private static int CountRegion(TerrainMeshData data, TerrainFaceRegion region)
    {
        int count = 0;
        foreach (var uv in data.Uvs)
            if ((int)Mathf.Floor(uv.x) == (int)region)
                count++;
        return count;
    }

    private static int CountVerts(TerrainMeshData data, Predicate<Vector3> match)
    {
        int count = 0;
        foreach (var p in data.Vertices)
            if (match(p))
                count++;
        return count;
    }

    private static int CountVerts(TerrainMeshData data, Func<Vector3, Color, bool> match)
    {
        int count = 0;
        for (int i = 0; i < data.Vertices.Count; i++)
            if (match(data.Vertices[i], data.Colors[i]))
                count++;
        return count;
    }

    private static bool SamePos(Vector3 p, float x, float y, float z) =>
        Mathf.Approximately(p.x, x) && Mathf.Approximately(p.y, y) && Mathf.Approximately(p.z, z);

    private static bool NearColor(Color c, float brightness) => Mathf.Abs(c.r - brightness) < Delta;

    private static void AssertAllColorAt(TerrainMeshData data, Predicate<Vector3> at, float brightness)
    {
        int matched = 0;
        for (int i = 0; i < data.Vertices.Count; i++)
        {
            if (!at(data.Vertices[i]))
                continue;
            matched++;
            Assert.AreEqual(brightness, data.Colors[i].r, Delta, $"頂点 {data.Vertices[i]}");
        }
        Assert.Greater(matched, 0, "対象頂点が存在すること");
    }

    private static void AssertAnyVertexWithColor(TerrainMeshData data, Predicate<Vector3> at, float brightness)
    {
        for (int i = 0; i < data.Vertices.Count; i++)
            if (at(data.Vertices[i]) && NearColor(data.Colors[i], brightness))
                return;
        Assert.Fail($"明度 {brightness} の頂点が見つかりません");
    }

    /// <summary>領域を u に、パレットインデックスを v に埋め込む検証用マップ（バリアント 1 種）。</summary>
    private class TestAtlasMap : ITerrainAtlasMap
    {
        public int GetVariantCount(int paletteIndex, TerrainFaceRegion region) => 1;

        public Rect GetUvRect(int paletteIndex, TerrainFaceRegion region, int variantIndex) =>
            new Rect((int)region, paletteIndex, 1f, 1f);
    }

    private class RecordingAtlasMap : ITerrainAtlasMap
    {
        public readonly List<(int palette, TerrainFaceRegion region, int variant)> Calls =
            new List<(int, TerrainFaceRegion, int)>();

        public int GetVariantCount(int paletteIndex, TerrainFaceRegion region) => 8;

        public Rect GetUvRect(int paletteIndex, TerrainFaceRegion region, int variantIndex)
        {
            Calls.Add((paletteIndex, region, variantIndex));
            return new Rect(0f, 0f, 1f, 1f);
        }
    }
}
