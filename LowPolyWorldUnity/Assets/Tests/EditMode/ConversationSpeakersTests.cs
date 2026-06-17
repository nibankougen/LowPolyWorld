using NUnit.Framework;

public class ConversationSpeakersTests
{
    private static ConversationLineJson Line(string speakerId) =>
        new ConversationLineJson { lineId = "l", speakerId = speakerId };

    [Test]
    public void DistinctSpeakerIds_FirstAppearanceOrderNoDupes()
    {
        var conv = new ConversationJson
        {
            lines = new[] { Line("a"), Line("b"), Line("a"), Line("c"), Line("b") },
        };
        CollectionAssert.AreEqual(new[] { "a", "b", "c" }, ConversationSpeakers.DistinctSpeakerIds(conv));
    }

    [Test]
    public void DistinctSpeakerIds_SkipsEmptyAndNull()
    {
        var conv = new ConversationJson
        {
            lines = new[] { Line(""), Line("a"), null, Line("") },
        };
        CollectionAssert.AreEqual(new[] { "a" }, ConversationSpeakers.DistinctSpeakerIds(conv));
    }

    [Test]
    public void DistinctSpeakerIds_EmptyForNullOrNoLines()
    {
        Assert.IsEmpty(ConversationSpeakers.DistinctSpeakerIds(null));
        Assert.IsEmpty(ConversationSpeakers.DistinctSpeakerIds(new ConversationJson()));
    }
}
