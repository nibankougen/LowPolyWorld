using NUnit.Framework;
using UnityEngine;

public class DeviceLanguageTests
{
    [Test]
    public void CodeFor_MapsKnownLanguages()
    {
        Assert.AreEqual("ja", DeviceLanguage.CodeFor(SystemLanguage.Japanese));
        Assert.AreEqual("en", DeviceLanguage.CodeFor(SystemLanguage.English));
        Assert.AreEqual("zh-Hans", DeviceLanguage.CodeFor(SystemLanguage.ChineseSimplified));
        Assert.AreEqual("zh-Hant", DeviceLanguage.CodeFor(SystemLanguage.ChineseTraditional));
        Assert.AreEqual("ko", DeviceLanguage.CodeFor(SystemLanguage.Korean));
        Assert.AreEqual("pt-BR", DeviceLanguage.CodeFor(SystemLanguage.Portuguese));
    }

    [Test]
    public void CodeFor_UnsupportedFallsBackToEnglish()
    {
        Assert.AreEqual(SupportedLanguages.Fallback, DeviceLanguage.CodeFor(SystemLanguage.Russian));
        Assert.AreEqual("en", DeviceLanguage.CodeFor(SystemLanguage.Unknown));
    }

    [Test]
    public void CodeFor_AlwaysReturnsSupportedCode()
    {
        foreach (SystemLanguage l in System.Enum.GetValues(typeof(SystemLanguage)))
            Assert.IsTrue(SupportedLanguages.IsSupported(DeviceLanguage.CodeFor(l)), l.ToString());
    }
}
