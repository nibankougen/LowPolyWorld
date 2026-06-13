using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

/// <summary>
/// オブジェクトタブの配置データモデル（world-creation.md 3.4 / 3.6・screens-and-modes.md 11.7.3）。純粋 C#。
///
/// - 配置オブジェクト一覧（描画順 = リスト順。複製は直後に挿入・D&D 並び替え対応）
/// - グループツリー（最大 4 段ネスト・合計 32 個。objects/groups は別配列で保持し、JSON 規約 12.2b と一致）
/// - オブジェクト数上限 400 / テクスチャコスト上限 4096（集計は TextureCostCalculator に委譲）
///
/// 特殊オブジェクト（スポーン・ポータル等）はこのストアの対象外（数・コスト非対象 — 3.6）。
/// instanceId / groupId はストア内で一意に採番する（"obj{n}" / "grp{n}"）。
/// </summary>
public class ObjectPlacementStore
{
    public const int MaxObjects = TextureCostCalculator.ObjectCountLimit; // 400
    public const int MaxGroups = 32;
    public const int MaxNestDepth = 4;
    public const int GroupNameMaxLength = 20;

    private readonly List<WorldObjectInstance> _objects = new();
    private readonly List<GroupJson> _groups = new();
    private int _nextId = 1;

    /// <summary>配置オブジェクト一覧（描画順）。</summary>
    public IReadOnlyList<WorldObjectInstance> Objects => _objects;

    /// <summary>グループ一覧（ツリー復元用メタデータ）。</summary>
    public IReadOnlyList<GroupJson> Groups => _groups;

    public int ObjectCount => _objects.Count;
    public int GroupCount => _groups.Count;
    public bool CanAddObject => _objects.Count < MaxObjects;
    public bool CanCreateGroup => _groups.Count < MaxGroups;

    // ── オブジェクト操作 ──────────────────────────────────────────────────────

    /// <summary>
    /// オブジェクトを末尾（最前面）に追加する。原点 (0,0,0)・回転 0・サイズはデフォルト（センチネル）。
    /// 数上限に達している場合は null。
    /// </summary>
    public WorldObjectInstance Add(string objectTypeId, string groupId = "", string savedVariantId = null)
    {
        if (!CanAddObject)
            return null;
        var obj = new WorldObjectInstance
        {
            instanceId = NewId("obj"),
            objectTypeId = objectTypeId ?? "",
            savedVariantId = savedVariantId,
            groupId = GroupExists(groupId) ? groupId : "",
            position = new IntVec3Json(),
            rotationY = 0,
            size = new IntVec3Json(),
        };
        _objects.Add(obj);
        return obj;
    }

    /// <summary>指定オブジェクトを削除する。</summary>
    public bool Remove(string instanceId)
    {
        int index = IndexOf(instanceId);
        if (index < 0)
            return false;
        _objects.RemoveAt(index);
        return true;
    }

    /// <summary>
    /// オブジェクトを複製し、複製元の直後に挿入する（同じ位置・回転・サイズ・グループ）。
    /// 数上限に達している場合・対象がない場合は null。
    /// </summary>
    public WorldObjectInstance Duplicate(string instanceId)
    {
        if (!CanAddObject)
            return null;
        int index = IndexOf(instanceId);
        if (index < 0)
            return null;
        var src = _objects[index];
        var copy = new WorldObjectInstance
        {
            instanceId = NewId("obj"),
            objectTypeId = src.objectTypeId,
            savedVariantId = src.savedVariantId,
            groupId = src.groupId,
            position = new IntVec3Json(src.position.x, src.position.y, src.position.z),
            rotationY = src.rotationY,
            size = new IntVec3Json(src.size.x, src.size.y, src.size.z),
        };
        _objects.Insert(index + 1, copy);
        return copy;
    }

