using NUnit.Framework;

public class TerrainRleTests
{
    [Test]
    public void Encode_RunsCompressToPairs()
    {
        // AAAABB → (A,4)(B,2)。pair = value(2 byte LE) + count(1 byte)
        var data = new ushort[] { 0x0111, 0x0111, 0x0111, 0x0111, 0x0022, 0x0022 };
        var rle = TerrainRle.Encode(data);

        CollectionAssert.AreEqual(
            new byte[] { 0x11, 0x01, 4, 0x22, 0x00, 2 }, rle);
    }

    [Test]
    public void Encode_RunOver255_SplitsIntoMultiplePairs()
    {
        var data = new ushort[300];
        for (int i = 0; i < data.Length; i++)
            data[i] = 0x0111;

        var rle = TerrainRle.Encode(data);

        CollectionAssert.AreEqual(
            new byte[] { 0x11, 0x01, 255, 0x11, 0x01, 45 }, rle,
            "256 以上の連続は複数ペアに分割（仕様 15.13）");
    }

    [Test]
    public void Encode_EmptyInput_ReturnsEmpty()
    {
        Assert.AreEqual(0, TerrainRle.Encode(System.Array.Empty<ushort>()).Length);
        Assert.AreEqual(0, TerrainRle.Encode(null).Length);
    }

    [Test]
    public void EncodeDecode_RoundTrips()
    {
        var data = new ushort[1000];
        var random = new System.Random(42);
        for (int i = 0; i < data.Length; i++)
            data[i] = (ushort)(random.Next(3) * 0x0111); // 繰り返しが出やすいデータ

        var decoded = TerrainRle.Decode(TerrainRle.Encode(data), data.Length);

        CollectionAssert.AreEqual(data, decoded);
    }

    [Test]
    public void Decode_InvalidInput_ReturnsNull()
    {
        Assert.IsNull(TerrainRle.Decode(null, 10));
        Assert.IsNull(TerrainRle.Decode(new byte[] { 0x11, 0x00 }, 10), "3 の倍数でない長さは不正");
        Assert.IsNull(TerrainRle.Decode(new byte[] { 0x11, 0x00, 0 }, 10), "count=0 は不正");
        Assert.IsNull(TerrainRle.Decode(new byte[] { 0x11, 0x00, 5 }, 10), "展開長の不足");
        Assert.IsNull(TerrainRle.Decode(new byte[] { 0x11, 0x00, 20 }, 10), "展開長の超過");
    }
}
