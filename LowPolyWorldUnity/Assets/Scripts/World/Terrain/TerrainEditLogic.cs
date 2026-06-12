using System;
using System.Collections.Generic;

/// <summary>
/// 地形タブの編集ロジック（screens-and-modes.md セクション 11.7.2）。純粋 C#・Unity 非依存。
///
/// - 編集対象は「現在の高さ」（Y グリッドインデックス）の XZ 平面
/// - ブラシ（cube 配置・上書き）/ 消しゴム / 図形（四角形）/ タイプ変更 / 範囲選択 / 移動 / コピー&ペースト
/// - 範囲選択中はブラシ・消しゴム・図形・タイプ変更・移動の対象が選択範囲内に制限される
/// - 変更されたチャンク（境界編集時は隣接チャンク含む）を Dirty として蓄積し、
///   上位レイヤーが ConsumeDirtyChunks() → TerrainRenderer.RebuildChunk で反映する
///
/// タイプ変更の向きの定義（仕様の「空いている側面の方向」の実装定義）:
/// - ramp: 低い側（斜面が下る方向）= 空いている側面。真上も空であること
/// - diag: 直角に隣り合う 2 側面が空 → その反対側 2 面が solid になる向き
/// - サイクル順: cube → ramp N/E/S/W → diag NW/NE/SE/SW → cube（有効な向きのみ）
/// </summary>
public class TerrainEditLogic
{
    public enum TypeChangeResult
    {
        Changed,       // 形状を変更した（立方体への復帰を含む）
        NotChangeable, // 変更できる形状がない（エラーフラッシュ対象）
    }

    private static readonly TerrainShape[] CycleOrder =
    {
        TerrainShape.Cube,
        TerrainShape.RampN, TerrainShape.RampE, TerrainShape.RampS, TerrainShape.RampW,
        TerrainShape.DiagNW, TerrainShape.DiagNE, TerrainShape.DiagSE, TerrainShape.DiagSW,
    };

    private readonly TerrainVoxelStore _store;
    private readonly HashSet<(int x, int z)> _selection = new HashSet<(int, int)>();
    private readonly HashSet<(int cx, int cy, int cz)> _dirtyChunks = new HashSet<(int, int, int)>();
    private readonly List<(int x, int z, byte voxel)> _clipboard = new List<(int, int, byte)>();
    private bool _hasClipboard;

