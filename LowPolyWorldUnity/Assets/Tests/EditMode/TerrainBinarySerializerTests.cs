using NUnit.Framework;

public class TerrainBinarySerializerTests
{
    private static TerrainVoxelStore BuildSampleStore()
    {
        var store = new TerrainVoxelStore();
        ushort grass = TerrainVoxel.Encode(TerrainShape.Cube, 0);
        ushort brick = TerrainVoxel.Encode(TerrainShape.RampN, 3);

        // 地表 1 層（チャンク (0,0,0)〜(1,0,1) にまたがる）
        for (int x = 0; x < 20; x++)
            for (int z = 0; z < 20; z++)
                store.SetVoxel(x, 0, z, grass);

        // 別チャンクの坂
        store.SetVoxel(40, 16, 40, brick);
        return store;
    }

    [Test]
    public void SerializeDeserialize_RoundTrips()
    {
        var store = BuildSampleStore();

        var data = TerrainBinarySerializer.Serialize(store);
        bool ok = TerrainBinarySerializer.TryDeserialize(data, out var restored, out string error);

        Assert.IsTrue(ok, error);
        Assert.AreEqual(store.NonEmptyChunkCount, restored.NonEmptyChunkCount);
        Assert.AreEqual(store.GetVoxel(0, 0, 0), restored.GetVoxel(0, 0, 0));
        Assert.AreEqual(store.GetVoxel(19, 0, 19), restored.GetVoxel(19, 0, 19));
        Assert.AreEqual(store.GetVoxel(40, 16, 40), restored.GetVoxel(40, 16, 40));
        Assert.AreEqual(TerrainVoxel.Empty, restored.GetVoxel(30, 5, 30), "未設定セルは empty");
    }

    [Test]
    public void SerializeDeserialize_CornerShapes_RoundTrip()
    {
        var store = new TerrainVoxelStore();
        store.SetVoxel(3, 5, 3, TerrainVoxel.Encode(TerrainShape.CornerNW, 1));
        store.SetVoxel(4, 5, 3, TerrainVoxel.Encode(TerrainShape.CornerNE, 2));
        store.SetVoxel(5, 5, 3, TerrainVoxel.Encode(TerrainShape.CornerSE, 0));
        store.SetVoxel(6, 5, 3, TerrainVoxel.Encode(TerrainShape.CornerSW, 15));

        var data = TerrainBinarySerializer.Serialize(store);
        Assert.IsTrue(TerrainBinarySerializer.TryDeserialize(data, out var restored, out string error), error);

        Assert.AreEqual(TerrainShape.CornerNW, TerrainVoxel.GetShape(restored.GetVoxel(3, 5, 3)));
        Assert.AreEqual(TerrainShape.CornerNE, TerrainVoxel.GetShape(restored.GetVoxel(4, 5, 3)));
        Assert.AreEqual(TerrainShape.CornerSE, TerrainVoxel.GetShape(restored.GetVoxel(5, 5, 3)));
        Assert.AreEqual(TerrainShape.CornerSW, TerrainVoxel.GetShape(restored.GetVoxel(6, 5, 3)));
        Assert.AreEqual(15, TerrainVoxel.GetPaletteIndex(restored.GetVoxel(6, 5, 3)));
    }

    [Test]
    public void Serialize_HeaderFormat_MatchesSpec()
    {
        var data = TerrainBinarySerializer.Serialize(new TerrainVoxelStore());

        // magic "LWVT"
        Assert.AreEqual(0x4C, data[0]);
        Assert.AreEqual(0x57, data[1]);
        Assert.AreEqual(0x56, data[2]);
        Assert.AreEqual(0x54, data[3]);
        // version 2（リトルエンディアン）
        Assert.AreEqual(2, System.BitConverter.ToInt32(data, 4));
        // chunk count 0
        Assert.AreEqual(0, System.BitConverter.ToInt32(data, 8));
        Assert.AreEqual(12, data.Length, "空ワールドはヘッダーのみ");
    }

    [Test]
    public void Serialize_EmptyChunks_AreOmitted()
    {
        var store = new TerrainVoxelStore();
        ushort v = TerrainVoxel.Encode(TerrainShape.Cube, 0);
        store.SetVoxel(0, 0, 0, v);
        store.SetVoxel(0, 0, 0, TerrainVoxel.Empty); // 空に戻す

        var data = TerrainBinarySerializer.Serialize(store);

        Assert.AreEqual(0, System.BitConverter.ToInt32(data, 8), "空チャンクは省略（仕様 15.13）");
    }

