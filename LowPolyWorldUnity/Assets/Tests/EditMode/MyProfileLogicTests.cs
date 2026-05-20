using NUnit.Framework;

public class MyProfileLogicTests
{
    // ── FormatSocialCount ─────────────────────────────────────────────────────

    [Test]
    public void FormatSocialCount_Zero_ReturnsZero()
    {
        Assert.AreEqual("0", MyProfileLogic.FormatSocialCount(0));
    }

    [Test]
    public void FormatSocialCount_Negative_TreatedAsZero()
    {
        Assert.AreEqual("0", MyProfileLogic.FormatSocialCount(-1));
    }

    [Test]
    public void FormatSocialCount_SmallNumber_FormattedWithComma()
    {
        Assert.AreEqual("1,234", MyProfileLogic.FormatSocialCount(1234));
    }

    [Test]
    public void FormatSocialCount_9999_NotCapped()
    {
        Assert.AreEqual("9,999", MyProfileLogic.FormatSocialCount(9999));
    }

    [Test]
    public void FormatSocialCount_10000_ReturnsCappedString()
    {
        Assert.AreEqual("9,999+", MyProfileLogic.FormatSocialCount(10000));
    }

    [Test]
    public void FormatSocialCount_LargeNumber_ReturnsCappedString()
    {
        Assert.AreEqual("9,999+", MyProfileLogic.FormatSocialCount(1_000_000));
    }

    // ── ValidateDisplayName ───────────────────────────────────────────────────

    [Test]
    public void ValidateDisplayName_Null_ReturnsEmpty()
    {
        Assert.AreEqual(
            MyProfileLogic.DisplayNameValidationResult.Empty,
            MyProfileLogic.ValidateDisplayName(null)
        );
    }

    [Test]
    public void ValidateDisplayName_Empty_ReturnsEmpty()
    {
        Assert.AreEqual(
            MyProfileLogic.DisplayNameValidationResult.Empty,
            MyProfileLogic.ValidateDisplayName("")
        );
    }

    [Test]
    public void ValidateDisplayName_Whitespace_ReturnsEmpty()
    {
        Assert.AreEqual(
            MyProfileLogic.DisplayNameValidationResult.Empty,
            MyProfileLogic.ValidateDisplayName("   ")
        );
    }

    [Test]
    public void ValidateDisplayName_OneChar_ReturnsOk()
    {
        Assert.AreEqual(
            MyProfileLogic.DisplayNameValidationResult.Ok,
            MyProfileLogic.ValidateDisplayName("A")
        );
    }

    [Test]
    public void ValidateDisplayName_ExactMaxLength_ReturnsOk()
    {
        var name = new string('a', MyProfileLogic.MaxDisplayNameLength);
        Assert.AreEqual(
            MyProfileLogic.DisplayNameValidationResult.Ok,
            MyProfileLogic.ValidateDisplayName(name)
        );
    }

    [Test]
    public void ValidateDisplayName_ExceedsMaxLength_ReturnsTooLong()
    {
        var name = new string('a', MyProfileLogic.MaxDisplayNameLength + 1);
        Assert.AreEqual(
            MyProfileLogic.DisplayNameValidationResult.TooLong,
            MyProfileLogic.ValidateDisplayName(name)
        );
    }

    // ── ValidationMessage ─────────────────────────────────────────────────────

    [Test]
    public void ValidationMessage_Ok_ReturnsEmpty()
    {
        Assert.AreEqual(
            string.Empty,
            MyProfileLogic.ValidationMessage(MyProfileLogic.DisplayNameValidationResult.Ok)
        );
    }

    [Test]
    public void ValidationMessage_Empty_NotEmpty()
    {
        var msg = MyProfileLogic.ValidationMessage(MyProfileLogic.DisplayNameValidationResult.Empty);
        Assert.IsFalse(string.IsNullOrEmpty(msg));
    }

    [Test]
    public void ValidationMessage_TooLong_ContainsMaxLength()
    {
        var msg = MyProfileLogic.ValidationMessage(MyProfileLogic.DisplayNameValidationResult.TooLong);
        StringAssert.Contains(MyProfileLogic.MaxDisplayNameLength.ToString(), msg);
    }
}
