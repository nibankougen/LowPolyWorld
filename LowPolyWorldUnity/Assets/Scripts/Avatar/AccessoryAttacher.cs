using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GLTFast;
using UnityEngine;

/// <summary>
/// アクセサリ（GLB）を Humanoid ボーンにアタッチする。
/// </summary>
public static class AccessoryAttacher
{
    /// <summary>サポートするアタッチポイント（Humanoid ボーン名）。</summary>
    public static readonly IReadOnlyList<HumanBodyBones> AttachBones = new[]
    {
        HumanBodyBones.Head,
        HumanBodyBones.LeftLowerArm,
        HumanBodyBones.RightLowerArm,
        HumanBodyBones.Chest,
        HumanBodyBones.LeftUpperLeg,
        HumanBodyBones.RightUpperLeg,
    };

    /// <summary>
    /// StreamingAssets/{relativePath} から GLB を読み込み、指定ボーンにアタッチする。
    /// </summary>
    public static async Task<GameObject> AttachAsync(
        GameObject avatarRoot,
        string relativePath,
        HumanBodyBones targetBone,
        Vector3 localPosition = default,
        Quaternion localRotation = default,
        Vector3 localScale = default,
        CancellationToken ct = default
    )
    {
        if (localRotation == default) localRotation = Quaternion.identity;
        if (localScale == default) localScale = Vector3.one;

        var animator = avatarRoot.GetComponent<Animator>();
        if (animator == null)
        {
            Debug.LogError("[AccessoryAttacher] Animator not found on avatarRoot.");
            return null;
        }

        var boneTransform = animator.GetBoneTransform(targetBone);
        if (boneTransform == null)
        {
            Debug.LogError($"[AccessoryAttacher] Bone {targetBone} not found.");
            return null;
        }

        string fullPath = Path.Combine(Application.streamingAssetsPath, relativePath);
        if (!File.Exists(fullPath))
        {
            Debug.LogError($"[AccessoryAttacher] GLB not found: {fullPath}");
            return null;
        }

        try
        {
            var go = new GameObject($"Accessory_{targetBone}");
            var gltf = go.AddComponent<GltfAsset>();

            var uri = new Uri(fullPath);
            var success = await gltf.Load(uri.AbsoluteUri);
            if (!success)
            {
                UnityEngine.Object.Destroy(go);
                Debug.LogError($"[AccessoryAttacher] Failed to load GLB: {fullPath}");
                return null;
            }

            go.transform.SetParent(boneTransform, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation;
            go.transform.localScale = localScale;

            return go;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception e)
        {
            Debug.LogError($"[AccessoryAttacher] Exception: {e}");
            return null;
        }
    }

    /// <summary>
    /// アタッチ済みアクセサリの Diffuse テクスチャを取得する（Atlas 書き込み用）。
    /// </summary>
    public static Texture2D ExtractDiffuseTexture(GameObject accessoryRoot)
    {
        var renderer = accessoryRoot.GetComponentInChildren<Renderer>(true);
        if (renderer == null || renderer.sharedMaterial == null)
            return null;

        return renderer.sharedMaterial.mainTexture as Texture2D;
    }
}