    [Test]
    public void TryDeserialize_InvalidMagicOrVersion_Fails()
    {
        var data = TerrainBinarySerializer.Serialize(new TerrainVoxelStore());

        var badMagic = (byte[])data.Clone();
        badMagic[0] = 0x00;
        Assert.IsFalse(TerrainBinarySerializer.TryDeserialize(badMagic, out _, out var e1));
        StringAssert.Contains("マジック", e1);

        var badVersion = (byte[])data.Clone();
        badVersion[4] = 99;
        Assert.IsFalse(TerrainBinarySerializer.TryDeserialize(badVersion, out _, out var e2));
        StringAssert.Contains("バージョン", e2);

        Assert.IsFalse(TerrainBinarySerializer.TryDeserialize(null, out _, out _));
        Assert.IsFalse(TerrainBinarySerializer.TryDeserialize(new byte[4], out _, out _));
    }

    [Test]
    public void TryDeserialize_TruncatedData_Fails()
    {
        var data = TerrainBinarySerializer.Serialize(BuildSampleStore());
        var truncated = new byte[data.Length - 10];
        System.Array.Copy(data, truncated, truncated.Length);

        Assert.IsFalse(TerrainBinarySerializer.TryDeserialize(truncated, out _, out string error));
        Assert.IsNotNull(error);
    }

    [Test]
    public void TryDeserialize_TrailingGarbage_Fails()
    {
        var data = TerrainBinarySerializer.Serialize(BuildSampleStore());
        var padded = new byte[data.Length + 4];
        System.Array.Copy(data, padded, data.Length);

        Assert.IsFalse(TerrainBinarySerializer.TryDeserialize(padded, out _, out string error));
        StringAssert.Contains("余分", error);
    }

    [Test]
    public void TryDeserialize_ChunkCoordsOutOfGrid_Fails()
    {
        // チャンク (4,0,0) はグリッド外（4×2×4）
        var data = BuildSingleChunkBinary(cx: 4, cy: 0, cz: 0, fillVoxel: TerrainVoxel.Empty);
        Assert.IsFalse(TerrainBinarySerializer.TryDeserialize(data, out _, out string error));
        StringAssert.Contains("範囲外", error);
    }

    [Test]
    public void TryDeserialize_PaddingRegionTerrain_Fails()
    {
        // 端チャンク (3,0,0) の全セルを埋めると、ワールド X=63 のパディング領域にも地形が入る
        var data = BuildSingleChunkBinary(
            cx: 3, cy: 0, cz: 0, fillVoxel: TerrainVoxel.Encode(TerrainShape.Cube, 0));
        Assert.IsFalse(TerrainBinarySerializer.TryDeserialize(data, out _, out string error));
        StringAssert.Contains("パディング", error);
    }

    [Test]
    public void TryDeserialize_DuplicateChunkCoords_Fails()
    {
        var single = BuildSingleChunkBinary(0, 0, 0, TerrainVoxel.Encode(TerrainShape.Cube, 0));
        // チャンクエントリ部分（ヘッダー 12 バイト以降）を複製して 2 チャンクにする
        int entryLength = single.Length - 12;
        var doubled = new byte[12 + entryLength * 2];
        System.Array.Copy(single, doubled, single.Length);
        System.Array.Copy(single, 12, doubled, single.Length, entryLength);
        System.Array.Copy(System.BitConverter.GetBytes(2), 0, doubled, 8, 4); // chunk count = 2

        Assert.IsFalse(TerrainBinarySerializer.TryDeserialize(doubled, out _, out string error));
        StringAssert.Contains("重複", error);
    }

    // 指定座標の 1 チャンク（全セルを fillVoxel で埋める）だけを含むバイナリを手組みする
    private static byte[] BuildSingleChunkBinary(int cx, int cy, int cz, ushort fillVoxel)
    {
        var voxels = new ushort[TerrainChunk.VoxelCount];
        for (int i = 0; i < voxels.Length; i++)
            voxels[i] = fillVoxel;
        var rle = TerrainRle.Encode(voxels);

        using var ms = new System.IO.MemoryStream();
        using var writer = new System.IO.BinaryWriter(ms);
        writer.Write(new byte[] { 0x4C, 0x57, 0x56, 0x54 });
        writer.Write(TerrainBinarySerializer.Version); // version
        writer.Write(1);          // chunk count
        writer.Write((byte)cx);
        writer.Write((byte)cy);
        writer.Write((byte)cz);
        writer.Write((ushort)rle.Length);
        writer.Write(rle);
        writer.Flush();
        return ms.ToArray();
    }
}
