using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

/// <summary>
/// オブジェクトタブのシーン統合 MonoBehaviour（screens-and-modes.md 11.7.3 — エンジン境界のみ）。
///
/// - ObjectTabController（UI）のイベントを ObjectPlacementStore / ObjectGizmoLogic / 3D プレースホルダに配線
/// - 配置オブジェクトを色付きプレースホルダ Cube（`LowPoly/Unlit`）で描画（実 GLB 未対応のため）
/// - ポインタ入力（3D ビュー領域上のみ）: タップ選択 / 移動モードでドラッグ移動（0.5m スナップ）/
///   回転モードでタップ回転（45°）。拡大縮小はタブの W/D/H ステッパー
/// - 変更後にコスト・オブジェクト数表示を更新する
///
/// ゲームロジックは ObjectPlacementStore / ObjectGizmoLogic / ObjectGridSnap / ObjectPlaceholderTransform
/// （いずれも純 C#・テスト済み）に委譲し、本クラスは Unity 境界のみを担当する。
/// </summary>
public class ObjectEditSceneController : MonoBehaviour
{
    /// <summary>所有オブジェクト種別の定義（プレビュー用プレースホルダ）。</summary>
    public struct ObjectType
    {
        public string TypeId;
        public string Name;
        public IntVec3Json DefaultSize; // 0.25m 単位
        public Color Color;
        public bool ScaleLocked;
        public int TextureSizePx;       // コスト計算用
    }

    private ObjectPlacementStore _store;
    private WorldEditorController _editor;
    private ObjectTabController _tab;
    private Camera _camera;
    private VisualElement _viewArea;
    private IPanel _panel;

    private readonly Dictionary<string, ObjectType> _types = new();
    private readonly Dictionary<string, GameObject> _placeholders = new();
    private readonly Dictionary<string, Material> _materials = new();
    private Shader _unlitShader;
    private string _selectedId;
    private bool _initialized;
    private bool _wasPressed;
    private bool _dragging;

    public void Initialize(
        WorldEditorController editor,
        Camera camera,
        VisualElement uiRoot,
        IReadOnlyList<ObjectType> ownedTypes)
    {
        _editor = editor;
        _tab = editor.ObjectTab;
        _camera = camera;
        _store = new ObjectPlacementStore();
        _viewArea = uiRoot.Q("view3d-area");
        _panel = uiRoot.panel;
        _unlitShader = Shader.Find("LowPoly/Unlit");

        var palette = new List<ObjectTabController.PaletteItem>();
        foreach (var t in ownedTypes)
        {
            _types[t.TypeId] = t;
            palette.Add(new ObjectTabController.PaletteItem(t.TypeId, t.Name, t.Color));
        }
        _tab.SetOwnedObjects(palette);

        _tab.PaletteObjectClicked += OnPaletteClicked;
        _tab.UsedObjectSelected += Select;
        _tab.DuplicateClicked += OnDuplicate;
        _tab.DeleteClicked += OnDelete;
        _tab.ScaleStep += OnScaleStep;

        PositionCamera();
        _initialized = true;
        RefreshAll();
        Select(null);
    }

    private void OnDestroy()
    {
        foreach (var mat in _materials.Values)
            DestroyIfCreated(mat);
    }

    // ── 入力（3D ビュー操作） ─────────────────────────────────────────────────

    private void Update()
    {
        if (!_initialized || _camera == null)
            return;
        var pointer = Pointer.current;
        if (pointer == null)
            return;

        bool pressed = pointer.press.isPressed;
        Vector2 screen = pointer.position.ReadValue();
        var mode = _editor.CurrentGizmoMode;

        if (pressed && !_wasPressed)
        {
            if (IsOverViewArea(screen) && TryPickObject(screen, out string id))
            {
                if (mode == WorldEditorController.WorldGizmoMode.Rotate)
                {
                    if (id == _selectedId)
                        RotateSelected();
                    else
                        Select(id);
                }
                else
                {
                    Select(id);
                    if (mode == WorldEditorController.WorldGizmoMode.Move)
                        _dragging = true;
                }
            }
        }
        else if (pressed && _dragging && mode == WorldEditorController.WorldGizmoMode.Move)
        {
            DragMove(screen);
        }
        else if (!pressed)
        {
            _dragging = false;
        }
        _wasPressed = pressed;
    }

