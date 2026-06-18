using NUnit.Framework;

public class GimmickRuleSummaryTests
{
    private static GimmickRule Rule(GimmickTrigger[] t, GimmickCondition[] c, GimmickAction[] a) =>
        new GimmickRule { triggers = t ?? System.Array.Empty<GimmickTrigger>(),
            conditions = c ?? System.Array.Empty<GimmickCondition>(),
            actions = a ?? System.Array.Empty<GimmickAction>() };

    [Test]
    public void Of_TriggerAndAction()
    {
        var s = GimmickRuleSummary.Of(Rule(
            new[] { new GimmickTrigger { type = "playerTouchObject" } },
            null,
            new[] { new GimmickAction { type = "setWorldState" } }));
        Assert.AreEqual("オブジェクトに接触したとき → ワールド変数を変更", s);
    }

    [Test]
    public void Of_MultipleShowsHoka()
    {
        var s = GimmickRuleSummary.Of(Rule(
            new[] { new GimmickTrigger { type = "roomStart" }, new GimmickTrigger { type = "actionButton" } },
            null,
            new[] { new GimmickAction { type = "timerStart" }, new GimmickAction { type = "showMessage" } }));
        Assert.AreEqual("ルーム開始時 ほか → タイマーを開始 ほか", s);
    }

    [Test]
    public void Of_WithCondition_ShowsMarker()
    {
        var s = GimmickRuleSummary.Of(Rule(
            new[] { new GimmickTrigger { type = "roomStart" } },
            new[] { new GimmickCondition { type = "worldState" } },
            new[] { new GimmickAction { type = "setWorldState" } }));
        StringAssert.EndsWith("（条件あり）", s);
    }

    [Test]
    public void Of_EmptyParts()
    {
        Assert.AreEqual("（きっかけなし） → （アクションなし）", GimmickRuleSummary.Of(Rule(null, null, null)));
        Assert.AreEqual("", GimmickRuleSummary.Of(null));
    }
}
