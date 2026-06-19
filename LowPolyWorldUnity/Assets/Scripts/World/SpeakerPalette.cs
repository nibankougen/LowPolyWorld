using UnityEngine;

/// <summary>
/// 話者の色分け用プリセットパレット（9.13）。話者編集画面で選び、会話エディタで話者アイコン
/// （icon_speaker）の色として表示する。<see cref="SpeakerJson.colorIndex"/> がこの配列の添字。
///
/// 純粋データ（<see cref="Color"/> は副作用のない値型なのでロジック層でも使用可）。新規話者には
/// <see cref="SpeakerLibraryLogic"/> がまだ使われていない色を先頭から自動で割り当てる。
/// </summary>
public static class SpeakerPalette
{
    /// <summary>
    /// プリセット色（順番が「上から割り当て」の順）。隣り合う色が見分けやすいよう、
    /// 色相をなるべく離して配置している（黄色とオレンジが近すぎた点を再配色済み）。
    /// </summary>
    public static readonly Color[] Colors =
    {
        (Color)new Color32(0xE5, 0x39, 0x35, 0xFF), // 赤
        (Color)new Color32(0xFB, 0x8C, 0x00, 0xFF), // オレンジ
        (Color)new Color32(0xFD, 0xD8, 0x35, 0xFF), // 黄（明るくしてオレンジと区別）
        (Color)new Color32(0x7C, 0xB3, 0x42, 0xFF), // 黄緑
        (Color)new Color32(0x1B, 0x9E, 0x5A, 0xFF), // 緑
        (Color)new Color32(0x00, 0x97, 0xA7, 0xFF), // ティール
        (Color)new Color32(0x1E, 0x88, 0xE5, 0xFF), // 青
        (Color)new Color32(0x3F, 0x37, 0xC9, 0xFF), // 藍
        (Color)new Color32(0x9C, 0x27, 0xB0, 0xFF), // 紫
        (Color)new Color32(0xE9, 0x1E, 0x8C, 0xFF), // ピンク
    };

    /// <summary>パレットの色数。</summary>
    public static int Count => Colors.Length;

    /// <summary>index が有効な添字か（割り当て済みの色か）。</summary>
    public static bool IsValidIndex(int index) => index >= 0 && index < Colors.Length;

    /// <summary>index の色（範囲外は先頭色）。</summary>
    public static Color ColorOf(int index) => IsValidIndex(index) ? Colors[index] : Colors[0];
}
