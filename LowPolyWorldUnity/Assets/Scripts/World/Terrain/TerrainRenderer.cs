using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 地形のメッシュ・コライダーを Unity シーンへ反映する MonoBehaviour（エンジン境界のみ）。
/// 生成ロジックは TerrainMeshBuilder / TerrainColliderBuilder / TerrainHeightCulling が担当する。
///
/// - チャンクごとに子 GameObject（Solid メッシュ + HiddenTops メッシュ + BoxCollider 群）を生成
/// - マテリアルは LowPoly/Terrain シェーダー（Solid と HiddenTops で _HIDDEN_TOP_MODE を切替）
/// - アトラステクスチャは Filter Mode: Point を強制（15.4）
/// - Height Culling はマテリアルの _CullHeightY / _CullGridY と
///   チャンクレンダラーの有効/無効で反映（15.11 表示反映方式）
/// - ワールド中心へのオフセットはこの GameObject の transform で行う
/// </summary>
public class TerrainRenderer : MonoBehaviour
{
    private const string ShaderName = "LowPoly/Terrain";
    private const float CullDisabled = 1000000f;

    private readonly TerrainMeshBuilder _meshBuilder = new TerrainMeshBuilder();
    private readonly TerrainColliderBuilder _colliderBuilder = new TerrainColliderBuilder();

    private readonly GameObject[,,] _chunkObjects =
        new GameObject[TerrainVoxelStore.ChunkCountX, TerrainVoxelStore.ChunkCountY, TerrainVoxelStore.ChunkCountZ];

    private TerrainVoxelStore _store;
    private ITerrainAtlasMap _atlasMap;
    private Material _solidMaterial;
    private Material _hiddenTopMaterial;
    private int _threshold = TerrainHeightCulling.NoCulling;

    /// <summary>ボクセルストア全体からチャンクオブジェクトを構築する。</summary>
    public void Build(TerrainVoxelStore store, ITerrainAtlasMap atlasMap, Texture2D atlasTexture)
    {
        Clear();
        _store = store;
        _atlasMap = atlasMap;
        EnsureMaterials(atlasTexture);

        var sampler = new TerrainStoreSampler(store);
        for (int cy = 0; cy < TerrainVoxelStore.ChunkCountY; cy++)
            for (int cz = 0; cz < TerrainVoxelStore.ChunkCountZ; cz++)
                for (int cx = 0; cx < TerrainVoxelStore.ChunkCountX; cx++)
                    BuildChunkObject(sampler, cx, cy, cz);

        ApplyHeightCullingThreshold(_threshold);
    }

    /// <summary>1 チャンクだけ再構築する（地形編集時）。</summary>
    public void RebuildChunk(int cx, int cy, int cz)
    {
        if (_store == null)
            return;
        DestroyChunkObject(cx, cy, cz);
        BuildChunkObject(new TerrainStoreSampler(_store), cx, cy, cz);
        ApplyHeightCullingThreshold(_threshold);
    }

    /// <summary>
    /// Height Culling の閾値を反映する（TerrainHeightCulling.NoCulling で無効化）。
    /// 閾値変更はマテリアル float の設定とチャンクレンダラーの切替のみ（メッシュ再構築なし）。
    /// </summary>
    public void ApplyHeightCullingThreshold(int threshold)
    {
        _threshold = threshold;
        if (_solidMaterial == null)
            return;

        bool active = threshold != TerrainHeightCulling.NoCulling;
        _solidMaterial.SetFloat("_CullHeightY",
            active ? transform.position.y + threshold * TerrainMeshBuilder.BlockSize : CullDisabled);
        _hiddenTopMaterial.SetFloat("_CullGridY", active ? threshold : CullDisabled);

        for (int cy = 0; cy < TerrainVoxelStore.ChunkCountY; cy++)
        {
            bool visible = !TerrainHeightCulling.IsChunkFullyHidden(cy, threshold);
            for (int cz = 0; cz < TerrainVoxelStore.ChunkCountZ; cz++)
                for (int cx = 0; cx < TerrainVoxelStore.ChunkCountX; cx++)
                    if (_chunkObjects[cx, cy, cz] != null)
                        _chunkObjects[cx, cy, cz].SetActive(visible);
        }
    }