    /// <summary>描画順（リスト順）を変更する。newIndex はリスト範囲にクランプする。</summary>
    public bool Reorder(string instanceId, int newIndex)
    {
        int index = IndexOf(instanceId);
        if (index < 0)
            return false;
        var obj = _objects[index];
        _objects.RemoveAt(index);
        newIndex = Math.Clamp(newIndex, 0, _objects.Count);
        _objects.Insert(newIndex, obj);
        return true;
    }

    public WorldObjectInstance Find(string instanceId) =>
        _objects.Find(o => o.instanceId == instanceId);

    /// <summary>描画順リスト内のインデックス（見つからなければ -1）。</summary>
    public int IndexOf(string instanceId) => _objects.FindIndex(o => o.instanceId == instanceId);

    /// <summary>オブジェクトの所属グループを設定する（"" = ルート直下）。グループが存在しなければ false。</summary>
    public bool SetObjectGroup(string instanceId, string groupId)
    {
        var obj = Find(instanceId);
        if (obj == null)
            return false;
        if (!string.IsNullOrEmpty(groupId) && !GroupExists(groupId))
            return false;
        obj.groupId = groupId ?? "";
        return true;
    }

    // ── グループ操作 ──────────────────────────────────────────────────────────

    /// <summary>
    /// グループを作成する。合計 32 個上限・親の深さが 4 段未満（新グループ深さ ≤ 4）のときのみ成功し、
    /// groupId を返す（失敗時 null）。name が空のときは「グループN」を自動採番する。
    /// </summary>
    public string CreateGroup(string parentGroupId = "", string name = null)
    {
        if (!CanCreateGroup)
            return null;
        if (!string.IsNullOrEmpty(parentGroupId) && !GroupExists(parentGroupId))
            return null;
        if (GroupDepth(parentGroupId) + 1 > MaxNestDepth)
            return null;

        string finalName = NormalizeGroupName(name);
        var group = new GroupJson
        {
            groupId = NewId("grp"),
            name = finalName,
            parentGroupId = parentGroupId ?? "",
            sortOrder = CountChildGroups(parentGroupId),
        };
        _groups.Add(group);
        return group.groupId;
    }

    /// <summary>グループ名を変更する（1〜20 文字・空不可）。</summary>
    public bool RenameGroup(string groupId, string name)
    {
        var group = FindGroup(groupId);
        if (group == null)
            return false;
        if (string.IsNullOrEmpty(name) || name.Length > GroupNameMaxLength)
            return false;
        group.name = name;
        return true;
    }

    /// <summary>
    /// グループを削除する。直下の子オブジェクト・子グループは、削除するグループの親へ繰り上げる
    /// （ツリーを 1 段詰める）。
    /// </summary>
    public bool DeleteGroup(string groupId)
    {
        var group = FindGroup(groupId);
        if (group == null)
            return false;

        string newParent = group.parentGroupId;
        foreach (var obj in _objects)
            if (obj.groupId == groupId)
                obj.groupId = newParent;
        foreach (var g in _groups)
            if (g.parentGroupId == groupId)
                g.parentGroupId = newParent;

        _groups.Remove(group);
        return true;
    }

    /// <summary>
    /// グループの親を変更する。自己・自身の子孫への移動は不可（循環防止）。
    /// 移動後のサブツリーの最深部が 4 段を超える場合も不可。
    /// </summary>
    public bool SetGroupParent(string groupId, string newParentId)
    {
        var group = FindGroup(groupId);
        if (group == null)
            return false;
        if (!string.IsNullOrEmpty(newParentId) && !GroupExists(newParentId))
            return false;
        if (groupId == newParentId || IsDescendantOf(newParentId, groupId))
            return false;
        if (GroupDepth(newParentId) + SubtreeHeight(groupId) > MaxNestDepth)
            return false;

        group.parentGroupId = newParentId ?? "";
        return true;
    }

