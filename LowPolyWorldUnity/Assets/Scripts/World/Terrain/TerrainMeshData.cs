using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// TerrainMeshBuilder の出力バッファ。UnityEngine.Mesh への反映は MonoBehaviour 層が行う。
/// 座標は store グリッド基準の Unity 単位（1 ブロック = 0.5m）。
/// Colors の RGB は頂点 AO（15.16）、A は「上向きの面 = 1 / その他 = 0」
/// （Height Culling のカット平面と一致する高さでの表示判定 — 15.11）。
/// Uvs2 は hidden tops メッシュのみ使用（x = ブロック上面の Y グリッドインデックス。15.11 参照）。
/// </summary>
public class TerrainMeshData
{
    public List<Vector3> Vertices { get; } = new List<Vector3>();
    public List<Vector2> Uvs { get; } = new List<Vector2>();
    public List<Vector2> Uvs2 { get; } = new List<Vector2>();
    public List<Color> Colors { get; } = new List<Color>();
    public List<int> Triangles { get; } = new List<int>();

    public bool IsEmpty => Vertices.Count == 0;
}

/// <summary>
/// チャンク 1 つ分のメッシュ生成結果。
/// Solid = 通常描画メッシュ、HiddenTops = Height Culling 時のみ表示する上面中間フェイス
/// （シェーダーが UV2.x == 閾値 のフェイスだけを表示する）。
/// </summary>
public class TerrainChunkMeshes
{
    public TerrainMeshData Solid { get; } = new TerrainMeshData();
    public TerrainMeshData HiddenTops { get; } = new TerrainMeshData();
}
