using NUnit.Framework;

public class ObjectPlacementStoreTests
{
    private ObjectPlacementStore _store;

    [SetUp]
    public void SetUp() => _store = new ObjectPlacementStore();

    // ── オブジェクト追加・削除 ────────────────────────────────────────────────

    [Test]
    public void Add_AppendsWithDefaults()
    {
        var obj = _store.Add("desk");
        Assert.IsNotNull(obj);
        Assert.AreEqual("desk", obj.objectTypeId);
        Assert.IsTrue(obj.position.IsZero, "原点に配置");
        Assert.IsTrue(obj.size.IsZero, "サイズはデフォルト（センチネル）");
        Assert.AreEqual(0, obj.rotationY);
        Assert.AreEqual(1, _store.ObjectCount);
        Assert.AreEqual(obj, _store.Objects[0]);
    }

    [Test]
    public void Add_AtCountLimit_ReturnsNull()
    {
        for (int i = 0; i < ObjectPlacementStore.MaxObjects; i++)
            Assert.IsNotNull(_store.Add("o"));
        Assert.IsFalse(_store.CanAddObject);
        Assert.IsNull(_store.Add("overflow"), "400 を超える追加は不可");
        Assert.AreEqual(ObjectPlacementStore.MaxObjects, _store.ObjectCount);
    }

    [Test]
    public void Remove_RemovesById()
    {
        var a = _store.Add("a");
        _store.Add("b");
        Assert.IsTrue(_store.Remove(a.instanceId));
        Assert.AreEqual(1, _store.ObjectCount);
        Assert.IsFalse(_store.Remove("missing"));
    }

    // ── 複製 ──────────────────────────────────────────────────────────────────

    [Test]
    public void Duplicate_InsertsCopyRightAfterSource()
    {
        var a = _store.Add("a");
        var b = _store.Add("b");
        a.position = new IntVec3Json(2, 0, 3);
        a.rotationY = 2;
        a.size = new IntVec3Json(4, 4, 4);
        a.groupId = "";

        var copy = _store.Duplicate(a.instanceId);
        Assert.IsNotNull(copy);
        Assert.AreNotEqual(a.instanceId, copy.instanceId, "新しい instanceId");
        Assert.AreEqual(1, _store.IndexOf(copy.instanceId), "複製元の直後に挿入");
        Assert.AreEqual(2, _store.IndexOf(b.instanceId), "b は後ろにずれる");
        Assert.AreEqual(2, copy.position.x);
        Assert.AreEqual(2, copy.rotationY);
        Assert.AreEqual(4, copy.size.x);
        Assert.AreNotSame(a.position, copy.position, "position は別インスタンス（参照共有なし）");
    }

    [Test]
    public void Duplicate_AtCountLimit_ReturnsNull()
    {
        var first = _store.Add("a");
        for (int i = 1; i < ObjectPlacementStore.MaxObjects; i++)
            _store.Add("o");
        Assert.IsNull(_store.Duplicate(first.instanceId));
    }

    // ── 並び替え（描画順） ────────────────────────────────────────────────────

    [Test]
    public void Reorder_MovesWithinDrawOrder()
    {
        var a = _store.Add("a");
        var b = _store.Add("b");
        var c = _store.Add("c");
        Assert.IsTrue(_store.Reorder(c.instanceId, 0));
        Assert.AreEqual(c, _store.Objects[0]);
        Assert.AreEqual(a, _store.Objects[1]);
        Assert.AreEqual(b, _store.Objects[2]);
    }

    [Test]
    public void Reorder_ClampsIndex()
    {
        var a = _store.Add("a");
        _store.Add("b");
        Assert.IsTrue(_store.Reorder(a.instanceId, 99));
        Assert.AreEqual(a, _store.Objects[1], "範囲外 index は末尾にクランプ");
    }

    // ── グループ作成・上限 ────────────────────────────────────────────────────

    [Test]
    public void CreateGroup_DefaultNameIsSequential()
    {
        string g1 = _store.CreateGroup();
        string g2 = _store.CreateGroup();
        Assert.AreEqual("グループ1", FindGroup(g1).name);
        Assert.AreEqual("グループ2", FindGroup(g2).name);
    }

    [Test]
    public void CreateGroup_RespectsTotalLimitOf32()
    {
        for (int i = 0; i < ObjectPlacementStore.MaxGroups; i++)
            Assert.IsNotNull(_store.CreateGroup());
        Assert.IsFalse(_store.CanCreateGroup);
        Assert.IsNull(_store.CreateGroup(), "合計 32 個を超えるグループ作成は不可");
    }

    [Test]
    public void CreateGroup_RespectsMaxNestDepthOf4()
    {
        string g1 = _store.CreateGroup();                 // 深さ 1
        string g2 = _store.CreateGroup(g1);               // 深さ 2
        string g3 = _store.CreateGroup(g2);               // 深さ 3
        string g4 = _store.CreateGroup(g3);               // 深さ 4
        Assert.IsNotNull(g4);
        Assert.AreEqual(4, _store.GroupDepth(g4));
        Assert.IsNull(_store.CreateGroup(g4), "深さ 5 は不可");
    }

    [Test]
    public void CreateGroup_UnderMissingParent_ReturnsNull()
    {
        Assert.IsNull(_store.CreateGroup("nope"));
    }

    // ── 改名 ──────────────────────────────────────────────────────────────────

    [Test]
    public void RenameGroup_EnforcesLength1To20()
    {
        string g = _store.CreateGroup();
        Assert.IsTrue(_store.RenameGroup(g, "拠点A"));
        Assert.AreEqual("拠点A", FindGroup(g).name);
        Assert.IsFalse(_store.RenameGroup(g, ""), "空不可");
        Assert.IsFalse(_store.RenameGroup(g, new string('x', 21)), "21 文字不可");
        Assert.IsTrue(_store.RenameGroup(g, new string('x', 20)), "20 文字は可");
    }