    /// <summary>グループの深さ（ルート直下 = 1）。"" / 不明は 0。</summary>
    public int GroupDepth(string groupId)
    {
        int depth = 0;
        string current = groupId;
        while (!string.IsNullOrEmpty(current))
        {
            var g = FindGroup(current);
            if (g == null)
                break;
            depth++;
            current = g.parentGroupId;
            if (depth > MaxGroups)
                break; // 万一の循環ガード
        }
        return depth;
    }

    /// <summary>サブツリーの高さ（自身のみ = 1）。</summary>
    public int SubtreeHeight(string groupId)
    {
        int max = 0;
        foreach (var g in _groups)
            if (g.parentGroupId == groupId)
                max = Math.Max(max, SubtreeHeight(g.groupId));
        return max + 1;
    }

    // ── 複数選択（同一階層判定） ──────────────────────────────────────────────

    /// <summary>
    /// id（オブジェクト or グループ）が属する階層コンテナ（オブジェクト = groupId / グループ = parentGroupId）。
    /// 見つからない場合は null。
    /// </summary>
    public string ContainerOf(string id)
    {
        var obj = Find(id);
        if (obj != null)
            return obj.groupId ?? "";
        var group = FindGroup(id);
        if (group != null)
            return group.parentGroupId ?? "";
        return null;
    }

    /// <summary>複数選択用: すべてのアイテムが同一階層レベル（同じコンテナ）にあるか（11.7.3）。</summary>
    public bool AreSameLevel(IEnumerable<string> ids)
    {
        string container = null;
        bool first = true;
        foreach (var id in ids)
        {
            string c = ContainerOf(id);
            if (c == null)
                return false; // 不明な ID
            if (first)
            {
                container = c;
                first = false;
            }
            else if (c != container)
            {
                return false;
            }
        }
        return true;
    }

    // ── 集計 ──────────────────────────────────────────────────────────────────

    /// <summary>合計テクスチャコスト（配置ベース・TextureCostCalculator に委譲）。</summary>
    public int CalculateCost(Func<string, int> textureSizeGetter, IEnumerable<string> switchTargetTypeIds = null) =>
        TextureCostCalculator.Calculate(_objects, textureSizeGetter, switchTargetTypeIds);

    // ── Private ───────────────────────────────────────────────────────────────

    private string NewId(string prefix) => $"{prefix}{_nextId++}";

    private GroupJson FindGroup(string groupId) =>
        string.IsNullOrEmpty(groupId) ? null : _groups.Find(g => g.groupId == groupId);

    private bool GroupExists(string groupId) => FindGroup(groupId) != null;

    private int CountChildGroups(string parentGroupId)
    {
        int count = 0;
        foreach (var g in _groups)
            if (g.parentGroupId == (parentGroupId ?? ""))
                count++;
        return count;
    }

    /// <summary>candidate が ancestor の子孫（自身を含む）か。</summary>
    private bool IsDescendantOf(string candidate, string ancestor)
    {
        string current = candidate;
        while (!string.IsNullOrEmpty(current))
        {
            if (current == ancestor)
                return true;
            var g = FindGroup(current);
            if (g == null)
                return false;
            current = g.parentGroupId;
        }
        return false;
    }

    private static readonly Regex DefaultNamePattern = new(@"^グループ(\d+)$", RegexOptions.Compiled);

    /// <summary>name が空なら「グループN」を採番（既存の最大連番 + 1）。20 文字超は切り詰める。</summary>
    private string NormalizeGroupName(string name)
    {
        if (!string.IsNullOrEmpty(name))
            return name.Length > GroupNameMaxLength ? name.Substring(0, GroupNameMaxLength) : name;

        int max = 0;
        foreach (var g in _groups)
        {
            var m = DefaultNamePattern.Match(g.name ?? "");
            if (m.Success && int.TryParse(m.Groups[1].Value, out int n))
                max = Math.Max(max, n);
        }
        return $"グループ{max + 1}";
    }
}
