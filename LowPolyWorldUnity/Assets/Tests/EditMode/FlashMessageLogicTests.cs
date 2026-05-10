using NUnit.Framework;

public class FlashMessageLogicTests
{
    [Test]
    public void InitialState_IsNotVisible()
    {
        var logic = new FlashMessageLogic(2f);
        Assert.IsFalse(logic.IsVisible);
        Assert.IsNull(logic.Current);
    }

    [Test]
    public void Show_ThenTick_BecomesVisible()
    {
        var logic = new FlashMessageLogic(2f);
        logic.Show("hello");
        Assert.IsFalse(logic.IsVisible); // キュー内、まだ非表示

        bool newMsg = logic.Tick(0.1f);
        Assert.IsTrue(newMsg);
        Assert.IsTrue(logic.IsVisible);
        Assert.AreEqual("hello", logic.Current.Text);
    }

    [Test]
    public void Tick_AfterDuration_HidesMessage()
    {
        var logic = new FlashMessageLogic(2f);
        logic.Show("test");
        logic.Tick(0.1f);
        Assert.IsTrue(logic.IsVisible);

        logic.Tick(2.0f); // 合計 2.1s > 2.0s
        Assert.IsFalse(logic.IsVisible);
    }

    [Test]
    public void Show_MultipleMessages_ShowsInFIFOOrder()
    {
        var logic = new FlashMessageLogic(1f);
        logic.Show("first");
        logic.Show("second");

        logic.Tick(0.1f);
        Assert.AreEqual("first", logic.Current.Text);

        logic.Tick(1.0f); // first 期限切れ
        logic.Tick(0.01f); // second 開始
        Assert.AreEqual("second", logic.Current.Text);
    }

    [Test]
    public void DefaultType_IsInfo()
    {
        var logic = new FlashMessageLogic(1f);
        logic.Show("test");
        logic.Tick(0.1f);
        Assert.AreEqual(FlashMessageType.Info, logic.Current.Type);
    }

    [Test]
    public void Show_WithExplicitType_CorrectType()
    {
        var logic = new FlashMessageLogic(1f);
        logic.Show("ok", FlashMessageType.Success);
        logic.Tick(0.1f);
        Assert.AreEqual(FlashMessageType.Success, logic.Current.Type);
    }

    [Test]
    public void Tick_NoMessages_ReturnsFalse()
    {
        var logic = new FlashMessageLogic(2f);
        bool result = logic.Tick(0.1f);
        Assert.IsFalse(result);
    }

    [Test]
    public void Tick_SecondCallWithMessageActive_ReturnsFalse()
    {
        var logic = new FlashMessageLogic(2f);
        logic.Show("msg");
        logic.Tick(0.1f); // newMsg = true
        bool result = logic.Tick(0.1f); // same message, no new start
        Assert.IsFalse(result);
    }

    [Test]
    public void Clear_RemovesCurrentAndQueue()
    {
        var logic = new FlashMessageLogic(5f);
        logic.Show("a");
        logic.Show("b");
        logic.Tick(0.1f);
        Assert.IsTrue(logic.IsVisible);

        logic.Clear();
        Assert.IsFalse(logic.IsVisible);

        logic.Tick(0.1f); // queue も空のはず
        Assert.IsFalse(logic.IsVisible);
    }
}