    /// <summary>全チャンクオブジェクトと生成リソースを破棄する。</summary>
    public void Clear()
    {
        for (int cy = 0; cy < TerrainVoxelStore.ChunkCountY; cy++)
            for (int cz = 0; cz < TerrainVoxelStore.ChunkCountZ; cz++)
                for (int cx = 0; cx < TerrainVoxelStore.ChunkCountX; cx++)
                    DestroyChunkObject(cx, cy, cz);
        _store = null;
        _atlasMap = null;
    }

    private void OnDestroy()
    {
        Clear();
        SafeDestroy(_solidMaterial);
        SafeDestroy(_hiddenTopMaterial);
        _solidMaterial = null;
        _hiddenTopMaterial = null;
    }

    // ── チャンクオブジェクト構築 ──────────────────────────────────────────────

    private void BuildChunkObject(ITerrainVoxelSampler sampler, int cx, int cy, int cz)
    {
        TerrainChunkMeshes meshes = _meshBuilder.BuildChunk(sampler, _atlasMap, cx, cy, cz);
        var boxes = _colliderBuilder.BuildChunk(sampler, cx, cy, cz);
        if (meshes.Solid.IsEmpty && meshes.HiddenTops.IsEmpty && boxes.Count == 0)
            return;

        var chunkGo = new GameObject($"TerrainChunk_{cx}_{cy}_{cz}");
        chunkGo.transform.SetParent(transform, false);
        _chunkObjects[cx, cy, cz] = chunkGo;

        if (!meshes.Solid.IsEmpty)
            CreateMeshChild(chunkGo.transform, "Solid", meshes.Solid, _solidMaterial, false);
        if (!meshes.HiddenTops.IsEmpty)
            CreateMeshChild(chunkGo.transform, "HiddenTops", meshes.HiddenTops, _hiddenTopMaterial, true);

        if (boxes.Count > 0)
        {
            var colliderGo = new GameObject("Colliders");
            colliderGo.transform.SetParent(chunkGo.transform, false);
            foreach (var box in boxes)
            {
                var collider = colliderGo.AddComponent<BoxCollider>();
                collider.center = box.Center;
                collider.size = box.Size;
            }
        }
    }

    private static void CreateMeshChild(
        Transform parent, string name, TerrainMeshData data, Material material, bool withUv2)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var mesh = new Mesh { name = $"{parent.name}_{name}" };
        if (data.Vertices.Count > ushort.MaxValue)
            mesh.indexFormat = IndexFormat.UInt32;
        mesh.SetVertices(data.Vertices);
        mesh.SetUVs(0, data.Uvs);
        if (withUv2)
            mesh.SetUVs(1, data.Uvs2);
        mesh.SetColors(data.Colors);
        mesh.SetTriangles(data.Triangles, 0);
        mesh.RecalculateBounds();

        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    private void EnsureMaterials(Texture2D atlasTexture)
    {
        if (atlasTexture != null)
        {
            atlasTexture.filterMode = FilterMode.Point; // ドット感維持（15.4）
        }

        if (_solidMaterial == null)
        {
            var shader = Shader.Find(ShaderName);
            _solidMaterial = new Material(shader) { name = "TerrainSolid" };
            _hiddenTopMaterial = new Material(shader) { name = "TerrainHiddenTops" };
            _hiddenTopMaterial.SetFloat("_HiddenTopMode", 1f);
            _hiddenTopMaterial.EnableKeyword("_HIDDEN_TOP_MODE");
        }
        _solidMaterial.mainTexture = atlasTexture;
        _hiddenTopMaterial.mainTexture = atlasTexture;
    }

    private void DestroyChunkObject(int cx, int cy, int cz)
    {
        var go = _chunkObjects[cx, cy, cz];
        if (go == null)
            return;
        foreach (var filter in go.GetComponentsInChildren<MeshFilter>(true))
            SafeDestroy(filter.sharedMesh);
        SafeDestroy(go);
        _chunkObjects[cx, cy, cz] = null;
    }

    // エディタプレビュー（非 Play モード）でも動作させるための破棄ヘルパー
    private static void SafeDestroy(Object obj)
    {
        if (obj == null)
            return;
        if (Application.isPlaying)
            Destroy(obj);
        else
            DestroyImmediate(obj);
    }
}
