using NUnit.Framework;

public class AccountSettingsLogicTests
{
    // ── ValidateName ──────────────────────────────────────────────────────────

    [Test]
    public void ValidateName_ExactMin_Ok()
    {
        Assert.AreEqual(
            AccountSettingsLogic.NameValidationResult.Ok,
            AccountSettingsLogic.ValidateName("abc")
        );
    }

    [Test]
    public void ValidateName_ExactMax_Ok()
    {
        Assert.AreEqual(
            AccountSettingsLogic.NameValidationResult.Ok,
            AccountSettingsLogic.ValidateName("123456789012345") // 15文字
        );
    }

    [Test]
    public void ValidateName_AllowsUnderscore_Ok()
    {
        Assert.AreEqual(
            AccountSettingsLogic.NameValidationResult.Ok,
            AccountSettingsLogic.ValidateName("foo_bar_123")
        );
    }

    [Test]
    public void ValidateName_UpperCase_Ok()
    {
        Assert.AreEqual(
            AccountSettingsLogic.NameValidationResult.Ok,
            AccountSettingsLogic.ValidateName("FooBar")
        );
    }

    [TestCase("")]
    [TestCase("ab")]
    [TestCase("a")]
    public void ValidateName_TooShort(string input)
    {
        Assert.AreEqual(
            AccountSettingsLogic.NameValidationResult.TooShort,
            AccountSettingsLogic.ValidateName(input)
        );
    }

    [Test]
    public void ValidateName_Null_TooShort()
    {
        Assert.AreEqual(
            AccountSettingsLogic.NameValidationResult.TooShort,
            AccountSettingsLogic.ValidateName(null)
        );
    }

    [Test]
    public void ValidateName_SixteenChars_TooLong()
    {
        Assert.AreEqual(
            AccountSettingsLogic.NameValidationResult.TooLong,
            AccountSettingsLogic.ValidateName("1234567890123456") // 16文字
        );
    }

    [TestCase("foo bar")]
    [TestCase("foo-bar")]
    [TestCase("foo.bar")]
    [TestCase("@foo")]
    [TestCase("fooあ")]
    public void ValidateName_InvalidChars(string input)
    {
        Assert.AreEqual(
            AccountSettingsLogic.NameValidationResult.InvalidChars,
            AccountSettingsLogic.ValidateName(input)
        );
    }

    // ── NormalizeName ─────────────────────────────────────────────────────────

    [Test]
    public void NormalizeName_ConvertsToLower()
    {
        Assert.AreEqual("foobar", AccountSettingsLogic.NormalizeName("FooBAR"));
    }

    [Test]
    public void NormalizeName_Null_ReturnsEmpty()
    {
        Assert.AreEqual(string.Empty, AccountSettingsLogic.NormalizeName(null));
    }

    [Test]
    public void NormalizeName_AlreadyLower_Unchanged()
    {
        Assert.AreEqual("foo_bar", AccountSettingsLogic.NormalizeName("foo_bar"));
    }

    // ── ValidationMessage ─────────────────────────────────────────────────────

    [Test]
    public void ValidationMessage_Ok_ReturnsEmpty()
    {
        Assert.AreEqual(
            string.Empty,
            AccountSettingsLogic.ValidationMessage(AccountSettingsLogic.NameValidationResult.Ok)
        );
    }

    [Test]
    public void ValidationMessage_TooShort_NotEmpty()
    {
        var msg = AccountSettingsLogic.ValidationMessage(AccountSettingsLogic.NameValidationResult.TooShort);
        Assert.IsFalse(string.IsNullOrEmpty(msg));
    }

    [Test]
    public void ValidationMessage_TooLong_NotEmpty()
    {
        var msg = AccountSettingsLogic.ValidationMessage(AccountSettingsLogic.NameValidationResult.TooLong);
        Assert.IsFalse(string.IsNullOrEmpty(msg));
    }

    [Test]
    public void ValidationMessage_InvalidChars_NotEmpty()
    {
        var msg = AccountSettingsLogic.ValidationMessage(AccountSettingsLogic.NameValidationResult.InvalidChars);
        Assert.IsFalse(string.IsNullOrEmpty(msg));
    }

    // ── FormatDate ────────────────────────────────────────────────────────────

    [Test]
    public void FormatDate_Null_ReturnsNull()
    {
        Assert.IsNull(AccountSettingsLogic.FormatDate(null));
    }

    [Test]
    public void FormatDate_Empty_ReturnsNull()
    {
        Assert.IsNull(AccountSettingsLogic.FormatDate(string.Empty));
    }

    [Test]
    public void FormatDate_ValidIso_ReturnsFormattedString()
    {
        var result = AccountSettingsLogic.FormatDate("2026-05-15T00:00:00Z");
        Assert.IsNotNull(result);
        StringAssert.Contains("2026", result);
        StringAssert.Contains("年", result);
    }

    [Test]
    public void FormatDate_InvalidString_ReturnsNull()
    {
        Assert.IsNull(AccountSettingsLogic.FormatDate("not-a-date"));
    }
}
