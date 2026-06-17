using System.Collections.Generic;
using NUnit.Framework;

public class SupportedLanguagesTests
{
    [Test]
    public void All_HasUniqueNonEmptyCodesAndLabels()
    {
        var seen = new HashSet<string>();
        foreach (var l in SupportedLanguages.All)
        {
            Assert.IsFalse(string.IsNullOrEmpty(l.Code), "コードが空");
            Assert.IsFalse(string.IsNullOrEmpty(l.Label), $"ラベルが空: {l.Code}");
            Assert.IsTrue(seen.Add(l.Code), $"コード重複: {l.Code}");
        }
        Assert.AreEqual(10, SupportedLanguages.All.Count);
    }

    [Test]
    public void LabelOf_ReturnsLabelForKnown_CodeForUnknown()
    {
        Assert.AreEqual("日本語", SupportedLanguages.LabelOf("ja"));
        Assert.AreEqual("English", SupportedLanguages.LabelOf("en"));
        Assert.AreEqual("xx", SupportedLanguages.LabelOf("xx"));
    }

    [Test]
    public void IsSupported_TrueForListedFalseForDefaultAndUnknown()
    {
        Assert.IsTrue(SupportedLanguages.IsSupported("ja"));
        Assert.IsTrue(SupportedLanguages.IsSupported("pt-BR"));
        Assert.IsFalse(SupportedLanguages.IsSupported(SupportedLanguages.Default)); // "" は対応言語ではない
        Assert.IsFalse(SupportedLanguages.IsSupported("xx"));
    }

    [Test]
    public void Fallback_IsEnglishAndSupported()
    {
        Assert.AreEqual("en", SupportedLanguages.Fallback);
        Assert.IsTrue(SupportedLanguages.IsSupported(SupportedLanguages.Fallback));
    }
}
