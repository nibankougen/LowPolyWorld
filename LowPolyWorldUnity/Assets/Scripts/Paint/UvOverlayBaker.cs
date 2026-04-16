using System;
using Unity.Collections;
using UnityEngine;

/// <summary>
/// メッシュの UV チャート（UV0）をテクスチャ空間のエッジラインとしてベイクするロジッククラス。
/// Mesh.AcquireReadOnlyMeshData を使用するため isReadable に依存せず、VRM・GLB 両対応。
/// </summary>
public static class UvOverlayBaker
{
    /// UV エッジの描画色（半透明グレー）
    private static readonly Color32 LineColor = new Color32(220, 220, 220, 160);

    /// <summary>
    /// 複数メッシュの UV0 エッジを width×height の RGBA バイト列としてベイクする。
    /// 該当するメッシュがない場合や UV データが空の場合は null を返す。
    /// </summary>
    public static byte[] Bake(Mesh[] meshes, int width, int height)
    {
        if (meshes == null || meshes.Length == 0 || width <= 0 || height <= 0)
            return null;

        var pixels = new Color32[width * height];
        bool anyDrawn = false;

        foreach (var mesh in meshes)
        {
            if (mesh == null)
                continue;
            if (BakeMesh(mesh, pixels, width, height))
                anyDrawn = true;
        }

        if (!anyDrawn)
            return null;

        var result = new byte[width * height * 4];
        for (int i = 0; i < pixels.Length; i++)
        {
            result[i * 4] = pixels[i].r;
            result[i * 4 + 1] = pixels[i].g;
            result[i * 4 + 2] = pixels[i].b;
            result[i * 4 + 3] = pixels[i].a;
        }
        return result;
    }

    private static bool BakeMesh(Mesh mesh, Color32[] pixels, int width, int height)
    {
        using var meshDataArray = Mesh.AcquireReadOnlyMeshData(mesh);
        var meshData = meshDataArray[0];

        int vertexCount = meshData.vertexCount;
        if (vertexCount == 0)
            return false;

        using var uvs = new NativeArray<Vector2>(vertexCount, Allocator.Temp);
        meshData.GetUVs(0, uvs);

        bool drawn = false;
        for (int sub = 0; sub < meshData.subMeshCount; sub++)
        {
            var desc = meshData.GetSubMesh(sub);
            if (desc.indexCount == 0)
                continue;

            using var indices = new NativeArray<int>(desc.indexCount, Allocator.Temp);
            meshData.GetIndices(indices, sub);

            for (int i = 0; i < indices.Length; i += 3)
            {
                var uv0 = uvs[indices[i]];
                var uv1 = uvs[indices[i + 1]];
                var uv2 = uvs[indices[i + 2]];

                DrawUvEdge(pixels, width, height, uv0, uv1);
                DrawUvEdge(pixels, width, height, uv1, uv2);
                DrawUvEdge(pixels, width, height, uv2, uv0);
                drawn = true;
            }
        }

        return drawn;
    }

    private static void DrawUvEdge(Color32[] pixels, int width, int height, Vector2 uvA, Vector2 uvB)
    {
        int x0 = Mathf.Clamp(Mathf.RoundToInt(uvA.x * (width - 1)), 0, width - 1);
        int y0 = Mathf.Clamp(Mathf.RoundToInt(uvA.y * (height - 1)), 0, height - 1);
        int x1 = Mathf.Clamp(Mathf.RoundToInt(uvB.x * (width - 1)), 0, width - 1);
        int y1 = Mathf.Clamp(Mathf.RoundToInt(uvB.y * (height - 1)), 0, height - 1);
        BresenhamLine(pixels, width, height, x0, y0, x1, y1);
    }

    /// <summary>
    /// Bresenham のライン描画アルゴリズム。テストから直接呼び出し可能。
    /// </summary>
    public static void BresenhamLine(Color32[] pixels, int width, int height, int x0, int y0, int x1, int y1)
    {
        int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
        int dy = -Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
        int err = dx + dy;

        while (true)
        {
            if (x0 >= 0 && x0 < width && y0 >= 0 && y0 < height)
                pixels[y0 * width + x0] = LineColor;

            if (x0 == x1 && y0 == y1)
                break;

            int e2 = 2 * err;
            if (e2 >= dy)
            {
                err += dy;
                x0 += sx;
            }
            if (e2 <= dx)
            {
                err += dx;
                y0 += sy;
            }
        }
    }
}
