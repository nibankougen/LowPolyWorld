using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

/// <summary>
/// 地形タブのシーン統合 MonoBehaviour（screens-and-modes.md 11.7.2 — エンジン境界のみ）。
///
/// - TerrainTabController（UI）のイベントを TerrainEditLogic / TerrainEditSession / TerrainRenderer に配線
/// - 斜め上方向の固定カメラ（現在の高さスライスを俯瞰）
/// - 現在の高さのグリッド境界・選択範囲・ドラッグ範囲のラインオーバーレイ描画
/// - ポインタ入力（3D ビュー領域のみ）→ TerrainGridPicker → セル操作 → Dirty チャンク再構築
/// - 上方半透明（ディザ）と上方非表示（クリップ）の切替
/// </summary>
[RequireComponent(typeof(TerrainRenderer))]
public class TerrainEditSceneController : MonoBehaviour
{
    [SerializeField] private float cameraPitch = 55f;
    [SerializeField] private float cameraYaw = 0f;
    [SerializeField] private float cameraDistance = 12f;

    private TerrainRenderer _terrainRenderer;
    private TerrainVoxelStore _store;
    private TerrainEditLogic _edit;
    private TerrainEditSession _session;
    private TerrainTabController _tab;
    private Camera _camera;
    private VisualElement _viewArea;
    private IPanel _panel;

    private GameObject _gridLines;
    private LineOverlay _selectionOverlay;
    private LineOverlay _dragRectOverlay;
    private Material _gridMaterial;
    private Material _selectionMaterial;
    private Material _dragRectMaterial;
    private bool _pointerActive;
    private bool _hideAbove;
    private bool _initialized;

    /// <summary>地形タブが選択されている間のみ true（WorldEditorController 側が制御）。</summary>
    public bool EditingEnabled { get; set; } = true;

    public TerrainEditLogic EditLogic => _edit;

    public void Initialize(
        TerrainTabController tab,
        VisualElement uiRoot,
        Camera editCamera,
        TerrainVoxelStore store,
        ITerrainAtlasMap atlasMap,
        Texture2D atlasTexture)
    {
        _terrainRenderer = GetComponent<TerrainRenderer>();
        _tab = tab;
        _camera = editCamera;
        _store = store;
        _edit = new TerrainEditLogic(store);
        _session = new TerrainEditSession(_edit);
        _viewArea = uiRoot.Q("view3d-area");
        _panel = uiRoot.panel;

        _terrainRenderer.Build(store, atlasMap, atlasTexture);
        CreateOverlays();

        tab.ModeChanged += mode => _session.Mode = mode;
        tab.FloodSelectChanged += flood => _session.FloodSelect = flood;
        tab.TerrainSelected += index => _session.SelectedPalette = Mathf.Clamp(index, 0, TerrainVoxel.MaxPaletteIndex);
        tab.HeightChanged += OnHeightChanged;
        tab.HideAboveChanged += hide =>
        {
            _hideAbove = hide;
            ApplyVisibility();
        };
        tab.CopyClicked += () =>
        {
            _edit.CopySelection();
            RefreshSelectionState();
        };
        tab.PasteClicked += () =>
        {
            if (_edit.Paste())
                RebuildDirtyChunks();
        };

        _edit.SetHeight(tab.Height);
        _session.SelectedPalette = Mathf.Max(0, tab.SelectedTerrainIndex);
        _initialized = true;
        OnHeightChanged(tab.Height);
        RefreshSelectionState();
    }

    private void OnDestroy()
    {
        DestroyIfCreated(_gridMaterial);
        DestroyIfCreated(_selectionMaterial);
        DestroyIfCreated(_dragRectMaterial);
    }

    // ── 入力 ──────────────────────────────────────────────────────────────────

