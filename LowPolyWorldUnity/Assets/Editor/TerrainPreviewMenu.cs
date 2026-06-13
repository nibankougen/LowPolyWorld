using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 地形プレビューの生成・削除メニュー（見た目確認用 — docs/world-creation.md セクション 15）。
/// </summary>
public static class TerrainPreviewMenu
{
    private const string PreviewObjectName = "TerrainPreview";
    private const string UiPreviewObjectName = "WorldEditorUIPreview";
    private const string ObjectPreviewObjectName = "ObjectEditPreview";

    [MenuItem("Tools/LowPolyWorld/地形プレビューを生成")]
    public static void CreatePreview() => CreatePreview(false);

    [MenuItem("Tools/LowPolyWorld/地形プレビューを生成（AO確認・白一色）")]
    public static void CreatePlainPreview() => CreatePreview(true);

    private static void CreatePreview(bool plainWhiteTexture)
    {
        RemovePreview();

        var go = new GameObject(PreviewObjectName);
        var preview = go.AddComponent<TerrainPreview>();
        preview.plainWhiteTexture = plainWhiteTexture;
        preview.BuildSample();
        Selection.activeGameObject = go;
        SceneView.lastActiveSceneView?.Frame(new Bounds(new Vector3(8f, 2f, 8f), new Vector3(18f, 8f, 18f)), false);
        Debug.Log("地形プレビューを生成しました。Inspector の Height Cull Threshold で Height Culling を確認できます（-1 = 無効）。");
    }

    [MenuItem("Tools/LowPolyWorld/地形プレビューを削除")]
    public static void RemovePreview()
    {
        var existing = GameObject.Find(PreviewObjectName);
        if (existing != null)
            Object.DestroyImmediate(existing);

        var uiPreview = GameObject.Find(UiPreviewObjectName);
        if (uiPreview != null)
            Object.DestroyImmediate(uiPreview);

        var objPreview = GameObject.Find(ObjectPreviewObjectName);
        if (objPreview != null)
            Object.DestroyImmediate(objPreview);
    }

    [MenuItem("Tools/LowPolyWorld/ワールドエディタ UI プレビューを生成")]
    public static void CreateUiPreview()
    {
        var go = CreateEditorUiObject();
        go.AddComponent<WorldEditorUiPreview>();
        Selection.activeGameObject = go;
        Debug.Log("ワールドエディタ UI プレビューを生成しました。Play モードに入り、下部の「地形」タブをタップして確認してください。");
    }

    [MenuItem("Tools/LowPolyWorld/地形タブ統合プレビューを生成")]
    public static void CreateTerrainEditPreview()
    {
        RemovePreview();
        var uiGo = CreateEditorUiObject();

        var cameraGo = new GameObject("TerrainEditCamera");
        cameraGo.transform.SetParent(uiGo.transform, false);
        var camera = cameraGo.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.12f, 0.13f, 0.18f);

        var terrainGo = new GameObject(PreviewObjectName);
        terrainGo.AddComponent<TerrainRenderer>();
        terrainGo.AddComponent<TerrainEditSceneController>();
        var preview = terrainGo.AddComponent<TerrainEditScenePreview>();
        preview.editorController = uiGo.GetComponent<WorldEditorController>();
        preview.editCamera = camera;

        Selection.activeGameObject = terrainGo;
        Debug.Log("地形タブ統合プレビューを生成しました。Play モードに入ると地形タブで実際に編集できます。");
    }

    [MenuItem("Tools/LowPolyWorld/オブジェクトタブ統合プレビューを生成")]
    public static void CreateObjectEditPreview()
    {
        RemovePreview();
        var uiGo = CreateEditorUiObject();

        var objGo = new GameObject(ObjectPreviewObjectName);
        objGo.AddComponent<ObjectEditSceneController>();

        var cameraGo = new GameObject("ObjectEditCamera");
        cameraGo.transform.SetParent(objGo.transform, false);
        var camera = cameraGo.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.12f, 0.13f, 0.18f);

        var preview = objGo.AddComponent<ObjectEditScenePreview>();
        preview.editorController = uiGo.GetComponent<WorldEditorController>();
        preview.editCamera = camera;

        Selection.activeGameObject = objGo;
        Debug.Log("オブジェクトタブ統合プレビューを生成しました。Play モードに入るとオブジェクトを配置・操作できます。");
    }

    private static GameObject CreateEditorUiObject()
    {
        var existing = GameObject.Find(UiPreviewObjectName);
        if (existing != null)
            Object.DestroyImmediate(existing);

        var go = new GameObject(UiPreviewObjectName);
        var doc = go.AddComponent<UIDocument>();
        doc.panelSettings =
            AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/Settings/UI_PanelSettings.asset");
        doc.visualTreeAsset =
            AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/Screens/WorldEditor.uxml");
        go.AddComponent<WorldEditorController>();
        return go;
    }
}
