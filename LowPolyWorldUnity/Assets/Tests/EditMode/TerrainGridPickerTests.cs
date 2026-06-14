using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class TerrainGridPickerTests
{
    [Test]
    public void TryPickCell_StraightDown_ReturnsCellUnderRay()
    {
        // セル (10, 12) の中央上空から真下（高さ 0 の床平面 y = 0）
        bool hit = TerrainGridPicker.TryPickCell(
            new Vector3(5.25f, 10f, 6.25f), Vector3.down, 0, out int x, out int z);

        Assert.IsTrue(hit);
        Assert.AreEqual(10, x);
        Assert.AreEqual(12, z);
    }

    [Test]
    public void TryPickCell_UsesEditHeightPlane()
    {
        // 高さ 6 の床平面は y = 3.0m。斜めレイで交点がずれることを確認
        var origin = new Vector3(0.3f, 5f, 0.1f);
        var direction = new Vector3(1f, -1f, 0f).normalized;
        bool hit = TerrainGridPicker.TryPickCell(origin, direction, 6, out int x, out int z);

        Assert.IsTrue(hit);
        Assert.AreEqual(4, x, "y=5→3 で 2m 進む → x=2.3m → グリッド 4（セル中央狙いで境界誤差を回避）");
        Assert.AreEqual(0, z);
    }

    [Test]
    public void TryPickCell_ParallelRay_ReturnsFalse()
    {
        Assert.IsFalse(TerrainGridPicker.TryPickCell(
            new Vector3(1f, 5f, 1f), Vector3.forward, 0, out _, out _));
    }

    [Test]
    public void TryPickCell_PlaneBehindRay_ReturnsFalse()
    {
        // 上向きレイでは下の平面に当たらない
        Assert.IsFalse(TerrainGridPicker.TryPickCell(
            new Vector3(1f, 5f, 1f), Vector3.up, 0, out _, out _));
    }

    [Test]
    public void TryPickCell_OutsideWorld_ReturnsFalse()
    {
        Assert.IsFalse(TerrainGridPicker.TryPickCell(
            new Vector3(-3f, 5f, 1f), Vector3.down, 0, out _, out _), "x < 0");
        Assert.IsFalse(TerrainGridPicker.TryPickCell(
            new Vector3(40f, 5f, 1f), Vector3.down, 0, out _, out _), "x ≥ 63 グリッド（31.5m）");
    }

    [Test]
    public void TryPickCell_WorldEdgeCells_AreValid()
    {
        Assert.IsTrue(TerrainGridPicker.TryPickCell(
            new Vector3(0.1f, 5f, 0.1f), Vector3.down, 0, out int x0, out int z0));
        Assert.AreEqual(0, x0);
        Assert.AreEqual(0, z0);

        Assert.IsTrue(TerrainGridPicker.TryPickCell(
            new Vector3(31.4f, 5f, 31.4f), Vector3.down, 0, out int x1, out int z1));
        Assert.AreEqual(62, x1);
        Assert.AreEqual(62, z1);
    }

    // ── TryPickEditCell（下面 / 上面の優先判定） ──────────────────────────────

    private static HashSet<(int x, int z)> Blocks(params (int x, int z)[] cells)
    {
        var set = new HashSet<(int, int)>();
        foreach (var c in cells)
            set.Add(c);
        return set;
    }

    [Test]
    public void TryPickEditCell_NoBlocks_FallsBackToBottomPlane()
    {
        // ブロックが無ければ上面は反応せず、従来の下面ぴったり判定になる
        bool hit = TerrainGridPicker.TryPickEditCell(
            new Vector3(5.25f, 10f, 6.25f), Vector3.down, 0,
            cameraForward: Vector3.down, Blocks(), additive: false, out int x, out int z);

        Assert.IsTrue(hit);
        Assert.AreEqual(10, x);
        Assert.AreEqual(12, z);
    }

    [Test]
    public void TryPickEditCell_BlockPresent_TopFaceTakesPriority()
    {
        // 真下レイ。下面・上面とも同じセル (10,12) に当たるが、ブロックがあるので上面優先で成立。
        bool hit = TerrainGridPicker.TryPickEditCell(
            new Vector3(5.25f, 10f, 6.25f), Vector3.down, 0,
            cameraForward: Vector3.down, Blocks((10, 12)), additive: false, out int x, out int z);

        Assert.IsTrue(hit);
        Assert.AreEqual(10, x);
        Assert.AreEqual(12, z);
    }

    [Test]
    public void TryPickEditCell_TopFaceGridShiftedTowardCamera()
    {
        // -Z を向くカメラ（forward の水平成分 = -Z）→ カメラ側 = +Z。
        // 上面平面 y = 0.5m。真下レイでセル境界手前 z = 6.0m（= グリッド 12 の手前端）を狙う。
        // ずらしが無ければ floor(6.0/0.5)=12 だが、+Z へ 15%(=0.075m) ずれて floor((6.0-0.075)/0.5)=11。
        var cameraForward = new Vector3(0f, -1f, -1f); // 水平成分 = -Z（カメラは +Z 側）
        bool hit = TerrainGridPicker.TryPickEditCell(
            new Vector3(5.75f, 10f, 6.0f), Vector3.down, 0,
            cameraForward, Blocks((11, 11)), additive: false, out int x, out int z);

        Assert.IsTrue(hit);
        Assert.AreEqual(11, x, "x は 5.75/0.5=11.5 → 11");
        Assert.AreEqual(11, z, "z=6.0 はカメラ側(+Z)へ 0.075m ずれて floor(5.925/0.5)=11");
    }

    [Test]
    public void TryPickEditCell_AdjacentCell_OnlyAdditiveRespondsOnTop()
    {
        // 斜めレイ（X 方向に下る）。下面は (20,12)・上面は 0.5m 高いぶん 1 セル手前の (19,12) に当たる。
        // ブロックは (20,12) → 上面セル (19,12) はブロックに隣接する空セル。
        // カメラ前方は真下（水平成分なし）にして上面ずらしを 0 にし、隣接判定だけを切り分ける。
        var origin = new Vector3(0.3f, 10f, 6.25f);
        var direction = new Vector3(1f, -1f, 0f).normalized;
        var blocks = Blocks((20, 12));

        // 非追加系: 隣接セル (19,12) は上面で反応しない → 下面 (20,12) にフォールバック
        Assert.IsTrue(TerrainGridPicker.TryPickEditCell(
            origin, direction, 0, Vector3.down, blocks, additive: false, out int nx, out int nz));
        Assert.AreEqual(20, nx);
        Assert.AreEqual(12, nz);

        // 追加系: 隣接セル (19,12) が上面で成立し、下面より優先される
        Assert.IsTrue(TerrainGridPicker.TryPickEditCell(
            origin, direction, 0, Vector3.down, blocks, additive: true, out int ax, out int az));
        Assert.AreEqual(19, ax);
        Assert.AreEqual(12, az);
    }
}
