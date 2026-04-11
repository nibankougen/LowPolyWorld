using UnityEngine;

/// <summary>
/// 1024×2048 テクスチャアトラスのスロット割り当てと UV 座標計算を担う純粋 C# クラス。
/// キャラクタースロット 24 枠（4列×6行 @256×256、y:0〜1535）
/// アクセサリスロット 96 枠（16列×6行 @64×64、y:1536〜1919）
/// </summary>
public class AtlasLayout
{
    public const int AtlasWidth = 1024;
    public const int AtlasHeight = 2048;

    public const int CharacterSlotSize = 256;
    public const int CharacterColumns = 4;
    public const int CharacterRows = 6;
    public const int CharacterSlotCount = CharacterColumns * CharacterRows; // 24

    public const int AccessorySlotSize = 64;
    public const int AccessoryColumns = 16;
    public const int AccessoryRows = 6;
    public const int AccessorySlotCount = AccessoryColumns * AccessoryRows; // 96

    // アクセサリ領域の開始 Y ピクセル（y:1536〜）
    public const int AccessoryRegionY = CharacterSlotSize * CharacterRows; // 1536

    /// <summary>スロット境界の内側パディング（px）。ミップマップによるアトラスブリードを防ぐ。</summary>
    public const int SlotPadding = 2;

    private readonly bool[] _characterSlots = new bool[CharacterSlotCount];
    private readonly bool[] _accessorySlots = new bool[AccessorySlotCount];

    // ---- キャラクタースロット ----

    /// <summary>空きキャラクタースロットを確保して返す。満杯なら -1。</summary>
    public int AllocateCharacterSlot()
    {
        for (int i = 0; i < CharacterSlotCount; i++)
        {
            if (!_characterSlots[i])
            {
                _characterSlots[i] = true;
                return i;
            }
        }
        return -1;
    }

    /// <summary>キャラクタースロットを解放する。</summary>
    public void ReleaseCharacterSlot(int slotIndex)
    {
        if (slotIndex >= 0 && slotIndex < CharacterSlotCount)
            _characterSlots[slotIndex] = false;
    }

    /// <summary>
    /// キャラクタースロットのピクセル矩形（パディング内側）を返す。
    /// RenderTexture へのブリット領域として使用する。
    /// </summary>
    public RectInt GetCharacterPixelRect(int slotIndex)
    {
        int col = slotIndex % CharacterColumns;
        int row = slotIndex / CharacterColumns;
        int x = col * CharacterSlotSize + SlotPadding;
        int y = row * CharacterSlotSize + SlotPadding;
        int size = CharacterSlotSize - SlotPadding * 2;
        return new RectInt(x, y, size, size);
    }

    /// <summary>
    /// キャラクタースロットの UV 矩形（正規化・パディング内側）を返す。
    /// UV 原点は左下（Unity 標準）。
    /// </summary>
    public Rect GetCharacterUV(int slotIndex)
    {
        var px = GetCharacterPixelRect(slotIndex);
        return new Rect(
            (float)px.x / AtlasWidth,
            1f - (float)(px.y + px.height) / AtlasHeight,
            (float)px.width / AtlasWidth,
            (float)px.height / AtlasHeight
        );
    }

    // ---- アクセサリスロット ----

    /// <summary>空きアクセサリスロットを確保して返す。満杯なら -1。</summary>
    public int AllocateAccessorySlot()
    {
        for (int i = 0; i < AccessorySlotCount; i++)
        {
            if (!_accessorySlots[i])
            {
                _accessorySlots[i] = true;
                return i;
            }
        }
        return -1;
    }

    /// <summary>アクセサリスロットを解放する。</summary>
    public void ReleaseAccessorySlot(int slotIndex)
    {
        if (slotIndex >= 0 && slotIndex < AccessorySlotCount)
            _accessorySlots[slotIndex] = false;
    }

    /// <summary>
    /// アクセサリスロットのピクセル矩形（パディング内側）を返す。
    /// </summary>
    public RectInt GetAccessoryPixelRect(int slotIndex)
    {
        int col = slotIndex % AccessoryColumns;
        int row = slotIndex / AccessoryColumns;
        int x = col * AccessorySlotSize + SlotPadding;
        int y = AccessoryRegionY + row * AccessorySlotSize + SlotPadding;
        int size = AccessorySlotSize - SlotPadding * 2;
        return new RectInt(x, y, size, size);
    }

    /// <summary>
    /// アクセサリスロットの UV 矩形（正規化・パディング内側）を返す。
    /// </summary>
    public Rect GetAccessoryUV(int slotIndex)
    {
        var px = GetAccessoryPixelRect(slotIndex);
        return new Rect(
            (float)px.x / AtlasWidth,
            1f - (float)(px.y + px.height) / AtlasHeight,
            (float)px.width / AtlasWidth,
            (float)px.height / AtlasHeight
        );
    }

    // ---- 状態参照 ----

    public bool IsCharacterSlotAllocated(int slotIndex) =>
        slotIndex >= 0 && slotIndex < CharacterSlotCount && _characterSlots[slotIndex];

    public bool IsAccessorySlotAllocated(int slotIndex) =>
        slotIndex >= 0 && slotIndex < AccessorySlotCount && _accessorySlots[slotIndex];
}
