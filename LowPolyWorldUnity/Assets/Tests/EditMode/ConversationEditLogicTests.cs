using System;
using NUnit.Framework;

public class ConversationEditLogicTests
{
    private static ConversationEditLogic NewEditor() => new(new ConversationJson { conversationId = "c1" });

    [Test]
    public void AddLine_AppendsAndSyncs()
    {
        var edit = NewEditor();
        var a = edit.AddLine();
        var b = edit.AddLine();
        Assert.AreEqual(2, edit.Lines.Count);
        Assert.AreNotEqual(a.lineId, b.lineId);
        // Conversation は常に最新（Sync）
        Assert.AreEqual(2, edit.Conversation.lines.Length);
    }

    [Test]
    public void AddLine_FailsAtLimit()
    {
        var edit = NewEditor();
        for (int i = 0; i < ConversationEditLogic.MaxLines; i++)
            Assert.IsNotNull(edit.AddLine());
        Assert.IsFalse(edit.CanAddLine);
        Assert.IsNull(edit.AddLine());
    }

    [Test]
    public void SetLineText_UpsertsClampsAndRejectsEmpty()
    {
        var edit = NewEditor();
        var line = edit.AddLine();
        Assert.IsTrue(edit.SetLineText(line.lineId, "", "こんにちは"));
        Assert.IsTrue(edit.SetLineText(line.lineId, "", "やあ")); // 上書き
        Assert.AreEqual(1, line.texts.Length);
        Assert.AreEqual("やあ", line.texts[0].text);

        Assert.IsTrue(edit.SetLineText(line.lineId, "en", "Hello")); // 別言語追加
        Assert.AreEqual(2, line.texts.Length);

        Assert.IsFalse(edit.SetLineText(line.lineId, "", ""), "空テキストは拒否");

        string over = new string('あ', ConversationEditLogic.TextMaxLength + 10);
        Assert.IsTrue(edit.SetLineText(line.lineId, "", over));
        Assert.AreEqual(ConversationEditLogic.TextMaxLength, line.texts[0].text.Length);
    }

    [Test]
    public void SetLineSpeaker_Clamps40()
    {
        var edit = NewEditor();
        var line = edit.AddLine();
        string over = new string('x', 50);
        Assert.IsTrue(edit.SetLineSpeaker(line.lineId, "", over));
        Assert.AreEqual(ConversationEditLogic.SpeakerMaxLength, line.speakers[0].text.Length);
    }

    [Test]
    public void SetLineGoto_ValidatesTarget()
    {
        var edit = NewEditor();
        var a = edit.AddLine();
        var b = edit.AddLine();

        Assert.IsTrue(edit.SetLineGoto(a.lineId, ConversationEditLogic.GotoEnd));
        Assert.AreEqual("end", a.gotoLineId);
        Assert.IsTrue(edit.SetLineGoto(a.lineId, b.lineId)); // 実在行
        Assert.AreEqual(b.lineId, a.gotoLineId);
        Assert.IsTrue(edit.SetLineGoto(a.lineId, "")); // 次へ
        Assert.IsFalse(edit.SetLineGoto(a.lineId, "missing"), "実在しない行は拒否");
    }

    [Test]
    public void RemoveLine_ClearsDanglingGoto()
    {
        var edit = NewEditor();
        var a = edit.AddLine();
        var b = edit.AddLine();
        edit.SetLineGoto(a.lineId, b.lineId);

        Assert.IsTrue(edit.RemoveLine(b.lineId));
        Assert.AreEqual("", a.gotoLineId, "削除した行を指すジャンプ先は「次へ」に戻る");
    }

    [Test]
    public void MoveLine_ReordersAndClamps()
    {
        var edit = NewEditor();
        var a = edit.AddLine();
        edit.AddLine();
        var c = edit.AddLine();
        Assert.IsTrue(edit.MoveLine(c.lineId, 0));
        Assert.AreEqual(c.lineId, edit.Lines[0].lineId);
        Assert.IsTrue(edit.MoveLine(c.lineId, 99));
        Assert.AreEqual(c.lineId, edit.Lines[2].lineId);
        Assert.AreEqual(a.lineId, edit.Lines[0].lineId);
    }

    [Test]
    public void AddChoice_MaxFour()
    {
        var edit = NewEditor();
        var line = edit.AddLine();
        for (int i = 0; i < ConversationEditLogic.MaxChoices; i++)
            Assert.IsNotNull(edit.AddChoice(line.lineId));
        Assert.IsNull(edit.AddChoice(line.lineId), "5 個目は不可");
        Assert.AreEqual(4, line.choices.Length);
    }

    [Test]
    public void Choice_TextGotoAndRemove()
    {
        var edit = NewEditor();
        var a = edit.AddLine();
        var b = edit.AddLine();
        edit.AddChoice(a.lineId);

        Assert.IsTrue(edit.SetChoiceText(a.lineId, 0, "", "はい"));
        Assert.AreEqual("はい", a.choices[0].texts[0].text);
        Assert.IsTrue(edit.SetChoiceGoto(a.lineId, 0, b.lineId));
        Assert.AreEqual(b.lineId, a.choices[0].gotoLineId);
        Assert.IsFalse(edit.SetChoiceGoto(a.lineId, 0, "missing"));

        Assert.IsTrue(edit.RemoveChoice(a.lineId, 0));
        Assert.AreEqual(0, a.choices.Length);
        Assert.IsFalse(edit.RemoveChoice(a.lineId, 0));
    }

    [Test]
    public void SetLineOnReach_NormalizesAndClampsValue()
    {
        var edit = NewEditor();
        var line = edit.AddLine();
        Assert.IsTrue(edit.SetLineOnReach(line.lineId,
            new ConversationEffectJson { kind = "worldState", stateIndex = 2, stateOp = "set", value = 999 }));
        Assert.AreEqual("worldState", line.onReach.kind);
        Assert.AreEqual(255, line.onReach.value);

        Assert.IsTrue(edit.SetLineOnReach(line.lineId, null));
        Assert.AreEqual("none", line.onReach.kind);
    }

    [Test]
    public void Constructor_NullThrows()
    {
        Assert.Throws<ArgumentNullException>(() => new ConversationEditLogic(null));
    }
}