    private bool TryPickObject(Vector2 screenPos, out string instanceId)
    {
        instanceId = null;
        Ray ray = _camera.ScreenPointToRay(screenPos);
        if (!Physics.Raycast(ray, out RaycastHit hit, 1000f))
            return false;
        foreach (var kv in _placeholders)
        {
            if (kv.Value == hit.collider.gameObject)
            {
                instanceId = kv.Key;
                return true;
            }
        }
        return false;
    }

    private void DragMove(Vector2 screenPos)
    {
        var obj = _store.Find(_selectedId);
        if (obj == null)
            return;
        Ray ray = _camera.ScreenPointToRay(screenPos);
        float planeY = obj.position.y * ObjectGridSnap.PositionUnit;
        var plane = new Plane(Vector3.up, new Vector3(0f, planeY, 0f));
        if (!plane.Raycast(ray, out float enter))
            return;
        Vector3 point = ray.GetPoint(enter);
        var target = ObjectGridSnap.Clamp(new IntVec3Json(
            ObjectGridSnap.SnapAxis(point.x), obj.position.y, ObjectGridSnap.SnapAxis(point.z)));
        if (ObjectGizmoLogic.TryMoveTo(obj, target))
            UpdatePlaceholderTransform(obj);
    }

    // 3D ビュー領域上のポインタのみ編集対象（UI ボタン上の操作は無視）
    private bool IsOverViewArea(Vector2 screenPos)
    {
        if (_panel == null || _viewArea == null)
            return true;
        Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(
            _panel, new Vector2(screenPos.x, Screen.height - screenPos.y));
        return _panel.Pick(panelPos) == _viewArea;
    }

    // ── UI イベント ────────────────────────────────────────────────────────────

    private void OnPaletteClicked(string typeId)
    {
        var obj = _store.Add(typeId);
        if (obj == null)
        {
            _tab.ShowFlash("オブジェクト数が上限（400）に達しています");
            return;
        }
        RefreshAll();
        Select(obj.instanceId);
    }

    private void OnDuplicate()
    {
        if (_selectedId == null)
            return;
        var copy = _store.Duplicate(_selectedId);
        if (copy == null)
        {
            _tab.ShowFlash("オブジェクト数が上限（400）に達しています");
            return;
        }
        RefreshAll();
        Select(copy.instanceId);
    }

    private void OnDelete()
    {
        if (_selectedId == null)
            return;
        _store.Remove(_selectedId);
        Select(null);
        RefreshAll();
    }

    private void OnScaleStep(int axis, int delta)
    {
        var obj = _store.Find(_selectedId);
        if (obj == null || !_types.TryGetValue(obj.objectTypeId, out var type))
            return;
        int dw = axis == 0 ? delta : 0;
        int dd = axis == 1 ? delta : 0;
        int dh = axis == 2 ? delta : 0;
        if (ObjectGizmoLogic.TryScaleBy(obj, dw, dd, dh, type.DefaultSize, type.ScaleLocked))
        {
            UpdatePlaceholderTransform(obj);
            RefreshScaleLabels(obj);
        }
        else
        {
            _tab.ShowFlash(type.ScaleLocked ? "このオブジェクトはサイズ変更できません" : "これ以上小さくできません");
        }
    }

    // ── 選択 ──────────────────────────────────────────────────────────────────

    private void Select(string instanceId)
    {
        _selectedId = instanceId;
        bool has = instanceId != null;
        _tab.SetSelection(has);
        _editor.ShowGizmoBar(has);
        if (has)
            RefreshScaleLabels(_store.Find(instanceId));
        RefreshUsedList();
        RefreshHighlights();
    }

    // ── プレースホルダ描画 ────────────────────────────────────────────────────

    private void RefreshAll()
    {
        BuildPlaceholders();
        RefreshUsedList();
        RefreshCostAndCount();
    }

