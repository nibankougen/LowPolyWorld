using UnityEngine;
using UnityEditor;
using System.Reflection;

/// <summary>
/// プロジェクト初期セットアップ用 Editor ユーティリティ。
/// </summary>
public static class ProjectSetup
{
    // ---- AudioMixer ----

    [MenuItem("LowPolyWorld/Setup/Create AudioMixer")]
    public static void CreateAudioMixer()
    {
        const string path = "Assets/Audio/MainMixer.mixer";
        if (AssetDatabase.LoadAssetAtPath<Object>(path) != null)
        {
            Debug.Log("[ProjectSetup] MainMixer already exists.");
            return;
        }

        var assembly = typeof(AudioImporter).Assembly;
        var controllerType = assembly.GetType("UnityEditor.Audio.AudioMixerController");
        if (controllerType == null)
        {
            Debug.LogError("[ProjectSetup] AudioMixerController type not found.");
            return;
        }

        var mixer = ScriptableObject.CreateInstance(controllerType);
        if (mixer == null)
        {
            // Fallback: string overload
            mixer = ScriptableObject.CreateInstance("AudioMixerController");
        }
        if (mixer == null)
        {
            Debug.LogError("[ProjectSetup] Failed to create AudioMixerController instance.");
            return;
        }

        AssetDatabase.CreateAsset(mixer, path);

        // WorldSFX グループ追加
        var createGroup = controllerType.GetMethod(
            "CreateNewGroup",
            BindingFlags.Public | BindingFlags.Instance,
            null,
            new System.Type[] { typeof(string), typeof(bool) },
            null
        );

        if (createGroup != null)
        {
            createGroup.Invoke(mixer, new object[] { "WorldSFX", false });
            createGroup.Invoke(mixer, new object[] { "SystemSFX", false });
        }
        else
        {
            Debug.LogWarning("[ProjectSetup] CreateNewGroup not found. Add WorldSFX/SystemSFX groups manually.");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[ProjectSetup] MainMixer created at {path}");
    }

    // ---- Physics Layers ----

    [MenuItem("LowPolyWorld/Setup/Add Physics Layers")]
    public static void AddPhysicsLayers()
    {
        var tagManager = new SerializedObject(
            AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]
        );
        var layersProp = tagManager.FindProperty("layers");

        int playerLayer = -1;
        int worldLayer = -1;

        // 既存チェック
        for (int i = 8; i < layersProp.arraySize; i++)
        {
            string name = layersProp.GetArrayElementAtIndex(i).stringValue;
            if (name == "Player") playerLayer = i;
            if (name == "World") worldLayer = i;
        }

        // 空きスロットに追加
        for (int i = 8; i < layersProp.arraySize && (playerLayer < 0 || worldLayer < 0); i++)
        {
            string name = layersProp.GetArrayElementAtIndex(i).stringValue;
            if (!string.IsNullOrEmpty(name)) continue;

            if (playerLayer < 0)
            {
                layersProp.GetArrayElementAtIndex(i).stringValue = "Player";
                playerLayer = i;
            }
            else if (worldLayer < 0)
            {
                layersProp.GetArrayElementAtIndex(i).stringValue = "World";
                worldLayer = i;
            }
        }

        tagManager.ApplyModifiedPropertiesWithoutUndo();

        // Layer Collision Matrix: Player-Player を無効化
        if (playerLayer >= 0)
        {
            SetLayerCollision(playerLayer, playerLayer, ignore: true);
        }

        Debug.Log($"[ProjectSetup] Layers set. Player={playerLayer}, World={worldLayer}");
    }

    private static void SetLayerCollision(int layerA, int layerB, bool ignore)
    {
        var dynManager = new SerializedObject(
            AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/DynamicsManager.asset")[0]
        );
        var matrixProp = dynManager.FindProperty("m_LayerCollisionMatrix");
        if (matrixProp == null) return;

        // Unity の Layer Collision Matrix: layerB 行の layerA ビットを操作
        // m_LayerCollisionMatrix[row] の bit[col] が 0 = 衝突しない
        var rowProp = matrixProp.GetArrayElementAtIndex(layerB);
        uint row = (uint)rowProp.longValue;
        if (ignore)
            row &= ~(1u << layerA);
        else
            row |= (1u << layerA);
        rowProp.longValue = row;

        // 対称も設定
        var rowPropB = matrixProp.GetArrayElementAtIndex(layerA);
        uint rowB = (uint)rowPropB.longValue;
        if (ignore)
            rowB &= ~(1u << layerB);
        else
            rowB |= (1u << layerB);
        rowPropB.longValue = rowB;

        dynManager.ApplyModifiedPropertiesWithoutUndo();
    }
}
