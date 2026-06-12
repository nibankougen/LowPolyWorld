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
}
