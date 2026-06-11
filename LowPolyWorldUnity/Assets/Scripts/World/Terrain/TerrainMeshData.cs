using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// TerrainMeshBuilder の出力バッファ。UnityEngine.Mesh への反映は MonoBehaviour 層が行う。
/// 座標は store グリッド基準の Unity 単位（1 ブロック = 0.5m）、Colors は頂点 AO（15.16）。
/// </summary>
public class TerrainMeshData
{
    public List<Vector3> Vertices { get; } = new List<Vector3>();
    public List<Vector2> Uvs { get; } = new List<Vector2>();
    public List<Color> Colors { get; } = new List<Color>();
    public List<int> Triangles { get; } = new List<int>();

    public bool IsEmpty => Vertices.Count == 0;
}
