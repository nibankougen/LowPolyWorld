using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// パレットインデックス + 領域 + バリアント → 地形アトラス上の UV 矩形を解決する。
/// TerrainMeshBuilder はこのインターフェース経由でアトラスレイアウトを参照する。
/// </summary>
public interface ITerrainAtlasMap
{
    /// <summary>領域のバリアント数（必ず 2 の累乗。バリアントなしは 1）。</summary>
    int GetVariantCount(int paletteIndex, TerrainFaceRegion region);

    /// <summary>アトラス全体を [0,1]² としたときの領域 UV 矩形。</summary>
    Rect GetUvRect(int paletteIndex, TerrainFaceRegion region, int variantIndex);
}

/// <summary>
/// 標準実装。パレットごとの「アトラス内テクスチャ矩形 + テクスチャ種別（ランダム / 固定）」から、
/// TerrainTextureLayout の領域レイアウトを合成して最終 UV 矩形を返す。
/// テクスチャ矩形はサーバー生成の terrainAtlasUVMap（world JSON）から構築する想定。
/// </summary>
public class TerrainAtlasMap : ITerrainAtlasMap
{
    public readonly struct Entry
    {
        public readonly bool IsFixedTexture;
        public readonly Rect TextureRect; // アトラス全体 [0,1]² の中でのテクスチャ矩形

        public Entry(bool isFixedTexture, Rect textureRect)
        {
            IsFixedTexture = isFixedTexture;
            TextureRect = textureRect;
        }
    }

    private readonly Entry[] _entries;

    public TerrainAtlasMap(IReadOnlyList<Entry> entries)
    {
        if (entries == null)
            throw new ArgumentNullException(nameof(entries));
        if (entries.Count > TerrainVoxel.MaxPaletteIndex + 1)
            throw new ArgumentException(
                $"パレットは最大 {TerrainVoxel.MaxPaletteIndex + 1} 種類です。", nameof(entries));

        _entries = new Entry[entries.Count];
        for (int i = 0; i < entries.Count; i++)
            _entries[i] = entries[i];
    }

    public int GetVariantCount(int paletteIndex, TerrainFaceRegion region) =>
        TerrainTextureLayout.GetVariantCount(region, EntryAt(paletteIndex).IsFixedTexture);

    public Rect GetUvRect(int paletteIndex, TerrainFaceRegion region, int variantIndex)
    {
        var entry = EntryAt(paletteIndex);
        Rect local = TerrainTextureLayout.GetRegionRect(region, variantIndex, entry.IsFixedTexture);
        Rect tex = entry.TextureRect;
        return new Rect(
            tex.x + local.x * tex.width,
            tex.y + local.y * tex.height,
            local.width * tex.width,
            local.height * tex.height);
    }

    private Entry EntryAt(int paletteIndex)
    {
        if ((uint)paletteIndex >= (uint)_entries.Length)
            throw new ArgumentOutOfRangeException(
                nameof(paletteIndex), $"パレットインデックスは 0〜{_entries.Length - 1} の範囲で指定してください。");
        return _entries[paletteIndex];
    }
}
