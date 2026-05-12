using NUnit.Framework;

public class HideWorldListLogicTests
{
    [Test]
    public void Add_MakesWorldHidden()
    {
        var logic = new HideWorldListLogic();
        logic.Add("world1");
        Assert.IsTrue(logic.IsHidden("world1"));
    }

    [Test]
    public void Remove_MakesWorldNotHidden()
    {
        var logic = new HideWorldListLogic();
        logic.Add("world1");
        logic.Remove("world1");
        Assert.IsFalse(logic.IsHidden("world1"));
    }

    [Test]
    public void IsHidden_UnknownWorld_ReturnsFalse()
    {
        var logic = new HideWorldListLogic();
        Assert.IsFalse(logic.IsHidden("unknown"));
    }

    [Test]
    public void Add_Duplicate_IsIdempotent()
    {
        var logic = new HideWorldListLogic();
        logic.Add("world1");
        logic.Add("world1");
        Assert.AreEqual(1, logic.Count);
    }

    [Test]
    public void Remove_NonExistent_DoesNotThrow()
    {
        var logic = new HideWorldListLogic();
        Assert.DoesNotThrow(() => logic.Remove("nonexistent"));
    }

    [Test]
    public void GetAll_ReturnsAllHiddenWorlds()
    {
        var logic = new HideWorldListLogic();
        logic.Add("w1");
        logic.Add("w2");
        logic.Add("w3");

        var all = logic.GetAll();

        Assert.AreEqual(3, all.Count);
        CollectionAssert.Contains(all, "w1");
        CollectionAssert.Contains(all, "w2");
        CollectionAssert.Contains(all, "w3");
    }

    [Test]
    public void SetAll_ReplacesExistingList()
    {
        var logic = new HideWorldListLogic();
        logic.Add("w1");
        logic.Add("w2");

        logic.SetAll(new[] { "w3", "w4" });

        Assert.IsFalse(logic.IsHidden("w1"));
        Assert.IsFalse(logic.IsHidden("w2"));
        Assert.IsTrue(logic.IsHidden("w3"));
        Assert.IsTrue(logic.IsHidden("w4"));
    }

    [Test]
    public void SetAll_Empty_ClearsAll()
    {
        var logic = new HideWorldListLogic();
        logic.Add("w1");

        logic.SetAll(System.Array.Empty<string>());

        Assert.AreEqual(0, logic.Count);
    }
}
