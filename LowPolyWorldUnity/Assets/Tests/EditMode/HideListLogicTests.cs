using NUnit.Framework;

public class HideListLogicTests
{
    // ── Add / Remove / IsHidden ───────────────────────────────────────────────

    [Test]
    public void Add_MakesUserHidden()
    {
        var logic = new HideListLogic();
        logic.Add("user1");
        Assert.IsTrue(logic.IsHidden("user1"));
    }

    [Test]
    public void Remove_MakesUserNotHidden()
    {
        var logic = new HideListLogic();
        logic.Add("user1");
        logic.Remove("user1");
        Assert.IsFalse(logic.IsHidden("user1"));
    }

    [Test]
    public void IsHidden_UnknownUser_ReturnsFalse()
    {
        var logic = new HideListLogic();
        Assert.IsFalse(logic.IsHidden("unknown"));
    }

    [Test]
    public void Add_Duplicate_IsIdempotent()
    {
        var logic = new HideListLogic();
        logic.Add("user1");
        logic.Add("user1");
        Assert.AreEqual(1, logic.Count);
    }

    [Test]
    public void Remove_NonExistent_DoesNotThrow()
    {
        var logic = new HideListLogic();
        Assert.DoesNotThrow(() => logic.Remove("nonexistent"));
    }

    // ── GetAll ────────────────────────────────────────────────────────────────

    [Test]
    public void GetAll_ReturnsAllHiddenUsers()
    {
        var logic = new HideListLogic();
        logic.Add("user1");
        logic.Add("user2");
        logic.Add("user3");

        var all = logic.GetAll();

        Assert.AreEqual(3, all.Count);
        CollectionAssert.Contains(all, "user1");
        CollectionAssert.Contains(all, "user2");
        CollectionAssert.Contains(all, "user3");
    }

    [Test]
    public void GetAll_Empty_ReturnsEmpty()
    {
        var logic = new HideListLogic();
        Assert.AreEqual(0, logic.GetAll().Count);
    }

    // ── SetAll ────────────────────────────────────────────────────────────────

    [Test]
    public void SetAll_ReplacesExistingList()
    {
        var logic = new HideListLogic();
        logic.Add("user1");
        logic.Add("user2");

        logic.SetAll(new[] { "user3", "user4" });

        Assert.IsFalse(logic.IsHidden("user1"));
        Assert.IsFalse(logic.IsHidden("user2"));
        Assert.IsTrue(logic.IsHidden("user3"));
        Assert.IsTrue(logic.IsHidden("user4"));
    }

    [Test]
    public void SetAll_Empty_ClearsAll()
    {
        var logic = new HideListLogic();
        logic.Add("user1");

        logic.SetAll(System.Array.Empty<string>());

        Assert.AreEqual(0, logic.Count);
    }

    // ── ShouldSkipRendering ──────────────────────────────────────────────────

    [Test]
    public void ShouldSkipRendering_HiddenUser_ReturnsTrue()
    {
        var logic = new HideListLogic();
        logic.Add("user1");
        Assert.IsTrue(logic.ShouldSkipRendering("user1"));
    }

    [Test]
    public void ShouldSkipRendering_NotHiddenUser_ReturnsFalse()
    {
        var logic = new HideListLogic();
        Assert.IsFalse(logic.ShouldSkipRendering("user1"));
    }

    [Test]
    public void ShouldSkipRendering_AfterRemove_ReturnsFalse()
    {
        var logic = new HideListLogic();
        logic.Add("user1");
        logic.Remove("user1");
        Assert.IsFalse(logic.ShouldSkipRendering("user1"));
    }

    // ── ShouldMuteVoice ───────────────────────────────────────────────────────

    [Test]
    public void ShouldMuteVoice_HiddenUser_ReturnsTrue()
    {
        var logic = new HideListLogic();
        logic.Add("user1");
        Assert.IsTrue(logic.ShouldMuteVoice("user1"));
    }

    [Test]
    public void ShouldMuteVoice_NotHiddenUser_ReturnsFalse()
    {
        var logic = new HideListLogic();
        Assert.IsFalse(logic.ShouldMuteVoice("user1"));
    }

    [Test]
    public void ShouldMuteVoice_AfterRemove_ReturnsFalse()
    {
        var logic = new HideListLogic();
        logic.Add("user1");
        logic.Remove("user1");
        Assert.IsFalse(logic.ShouldMuteVoice("user1"));
    }
}