    /// <summary>
    /// 配置とプレースホルダ GameObject を差分同期する（消えたものを破棄・新規を生成・全件 transform 更新）。
    /// 全破棄＆全再生成にしないのは、Play モードの Destroy が遅延実行で同一フレーム内に重複が出るのと、
    /// 不要な GameObject 再生成を避けるため。
    /// </summary>
    private void BuildPlaceholders()
    {
        var alive = new HashSet<string>();
        foreach (var obj in _store.Objects)
            alive.Add(obj.instanceId);

        var stale = new List<string>();
        foreach (var id in _placeholders.Keys)
            if (!alive.Contains(id))
                stale.Add(id);
        foreach (var id in stale)
        {
            DestroyIfCreated(_placeholders[id]);
            if (_materials.TryGetValue(id, out var m))
                DestroyIfCreated(m);
            _placeholders.Remove(id);
            _materials.Remove(id);
        }

        foreach (var obj in _store.Objects)
        {
            if (!_placeholders.ContainsKey(obj.instanceId))
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = $"Obj_{obj.instanceId}";
                go.transform.SetParent(transform, false);
                var mat = new Material(_unlitShader);
                _materials[obj.instanceId] = mat;
                go.GetComponent<MeshRenderer>().sharedMaterial = mat;
                _placeholders[obj.instanceId] = go;
            }
            UpdatePlaceholderTransform(obj);
        }
        RefreshHighlights();
    }

    private void UpdatePlaceholderTransform(WorldObjectInstance obj)
    {
        if (!_placeholders.TryGetValue(obj.instanceId, out var go))
            return;
        var def = _types.TryGetValue(obj.objectTypeId, out var t) ? t.DefaultSize : null;
        go.transform.localPosition = ObjectPlaceholderTransform.WorldCenter(obj.position, obj.size, def);
        go.transform.localRotation = ObjectPlaceholderTransform.WorldRotation(obj.rotationY);
        go.transform.localScale = ObjectPlaceholderTransform.WorldScale(obj.size, def);
    }

    private void RefreshHighlights()
    {
        foreach (var obj in _store.Objects)
        {
            if (!_materials.TryGetValue(obj.instanceId, out var mat))
                continue;
            Color baseColor = _types.TryGetValue(obj.objectTypeId, out var t) ? t.Color : Color.gray;
            mat.color = obj.instanceId == _selectedId
                ? Color.Lerp(baseColor, Color.white, 0.45f)
                : baseColor;
        }
    }

    // ── UI 反映 ────────────────────────────────────────────────────────────────

    private void RefreshUsedList()
    {
        var items = new List<ObjectTabController.UsedItem>();
        foreach (var obj in _store.Objects)
        {
            _types.TryGetValue(obj.objectTypeId, out var t);
            int cost = TextureCostCalculator.CostForSize(t.TextureSizePx > 0 ? t.TextureSizePx : 64);
            items.Add(new ObjectTabController.UsedItem(
                obj.instanceId, t.Name ?? obj.objectTypeId, cost, t.Color));
        }
        _tab.SetUsedObjects(items, _selectedId);
    }

    private void RefreshCostAndCount()
    {
        int cost = _store.CalculateCost(TextureSizeOf);
        _editor.UpdateCostDisplay(cost, _store.ObjectCount);
        _tab.SetCount(_store.ObjectCount);
    }

    private void RefreshScaleLabels(WorldObjectInstance obj)
    {
        if (obj == null)
            return;
        var def = _types.TryGetValue(obj.objectTypeId, out var t) ? t.DefaultSize : null;
        var size = ObjectPlaceholderTransform.ResolveSize(obj.size, def);
        _tab.SetScaleLabels(size.x, size.y, size.z);
    }

    private int TextureSizeOf(string key) =>
        _types.TryGetValue(key, out var t) && t.TextureSizePx > 0 ? t.TextureSizePx : 64;

    // ── カメラ ────────────────────────────────────────────────────────────────

    private void RotateSelected()
    {
        var obj = _store.Find(_selectedId);
        if (obj == null)
            return;
        ObjectGizmoLogic.RotateBy(obj, 1);
        UpdatePlaceholderTransform(obj);
    }

    private void PositionCamera()
    {
        if (_camera == null)
            return;
        _camera.transform.SetParent(transform, false);
        _camera.transform.SetPositionAndRotation(
            new Vector3(0f, 9f, -11f), Quaternion.Euler(38f, 0f, 0f));
    }

    private static void DestroyIfCreated(Object obj)
    {
        if (obj == null)
            return;
        if (Application.isPlaying)
            Destroy(obj);
        else
            DestroyImmediate(obj);
    }
}
