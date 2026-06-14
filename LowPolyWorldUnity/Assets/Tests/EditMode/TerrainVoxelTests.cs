using NUnit.Framework;

public class TerrainVoxelTests
{
    [Test]
    public void Encode_ShapeAndPalette_RoundTrips()
    {
        ushort voxel = TerrainVoxel.Encode(TerrainShape.RampE, 12);

        Assert.AreEqual(TerrainShape.RampE, TerrainVoxel.GetShape(voxel));
        Assert.AreEqual(12, TerrainVoxel.GetPaletteIndex(voxel));
        Assert.IsFalse(TerrainVoxel.IsEmpty(voxel));
    }

    [Test]
    public void Encode_BitLayout_MatchesSpec()
    {
        // bit 8-4 = shape / bit 3-0 = palette index（仕様 15.13・2 byte voxel）
        ushort voxel = TerrainVoxel.Encode(TerrainShape.Cube, 15);
        Assert.AreEqual(0x1F, voxel);

        voxel = TerrainVoxel.Encode(TerrainShape.DiagSW, 0);
        Assert.AreEqual(0x90, voxel);
    }

    [Test]
    public void Encode_Empty_NormalizesPaletteToZero()
    {
        ushort voxel = TerrainVoxel.Encode(TerrainShape.Empty, 7);
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
    public void Encode_Corner_RoundTrips()
    {
        ushort voxel = TerrainVoxel.Encode(TerrainShape.CornerNW, 5);
        Assert.AreEqual(TerrainShape.CornerNW, TerrainVoxel.GetShape(voxel));
        Assert.AreEqual(5, TerrainVoxel.GetPaletteIndex(voxel));
        Assert.IsFalse(TerrainVoxel.IsEmpty(voxel));

        // bit 8-4 = shape（CornerSW = 13 = 0xD）/ bit 3-0 = palette
        Assert.AreEqual(0xD7, TerrainVoxel.Encode(TerrainShape.CornerSW, 7));
    }

    [Test]
    public void Encode_Concave_RoundTrips()
    {
        ushort voxel = TerrainVoxel.Encode(TerrainShape.ConcaveNW, 5);
        Assert.AreEqual(TerrainShape.ConcaveNW, TerrainVoxel.GetShape(voxel));
        Assert.AreEqual(5, TerrainVoxel.GetPaletteIndex(voxel));

        // ConcaveSW = 17 → (17 << 4) | 15 = 0x11F（1 byte に収まらないため 2 byte 化した）
        Assert.AreEqual(0x11F, TerrainVoxel.Encode(TerrainShape.ConcaveSW, 15));
    }

    [Test]
    public void IsValid_AcceptsDefinedShapes_RejectsUnknown()
    {
        Assert.IsTrue(TerrainVoxel.IsValid(0x00), "empty は有効");
        Assert.IsTrue(TerrainVoxel.IsValid(0x9F), "diag_SW + palette 15 は有効");
        Assert.IsTrue(TerrainVoxel.IsValid(0xA0), "corner_NW (shape 10) は有効");
        Assert.IsTrue(TerrainVoxel.IsValid(0xE0), "concave_NW (shape 14) は有効");
        Assert.IsTrue(TerrainVoxel.IsValid(0x11F), "concave_SW (shape 17) + palette 15 は有効");
        Assert.IsFalse(TerrainVoxel.IsValid(0x120), "shape 18 は未定義");
        Assert.IsFalse(TerrainVoxel.IsValid(0x200), "予約ビット（shape 32）は不正");
        Assert.IsFalse(TerrainVoxel.IsValid(0x05), "empty + palette ≠ 0 は不正（正規化前提）");
    }
}
