using System.Collections.Generic;

/// <summary>
/// 地形ボクセルデータの RLE 圧縮 / 展開（world-creation.md セクション 15.13）。
///
/// 形式: `(ushort value, byte count)` の 3 バイトペアの繰り返し。
/// value はリトルエンディアン 2 バイト。count は 1〜255。
/// 同一ボクセルが 256 以上続く場合は複数ペアに分割する。
/// </summary>
public static class TerrainRle
{
    public const int MaxRunLength = 255;

    private const int PairBytes = 3; // value(2 LE) + count(1)

    /// <summary>ボクセル列を RLE 圧縮する。</summary>
    public static byte[] Encode(ushort[] data)
    {
        if (data == null || data.Length == 0)
            return System.Array.Empty<byte>();

        var output = new List<byte>();
        ushort current = data[0];
        int run = 1;

        for (int i = 1; i < data.Length; i++)
        {
            if (data[i] == current && run < MaxRunLength)
            {
                run++;
                continue;
            }
            WritePair(output, current, run);
            current = data[i];
            run = 1;
        }
        WritePair(output, current, run);

        return output.ToArray();
    }

    /// <summary>
    /// RLE データを展開する。展開結果が expectedLength と一致しない・
    /// 形式が不正（長さが 3 の倍数でない・count=0）な場合は null を返す（UGC データの防御）。
    /// </summary>
    public static ushort[] Decode(byte[] rle, int expectedLength)
    {
        if (rle == null || rle.Length % PairBytes != 0)
            return null;

        var output = new ushort[expectedLength];
        int pos = 0;

        for (int i = 0; i < rle.Length; i += PairBytes)
        {
            ushort value = (ushort)(rle[i] | (rle[i + 1] << 8));
            int count = rle[i + 2];
            if (count == 0)
                return null;
            if (pos + count > expectedLength)
                return null;

            for (int n = 0; n < count; n++)
                output[pos++] = value;
        }

        return pos == expectedLength ? output : null;
    }

    private static void WritePair(List<byte> output, ushort value, int run)
    {
        output.Add((byte)(value & 0xFF));
        output.Add((byte)(value >> 8));
        output.Add((byte)run);
    }
}
