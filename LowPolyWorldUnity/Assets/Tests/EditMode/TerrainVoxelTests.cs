using NUnit.Framework;

public class TerrainVoxelTests
{
    [Test]
    public void Encode_ShapeAndPalette_RoundTrips()
    {
        byte voxel = TerrainVoxel.Encode(TerrainShape.RampE, 12);

        Assert.AreEqual(TerrainShape.RampE, TerrainVoxel.GetShape(voxel));
        Assert.AreEqual(12, TerrainVoxel.GetPaletteIndex(voxel));
        Assert.IsFalse(TerrainVoxel.IsEmpty(voxel));
    }

    [Test]
    public void Encode_BitLayout_MatchesSpec()
    {
        // bit 7-4 = shape / bit 3-0 = palette index（仕様 15.13）
        byte voxel = TerrainVoxel.Encode(TerrainShape.Cube, 15);
        Assert.AreEqual(0x1F, voxel);

        voxel = TerrainVoxel.Encode(TerrainShape.DiagSW, 0);
        Assert.AreEqual(0x90, voxel);
    }

    [Test]
    public void Encode_Empty_NormalizesPaletteToZero()
    {
        byte voxel = TerrainVoxel.Encode(TerrainShape.Empty, 7);
        Assert.AreEqual(TerrainVoxel.Empty, voxel, "empty はパレットも 0 に正規化");
        Assert.IsTrue(TerrainVoxel.IsEmpty(voxel));
    }

    [Test]
    public void Encode_PaletteOutOfRange_Throws()
    {
        Assert.Throws<System.ArgumentOutOfRangeException>(
            () => TerrainVoxel.Encode(TerrainShape.Cube, 16));
        Assert.Throws<System.ArgumentOutOfRangeException>(
            () => TerrainVoxel.Encode(TerrainShape.Cube, -1));
    }

    [Test]
    public void IsValid_RejectsUnknownShapeNibble()
    {
        Assert.IsTrue(TerrainVoxel.IsValid(0x00), "empty は有効");
        Assert.IsTrue(TerrainVoxel.IsValid(0x9F), "diag_SW + palette 15 は有効");
        Assert.IsFalse(TerrainVoxel.IsValid(0xA0), "shape 10 は未定義");
        Assert.IsFalse(TerrainVoxel.IsValid(0xF3), "shape 15 は未定義");
        Assert.IsFalse(TerrainVoxel.IsValid(0x05), "empty + palette ≠ 0 は不正（正規化前提）");
    }
}
