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
}
