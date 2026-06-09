using NUnit.Framework;

public class WorldTagLogicTests
{
    [Test]
    public void TryAdd_Success_AddsTag()
    {
        var logic = new WorldTagLogic();
        var result = logic.TryAdd("ファンタジー");
        Assert.AreEqual(TagAddResult.Success, result);
        Assert.AreEqual(1, logic.Count);
        Assert.Contains("ファンタジー", logic.GetTags() as System.Collections.ICollection);
    }

    [Test]
    public void TryAdd_EmptyOrWhitespace_ReturnsEmpty()
    {
        var logic = new WorldTagLogic();
        Assert.AreEqual(TagAddResult.Empty, logic.TryAdd(""));
        Assert.AreEqual(TagAddResult.Empty, logic.TryAdd("   "));
        Assert.AreEqual(0, logic.Count);
    }

    [Test]
    public void TryAdd_TooLong_ReturnsTooLong()
    {
        var logic = new WorldTagLogic();
        string longTag = new string('a', WorldTagLogic.MaxTagLength + 1);
        Assert.AreEqual(TagAddResult.TooLong, logic.TryAdd(longTag));
        Assert.AreEqual(0, logic.Count);
    }

    [Test]
    public void TryAdd_WhitespaceOnlyLongerThanMaxLength_ReturnsEmpty_NotTooLong()
    {
        // IsNullOrWhitespace チェックが文字数チェックより先に走ることを保証する
        var logic = new WorldTagLogic();
        string longWhitespace = new string(' ', WorldTagLogic.MaxTagLength + 1);
        Assert.AreEqual(TagAddResult.Empty, logic.TryAdd(longWhitespace),
            "空白のみの長い文字列は TooLong ではなく Empty を返す");
    }

    [Test]
    public void TryAdd_ExactlyMaxLength_Succeeds()
    {
        var logic = new WorldTagLogic();
        string tag = new string('a', WorldTagLogic.MaxTagLength);
        Assert.AreEqual(TagAddResult.Success, logic.TryAdd(tag));
    }

    [Test]
    public void TryAdd_LimitReached_ReturnsLimitReached()
    {
        var logic = new WorldTagLogic();
        for (int i = 0; i < WorldTagLogic.MaxTags; i++)
            logic.TryAdd($"tag{i}");
        Assert.IsTrue(logic.IsFull);
        Assert.AreEqual(TagAddResult.LimitReached, logic.TryAdd("overLimit"));
    }

    [Test]
    public void TryAdd_Duplicate_ReturnsAlreadyExists()
    {
        var logic = new WorldTagLogic();
        logic.TryAdd("重複");
        Assert.AreEqual(TagAddResult.AlreadyExists, logic.TryAdd("重複"));
        Assert.AreEqual(1, logic.Count);
    }

    [Test]
    public void Remove_ExistingTag_RemovesIt()
    {
        var logic = new WorldTagLogic();
        logic.TryAdd("削除対象");
        logic.Remove("削除対象");
        Assert.AreEqual(0, logic.Count);
    }

    [Test]
    public void Remove_NonExistentTag_DoesNothing()
    {
        var logic = new WorldTagLogic();
        logic.TryAdd("存在する");
        Assert.DoesNotThrow(() => logic.Remove("存在しない"));
        Assert.AreEqual(1, logic.Count);
    }

    [Test]
    public void Clear_RemovesAllTags()
    {
        var logic = new WorldTagLogic();
        for (int i = 0; i < WorldTagLogic.MaxTags; i++)
            logic.TryAdd($"tag{i}");
        logic.Clear();
        Assert.AreEqual(0, logic.Count);
        Assert.IsFalse(logic.IsFull);
    }

    [Test]
    public void IsFull_FalseBeforeLimit_TrueAtLimit()
    {
        var logic = new WorldTagLogic();
        for (int i = 0; i < WorldTagLogic.MaxTags - 1; i++)
        {
            Assert.IsFalse(logic.IsFull);
            logic.TryAdd($"tag{i}");
        }
        logic.TryAdd("last");
        Assert.IsTrue(logic.IsFull);
    }

    [Test]
    public void InitialTags_LoadedCorrectly()
    {
        var logic = new WorldTagLogic(new[] { "A", "B", "C" });
        Assert.AreEqual(3, logic.Count);
    }
}
