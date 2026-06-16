using NUnit.Framework;

public class ConversationValidatorTests
{
    // 妥当な会話を組み立てるヘルパー（編集ロジック経由）。
    private static ConversationJson ValidConversation()
    {
        var conv = new ConversationJson { conversationId = "c1", name = "テスト会話" };
        var edit = new ConversationEditLogic(conv);
        var l1 = edit.AddLine();
        edit.SetLineText(l1.lineId, "", "最初のセリフ");
        var l2 = edit.AddLine();
        edit.SetLineText(l2.lineId, "", "選んでください");
        edit.AddChoice(l2.lineId);
        edit.SetChoiceText(l2.lineId, 0, "", "はい");
        edit.SetChoiceGoto(l2.lineId, 0, ConversationEditLogic.GotoEnd);
        return conv;
    }

    [Test]
    public void ValidConversation_NoErrors()
    {
        var errors = ConversationValidator.Validate(ValidConversation());
        CollectionAssert.IsEmpty(errors);
    }

    [Test]
    public void LineWithoutText_Fails()
    {
        var conv = new ConversationJson { conversationId = "c1" };
        new ConversationEditLogic(conv).AddLine(); // テキスト未設定
        var errors = ConversationValidator.Validate(conv);
        Assert.IsNotEmpty(errors);
    }

    [Test]
    public void GotoToMissingLine_Fails()
    {
        var conv = ValidConversation();
        conv.lines[0].gotoLineId = "does-not-exist"; // バリデータを直接突く
        var errors = ConversationValidator.Validate(conv);
        Assert.IsTrue(errors.Exists(e => e.Contains("行 ID")));
    }

    [Test]
    public void TextOverLimit_Fails()
    {
        var conv = ValidConversation();
        conv.lines[0].texts = new[] { new GimmickTextJson { lang = "", text = new string('あ', 90) } };
        var errors = ConversationValidator.Validate(conv);
        Assert.IsTrue(errors.Exists(e => e.Contains("80 文字")));
    }

    [Test]
    public void TooManyChoices_Fails()
    {
        var conv = ValidConversation();
        var line = conv.lines[1];
        line.choices = new[]
        {
            Choice("a"), Choice("b"), Choice("c"), Choice("d"), Choice("e"),
        };
        var errors = ConversationValidator.Validate(conv);
        Assert.IsTrue(errors.Exists(e => e.Contains("選択肢が上限")));
    }

    [Test]
    public void WorldStateEffectOutOfRange_Fails()
    {
        var conv = ValidConversation();
        conv.lines[0].onReach = new ConversationEffectJson { kind = "worldState", stateIndex = 99, stateOp = "set" };
        var errors = ConversationValidator.Validate(conv);
        Assert.IsTrue(errors.Exists(e => e.Contains("ワールドステート番号")));
    }

    [Test]
    public void PlayerStateEffectInvalidTarget_Fails()
    {
        var conv = ValidConversation();
        conv.lines[0].onReach = new ConversationEffectJson
        {
            kind = "playerState", stateIndex = 1, stateOp = "add", playerTarget = "nobody",
        };
        var errors = ConversationValidator.Validate(conv);
        Assert.IsTrue(errors.Exists(e => e.Contains("対象プレイヤー")));
    }

    [Test]
    public void ValidateAll_CountAndDuplicateId()
    {
        var list = new System.Collections.Generic.List<ConversationJson>();
        for (int i = 0; i < ConversationLibraryLogic.MaxConversations + 1; i++)
            list.Add(ValidConversation()); // すべて同一 ID "c1"

        var errors = ConversationValidator.ValidateAll(list);
        Assert.IsTrue(errors.Exists(e => e.Contains("会話数が上限")));
        Assert.IsTrue(errors.Exists(e => e.Contains("会話 ID が重複")));
    }

    private static ConversationChoiceJson Choice(string text) =>
        new() { texts = new[] { new GimmickTextJson { lang = "", text = text } }, gotoLineId = "end" };
}