    // ── 削除（子の繰り上げ） ──────────────────────────────────────────────────

    [Test]
    public void DeleteGroup_ReparentsChildrenToParent()
    {
        string g1 = _store.CreateGroup();
        string g2 = _store.CreateGroup(g1);   // g1 の子グループ
        var obj = _store.Add("a", g2);        // g2 内のオブジェクト

        Assert.IsTrue(_store.DeleteGroup(g2));
        Assert.AreEqual(g1, _store.Find(obj.instanceId).groupId, "子オブジェクトは g2 の親 g1 へ繰り上げ");
        Assert.AreEqual(1, _store.GroupCount);
    }

    [Test]
    public void DeleteGroup_RootGroup_ChildrenGoToRoot()
    {
        string g1 = _store.CreateGroup();
        var obj = _store.Add("a", g1);
        Assert.IsTrue(_store.DeleteGroup(g1));
        Assert.AreEqual("", _store.Find(obj.instanceId).groupId, "ルート直下のグループ削除 → 子はルートへ");
    }

    // ── 親変更（循環・深さ） ──────────────────────────────────────────────────

    [Test]
    public void SetGroupParent_RejectsCycle()
    {
        string g1 = _store.CreateGroup();
        string g2 = _store.CreateGroup(g1);
        Assert.IsFalse(_store.SetGroupParent(g1, g1), "自己は不可");
        Assert.IsFalse(_store.SetGroupParent(g1, g2), "自身の子孫への移動は不可（循環）");
    }

    [Test]
    public void SetGroupParent_RejectsWhenSubtreeExceedsDepth()
    {
        // g1 > g2 > g3（g2 を起点とするサブツリー高さ 2）
        string g1 = _store.CreateGroup();
        string g2 = _store.CreateGroup(g1);
        _store.CreateGroup(g2);
        // 別系統に深さ 3 の親 h3 を作る
        string h1 = _store.CreateGroup();
        string h2 = _store.CreateGroup(h1);
        string h3 = _store.CreateGroup(h2);
        // h3(深さ3) に g2(サブツリー高さ2) を入れると最深 3+2=5 > 4 → 不可
        Assert.IsFalse(_store.SetGroupParent(g2, h3));
        // h2(深さ2) なら 2+2=4 → 可
        Assert.IsTrue(_store.SetGroupParent(g2, h2));
        Assert.AreEqual(h2, FindGroup(g2).parentGroupId);
    }

    [Test]
    public void SubtreeHeight_CountsDeepestBranch()
    {
        string g1 = _store.CreateGroup();
        string g2 = _store.CreateGroup(g1);
        string g3 = _store.CreateGroup(g2);
        string leaf = _store.CreateGroup();
        Assert.AreEqual(3, _store.SubtreeHeight(g1), "g1 > g2 > g3 の 3 段");
        Assert.AreEqual(1, _store.SubtreeHeight(g3), "末端グループは高さ 1");
        Assert.AreEqual(1, _store.SubtreeHeight(leaf), "子のないグループは高さ 1");
    }

    // ── 所属グループ設定 ──────────────────────────────────────────────────────

    [Test]
    public void SetObjectGroup_ValidatesGroupExistence()
    {
        var obj = _store.Add("a");
        string g = _store.CreateGroup();
        Assert.IsTrue(_store.SetObjectGroup(obj.instanceId, g));
        Assert.AreEqual(g, obj.groupId);
        Assert.IsTrue(_store.SetObjectGroup(obj.instanceId, ""), "ルートへ戻すのは可");
        Assert.IsFalse(_store.SetObjectGroup(obj.instanceId, "missing"));
    }

    // ── 複数選択（同一階層判定） ──────────────────────────────────────────────

    [Test]
    public void AreSameLevel_TrueForSameContainer_FalseAcrossLevels()
    {
        string g = _store.CreateGroup();
        var a = _store.Add("a");          // ルート
        var b = _store.Add("b");          // ルート
        var c = _store.Add("c", g);       // g 内

        Assert.IsTrue(_store.AreSameLevel(new[] { a.instanceId, b.instanceId }), "両方ルート直下");
        Assert.IsFalse(_store.AreSameLevel(new[] { a.instanceId, c.instanceId }), "階層が異なる");
        Assert.IsFalse(_store.AreSameLevel(new[] { a.instanceId, "missing" }), "不明 ID");
    }

    [Test]
    public void ContainerOf_ResolvesObjectsAndGroups()
    {
        string g1 = _store.CreateGroup();
        string g2 = _store.CreateGroup(g1);
        var obj = _store.Add("a", g1);
        Assert.AreEqual(g1, _store.ContainerOf(obj.instanceId), "オブジェクトのコンテナ = groupId");
        Assert.AreEqual(g1, _store.ContainerOf(g2), "グループのコンテナ = parentGroupId");
        Assert.IsNull(_store.ContainerOf("missing"));
    }

    // ── コスト集計 ────────────────────────────────────────────────────────────

    [Test]
    public void CalculateCost_DelegatesToCalculator()
    {
        _store.Add("desk");
        _store.Add("desk");
        _store.Add("chair");
        // desk(128px=64) + chair(64px=16) = 80（同種は 1 回）
        int cost = _store.CalculateCost(key => key == "desk" ? 128 : 64);
        Assert.AreEqual(80, cost);
    }

    private GroupJson FindGroup(string groupId)
    {
        foreach (var g in _store.Groups)
            if (g.groupId == groupId)
                return g;
        return null;
    }
}
