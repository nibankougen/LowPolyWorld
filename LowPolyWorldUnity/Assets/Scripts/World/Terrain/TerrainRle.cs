using System.Collections.Generic;

/// <summary>
/// 地形ボクセルデータの RLE 圧縮 / 展開（world-creation.md セクション 15.13）。
///
/// 形式: `(byte_value, count)` の 1 バイトペアの繰り返し。count は 1〜255。
/// 同一バイトが 256 以上続く場合は複数ペアに分割する。
/// </summary>
public static class TerrainRle
{
    public const int MaxRunLength = 255;

    /// <summary>バイト列を RLE 圧縮する。</summary>
    public static byte[] Encode(byte[] data)
    {
        if (data == null || data.Length == 0)
            return System.Array.Empty<byte>();

        var output = new List<byte>();
        byte current = data[0];
        int run = 1;

        for (int i = 1; i < data.Length; i++)
        {
            if (data[i] == current && run < MaxRunLength)
            {
                run++;
                continue;
            }
            output.Add(current);
            output.Add((byte)run);
            current = data[i];
            run = 1;
        }
        output.Add(current);
        output.Add((byte)run);

        return output.ToArray();
    }

    /// <summary>
    /// RLE データを展開する。展開結果が expectedLength と一致しない・
    /// 形式が不正（奇数長・count=0）な場合は null を返す（UGC データの防御）。
    /// </summary>
    public static byte[] Decode(byte[] rle, int expectedLength)
    {
        if (rle == null || (rle.Length & 1) != 0)
            return null;

        var output = new byte[expectedLength];
        int pos = 0;

        for (int i = 0; i < rle.Length; i += 2)
        {
            byte value = rle[i];
            int count = rle[i + 1];
            if (count == 0)
                return null;
            if (pos + count > expectedLength)
                return null;

            for (int n = 0; n < count; n++)
                output[pos++] = value;
        }

        return pos == expectedLength ? output : null;
    }
}
