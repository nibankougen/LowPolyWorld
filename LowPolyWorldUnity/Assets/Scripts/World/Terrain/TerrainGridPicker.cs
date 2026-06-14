using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 3D ビューのレイ → 編集対象高さの XZ グリッドセル変換（純粋 C#・地形タブのタッチ入力用）。
/// レイは TerrainRenderer のローカル座標系（store グリッド基準・1 ブロック = 0.5m）で渡すこと。
///
/// 下面（y = height × 0.5m）はグリッドぴったりで判定する（従来挙動）。
/// 既にブロックがあるセル（追加系では隣接セルも）は上面（y = (height+1) × 0.5m）でも反応し、
/// その場合グリッドをカメラ側へブロックサイズの 15% ずらして判定する（俯瞰時に前面・上面を
/// タップしても狙ったセルを選べるようにするため）。上面・下面が両立する場合は上面を優先する。
/// （screens-and-modes.md 11.7.2）
///
/// 上面判定に使うブロック存在情報（<c>blockCells</c>）は、編集開始時（ポインタ押下時）の
/// 現在高さレイヤーのスナップショットを渡す。ストローク中の自分の編集で判定が変わらないようにし、
/// 「1 タップで奥のブロックまで消える」「ブロックが 2 個できる」といった不具合を防ぐ。
/// </summary>
public static class TerrainGridPicker
{
    /// <summary>上面グリッドをカメラ側へずらす量（ブロックサイズに対する割合）。</summary>
    public const float TopFaceCameraShift = 0.15f;

    /// <summary>
    /// 編集高さの床平面（y = height × 0.5m）とレイの交点からグリッドセルを求める。
    /// 平面と交差しない・交点がワールド範囲外の場合は false。
    /// </summary>
    public static bool TryPickCell(
        Vector3 rayOrigin, Vector3 rayDirection, int height, out int x, out int z)
    {
        return TryPickPlane(
            rayOrigin, rayDirection, height * TerrainMeshBuilder.BlockSize, 0f, 0f, out x, out z);
    }

    /// <summary>
    /// 編集対象セルを求める（下面＝グリッドぴったり / 上面＝カメラ側へ 15% ずらし）。
    /// 上面は「既にブロックがあるセル」、追加系（<paramref name="additive"/> = true）では
    /// 「ブロックに隣接するセル」でのみ反応し、その場合は下面より優先する。
    /// </summary>
    /// <param name="cameraForward">カメラの前方向（store ローカル座標系）。上面ずらしの向きに使う。</param>
    /// <param name="blockCells">
    /// 編集開始時の現在高さレイヤーでブロックがある (x, z) セルの集合（スナップショット）。
    /// </param>
    /// <param name="additive">ブラシ・図形など追加系のモードか（隣接セルも上面で反応させる）。</param>
    public static bool TryPickEditCell(
        Vector3 rayOrigin,
        Vector3 rayDirection,
        int height,
        Vector3 cameraForward,
        HashSet<(int x, int z)> blockCells,
        bool additive,
        out int x,
        out int z)
    {
        // カメラ側 = -前方向。グリッドをその向きへ 15% ずらす（= 交点を逆向きにずらして floor）
        GetCameraShift(cameraForward, out float shiftX, out float shiftZ);
        float topPlaneY = (height + 1) * TerrainMeshBuilder.BlockSize;
        if (TryPickPlane(rayOrigin, rayDirection, topPlaneY, shiftX, shiftZ, out int tx, out int tz)
            && QualifiesForTopFace(blockCells, tx, tz, additive))
        {
            x = tx;
            z = tz;
            return true;
        }

        return TryPickCell(rayOrigin, rayDirection, height, out x, out z);
    }

    // ── Private ───────────────────────────────────────────────────────────────

    /// <summary>
    /// 平面 y = planeY との交点を求め、(shiftX, shiftZ) ぶんずらしたグリッドでセルに変換する。
    /// 平面と平行・平面が後方・交点がワールド範囲外なら false。
    /// </summary>
    private static bool TryPickPlane(
        Vector3 rayOrigin, Vector3 rayDirection, float planeY, float shiftX, float shiftZ,
        out int x, out int z)
    {
        x = 0;
        z = 0;
        float dy = rayDirection.y;
        if (Mathf.Abs(dy) < 1e-6f)
            return false; // 平面と平行

        float t = (planeY - rayOrigin.y) / dy;
        if (t < 0f)
            return false; // 平面が後方

        Vector3 hit = rayOrigin + rayDirection * t;
        x = Mathf.FloorToInt((hit.x - shiftX) / TerrainMeshBuilder.BlockSize);
        z = Mathf.FloorToInt((hit.z - shiftZ) / TerrainMeshBuilder.BlockSize);
        return (uint)x < TerrainVoxelStore.SizeX && (uint)z < TerrainVoxelStore.SizeZ;
    }

    /// <summary>上面グリッドのずらしベクトル（カメラ側 = -前方向の水平成分・大きさ 15%）。</summary>
    private static void GetCameraShift(Vector3 cameraForward, out float shiftX, out float shiftZ)
    {
        float fx = cameraForward.x;
        float fz = cameraForward.z;
        float len = Mathf.Sqrt(fx * fx + fz * fz);
        if (len < 1e-6f)
        {
            shiftX = 0f;
            shiftZ = 0f;
            return;
        }
        float k = TopFaceCameraShift * TerrainMeshBuilder.BlockSize / len;
        shiftX = -fx * k;
        shiftZ = -fz * k;
    }

    /// <summary>
    /// 上面で反応してよいセルか。ブロックがあるセルは常に true。
    /// 追加系では 4 近傍のいずれかにブロックがあるセルも true。
    /// （範囲外セルは blockCells に含まれないため自然に false 扱い）
    /// </summary>
    private static bool QualifiesForTopFace(
        HashSet<(int x, int z)> blockCells, int x, int z, bool additive)
    {
        if (blockCells.Contains((x, z)))
            return true;
        if (!additive)
            return false;
        return blockCells.Contains((x + 1, z))
            || blockCells.Contains((x - 1, z))
            || blockCells.Contains((x, z + 1))
            || blockCells.Contains((x, z - 1));
    }
}
