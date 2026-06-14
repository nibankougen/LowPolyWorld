using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class TerrainColliderBuilderTests
{
    private const float Delta = 1e-4f;
    private const float CellVolume = 0.5f * 0.5f * 0.5f; // 1 ブロック = 0.125 m³

    private TerrainVoxelStore _store;
    private TerrainColliderBuilder _builder;

    [SetUp]
    public void SetUp()
    {
        _store = new TerrainVoxelStore();
        _builder = new TerrainColliderBuilder();
    }

    // ── 基本・引数検証 ────────────────────────────────────────────────────────

    [Test]
    public void BuildChunk_EmptyChunk_ReturnsNoBoxes()
    {
        Assert.AreEqual(0, Build().Count);
    }

    [Test]
    public void BuildChunk_InvalidArguments_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => _builder.BuildChunk(null, 0, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => _builder.BuildChunk(new TerrainStoreSampler(_store), 4, 0, 0));
    }

    // ── cube グリーディー結合 ─────────────────────────────────────────────────

    [Test]
    public void BuildChunk_SingleCube_SingleBox()
    {
        Set(5, 5, 5, TerrainShape.Cube);
        var boxes = Build();

        Assert.AreEqual(1, boxes.Count);
        AssertVector(boxes[0].Center, 2.75f, 2.75f, 2.75f);
        AssertVector(boxes[0].Size, 0.5f, 0.5f, 0.5f);
    }

    [Test]
    public void BuildChunk_RowOfCubes_MergedAlongX()
    {
        Set(5, 5, 5, TerrainShape.Cube);
        Set(6, 5, 5, TerrainShape.Cube);
        Set(7, 5, 5, TerrainShape.Cube);
        var boxes = Build();

        Assert.AreEqual(1, boxes.Count, "X 方向に連続する cube は 1 つの BoxCollider に結合");
        AssertVector(boxes[0].Size, 1.5f, 0.5f, 0.5f);
        AssertVector(boxes[0].Min, 2.5f, 2.5f, 2.5f);
    }

    [Test]
    public void BuildChunk_RectangleOfCubes_MergedXZ()
    {
        for (int x = 5; x <= 6; x++)
            for (int z = 5; z <= 7; z++)
                Set(x, 5, z, TerrainShape.Cube);
        var boxes = Build();

        Assert.AreEqual(1, boxes.Count, "X 結合後に Z 方向にも結合");
        AssertVector(boxes[0].Size, 1.0f, 0.5f, 1.5f);
    }

    [Test]
    public void BuildChunk_ColumnOfCubes_MergedAlongY()
    {
        Set(5, 5, 5, TerrainShape.Cube);
        Set(5, 6, 5, TerrainShape.Cube);
        Set(5, 7, 5, TerrainShape.Cube);
        var boxes = Build();

        Assert.AreEqual(1, boxes.Count, "同一 XZ 矩形が連続する Y 層 → Y 結合");
        AssertVector(boxes[0].Size, 0.5f, 1.5f, 0.5f);
        AssertVector(boxes[0].Min, 2.5f, 2.5f, 2.5f);
    }

    [Test]
    public void BuildChunk_DifferentRectsAcrossLayers_NotMergedAlongY()
    {
        Set(5, 5, 5, TerrainShape.Cube);
        Set(6, 5, 5, TerrainShape.Cube);
        Set(5, 6, 5, TerrainShape.Cube); // 上の層は矩形が異なる
        var boxes = Build();

        Assert.AreEqual(2, boxes.Count, "矩形が同一の場合のみ Y 結合する");
        Assert.AreEqual(3 * CellVolume, TotalVolume(boxes), Delta);
        AssertNoOverlap(boxes);
    }

    [Test]
    public void BuildChunk_LShape_TwoRects()
    {
        Set(5, 5, 5, TerrainShape.Cube);
        Set(6, 5, 5, TerrainShape.Cube);
        Set(5, 5, 6, TerrainShape.Cube);
        var boxes = Build();

        Assert.AreEqual(2, boxes.Count);
        Assert.AreEqual(3 * CellVolume, TotalVolume(boxes), Delta);
        AssertNoOverlap(boxes);
    }

    [Test]
    public void BuildChunk_CubeBlob_VolumePreservedWithoutOverlap()
    {
        // 4×2×3 の直方体 + 突起 1 個
        for (int x = 4; x <= 7; x++)
            for (int y = 4; y <= 5; y++)
                for (int z = 4; z <= 6; z++)
                    Set(x, y, z, TerrainShape.Cube);
        Set(8, 4, 4, TerrainShape.Cube);
        var boxes = Build();

        Assert.AreEqual(25 * CellVolume, TotalVolume(boxes), Delta, "結合してもセル合計体積は不変");
        AssertNoOverlap(boxes);
    }

    [Test]
    public void BuildChunk_ChunkContained_NoMergeAcrossChunks()
    {
        Set(15, 5, 5, TerrainShape.Cube); // チャンク (0,0,0)
        Set(16, 5, 5, TerrainShape.Cube); // チャンク (1,0,0)
        var boxes = Build(0, 0, 0);

        Assert.AreEqual(1, boxes.Count, "コライダーはチャンク内完結");
        AssertVector(boxes[0].Min, 7.5f, 2.5f, 2.5f);
        AssertVector(boxes[0].Size, 0.5f, 0.5f, 0.5f);
    }

    // ── ramp の階段近似 ───────────────────────────────────────────────────────

    [Test]
    public void BuildChunk_RampN_FourThinSteps()
    {
        Set(5, 5, 5, TerrainShape.RampN);
        var boxes = Build();

        Assert.AreEqual(TerrainColliderBuilder.RampStepCount, boxes.Count);
        boxes.Sort((a, b) => a.Min.y.CompareTo(b.Min.y));
        for (int i = 0; i < 4; i++)
        {
            AssertVector(boxes[i].Size, 0.5f, 0.125f, 0.125f);
            Assert.AreEqual(2.5f + i * 0.125f, boxes[i].Min.y, Delta, $"段 {i} の下面");
            Assert.AreEqual(2.5f + i * 0.125f, boxes[i].Min.z, Delta, "低い側 (South) から North へ昇る");
            Assert.AreEqual(2.5f, boxes[i].Min.x, Delta, "X はブロック全幅");
        }
        Assert.AreEqual(3.0f, boxes[3].Max.y, Delta, "最上段の上面 = ブロック上端");

        // 各段差は stepOffset (0.26m) 未満
        for (int i = 1; i < 4; i++)
            Assert.Less(boxes[i].Max.y - boxes[i - 1].Max.y, TerrainColliderBuilder.RequiredStepOffset);
    }

    [Test]
    public void BuildChunk_RampE_StepsRotatedAlongX()
    {
        Set(5, 5, 5, TerrainShape.RampE);
        var boxes = Build();

        Assert.AreEqual(4, boxes.Count);
        boxes.Sort((a, b) => a.Min.y.CompareTo(b.Min.y));
        for (int i = 0; i < 4; i++)
        {
            AssertVector(boxes[i].Size, 0.125f, 0.125f, 0.5f);
            Assert.AreEqual(2.5f + i * 0.125f, boxes[i].Min.x, Delta, "低い側 (West) から East へ昇る");
        }
        Assert.AreEqual(3.0f, boxes[3].Max.x, Delta);
        Assert.AreEqual(3.0f, boxes[3].Max.y, Delta);
    }

    [Test]
    public void BuildChunk_RampS_HighSideAtSouth()
    {
        Set(5, 5, 5, TerrainShape.RampS);
        var boxes = Build();

        boxes.Sort((a, b) => a.Min.y.CompareTo(b.Min.y));
        Assert.AreEqual(2.5f, boxes[3].Min.z, Delta, "最上段は South 端");
        Assert.AreEqual(3.0f - 0.125f, boxes[0].Min.z, Delta, "最下段は North 端");
    }

    // ── diag の XZ 階段近似 ───────────────────────────────────────────────────

    [Test]
    public void BuildChunk_DiagNW_ThreeFullHeightInnerBoxes()
    {
        Set(5, 5, 5, TerrainShape.DiagNW);
        var boxes = Build();

        Assert.AreEqual(3, boxes.Count, "最後の段は奥行き 0 のためスキップ");
        boxes.Sort((a, b) => a.Min.x.CompareTo(b.Min.x));
        for (int i = 0; i < 3; i++)
        {
            Assert.AreEqual(0.5f, boxes[i].Size.y, Delta, "diag は全高（高さ方向に勾配なし）");
            Assert.AreEqual(2.5f + i * 0.125f, boxes[i].Min.x, Delta);
            Assert.AreEqual(0.125f, boxes[i].Size.x, Delta);
            Assert.AreEqual(3.0f, boxes[i].Max.z, Delta, "solid は North 側");
            // 内側近似: solid 領域 (z ≥ x) に内接し phantom wall を作らない
            Assert.GreaterOrEqual(boxes[i].Min.z, boxes[i].Max.x - Delta);
        }
        AssertNoOverlap(boxes);
    }

    [Test]
    public void BuildChunk_DiagSE_RotatedInnerBoxes()
    {
        Set(5, 5, 5, TerrainShape.DiagSE);
        var boxes = Build();

        Assert.AreEqual(3, boxes.Count);
        foreach (var box in boxes)
        {
            // diag_SE の solid は x + z ≤ 1 + ... ではなく SW(0,0)-SE(1,0)-NE(1,1): z ≤ x。
            // 内側近似: box 全体が z ≤ x を満たす（ローカル座標で max.z ≤ min.x）
            float localMinX = box.Min.x - 2.5f;
            float localMaxZ = box.Max.z - 2.5f;
            Assert.LessOrEqual(localMaxZ, localMinX + Delta, "solid (z ≤ x) に内接");
        }
        AssertNoOverlap(boxes);
    }

    // ── corner（四面体）の階段近似 ────────────────────────────────────────────

    [Test]
    public void BuildChunk_CornerNW_AscendingInnerBoxes()
    {
        Set(5, 5, 5, TerrainShape.CornerNW);
        var boxes = Build();

        Assert.AreEqual(3, boxes.Count, "最上段は断面 0 のためスキップ");
        boxes.Sort((a, b) => a.Min.y.CompareTo(b.Min.y));
        for (int i = 0; i < 3; i++)
        {
            Assert.AreEqual(0.125f, boxes[i].Size.y, Delta, "薄い水平段（厚さ 0.125m）");
            Assert.AreEqual(2.5f + i * 0.125f, boxes[i].Min.y, Delta, $"段 {i} の下面");
            Assert.AreEqual(2.5f, boxes[i].Min.x, Delta, "高頂点 NW の West 端に内接");
            Assert.AreEqual(3.0f, boxes[i].Max.z, Delta, "高頂点 NW の North 端に内接");
            // 内側近似: 段全体が solid 領域 (z ≥ x + y) に収まり phantom wall を作らない。
            // 最も制約の厳しい角（min z・max x・max y）で確認する（ブロックローカル分率）。
            float fMaxX = (boxes[i].Max.x - 2.5f) / 0.5f;
            float fMinZ = (boxes[i].Min.z - 2.5f) / 0.5f;
            float fMaxY = (boxes[i].Max.y - 2.5f) / 0.5f;
            Assert.GreaterOrEqual(fMinZ + Delta, fMaxX + fMaxY, "solid (z ≥ x + y) に内接");
        }
        Assert.Greater(boxes[0].Size.x, boxes[2].Size.x, "高くなるほど断面は NW 角へ縮む");

        for (int i = 1; i < 3; i++)
            Assert.Less(boxes[i].Max.y - boxes[i - 1].Max.y, TerrainColliderBuilder.RequiredStepOffset);
        AssertNoOverlap(boxes);
    }

    [Test]
    public void BuildChunk_CornerSE_RotatedToSouthEast()
    {
        Set(5, 5, 5, TerrainShape.CornerSE);
        var boxes = Build();

        Assert.AreEqual(3, boxes.Count);
        foreach (var box in boxes)
        {
            Assert.AreEqual(3.0f, box.Max.x, Delta, "高頂点 SE の East 端に内接");
            Assert.AreEqual(2.5f, box.Min.z, Delta, "高頂点 SE の South 端に内接");
        }
        AssertNoOverlap(boxes);
    }

    // ── concave（凹角）の階段近似 ─────────────────────────────────────────────

    [Test]
    public void BuildChunk_ConcaveNW_ConservativeInnerBoxes()
    {
        Set(5, 5, 5, TerrainShape.ConcaveNW);
        var boxes = Build();

        Assert.AreEqual(6, boxes.Count, "3 段 × 2 ボックス（最上段は断面 0 でスキップ）");
        foreach (var box in boxes)
        {
            // セル内に収まる
            Assert.GreaterOrEqual(box.Min.x, 2.5f - Delta);
            Assert.LessOrEqual(box.Max.x, 3.0f + Delta);
            Assert.GreaterOrEqual(box.Min.y, 2.5f - Delta);
            Assert.LessOrEqual(box.Max.y, 3.0f + Delta);
            Assert.GreaterOrEqual(box.Min.z, 2.5f - Delta);
            Assert.LessOrEqual(box.Max.z, 3.0f + Delta);
            // 内側近似: solid 領域 (y ≤ x − z + 1) に収まり phantom wall を作らない。
            // 最も制約の厳しい角（max y・min x・max z）で確認する（ブロックローカル分率）。
            float fMaxY = (box.Max.y - 2.5f) / 0.5f;
            float fMinX = (box.Min.x - 2.5f) / 0.5f;
            float fMaxZ = (box.Max.z - 2.5f) / 0.5f;
            Assert.LessOrEqual(fMaxY - fMinX + fMaxZ, 1f + Delta, "solid (y ≤ x − z + 1) に内接");
        }
    }

    [Test]
    public void BuildChunk_MixedCubeAndRamp_RampNotMerged()
    {
        Set(5, 5, 5, TerrainShape.Cube);
        Set(6, 5, 5, TerrainShape.RampN);
        var boxes = Build();

        Assert.AreEqual(1 + 4, boxes.Count, "ramp は結合対象外で階段近似");
        AssertNoOverlap(boxes);
    }

    // ── ヘルパー ──────────────────────────────────────────────────────────────

    private List<TerrainColliderBox> Build(int cx = 0, int cy = 0, int cz = 0) =>
        _builder.BuildChunk(new TerrainStoreSampler(_store), cx, cy, cz);

    private void Set(int x, int y, int z, TerrainShape shape, int palette = 0) =>
        _store.SetVoxel(x, y, z, TerrainVoxel.Encode(shape, palette));

    private static void AssertVector(Vector3 actual, float x, float y, float z)
    {
        Assert.AreEqual(x, actual.x, Delta);
        Assert.AreEqual(y, actual.y, Delta);
        Assert.AreEqual(z, actual.z, Delta);
    }

    private static float TotalVolume(List<TerrainColliderBox> boxes)
    {
        float volume = 0f;
        foreach (var box in boxes)
            volume += box.Size.x * box.Size.y * box.Size.z;
        return volume;
    }

    private static void AssertNoOverlap(List<TerrainColliderBox> boxes)
    {
        for (int i = 0; i < boxes.Count; i++)
        {
            for (int j = i + 1; j < boxes.Count; j++)
            {
                bool overlap =
                    boxes[i].Min.x < boxes[j].Max.x - Delta && boxes[j].Min.x < boxes[i].Max.x - Delta
                    && boxes[i].Min.y < boxes[j].Max.y - Delta && boxes[j].Min.y < boxes[i].Max.y - Delta
                    && boxes[i].Min.z < boxes[j].Max.z - Delta && boxes[j].Min.z < boxes[i].Max.z - Delta;
                Assert.IsFalse(overlap, $"box {i} と box {j} が重なっています");
            }
        }
    }
}
