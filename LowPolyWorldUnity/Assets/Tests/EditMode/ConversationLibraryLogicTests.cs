using NUnit.Framework;

public class ConversationLibraryLogicTests
{
    [Test]
    public void Add_DefaultSequentialName()
    {
        var lib = new ConversationLibraryLogic();
        var a = lib.Add();
        var b = lib.Add();
        Assert.AreEqual("会話1", a.name);
        Assert.AreEqual("会話2", b.name);
        Assert.AreEqual(2, lib.Count);
        Assert.AreNotEqual(a.conversationId, b.conversationId);
    }

    [Test]
    public void Add_CustomNameSanitized()
    {
        var lib = new ConversationLibraryLogic();
        var c = lib.Add("  序章の会話  ");
        Assert.AreEqual("序章の会話", c.name);
    }

    [Test]
    public void Add_FailsAtLimit()
    {
        var lib = new ConversationLibraryLogic();
        for (int i = 0; i < ConversationLibraryLogic.MaxConversations; i++)
            Assert.IsNotNull(lib.Add());
        Assert.IsFalse(lib.CanAdd);
        Assert.IsNull(lib.Add());
        Assert.AreEqual(ConversationLibraryLogic.MaxConversations, lib.Count);
    }

    [Test]
    public void Rename_RejectsEmpty()
    {
        var lib = new ConversationLibraryLogic();
        var c = lib.Add();
        Assert.IsFalse(lib.Rename(c.conversationId, "   "));
        Assert.AreEqual("会話1", c.name);
        Assert.IsTrue(lib.Rename(c.conversationId, "新名称"));
        Assert.AreEqual("新名称", c.name);
    }

    [Test]
    public void Remove_ById()
    {
        var lib = new ConversationLibraryLogic();
        var a = lib.Add();
        var b = lib.Add();
        Assert.IsTrue(lib.Remove(a.conversationId));
        Assert.AreEqual(1, lib.Count);
        Assert.AreEqual(b.conversationId, lib.Conversations[0].conversationId);
        Assert.IsFalse(lib.Remove("missing"));
    }

    [Test]
    public void Move_ReordersAndClamps()
    {
        var lib = new ConversationLibraryLogic();
        var a = lib.Add();
        lib.Add();
        var c = lib.Add();
        Assert.IsTrue(lib.Move(c.conversationId, 0));
        Assert.AreEqual(c.conversationId, lib.Conversations[0].conversationId);
        Assert.IsTrue(lib.Move(c.conversationId, 99));
        Assert.AreEqual(c.conversationId, lib.Conversations[2].conversationId);
        Assert.AreEqual(a.conversationId, lib.Conversations[0].conversationId);
    }

    [Test]
    public void LoadFrom_WriteTo_RoundTrips()
    {
        var lib = new ConversationLibraryLogic();
        lib.Add("会話A");
        lib.Add("会話B");

        var def = new WorldDefinitionJson();
        lib.WriteTo(def);
        Assert.AreEqual(2, def.conversations.Length);

        var lib2 = new ConversationLibraryLogic();
        lib2.LoadFrom(def);
        Assert.AreEqual(2, lib2.Count);
        Assert.AreEqual("会話A", lib2.Conversations[0].name);
    }

    [Test]
    public void LoadFrom_Null_Clears()
    {
        var lib = new ConversationLibraryLogic();
        lib.Add();
        lib.LoadFrom(null);
        Assert.AreEqual(0, lib.Count);
    }

    [Test]
    public void TotalLineCount_AndCanAddLine()
    {
        var lib = new ConversationLibraryLogic();
        var a = lib.Add();
        var b = lib.Add();
        new ConversationEditLogic(a).AddLine();
        var editB = new ConversationEditLogic(b);
        editB.AddLine();
        editB.AddLine();

        Assert.AreEqual(3, lib.TotalLineCount, "全会話のセリフ行合計");
        Assert.IsTrue(lib.CanAddLine);
    }

    [Test]
    public void CanAddLine_FalseAtGlobalLimit()
    {
        var lib = new ConversationLibraryLogic();
        // 1 会話 50 行 × 10 会話 = 500 行（全体上限）
        for (int c = 0; c < ConversationLibraryLogic.MaxTotalLines / ConversationEditLogic.MaxLines; c++)
        {
            var edit = new ConversationEditLogic(lib.Add());
            for (int i = 0; i < ConversationEditLogic.MaxLines; i++)
                edit.AddLine();
        }
        Assert.AreEqual(ConversationLibraryLogic.MaxTotalLines, lib.TotalLineCount);
        Assert.IsFalse(lib.CanAddLine, "全体上限に達したら行追加不可");
    }
}
