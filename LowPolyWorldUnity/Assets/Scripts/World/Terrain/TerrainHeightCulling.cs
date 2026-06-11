using UnityEngine;

/// <summary>
/// プレイヤー高さによる非表示（Height Culling）の閾値計算と判定（world-creation.md セクション 15.11）。
///
/// プレイヤー位置の真上のボクセル列を走査し、最初に見つかった地形ブロックの Y グリッドインデックスを
/// 「非表示高さ閾値」とする（物理レイキャストと等価・ボクセルデータ直接参照）。
/// 閾値以上の Y にある地形・オブジェクト・他プレイヤーを非表示にする。
///
/// - デフォルト ON。ワールド設定で OFF 可・ワールド編集モード中は常に OFF（呼び出し側が制御）
/// - 非表示はクライアントサイドのみ（他プレイヤーの画面には影響しない）
/// - 座標は store グリッド基準（メッシュ・コライダーと同じ。1 ブロック = 0.5m）
/// </summary>
public static class TerrainHeightCulling
{
    /// <summary>真上に地形がない（何も非表示にしない）ことを表す閾値。</summary>
    public const int NoCulling = -1;

    /// <summary>
    /// 非表示高さ閾値（Y グリッドインデックス）を計算する。
    /// プレイヤーがいるグリッドセルの 1 つ上から上方向に走査し、最初の非 empty ボクセルの Y を返す。
    /// ヒットしない場合（屋外）・XZ がワールド範囲外の場合は NoCulling。
    /// </summary>
    public static int ComputeThreshold(ITerrainVoxelSampler sampler, Vector3 playerStorePosition)
    {
        if (sampler == null)
            throw new System.ArgumentNullException(nameof(sampler));

        int gx = Mathf.FloorToInt(playerStorePosition.x / TerrainMeshBuilder.BlockSize);
        int gz = Mathf.FloorToInt(playerStorePosition.z / TerrainMeshBuilder.BlockSize);
        if ((uint)gx >= TerrainVoxelStore.SizeX || (uint)gz >= TerrainVoxelStore.SizeZ)
            return NoCulling;

        int gy = Mathf.FloorToInt(playerStorePosition.y / TerrainMeshBuilder.BlockSize);
        int startY = gy + 1 > 0 ? gy + 1 : 0;
        for (int y = startY; y < TerrainVoxelStore.SizeY; y++)
        {
            if (!TerrainVoxel.IsEmpty(sampler.GetVoxel(gx, y, gz)))
                return y;
        }
        return NoCulling;
    }

    /// <summary>グリッド Y（地形ブロック・グリッド配置オブジェクト）が非表示対象か。</summary>
    public static bool IsGridYHidden(int gridY, int threshold) =>
        threshold != NoCulling && gridY >= threshold;

    /// <summary>
    /// store グリッド基準 Unity 座標の Y（他プレイヤー・自由配置オブジェクト）が非表示対象か。
    /// 閾値ブロックの下端（threshold × 0.5m）以上を非表示にする。
    /// </summary>
    public static bool IsWorldYHidden(float storeUnityY, int threshold) =>
        threshold != NoCulling && storeUnityY >= threshold * TerrainMeshBuilder.BlockSize;

    /// <summary>
    /// チャンク全体が閾値より上にあるか（true ならシェーダークリップを待たず
    /// チャンクのレンダラーごと無効化して頂点コストも削減できる — 15.11 表示反映方式）。
    /// </summary>
    public static bool IsChunkFullyHidden(int chunkY, int threshold) =>
        threshold != NoCulling && chunkY * TerrainChunk.Size >= threshold;
}
