/// <summary>
/// 保存前に半透明ピクセルを丸める処理（純粋 C#）。
/// α &lt; 128 → α=0, RGB=(0,0,0) / α ≥ 128 → α=255, RGB 保持。
/// ASTC 圧縮時の容量削減が目的。
/// </summary>
public static class TransparentPixelProcessor
{
    /// <summary>
    /// RGBA バイト配列をインプレースで処理する（width * height * 4 バイト）。
    /// </summary>
    public static void Process(byte[] pixels)
    {
        if (pixels == null)
            return;

        int pixelCount = pixels.Length / 4;
        for (int i = 0; i < pixelCount; i++)
        {
            int b = i * 4;
            byte a = pixels[b + 3];
            if (a < 128)
            {
                pixels[b] = 0;
                pixels[b + 1] = 0;
                pixels[b + 2] = 0;
                pixels[b + 3] = 0;
            }
            else
            {
                pixels[b + 3] = 255;
                // RGB は保持（変更しない）
            }
        }
    }
}
