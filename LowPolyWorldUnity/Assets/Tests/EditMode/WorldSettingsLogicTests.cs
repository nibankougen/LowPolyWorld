using NUnit.Framework;

public class WorldSettingsLogicTests
{
    // ---- 初期値 ----

    [Test]
    public void DefaultConstructor_AllVolumesAreOne()
    {
        var logic = new WorldSettingsLogic();
        Assert.AreEqual(1.0f, logic.VoiceVolume, 0.0001f);
        Assert.AreEqual(1.0f, logic.WorldSfxVolume, 0.0001f);
        Assert.AreEqual(1.0f, logic.SystemSfxVolume, 0.0001f);
    }

    [Test]
    public void Constructor_WithInitialValues_AppliesCorrectly()
    {
        var logic = new WorldSettingsLogic(initialVoice: 0.5f, initialWorldSfx: 0.3f, initialSystemSfx: 0.8f);
        Assert.AreEqual(0.5f, logic.VoiceVolume, 0.0001f);
        Assert.AreEqual(0.3f, logic.WorldSfxVolume, 0.0001f);
        Assert.AreEqual(0.8f, logic.SystemSfxVolume, 0.0001f);
    }

    // ---- クランプ ----

    [Test]
    public void SetVoiceVolume_AboveOne_ClampsToOne()
    {
        var logic = new WorldSettingsLogic(initialVoice: 0.5f);
        logic.SetVoiceVolume(2.0f);
        Assert.AreEqual(1.0f, logic.VoiceVolume, 0.0001f);
    }

    [Test]
    public void SetVoiceVolume_BelowZero_ClampsToZero()
    {
        var logic = new WorldSettingsLogic();
        logic.SetVoiceVolume(-1.0f);
        Assert.AreEqual(0.0f, logic.VoiceVolume, 0.0001f);
    }

    [Test]
    public void SetWorldSfxVolume_AboveOne_ClampsToOne()
    {
        var logic = new WorldSettingsLogic(initialWorldSfx: 0.5f);
        logic.SetWorldSfxVolume(99f);
        Assert.AreEqual(1.0f, logic.WorldSfxVolume, 0.0001f);
    }

    [Test]
    public void SetWorldSfxVolume_BelowZero_ClampsToZero()
    {
        var logic = new WorldSettingsLogic();
        logic.SetWorldSfxVolume(-0.1f);
        Assert.AreEqual(0.0f, logic.WorldSfxVolume, 0.0001f);
    }

    [Test]
    public void SetSystemSfxVolume_AboveOne_ClampsToOne()
    {
        var logic = new WorldSettingsLogic(initialSystemSfx: 0.5f);
        logic.SetSystemSfxVolume(5f);
        Assert.AreEqual(1.0f, logic.SystemSfxVolume, 0.0001f);
    }

    [Test]
    public void SetSystemSfxVolume_BelowZero_ClampsToZero()
    {
        var logic = new WorldSettingsLogic();
        logic.SetSystemSfxVolume(-5f);
        Assert.AreEqual(0.0f, logic.SystemSfxVolume, 0.0001f);
    }

    [Test]
    public void Constructor_InitialValueAboveOne_ClampsToOne()
    {
        var logic = new WorldSettingsLogic(initialVoice: 2.0f);
        Assert.AreEqual(1.0f, logic.VoiceVolume, 0.0001f);
    }

    [Test]
    public void Constructor_InitialValueBelowZero_ClampsToZero()
    {
        var logic = new WorldSettingsLogic(initialWorldSfx: -1.0f);
        Assert.AreEqual(0.0f, logic.WorldSfxVolume, 0.0001f);
    }

    // ---- 変更イベント発火 ----

    [Test]
    public void SetVoiceVolume_Changed_FiresEvent()
    {
        var logic = new WorldSettingsLogic(initialVoice: 1.0f);
        float received = -1f;
        logic.OnVoiceVolumeChanged += v => received = v;

        logic.SetVoiceVolume(0.5f);

        Assert.AreEqual(0.5f, received, 0.0001f);
    }

    [Test]
    public void SetWorldSfxVolume_Changed_FiresEvent()
    {
        var logic = new WorldSettingsLogic(initialWorldSfx: 1.0f);
        float received = -1f;
        logic.OnWorldSfxVolumeChanged += v => received = v;

        logic.SetWorldSfxVolume(0.3f);

        Assert.AreEqual(0.3f, received, 0.0001f);
    }

    [Test]
    public void SetSystemSfxVolume_Changed_FiresEvent()
    {
        var logic = new WorldSettingsLogic(initialSystemSfx: 1.0f);
        float received = -1f;
        logic.OnSystemSfxVolumeChanged += v => received = v;

        logic.SetSystemSfxVolume(0.7f);

        Assert.AreEqual(0.7f, received, 0.0001f);
    }

    // ---- 同値では発火しない ----

    [Test]
    public void SetVoiceVolume_SameValue_DoesNotFireEvent()
    {
        var logic = new WorldSettingsLogic(initialVoice: 0.5f);
        int callCount = 0;
        logic.OnVoiceVolumeChanged += _ => callCount++;

        logic.SetVoiceVolume(0.5f);

        Assert.AreEqual(0, callCount);
    }

    [Test]
    public void SetWorldSfxVolume_SameValue_DoesNotFireEvent()
    {
        var logic = new WorldSettingsLogic(initialWorldSfx: 0.8f);
        int callCount = 0;
        logic.OnWorldSfxVolumeChanged += _ => callCount++;

        logic.SetWorldSfxVolume(0.8f);

        Assert.AreEqual(0, callCount);
    }

    [Test]
    public void SetSystemSfxVolume_SameValue_DoesNotFireEvent()
    {
        var logic = new WorldSettingsLogic(initialSystemSfx: 0.2f);
        int callCount = 0;
        logic.OnSystemSfxVolumeChanged += _ => callCount++;

        logic.SetSystemSfxVolume(0.2f);

        Assert.AreEqual(0, callCount);
    }

    // ---- クランプ後に同値になる場合はイベントなし ----

    [Test]
    public void SetVoiceVolume_ClampedToSameValue_DoesNotFireEvent()
    {
        var logic = new WorldSettingsLogic(initialVoice: 1.0f);
        int callCount = 0;
        logic.OnVoiceVolumeChanged += _ => callCount++;

        // 2.0f はクランプされて 1.0f になる → 現在値と同じ → イベントなし
        logic.SetVoiceVolume(2.0f);

        Assert.AreEqual(0, callCount);
    }
}