    private void Update()
    {
        if (!_initialized || !EditingEnabled || _camera == null)
            return;
        var pointer = Pointer.current;
        if (pointer == null)
            return;

        bool pressed = pointer.press.isPressed;
        Vector2 screenPos = pointer.position.ReadValue();

        // 高さ ▲/▼ の長押し連続移動（単発タップは UI の clicked が担当）。
        // UI Toolkit の Button キャプチャ挙動で schedule + ポインタイベント方式が不安定なため、
        // 3D 編集と同じ Pointer.current のポーリングで実装する。
        UpdateHeightHold(screenPos, pressed);

        if (pressed && !_pointerActive)
        {
            if (IsOverViewArea(screenPos) && TryPickCell(screenPos, out int x, out int z))
            {
                _pointerActive = true;
                ApplyResult(_session.OnPointerDown(x, z));
                RefreshDragRectOverlay();
            }
        }
        else if (pressed && _pointerActive)
        {
            if (TryPickCell(screenPos, out int x, out int z))
            {
                ApplyResult(_session.OnPointerDrag(x, z));
                RefreshDragRectOverlay();
            }
        }
        else if (!pressed && _pointerActive)
        {
            _pointerActive = false;
            ApplyResult(_session.OnPointerUp());
            RefreshDragRectOverlay();
        }
    }

