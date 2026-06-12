using UnityEngine;

/// <summary>
/// 3D ビューのレイ → 編集対象高さの XZ グリッドセル変換（純粋 C#・地形タブのタッチ入力用）。
/// レイは TerrainRenderer のローカル座標系（store グリッド基準・1 ブロック = 0.5m）で渡すこと。
/// </summary>
public static class TerrainGridPicker
{
    /// <summary>
    /// 編集高さの床平面（y = height × 0.5m）とレイの交点からグリッドセルを求める。
    /// 平面と交差しない・交点がワールド範囲外の場合は false。
    /// </summary>
    public static bool TryPickCell(
        Vector3 rayOrigin, Vector3 rayDirection, int height, out int x, out int z)
    {
        x = 0;
        z = 0;
        float planeY = height * TerrainMeshBuilder.BlockSize;
        float dy = rayDirection.y;
        if (Mathf.Abs(dy) < 1e-6f)
            return false; // 平面と平行

        float t = (planeY - rayOrigin.y) / dy;
        if (t < 0f)
            return false; // 平面が後方

        Vector3 hit = rayOrigin + rayDirection * t;
        x = Mathf.FloorToInt(hit.x / TerrainMeshBuilder.BlockSize);
        z = Mathf.FloorToInt(hit.z / TerrainMeshBuilder.BlockSize);
        return (uint)x < TerrainVoxelStore.SizeX && (uint)z < TerrainVoxelStore.SizeZ;
    }
}
