using System;

/// <summary>
/// メッシュ生成の隣接判定用ボクセル参照。範囲外座標も安全に参照できることを保証する
/// （world-creation.md セクション 15.14 のチャンク境界・ワールド境界ルールを実装側が吸収する）。
/// </summary>
public interface ITerrainVoxelSampler
{
    /// <summary>
    /// ワールド座標のボクセルバイトを返す。
    /// ワールド下端より下（y &lt; 0）は「地形あり」（仮想 cube）、その他の範囲外は empty を返すこと。
    /// ストリーミング実装では未ロードチャンクも「地形あり」を返すこと（見えすぎ回避）。
    /// </summary>
    byte GetVoxel(int x, int y, int z);
}

/// <summary>
/// TerrainVoxelStore 全体をロード済みとして参照する標準サンプラー。
/// </summary>
public class TerrainStoreSampler : ITerrainVoxelSampler
{
    private static readonly byte VirtualGround = TerrainVoxel.Encode(TerrainShape.Cube, 0);

    private readonly TerrainVoxelStore _store;

    public TerrainStoreSampler(TerrainVoxelStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public byte GetVoxel(int x, int y, int z)
    {
        if (y < 0)
            return VirtualGround; // ワールド下端の下は「地形あり」扱い（地表の底面を生成しない）
        if (!TerrainVoxelStore.InBounds(x, y, z))
            return TerrainVoxel.Empty;
        return _store.GetVoxel(x, y, z);
    }
}