    public TerrainEditLogic(TerrainVoxelStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    /// <summary>編集対象の高さ（Y グリッドインデックス。高さバーで変更）。</summary>
    public int CurrentHeight { get; private set; }

    public void SetHeight(int height)
    {
        if (height < 0)
            height = 0;
        else if (height >= TerrainVoxelStore.SizeY)
            height = TerrainVoxelStore.SizeY - 1;
        CurrentHeight = height;
    }

    // ── 範囲選択 ──────────────────────────────────────────────────────────────

    public bool HasSelection => _selection.Count > 0;
    public IReadOnlyCollection<(int x, int z)> Selection => _selection;
    public bool HasClipboard => _hasClipboard;

    /// <summary>矩形範囲を選択する（addToSelection = true で複数範囲）。空セルも選択に含む。</summary>
    public void SelectRect(int x0, int z0, int x1, int z1, bool addToSelection = false)
    {
        if (!addToSelection)
            _selection.Clear();
        Normalize(ref x0, ref x1);
        Normalize(ref z0, ref z1);
        for (int z = Math.Max(z0, 0); z <= Math.Min(z1, TerrainVoxelStore.SizeZ - 1); z++)
            for (int x = Math.Max(x0, 0); x <= Math.Min(x1, TerrainVoxelStore.SizeX - 1); x++)
                _selection.Add((x, z));
    }

    /// <summary>
    /// 塗りつぶし範囲選択。タッチ地点から X / Z 方向に同じ地形種別（ramp・diag も同種とみなす =
    /// 同一パレットインデックス）が隣接している限り広げる。斜め隣接は接続しない。
    /// タッチ地点に地形がない場合は false。
    /// </summary>
    public bool SelectFlood(int x, int z, bool addToSelection = false)
    {
        if (!InBoundsXZ(x, z))
            return false;
        byte seed = _store.GetVoxel(x, CurrentHeight, z);
        if (TerrainVoxel.IsEmpty(seed))
            return false;
        if (!addToSelection)
            _selection.Clear();

        int palette = TerrainVoxel.GetPaletteIndex(seed);
        var queue = new Queue<(int x, int z)>();
        var visited = new HashSet<(int, int)>();
        queue.Enqueue((x, z));
        visited.Add((x, z));
        while (queue.Count > 0)
        {
            var (cx, cz) = queue.Dequeue();
            _selection.Add((cx, cz));
            foreach (var (nx, nz) in new[] { (cx + 1, cz), (cx - 1, cz), (cx, cz + 1), (cx, cz - 1) })
            {
                if (!InBoundsXZ(nx, nz) || visited.Contains((nx, nz)))
                    continue;
                byte v = _store.GetVoxel(nx, CurrentHeight, nz);
                if (TerrainVoxel.IsEmpty(v) || TerrainVoxel.GetPaletteIndex(v) != palette)
                    continue;
                visited.Add((nx, nz));
                queue.Enqueue((nx, nz));
            }
        }
        return true;
    }

    public void ClearSelection() => _selection.Clear();

    // ── ブラシ / 消しゴム / 図形 ──────────────────────────────────────────────

    /// <summary>ブラシ: 現在の高さに選択中の地形（cube）を配置する。配置済みは上書き。</summary>
    public bool PaintCell(int x, int z, int paletteIndex)
    {
        if (!CanEdit(x, z))
            return false;
        SetVoxel(x, CurrentHeight, z, TerrainVoxel.Encode(TerrainShape.Cube, paletteIndex));
        return true;
    }

    /// <summary>消しゴム: 現在の高さの地形を削除する。</summary>
    public bool EraseCell(int x, int z)
    {
        if (!CanEdit(x, z))
            return false;
        if (TerrainVoxel.IsEmpty(_store.GetVoxel(x, CurrentHeight, z)))
            return false;
        SetVoxel(x, CurrentHeight, z, TerrainVoxel.Empty);
        return true;
    }

    /// <summary>図形モード（四角形）: 矩形範囲に一括配置する。配置したセル数を返す。</summary>
    public int FillRect(int x0, int z0, int x1, int z1, int paletteIndex)
    {
        Normalize(ref x0, ref x1);
        Normalize(ref z0, ref z1);
        byte voxel = TerrainVoxel.Encode(TerrainShape.Cube, paletteIndex);
        int count = 0;
        for (int z = Math.Max(z0, 0); z <= Math.Min(z1, TerrainVoxelStore.SizeZ - 1); z++)
        {
            for (int x = Math.Max(x0, 0); x <= Math.Min(x1, TerrainVoxelStore.SizeX - 1); x++)
            {
                if (!CanEdit(x, z))
                    continue;
                SetVoxel(x, CurrentHeight, z, voxel);
                count++;
            }
        }
        return count;
    }

    // ── タイプ変更 ────────────────────────────────────────────────────────────

    /// <summary>
    /// タイプ変更: cube → ramp（有効な向き）→ diag（有効な向き）→ cube のサイクルで形状を変える。
    /// ramp/diag の状態でどの形状の条件も満たさない場合は立方体に戻す。
    /// cube の状態で変更できる形状がない場合・地形がない場合は NotChangeable。
    /// </summary>
    public TypeChangeResult CycleType(int x, int z)
    {
        if (!CanEdit(x, z))
            return TypeChangeResult.NotChangeable;
        byte voxel = _store.GetVoxel(x, CurrentHeight, z);
        if (TerrainVoxel.IsEmpty(voxel))
            return TypeChangeResult.NotChangeable;

        var shape = TerrainVoxel.GetShape(voxel);
        int palette = TerrainVoxel.GetPaletteIndex(voxel);
        int index = Array.IndexOf(CycleOrder, shape);
        for (int step = 1; step <= CycleOrder.Length; step++)
        {
            var candidate = CycleOrder[(index + step) % CycleOrder.Length];
            if (candidate == TerrainShape.Cube)
            {
                // 一周（または条件をどれも満たさない）→ 立方体に戻す。元から cube なら変更不可
                if (shape == TerrainShape.Cube)
                    return TypeChangeResult.NotChangeable;
                SetVoxel(x, CurrentHeight, z, TerrainVoxel.Encode(TerrainShape.Cube, palette));
                return TypeChangeResult.Changed;
            }
            if (IsShapeValid(candidate, x, z))
            {
                SetVoxel(x, CurrentHeight, z, TerrainVoxel.Encode(candidate, palette));
                return TypeChangeResult.Changed;
            }
        }
        return TypeChangeResult.NotChangeable;
    }

    /// <summary>形状の配置条件（11.7.2）。ramp = 真上が空 + 低い側が空 / diag = 直角に隣り合う 2 側面が空。</summary>
    private bool IsShapeValid(TerrainShape shape, int x, int z)
    {
        int y = CurrentHeight;
        bool aboveEmpty = IsEmptyAt(x, y + 1, z);
        bool northEmpty = IsEmptyAt(x, y, z + 1);
        bool southEmpty = IsEmptyAt(x, y, z - 1);
        bool eastEmpty = IsEmptyAt(x + 1, y, z);
        bool westEmpty = IsEmptyAt(x - 1, y, z);
        switch (shape)
        {
            case TerrainShape.RampN: return aboveEmpty && southEmpty; // 低い側 = South
            case TerrainShape.RampE: return aboveEmpty && westEmpty;
            case TerrainShape.RampS: return aboveEmpty && northEmpty;
            case TerrainShape.RampW: return aboveEmpty && eastEmpty;
            case TerrainShape.DiagNW: return southEmpty && eastEmpty; // solid = N/W
            case TerrainShape.DiagNE: return southEmpty && westEmpty;
            case TerrainShape.DiagSE: return northEmpty && westEmpty;
            case TerrainShape.DiagSW: return northEmpty && eastEmpty;
            default: return false;
        }
    }

    // ── 移動 / コピー&ペースト ────────────────────────────────────────────────

    /// <summary>
    /// 移動: 選択範囲（なければ現在の高さの地形全体）を XZ 平面方向に移動する。
    /// 配置可能範囲の外に出たブロックは削除。選択範囲も移動後の位置に更新する。
    /// </summary>
    public bool Move(int dx, int dz)
    {
        if (dx == 0 && dz == 0)
            return false;

        int y = CurrentHeight;
        var cells = new List<(int x, int z, byte voxel)>();
        if (HasSelection)
        {
            foreach (var (x, z) in _selection)
            {
                byte v = _store.GetVoxel(x, y, z);
                if (!TerrainVoxel.IsEmpty(v))
                    cells.Add((x, z, v));
            }
        }
        else
        {
            for (int z = 0; z < TerrainVoxelStore.SizeZ; z++)
                for (int x = 0; x < TerrainVoxelStore.SizeX; x++)
                {
                    byte v = _store.GetVoxel(x, y, z);
                    if (!TerrainVoxel.IsEmpty(v))
                        cells.Add((x, z, v));
                }
        }

        // クリア → 書き込み（重なりを安全に処理）。範囲外に出たブロックは削除
        foreach (var (x, z, _) in cells)
            SetVoxel(x, y, z, TerrainVoxel.Empty);
        foreach (var (x, z, v) in cells)
            if (InBoundsXZ(x + dx, z + dz))
                SetVoxel(x + dx, y, z + dz, v);

        if (HasSelection)
        {
            var moved = new List<(int, int)>();
            foreach (var (x, z) in _selection)
                if (InBoundsXZ(x + dx, z + dz))
                    moved.Add((x + dx, z + dz));
            _selection.Clear();
            foreach (var cell in moved)
                _selection.Add(cell);
        }
        return true;
    }

    /// <summary>選択範囲内の地形をコピーする（地形がなければ false）。</summary>
    public bool CopySelection()
    {
        _clipboard.Clear();
        _hasClipboard = false;
        if (!HasSelection)
            return false;
        foreach (var (x, z) in _selection)
        {
            byte v = _store.GetVoxel(x, CurrentHeight, z);
            if (!TerrainVoxel.IsEmpty(v))
                _clipboard.Add((x, z, v));
        }
        _hasClipboard = _clipboard.Count > 0;
        return _hasClipboard;
    }

    /// <summary>コピーした地形を現在の高さに同じ XZ 位置で配置する（別の高さへのコピー用）。</summary>
    public bool Paste()
    {
        if (!_hasClipboard)
            return false;
        foreach (var (x, z, v) in _clipboard)
            SetVoxel(x, CurrentHeight, z, v);
        return true;
    }

    // ── Dirty チャンク ────────────────────────────────────────────────────────

    /// <summary>変更のあったチャンク一覧を取り出してクリアする（メッシュ・コライダー再構築用）。</summary>
    public List<(int cx, int cy, int cz)> ConsumeDirtyChunks()
    {
        var result = new List<(int, int, int)>(_dirtyChunks);
        _dirtyChunks.Clear();
        return result;
    }

    // ── Private ───────────────────────────────────────────────────────────────

    // 選択範囲があるときは範囲内のセルのみ編集対象（11.7.2 範囲選択）
    private bool CanEdit(int x, int z) =>
        InBoundsXZ(x, z) && (!HasSelection || _selection.Contains((x, z)));

    private static bool InBoundsXZ(int x, int z) =>
        (uint)x < TerrainVoxelStore.SizeX && (uint)z < TerrainVoxelStore.SizeZ;

    // 範囲外（ワールド境界の外・上端の上）は「空いている」として扱う
    private bool IsEmptyAt(int x, int y, int z) =>
        !TerrainVoxelStore.InBounds(x, y, z) || TerrainVoxel.IsEmpty(_store.GetVoxel(x, y, z));

    private void SetVoxel(int x, int y, int z, byte voxel)
    {
        if (_store.GetVoxel(x, y, z) == voxel)
            return;
        _store.SetVoxel(x, y, z, voxel);
        MarkDirty(x, y, z);
    }

    /// <summary>
    /// 変更セルのチャンクを Dirty にする。チャンク境界のセルは隣接チャンクのメッシュ
    /// （面カリング・AO の斜め参照）にも影響するため、境界方向の隣接チャンクも含める。
    /// </summary>
    private void MarkDirty(int x, int y, int z)
    {
        int cx = x >> 4;
        int cy = y >> 4;
        int cz = z >> 4;
        Span<int> xs = stackalloc int[2] { cx, cx };
        Span<int> ys = stackalloc int[2] { cy, cy };
        Span<int> zs = stackalloc int[2] { cz, cz };
        int xn = 1, yn = 1, zn = 1;
        if ((x & 15) == 0 && cx > 0) { xs[1] = cx - 1; xn = 2; }
        if ((x & 15) == 15 && cx < TerrainVoxelStore.ChunkCountX - 1) { xs[1] = cx + 1; xn = 2; }
        if ((y & 15) == 0 && cy > 0) { ys[1] = cy - 1; yn = 2; }
        if ((y & 15) == 15 && cy < TerrainVoxelStore.ChunkCountY - 1) { ys[1] = cy + 1; yn = 2; }
        if ((z & 15) == 0 && cz > 0) { zs[1] = cz - 1; zn = 2; }
        if ((z & 15) == 15 && cz < TerrainVoxelStore.ChunkCountZ - 1) { zs[1] = cz + 1; zn = 2; }
        for (int i = 0; i < xn; i++)
            for (int j = 0; j < yn; j++)
                for (int k = 0; k < zn; k++)
                    _dirtyChunks.Add((xs[i], ys[j], zs[k]));
    }

    private static void Normalize(ref int a, ref int b)
    {
        if (a > b)
            (a, b) = (b, a);
    }
}
