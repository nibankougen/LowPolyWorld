using NUnit.Framework;

public class LayerStackTests
{
    private LayerStack _stack;

    [SetUp]
    public void SetUp()
    {
        _stack = new LayerStack();
    }

    // ---- 追加 ----

    [Test]
    public void AddNormalLayer_FirstLayer_ReturnsLayerWithId1()
    {
        var layer = _stack.AddNormalLayer();

        Assert.IsNotNull(layer);
        Assert.AreEqual(1u, layer.Id);
        Assert.AreEqual(PaintLayerType.Normal, layer.Type);
        Assert.AreEqual(1, _stack.NormalLayerCount);
    }

    [Test]
    public void AddNormalLayer_CustomName_SetsName()
    {
        var layer = _stack.AddNormalLayer("Background");

        Assert.AreEqual("Background", layer.Name);
    }

    [Test]
    public void AddNormalLayer_AutoName_UsesLayerIndex()
    {
        var layer = _stack.AddNormalLayer();

        Assert.AreEqual("Layer 1", layer.Name);
    }

    [Test]
    public void AddNormalLayer_AtMaxLimit_ReturnsNull()
    {
        for (int i = 0; i < LayerStack.MaxNormalLayers; i++)
            _stack.AddNormalLayer();

        var overflow = _stack.AddNormalLayer();

        Assert.IsNull(overflow);
        Assert.AreEqual(LayerStack.MaxNormalLayers, _stack.NormalLayerCount);
    }

    [Test]
    public void AddNormalLayer_MaxMinusOneAndAdd_ReturnsLayer()
    {
        for (int i = 0; i < LayerStack.MaxNormalLayers - 1; i++)
            _stack.AddNormalLayer();

        var last = _stack.AddNormalLayer();

        Assert.IsNotNull(last);
        Assert.AreEqual(LayerStack.MaxNormalLayers, _stack.NormalLayerCount);
    }

    // ---- 色調補正レイヤー ----

    [Test]
    public void AddColorAdjustmentLayer_First_ReturnsLayer()
    {
        var layer = _stack.AddColorAdjustmentLayer();

        Assert.IsNotNull(layer);
        Assert.AreEqual(PaintLayerType.ColorAdjustment, layer.Type);
        Assert.IsTrue(_stack.HasColorAdjustment);
    }

    [Test]
    public void AddColorAdjustmentLayer_Second_ReturnsNull()
    {
        _stack.AddColorAdjustmentLayer();

        var second = _stack.AddColorAdjustmentLayer();

        Assert.IsNull(second);
    }

    [Test]
    public void ColorAdjustmentLayer_DoesNotCountAsNormal()
    {
        for (int i = 0; i < LayerStack.MaxNormalLayers; i++)
            _stack.AddNormalLayer();
        _stack.AddColorAdjustmentLayer();

        Assert.AreEqual(LayerStack.MaxNormalLayers, _stack.NormalLayerCount);
        Assert.IsTrue(_stack.HasColorAdjustment);
    }

    [Test]
    public void AddNormalLayer_AfterColorAdjustment_StillCountsOnlyNormal()
    {
        _stack.AddColorAdjustmentLayer();
        _stack.AddNormalLayer();
        _stack.AddNormalLayer();

        Assert.AreEqual(2, _stack.NormalLayerCount);
        Assert.IsTrue(_stack.HasColorAdjustment);
    }

    // ---- 削除 ----

    [Test]
    public void RemoveLayer_ExistingNormal_ReturnsTrue()
    {
        var layer = _stack.AddNormalLayer();

        bool result = _stack.RemoveLayer(layer.Id);

        Assert.IsTrue(result);
        Assert.AreEqual(0, _stack.NormalLayerCount);
    }

    [Test]
    public void RemoveLayer_ColorAdjustment_RemovesAndReturnsFalseForHasColorAdjustment()
    {
        var adj = _stack.AddColorAdjustmentLayer();

        bool result = _stack.RemoveLayer(adj.Id);

        Assert.IsTrue(result);
        Assert.IsFalse(_stack.HasColorAdjustment);
    }

    [Test]
    public void RemoveLayer_NonExistentId_ReturnsFalse()
    {
        bool result = _stack.RemoveLayer(999u);

        Assert.IsFalse(result);
    }

    [Test]
    public void RemoveLayer_AfterRemoval_CanAddNewLayer()
    {
        for (int i = 0; i < LayerStack.MaxNormalLayers; i++)
            _stack.AddNormalLayer();
        var toRemove = _stack.Layers[0];
        _stack.RemoveLayer(toRemove.Id);

        var newLayer = _stack.AddNormalLayer();

        Assert.IsNotNull(newLayer);
        Assert.AreEqual(LayerStack.MaxNormalLayers, _stack.NormalLayerCount);
    }

    // ---- 複製 ----

    [Test]
    public void DuplicateLayer_Normal_ReturnsNewLayerWithCopySuffix()
    {
        var src = _stack.AddNormalLayer("Base");

        var dup = _stack.DuplicateLayer(src.Id);

        Assert.IsNotNull(dup);
        Assert.AreEqual("Base Copy", dup.Name);
        Assert.AreNotEqual(src.Id, dup.Id);
        Assert.AreEqual(2, _stack.NormalLayerCount);
    }

    [Test]
    public void DuplicateLayer_Normal_InsertsDirectlyAboveSource()
    {
        var bottom = _stack.AddNormalLayer("Bottom");
        var top = _stack.AddNormalLayer("Top");

        var dup = _stack.DuplicateLayer(bottom.Id);

        // bottom=0, dup=1, top=2
        Assert.AreEqual(bottom, _stack.Layers[0]);
        Assert.AreEqual(dup, _stack.Layers[1]);
        Assert.AreEqual(top, _stack.Layers[2]);
    }

