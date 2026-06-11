using NUnit.Framework;

public class TerrainNeighborRulesTests
{
    private static byte V(TerrainShape shape, int palette = 0) => TerrainVoxel.Encode(shape, palette);

    [Test]
    public void HidesTopFace_CubeAndRampHide_DiagAndEmptyDoNot()
    {
        Assert.IsTrue(TerrainNeighborRules.HidesTopFace(V(TerrainShape.Cube)));
        Assert.IsTrue(TerrainNeighborRules.HidesTopFace(V(TerrainShape.RampN)), "ramp の下面は full");
        Assert.IsTrue(TerrainNeighborRules.HidesTopFace(V(TerrainShape.RampE)));
        Assert.IsTrue(TerrainNeighborRules.HidesTopFace(V(TerrainShape.RampS)));
        Assert.IsTrue(TerrainNeighborRules.HidesTopFace(V(TerrainShape.RampW)));
        Assert.IsFalse(TerrainNeighborRules.HidesTopFace(V(TerrainShape.DiagNW)), "diag の下面は三角形");
        Assert.IsFalse(TerrainNeighborRules.HidesTopFace(TerrainVoxel.Empty));
    }

    [Test]
    public void HidesBottomFace_OnlyCubeHides()
    {
        Assert.IsTrue(TerrainNeighborRules.HidesBottomFace(V(TerrainShape.Cube)));
        Assert.IsFalse(TerrainNeighborRules.HidesBottomFace(V(TerrainShape.RampN)), "ramp の上面は斜面");
        Assert.IsFalse(TerrainNeighborRules.HidesBottomFace(V(TerrainShape.DiagSE)));
        Assert.IsFalse(TerrainNeighborRules.HidesBottomFace(TerrainVoxel.Empty));
    }

    [Test]
    public void HidesSideFace_CubeHides_RampAndEmptyDoNot()
    {
        Assert.IsTrue(TerrainNeighborRules.HidesSideFace(V(TerrainShape.Cube), TerrainFaceDir.East));
        Assert.IsFalse(TerrainNeighborRules.HidesSideFace(V(TerrainShape.RampN), TerrainFaceDir.South), "ramp は側面を隠さない（15.12）");
        Assert.IsFalse(TerrainNeighborRules.HidesSideFace(TerrainVoxel.Empty, TerrainFaceDir.North));
    }

    [Test]
    public void HidesSideFace_DiagNW_MatchesSpecExample()
    {
        byte diagNW = V(TerrainShape.DiagNW);

        // A が B の West 側（A の East 面）→ B の West 面は full → 非表示
        Assert.IsTrue(TerrainNeighborRules.HidesSideFace(diagNW, TerrainFaceDir.East));
        // A が B の North 側（A の South 面）→ B の North 面は full → 非表示
        Assert.IsTrue(TerrainNeighborRules.HidesSideFace(diagNW, TerrainFaceDir.South));
        // A が B の East 側（A の West 面）→ B の East 面は partial → 表示
        Assert.IsFalse(TerrainNeighborRules.HidesSideFace(diagNW, TerrainFaceDir.West));
        // A が B の South 側（A の North 面）→ B の South 面は partial → 表示
        Assert.IsFalse(TerrainNeighborRules.HidesSideFace(diagNW, TerrainFaceDir.North));
    }

    [Test]
    public void DiagCoversFace_AllDirections()
    {
        Assert.IsTrue(TerrainNeighborRules.DiagCoversFace(TerrainShape.DiagNW, TerrainFaceDir.North));
        Assert.IsTrue(TerrainNeighborRules.DiagCoversFace(TerrainShape.DiagNW, TerrainFaceDir.West));
        Assert.IsTrue(TerrainNeighborRules.DiagCoversFace(TerrainShape.DiagNE, TerrainFaceDir.North));
        Assert.IsTrue(TerrainNeighborRules.DiagCoversFace(TerrainShape.DiagNE, TerrainFaceDir.East));
        Assert.IsTrue(TerrainNeighborRules.DiagCoversFace(TerrainShape.DiagSE, TerrainFaceDir.South));
        Assert.IsTrue(TerrainNeighborRules.DiagCoversFace(TerrainShape.DiagSE, TerrainFaceDir.East));
        Assert.IsTrue(TerrainNeighborRules.DiagCoversFace(TerrainShape.DiagSW, TerrainFaceDir.South));
        Assert.IsTrue(TerrainNeighborRules.DiagCoversFace(TerrainShape.DiagSW, TerrainFaceDir.West));

        Assert.IsFalse(TerrainNeighborRules.DiagCoversFace(TerrainShape.DiagNW, TerrainFaceDir.East));
        Assert.IsFalse(TerrainNeighborRules.DiagCoversFace(TerrainShape.DiagNW, TerrainFaceDir.South));
        Assert.IsFalse(TerrainNeighborRules.DiagCoversFace(TerrainShape.Cube, TerrainFaceDir.North), "diag 以外は常に false");
    }

    [Test]
    public void IsSameKind_SamePaletteAndNonEmpty()
    {
        Assert.IsTrue(TerrainNeighborRules.IsSameKind(V(TerrainShape.Cube, 3), V(TerrainShape.RampN, 3)), "形状が違ってもパレットが同じなら同種");
        Assert.IsFalse(TerrainNeighborRules.IsSameKind(V(TerrainShape.Cube, 3), V(TerrainShape.Cube, 4)));
        Assert.IsFalse(TerrainNeighborRules.IsSameKind(TerrainVoxel.Empty, TerrainVoxel.Empty), "empty 同士は同種ではない");
        Assert.IsFalse(TerrainNeighborRules.IsSameKind(V(TerrainShape.Cube, 0), TerrainVoxel.Empty));
    }

    [Test]
    public void FaceDirUtil_OffsetAndOpposite()
    {
        Assert.AreEqual((0, 1, 0), TerrainFaceDirUtil.Offset(TerrainFaceDir.Up));
        Assert.AreEqual((0, 0, 1), TerrainFaceDirUtil.Offset(TerrainFaceDir.North));
        Assert.AreEqual((1, 0, 0), TerrainFaceDirUtil.Offset(TerrainFaceDir.East));
        Assert.AreEqual(TerrainFaceDir.South, TerrainFaceDirUtil.Opposite(TerrainFaceDir.North));
        Assert.AreEqual(TerrainFaceDir.Up, TerrainFaceDirUtil.Opposite(TerrainFaceDir.Down));
        Assert.Throws<System.ArgumentOutOfRangeException>(() => TerrainFaceDirUtil.Offset(TerrainFaceDir.Slope));
        Assert.Throws<System.ArgumentOutOfRangeException>(() => TerrainFaceDirUtil.Opposite(TerrainFaceDir.Hypotenuse));
    }
}