    private bool TryPickCell(Vector2 screenPos, out int x, out int z)
    {
        Ray ray = _camera.ScreenPointToRay(screenPos);
        Vector3 localOrigin = transform.InverseTransformPoint(ray.origin);
        Vector3 localDir = transform.InverseTransformDirection(ray.direction);
        return TerrainGridPicker.TryPickCell(localOrigin, localDir, _edit.CurrentHeight, out x, out z);
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

    // ── 高さ ▲/▼ の長押し連続移動 ─────────────────────────────────────────────

    private const float HeightHoldDelaySec = 0.5f;     // 連続移動を始めるまでの長押し時間
    private const float HeightHoldIntervalSec = 0.1f;  // 連続移動の間隔
    private string _heldHeightButton;
    private float _heightHoldElapsed;
    private float _heightNextRepeat;

    /// <summary>
    /// 高さ ▲/▼ ボタンを 0.5 秒以上押し続けている間、0.1 秒ごとに高さを 1 段ずつ変える。
    /// 単発タップは UI（clicked）が担当するため、ここでは押下開始後 0.5 秒経過分のみ処理する。
    /// </summary>
    private void UpdateHeightHold(Vector2 screenPos, bool pressed)
    {
        string name = pressed ? PickHeightButton(screenPos) : null;
        if (name != _heldHeightButton)
        {
            _heldHeightButton = name;
            _heightHoldElapsed = 0f;
            _heightNextRepeat = HeightHoldDelaySec;
            return;
        }
        if (name == null)
            return;

        _heightHoldElapsed += Time.deltaTime;
        int delta = name == "terrain-height-up" ? 1 : -1;
        while (_heightHoldElapsed >= _heightNextRepeat)
        {
            _tab.SetHeight(_tab.Height + delta);
            _heightNextRepeat += HeightHoldIntervalSec;
        }
    }

    /// <summary>ポインタ位置にある高さボタンの名前（terrain-height-up / -down）。無ければ null。</summary>
    private string PickHeightButton(Vector2 screenPos)
    {
        if (_panel == null)
            return null;
        Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(
            _panel, new Vector2(screenPos.x, Screen.height - screenPos.y));
        var picked = _panel.Pick(panelPos);
        while (picked != null)
        {
            if (picked.name == "terrain-height-up" || picked.name == "terrain-height-down")
                return picked.name;
            picked = picked.parent;
        }
        return null;
    }

    private void ApplyResult(TerrainEditSession.EditResult result)
    {
        if (result.TerrainChanged)
            RebuildDirtyChunks();
        if (result.SelectionChanged || result.TerrainChanged)
            RefreshSelectionState();
        if (!string.IsNullOrEmpty(result.FlashMessage))
            _tab.ShowFlash(result.FlashMessage);
    }

    private void RebuildDirtyChunks()
    {
        foreach (var (cx, cy, cz) in _edit.ConsumeDirtyChunks())
            _terrainRenderer.RebuildChunk(cx, cy, cz);
    }

    private void RefreshSelectionState()
    {
        _tab.SetSelectionState(_edit.HasSelection, _edit.HasClipboard);
        _selectionOverlay.SetCells(_edit.Selection, _edit.CurrentHeight);
    }

    // ── 高さ・表示 ────────────────────────────────────────────────────────────

    private void OnHeightChanged(int height)
    {
        _edit.SetHeight(height);
        ApplyVisibility();
        ApplyCamera();

        float planeY = height * TerrainMeshBuilder.BlockSize;
        _gridLines.transform.localPosition = new Vector3(0f, planeY + 0.002f, 0f);
        _selectionOverlay.SetCells(_edit.Selection, height);
        RefreshDragRectOverlay();
    }

    // 編集レイヤー強調: 編集中レイヤー以外（上・下）を市松ディザの疑似半透明にする。
    // 上方非表示 ON のときは上をクリップで完全非表示にし、下のみディザ（11.7.2）
    private void ApplyVisibility()
    {
        int height = _edit.CurrentHeight;
        _terrainRenderer.ApplyHeightCullingThreshold(
            _hideAbove ? height + 1 : TerrainHeightCulling.NoCulling);
        _terrainRenderer.ApplyDitherRange(
            height, _hideAbove ? TerrainHeightCulling.NoCulling : height + 1);
    }

    private void ApplyCamera()
    {
        if (_camera == null)
            return;
        Vector3 center = transform.TransformPoint(new Vector3(
            TerrainVoxelStore.SizeX * TerrainMeshBuilder.BlockSize * 0.5f,
            _edit.CurrentHeight * TerrainMeshBuilder.BlockSize,
            TerrainVoxelStore.SizeZ * TerrainMeshBuilder.BlockSize * 0.5f));
        var rotation = Quaternion.Euler(cameraPitch, cameraYaw, 0f);
        _camera.transform.SetPositionAndRotation(center - rotation * Vector3.forward * cameraDistance, rotation);
    }

    // ── オーバーレイ（ライン描画） ────────────────────────────────────────────

    private void CreateOverlays()
    {
        var shader = Shader.Find("LowPoly/Unlit");
        _gridMaterial = new Material(shader) { color = new Color(0.1f, 0.1f, 0.1f, 1f) };
        _selectionMaterial = new Material(shader) { color = new Color(0.2f, 0.9f, 1f, 1f) };
        _dragRectMaterial = new Material(shader) { color = new Color(1f, 0.85f, 0.2f, 1f) };

        _gridLines = new GameObject("GridLines");
        _gridLines.transform.SetParent(transform, false);
        _gridLines.AddComponent<MeshFilter>().sharedMesh = BuildGridMesh();
        var gridRenderer = _gridLines.AddComponent<MeshRenderer>();
        gridRenderer.sharedMaterial = _gridMaterial;
        gridRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        _selectionOverlay = new LineOverlay(transform, "SelectionLines", _selectionMaterial, 0.004f);
        _dragRectOverlay = new LineOverlay(transform, "DragRectLines", _dragRectMaterial, 0.006f);
    }

    private void RefreshDragRectOverlay()
    {
        if (_session.TryGetDragRect(out int x0, out int z0, out int x1, out int z1))
            _dragRectOverlay.SetRect(x0, z0, x1, z1, _edit.CurrentHeight);
        else
            _dragRectOverlay.Clear();
    }

    /// <summary>現在の高さのグリッド境界ライン（63 × 63 セル / ローカル y = 0 平面）。</summary>
    private static Mesh BuildGridMesh()
    {
        const float cell = TerrainMeshBuilder.BlockSize;
        float sizeX = TerrainVoxelStore.SizeX * cell;
        float sizeZ = TerrainVoxelStore.SizeZ * cell;

        var vertices = new List<Vector3>();
        var indices = new List<int>();
        for (int x = 0; x <= TerrainVoxelStore.SizeX; x++)
        {
            indices.Add(vertices.Count);
            vertices.Add(new Vector3(x * cell, 0f, 0f));
            indices.Add(vertices.Count);
            vertices.Add(new Vector3(x * cell, 0f, sizeZ));
        }
        for (int z = 0; z <= TerrainVoxelStore.SizeZ; z++)
        {
            indices.Add(vertices.Count);
            vertices.Add(new Vector3(0f, 0f, z * cell));
            indices.Add(vertices.Count);
            vertices.Add(new Vector3(sizeX, 0f, z * cell));
        }

        var mesh = new Mesh { name = "TerrainGridLines" };
        mesh.SetVertices(vertices);
        mesh.SetIndices(indices, MeshTopology.Lines, 0);
        mesh.RecalculateBounds();
        return mesh;
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

    /// <summary>セル外周ライン群の表示用ヘルパー（選択範囲・ドラッグ範囲）。</summary>
    private sealed class LineOverlay
    {
        private readonly GameObject _go;
        private readonly Mesh _mesh;
        private readonly float _yOffset;

        public LineOverlay(Transform parent, string name, Material material, float yOffset)
        {
            _yOffset = yOffset;
            _mesh = new Mesh { name = name };
            _go = new GameObject(name);
            _go.transform.SetParent(parent, false);
            _go.AddComponent<MeshFilter>().sharedMesh = _mesh;
            var meshRenderer = _go.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        public void Clear() => _mesh.Clear();

        public void SetRect(int x0, int z0, int x1, int z1, int height)
        {
            var cells = new List<(int x, int z)>();
            for (int z = z0; z <= z1; z++)
                for (int x = x0; x <= x1; x++)
                    cells.Add((x, z));
            SetCells(cells, height);
        }

        public void SetCells(IReadOnlyCollection<(int x, int z)> cells, int height)
        {
            _mesh.Clear();
            if (cells.Count == 0)
                return;

            const float cell = TerrainMeshBuilder.BlockSize;
            // レイヤーの下面と上面の両方に枠線を描く
            //（ブロックが配置済みのセルでは下面側の枠線が隠れて見えなくなるため）
            float bottomY = height * cell + _yOffset;
            float topY = (height + 1) * cell + _yOffset;
            var vertices = new List<Vector3>(cells.Count * 16);
            var indices = new List<int>(cells.Count * 16);
            foreach (var (x, z) in cells)
            {
                float x0 = x * cell;
                float x1 = x0 + cell;
                float z0 = z * cell;
                float z1 = z0 + cell;
                AddCellOutline(vertices, indices, x0, z0, x1, z1, bottomY);
                AddCellOutline(vertices, indices, x0, z0, x1, z1, topY);
            }
            _mesh.SetVertices(vertices);
            _mesh.SetIndices(indices, MeshTopology.Lines, 0);
            _mesh.RecalculateBounds();
        }

        private static void AddCellOutline(
            List<Vector3> vertices, List<int> indices, float x0, float z0, float x1, float z1, float y)
        {
            AddLine(vertices, indices, new Vector3(x0, y, z0), new Vector3(x1, y, z0));
            AddLine(vertices, indices, new Vector3(x1, y, z0), new Vector3(x1, y, z1));
            AddLine(vertices, indices, new Vector3(x1, y, z1), new Vector3(x0, y, z1));
            AddLine(vertices, indices, new Vector3(x0, y, z1), new Vector3(x0, y, z0));
        }

        private static void AddLine(List<Vector3> vertices, List<int> indices, Vector3 a, Vector3 b)
        {
            indices.Add(vertices.Count);
            vertices.Add(a);
            indices.Add(vertices.Count);
            vertices.Add(b);
        }
    }
}