    [Test]
    public void DuplicateLayer_ColorAdjustment_ReturnsNull()
    {
        var adj = _stack.AddColorAdjustmentLayer();

        var result = _stack.DuplicateLayer(adj.Id);

        Assert.IsNull(result);
    }

    [Test]
    public void DuplicateLayer_AtMaxLimit_ReturnsNull()
    {
        PaintLayer last = null;
        for (int i = 0; i < LayerStack.MaxNormalLayers; i++)
            last = _stack.AddNormalLayer();

        var result = _stack.DuplicateLayer(last.Id);

        Assert.IsNull(result);
    }

    [Test]
    public void DuplicateLayer_CopiesProperties()
    {
        var src = _stack.AddNormalLayer("Test");
        src.Visible = false;
        src.Locked = true;
        src.Opacity = 0.5f;
        src.MaskBelow = true;

        var dup = _stack.DuplicateLayer(src.Id);

        Assert.AreEqual(false, dup.Visible);
        Assert.AreEqual(true, dup.Locked);
        Assert.AreEqual(0.5f, dup.Opacity, 0.001f);
        Assert.AreEqual(true, dup.MaskBelow);
    }

    // ---- 並び替え ----

    [Test]
    public void MoveLayer_MoveToBottom_Succeeds()
    {
        var a = _stack.AddNormalLayer("A");
        var b = _stack.AddNormalLayer("B");
        var c = _stack.AddNormalLayer("C");

        bool result = _stack.MoveLayer(c.Id, 0);

        Assert.IsTrue(result);
        Assert.AreEqual(c, _stack.Layers[0]);
        Assert.AreEqual(a, _stack.Layers[1]);
        Assert.AreEqual(b, _stack.Layers[2]);
    }

    [Test]
    public void MoveLayer_SameIndex_ReturnsTrue()
    {
        var a = _stack.AddNormalLayer("A");

        bool result = _stack.MoveLayer(a.Id, 0);

        Assert.IsTrue(result);
    }

    [Test]
    public void MoveLayer_OutOfRange_ReturnsFalse()
    {
        var a = _stack.AddNormalLayer("A");

        bool result = _stack.MoveLayer(a.Id, 99);

        Assert.IsFalse(result);
    }

    [Test]
    public void MoveLayer_NegativeIndex_ReturnsFalse()
    {
        var a = _stack.AddNormalLayer("A");

        bool result = _stack.MoveLayer(a.Id, -1);

        Assert.IsFalse(result);
    }

    // ---- 結合 ----

    [Test]
    public void MergeLayerDown_TwoLayers_RemovesTopAndReturnsBottom()
    {
        var bottom = _stack.AddNormalLayer("Bottom");
        var top = _stack.AddNormalLayer("Top");

        var result = _stack.MergeLayerDown(top.Id);

        Assert.AreEqual(bottom, result);
        Assert.AreEqual(1, _stack.NormalLayerCount);
        Assert.IsNull(_stack.FindLayer(top.Id));
    }

    [Test]
    public void MergeLayerDown_BottomLayer_ReturnsNull()
    {
        var bottom = _stack.AddNormalLayer("Bottom");
        _stack.AddNormalLayer("Top");

        var result = _stack.MergeLayerDown(bottom.Id);

        Assert.IsNull(result);
    }

    [Test]
    public void MergeLayerDown_SingleLayer_ReturnsNull()
    {
        var only = _stack.AddNormalLayer("Only");

        var result = _stack.MergeLayerDown(only.Id);

        Assert.IsNull(result);
    }

    // ---- 存在しない ID への操作 ----

    [Test]
    public void MoveLayer_NonExistentId_ReturnsFalse()
    {
        _stack.AddNormalLayer("A");

        bool result = _stack.MoveLayer(999u, 0);

        Assert.IsFalse(result);
    }

    [Test]
    public void DuplicateLayer_NonExistentId_ReturnsNull()
    {
        _stack.AddNormalLayer("A");

        var result = _stack.DuplicateLayer(999u);

        Assert.IsNull(result);
    }

    [Test]
    public void MergeLayerDown_NonExistentId_ReturnsNull()
    {
        _stack.AddNormalLayer("A");
        _stack.AddNormalLayer("B");

        var result = _stack.MergeLayerDown(999u);

        Assert.IsNull(result);
    }

    [Test]
    public void MoveLayer_ColorAdjustmentId_ReturnsFalse()
    {
        _stack.AddNormalLayer("A");
        var adj = _stack.AddColorAdjustmentLayer();

        // 色調補正レイヤーは _layers に含まれないので MoveLayer は false を返す
        bool result = _stack.MoveLayer(adj.Id, 0);

        Assert.IsFalse(result);
    }

    // ---- FindLayer ----

    [Test]
    public void FindLayer_NormalLayer_ReturnsLayer()
    {
        var layer = _stack.AddNormalLayer();

        var found = _stack.FindLayer(layer.Id);

        Assert.AreEqual(layer, found);
    }

    [Test]
    public void FindLayer_ColorAdjustment_ReturnsLayer()
    {
        var adj = _stack.AddColorAdjustmentLayer();

        var found = _stack.FindLayer(adj.Id);

        Assert.AreEqual(adj, found);
    }

    [Test]
    public void FindLayer_MissingId_ReturnsNull()
    {
        var found = _stack.FindLayer(999u);

        Assert.IsNull(found);
    }
}
