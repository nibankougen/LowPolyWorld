using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UniVRM10;
using UnityEngine;

/// <summary>
/// VRM 1.0 ファイルを StreamingAssets から読み込み、
/// カスタム Unlit シェーダーを適用して GameObject を返す。
/// </summary>
public static class VrmLoader
{
    private const string ShaderName = "LowPoly/Unlit";

    /// <summary>
    /// StreamingAssets/{relativePath} から VRM を非同期で読み込む。
    /// </summary>
    /// <param name="relativePath">StreamingAssets 以下の相対パス（例: "avatars/test.vrm"）</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>ロード済みの VRM GameObject。失敗時は null。</returns>
    public static async Task<GameObject> LoadFromStreamingAssetsAsync(
        string relativePath,
        CancellationToken ct = default
    )
    {
        string fullPath = Path.Combine(Application.streamingAssetsPath, relativePath);

        if (!File.Exists(fullPath))
        {
            Debug.LogError($"[VrmLoader] VRM file not found: {fullPath}");
            return null;
        }

        try
        {
            var instance = await Vrm10.LoadPathAsync(
                fullPath,
                canLoadVrm0X: true,
                ct: ct
            );

            if (instance == null)
            {
                Debug.LogError($"[VrmLoader] Failed to load VRM: {fullPath}");
                return null;
            }

            var go = instance.gameObject;
            ApplyCustomShader(go);
            return go;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception e)
        {
            Debug.LogError($"[VrmLoader] Exception loading VRM: {e}");
            return null;
        }
    }

    /// <summary>
    /// VRM GameObject の全 Renderer にカスタム Unlit シェーダーを適用する。
    /// </summary>
    public static void ApplyCustomShader(GameObject vrmRoot)
    {
        var shader = Shader.Find(ShaderName);
        if (shader == null)
        {
            Debug.LogError($"[VrmLoader] Shader not found: {ShaderName}");
            return;
        }

        foreach (var renderer in vrmRoot.GetComponentsInChildren<Renderer>(true))
        {
            var mats = renderer.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null) continue;
                var newMat = new Material(shader);
                if (mats[i].HasProperty("_MainTex"))
                    newMat.mainTexture = mats[i].mainTexture;
                if (mats[i].HasProperty("_Color"))
                    newMat.color = mats[i].color;
                mats[i] = newMat;
            }
            renderer.sharedMaterials = mats;
        }
    }

    /// <summary>
    /// VRM GameObject に Animator を設定して共通アニメーションを適用する。
    /// </summary>
    public static void ApplyAnimator(GameObject vrmRoot, RuntimeAnimatorController animatorController)
    {
        if (animatorController == null) return;

        var animator = vrmRoot.GetComponent<Animator>();
        if (animator == null)
            animator = vrmRoot.AddComponent<Animator>();

        animator.runtimeAnimatorController = animatorController;
        animator.applyRootMotion = false;
        animator.updateMode = AnimatorUpdateMode.Normal;
        animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
    }
}
