#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// オブジェクトタブの統合プレビュー（エディタ専用・Play モードで動作）。
/// ワールドエディタ UI + オブジェクト配置シーン統合 + カメラを配線し、
/// 所有パレットにプレースホルダ種別をシードして、実際に配置・移動・回転・拡大縮小を試せる状態にする。
/// メニュー「Tools/LowPolyWorld/オブジェクトタブ統合プレビューを生成」から生成する。
/// </summary>
[RequireComponent(typeof(ObjectEditSceneController))]
public class ObjectEditScenePreview : MonoBehaviour
{
    [Tooltip("ワールドエディタ UI の GameObject（WorldEditorController 付き）")]
    public WorldEditorController editorController;

    [Tooltip("編集ビュー用カメラ")]
    public Camera editCamera;

    private void Start()
    {
        if (editorController == null || editorController.ObjectTab == null)
        {
            Debug.LogError("ObjectEditScenePreview: WorldEditorController が見つかりません。");
            return;
        }

        var types = new List<ObjectEditSceneController.ObjectType>
        {
            new ObjectEditSceneController.ObjectType
            {
                TypeId = "desk", Name = "机", DefaultSize = new IntVec3Json(4, 3, 3),
                Color = new Color(0.75f, 0.55f, 0.35f), TextureSizePx = 64,
            },
            new ObjectEditSceneController.ObjectType
            {
                TypeId = "chair", Name = "椅子", DefaultSize = new IntVec3Json(2, 2, 4),
                Color = new Color(0.45f, 0.60f, 0.80f), TextureSizePx = 64,
            },
            new ObjectEditSceneController.ObjectType
            {
                TypeId = "box", Name = "箱", DefaultSize = new IntVec3Json(4, 4, 4),
                Color = new Color(0.60f, 0.75f, 0.45f), TextureSizePx = 32,
            },
            new ObjectEditSceneController.ObjectType
            {
                TypeId = "pillar", Name = "柱", DefaultSize = new IntVec3Json(2, 2, 12),
                Color = new Color(0.70f, 0.70f, 0.75f), TextureSizePx = 128, ScaleLocked = true,
            },
        };

        var doc = editorController.GetComponent<UIDocument>();
        GetComponent<ObjectEditSceneController>().Initialize(
            editorController, editCamera, doc.rootVisualElement, types);

        CreateGround();

        // オブジェクトタブを開いた状態で開始
        var switchTab = typeof(WorldEditorController).GetMethod(
            "SwitchTab",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        switchTab?.Invoke(editorController, new object[] { 1 });

        Debug.Log("オブジェクトタブ統合プレビュー: 所有パレットの種別をタップで配置 → 移動モードでドラッグ移動・"
            + "回転モードでタップ回転・W/D/H ステッパーで拡大縮小（柱はスケールロック）。");
    }

    private void CreateGround()
    {
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "PreviewGround";
        ground.transform.SetParent(transform, false);
        ground.transform.localScale = new Vector3(4f, 1f, 4f); // 40m 四方
        var shader = Shader.Find("LowPoly/Unlit");
        if (shader != null)
            ground.GetComponent<MeshRenderer>().sharedMaterial =
                new Material(shader) { color = new Color(0.22f, 0.24f, 0.28f) };
    }
}
#endif
