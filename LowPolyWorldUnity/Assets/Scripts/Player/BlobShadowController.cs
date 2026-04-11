using UnityEngine;

/// <summary>
/// プレイヤーの足元に円形テクスチャを地面へ投影するブロブシャドウ。
/// 地面への RaycastHit の位置にフラットメッシュを配置し、
/// スケールをカメラ距離に応じて調整する。
/// </summary>
public class BlobShadowController : MonoBehaviour
{
    [SerializeField] private Transform _target;
    [SerializeField] private float _baseScale = 0.6f;
    [SerializeField] private float _maxRayDistance = 5f;
    [SerializeField] private LayerMask _groundLayerMask;
    [SerializeField] private float _groundOffset = 0.01f;

    private MeshRenderer _meshRenderer;
    private Transform _shadowTransform;

    private void Awake()
    {
        // 影用メッシュオブジェクトを生成
        var go = new GameObject("BlobShadowMesh");
        _shadowTransform = go.transform;

        var meshFilter = go.AddComponent<MeshFilter>();
        meshFilter.mesh = CreateQuadMesh();

        _meshRenderer = go.AddComponent<MeshRenderer>();

        // マテリアルを動的生成
        var mat = new Material(Shader.Find("LowPoly/BlobShadow"));
        mat.mainTexture = GenerateShadowTexture();
        _meshRenderer.material = mat;
        _meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _meshRenderer.receiveShadows = false;
    }

    private void LateUpdate()
    {
        if (_target == null)
        {
            _meshRenderer.enabled = false;
            return;
        }

        var origin = _target.position + Vector3.up * 0.1f;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, _maxRayDistance, _groundLayerMask))
        {
            _shadowTransform.position = hit.point + Vector3.up * _groundOffset;
            _shadowTransform.rotation = Quaternion.LookRotation(hit.normal) * Quaternion.Euler(90f, 0f, 0f);

            // 高さに応じてスケールを縮小
            float heightRatio = 1f - Mathf.Clamp01(hit.distance / _maxRayDistance);
            float scale = _baseScale * heightRatio;
            _shadowTransform.localScale = new Vector3(scale, scale, 1f);

            _meshRenderer.enabled = true;
        }
        else
        {
            _meshRenderer.enabled = false;
        }
    }

    private static Mesh CreateQuadMesh()
    {
        var mesh = new Mesh { name = "BlobShadowQuad" };
        mesh.vertices = new Vector3[]
        {
            new(-0.5f, 0f, -0.5f),
            new( 0.5f, 0f, -0.5f),
            new( 0.5f, 0f,  0.5f),
            new(-0.5f, 0f,  0.5f),
        };
        mesh.uv = new Vector2[]
        {
            new(0f, 0f), new(1f, 0f), new(1f, 1f), new(0f, 1f),
        };
        mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
        mesh.RecalculateNormals();
        return mesh;
    }

    /// <summary>中央が暗く周囲が明るい（=白）グラデーション円テクスチャを生成する。</summary>
    private static Texture2D GenerateShadowTexture()
    {
        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.R8, false);
        tex.name = "BlobShadowTex";
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        float center = size * 0.5f;
        float radius = center;
        var pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(center, center));
                float t = Mathf.Clamp01(dist / radius);
                // 中央ほど強い（r=1 が影の濃さを表す）
                float intensity = 1f - (t * t);
                pixels[y * size + x] = new Color(intensity, 0f, 0f, 1f);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }
}
