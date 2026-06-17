using NUnit.Framework;

public class SpeakerLibraryLogicTests
{
    [Test]
    public void Add_AutoNumbersAndStoresUnderLang()
    {
        var lib = new SpeakerLibraryLogic();
        var a = lib.Add("ja");
        var b = lib.Add("ja");
        Assert.AreEqual("話者1", SpeakerLibraryLogic.ResolveName(a, "ja"));
        Assert.AreEqual("話者2", SpeakerLibraryLogic.ResolveName(b, "ja"));
        Assert.AreNotEqual(a.speakerId, b.speakerId);
    }

    [Test]
    public void Add_FailsAtLimit()
    {
        var lib = new SpeakerLibraryLogic();
        for (int i = 0; i < SpeakerLibraryLogic.MaxSpeakers; i++)
            Assert.IsNotNull(lib.Add());
        Assert.IsFalse(lib.CanAdd);
        Assert.IsNull(lib.Add());
    }

    [Test]
    public void SetName_UpsertsAndClamps()
    {
        var lib = new SpeakerLibraryLogic();
        var s = lib.Add();
        Assert.IsTrue(lib.SetName(s.speakerId, "ja", "村人"));
        Assert.IsTrue(lib.SetName(s.speakerId, "ja", "町人")); // 上書き
        Assert.AreEqual("町人", SpeakerLibraryLogic.ResolveName(s, "ja"));

        string over = new string('x', SpeakerLibraryLogic.NameMaxLength + 5);
        Assert.IsTrue(lib.SetName(s.speakerId, "en", over));
        Assert.AreEqual(SpeakerLibraryLogic.NameMaxLength, SpeakerLibraryLogic.ResolveName(s, "en").Length);

        Assert.IsFalse(lib.SetName(s.speakerId, "ja", ""), "空は拒否（RemoveName を使う）");
        Assert.IsFalse(lib.SetName("missing", "ja", "x"));
    }

    [Test]
    public void RemoveName_RemovesPerLanguage()
    {
        var lib = new SpeakerLibraryLogic();
        var s = lib.Add("ja", "村人");
        lib.SetName(s.speakerId, "en", "Villager");
        Assert.IsTrue(lib.RemoveName(s.speakerId, "ja"));
        Assert.AreEqual("Villager", SpeakerLibraryLogic.ResolveName(s, "ja"), "ja 削除後は英語フォールバック");
        Assert.IsFalse(lib.RemoveName(s.speakerId, "ja"));
    }

    [Test]
    public void ResolveName_FallsBackEnglishThenFirst()
    {
        var s = new SpeakerJson
        {
            speakerId = "x",
            names = new[]
            {
                new GimmickTextJson { lang = "en", text = "Villager" },
                new GimmickTextJson { lang = "de", text = "Dorfbewohner" },
            },
        };
        Assert.AreEqual("Villager", SpeakerLibraryLogic.ResolveName(s, "fr")); // 英語優先
        Assert.AreEqual("Dorfbewohner", SpeakerLibraryLogic.ResolveName(s, "de"));
        Assert.AreEqual("", SpeakerLibraryLogic.ResolveName(null, "ja"));
    }

    [Test]
    public void Move_ReordersWithClamp()
    {
        var lib = new SpeakerLibraryLogic();
        var a = lib.Add();
        var b = lib.Add();
        var c = lib.Add();
        Assert.IsTrue(lib.Move(c.speakerId, 0));
        Assert.AreEqual(c.speakerId, lib.Speakers[0].speakerId);
        Assert.IsTrue(lib.Move(c.speakerId, 99)); // クランプで末尾
        Assert.AreEqual(c.speakerId, lib.Speakers[2].speakerId);
    }

    [Test]
    public void LoadFrom_WriteTo_Roundtrips()
    {
        var lib = new SpeakerLibraryLogic();
        lib.Add("ja", "村人");
        lib.Add("ja", "店主");
        var def = new WorldDefinitionJson();
        lib.WriteTo(def);
        Assert.AreEqual(2, def.speakers.Length);

        var lib2 = new SpeakerLibraryLogic();
        lib2.LoadFrom(def);
        Assert.AreEqual(2, lib2.Count);
        Assert.AreEqual("村人", lib2.DisplayName(def.speakers[0].speakerId, "ja"));
    }

    [Test]
    public void DisplayName_UnknownIsEmpty()
    {
        var lib = new SpeakerLibraryLogic();
        Assert.AreEqual("", lib.DisplayName("nope", "ja"));
        Assert.AreEqual("", lib.DisplayName("", "ja"));
    }
}
