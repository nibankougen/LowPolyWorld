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
    public void SetLineSpeakerId_SetsReference()
    {
        var edit = NewEditor();
        var line = edit.AddLine();
        Assert.IsTrue(edit.SetLineSpeakerId(line.lineId, "spk_1"));
        Assert.AreEqual("spk_1", line.speakerId);
        Assert.IsTrue(edit.SetLineSpeakerId(line.lineId, "")); // 話者なしへ
        Assert.AreEqual("", line.speakerId);
        Assert.IsFalse(edit.SetLineSpeakerId("missing", "spk_1"));
    }

    [Test]
    public void AddLine_InheritsPreviousSpeaker()
    {
        var edit = NewEditor();
        var a = edit.AddLine();
        Assert.AreEqual("", a.speakerId, "最初の行は話者なし");
        edit.SetLineSpeakerId(a.lineId, "spk_1");

        var b = edit.AddLine();
        Assert.AreEqual("spk_1", b.speakerId, "新規行は直前の行の話者を引き継ぐ");

        edit.SetLineSpeakerId(b.lineId, "spk_2");
        var c = edit.AddLine();
        Assert.AreEqual("spk_2", c.speakerId);
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
    public void RemoveChoiceText_RemovesPerLanguage()
    {
        var edit = NewEditor();
        var a = edit.AddLine();
        edit.AddChoice(a.lineId);

        Assert.IsTrue(edit.SetChoiceText(a.lineId, 0, "ja", "はい"));
        Assert.IsTrue(edit.SetChoiceText(a.lineId, 0, "en", "Yes"));
        Assert.AreEqual(2, a.choices[0].texts.Length);

        Assert.IsTrue(edit.RemoveChoiceText(a.lineId, 0, "ja"));
        Assert.AreEqual(1, a.choices[0].texts.Length);
        Assert.AreEqual("en", a.choices[0].texts[0].lang);

        Assert.IsFalse(edit.RemoveChoiceText(a.lineId, 0, "ja"), "存在しない言語は false");
        Assert.IsFalse(edit.RemoveChoiceText("missing", 0, "en"), "存在しない行は false");
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
