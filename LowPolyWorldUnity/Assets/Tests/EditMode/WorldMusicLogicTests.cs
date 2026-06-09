using NUnit.Framework;

public class WorldMusicLogicTests
{
    private WorldMusicLogic _logic;

    [SetUp]
    public void SetUp() => _logic = new WorldMusicLogic();

    [Test]
    public void InitialState_IsNone()
    {
        Assert.AreEqual("none", _logic.Current.SoundId);
        Assert.AreEqual(1f, _logic.Current.Volume);
        Assert.IsTrue(_logic.Current.Loop);
        Assert.IsFalse(_logic.IsOverridden);
    }

    [Test]
    public void SetDefault_UpdatesCurrent_WhenNoOverride()
    {
        var state = _logic.SetDefault("rain", 0.8f);

        Assert.AreEqual("rain", state.SoundId);
        Assert.AreEqual(0.8f, state.Volume);
        Assert.IsTrue(state.Loop);
        Assert.AreEqual("rain", _logic.Current.SoundId);
        Assert.IsFalse(_logic.IsOverridden);
    }

    [Test]
    public void SetDefault_DoesNotChangeCurrent_WhenOverrideActive()
    {
        _logic.SetDefault("rain", 1f);
        _logic.SwitchTo("bgmFunNightStage", 0.7f, true);

        var state = _logic.SetDefault("ocean", 0.9f);

        Assert.AreEqual("bgmFunNightStage", state.SoundId, "オーバーライド中は Current が変わらない");
        Assert.AreEqual("bgmFunNightStage", _logic.Current.SoundId);
        Assert.IsTrue(_logic.IsOverridden);
    }

    [Test]
    public void SwitchTo_OverridesCurrent()
    {
        _logic.SetDefault("rain", 1f);

        var state = _logic.SwitchTo("bgmBrightPlains", 0.5f, true);

        Assert.AreEqual("bgmBrightPlains", state.SoundId);
        Assert.AreEqual(0.5f, state.Volume);
        Assert.IsTrue(_logic.IsOverridden);
        Assert.AreEqual("bgmBrightPlains", _logic.Current.SoundId);
    }

    [Test]
    public void SwitchTo_WithLoopFalse_ReflectedInState()
    {
        var state = _logic.SwitchTo("bgmATenseMoment", 1f, false);

        Assert.IsFalse(state.Loop);
    }

    [Test]
    public void ResetToDefault_ClearsOverride_AndReturnsDefault()
    {
        _logic.SetDefault("wind", 0.6f);
        _logic.SwitchTo("bgmFunNightStage", 1f, true);

        var state = _logic.ResetToDefault();

        Assert.AreEqual("wind", state.SoundId);
        Assert.AreEqual(0.6f, state.Volume);
        Assert.IsFalse(_logic.IsOverridden);
        Assert.AreEqual("wind", _logic.Current.SoundId);
    }

    [Test]
    public void ResetToDefault_WhenNoOverride_ReturnsDefault()
    {
        _logic.SetDefault("cave", 1f);

        var state = _logic.ResetToDefault();

        Assert.AreEqual("cave", state.SoundId);
        Assert.IsFalse(_logic.IsOverridden);
    }

    [Test]
    public void SetDefault_VolumeClamped()
    {
        var state = _logic.SetDefault("rain", 1.5f);
        Assert.AreEqual(1f, state.Volume);

        state = _logic.SetDefault("rain", -0.1f);
        Assert.AreEqual(0f, state.Volume);
    }

    [Test]
    public void SwitchTo_VolumeClamped()
    {
        var state = _logic.SwitchTo("ocean", 2f, true);
        Assert.AreEqual(1f, state.Volume);
    }

    [Test]
    public void SetDefault_WhileOverrideActive_ThenReset_ReturnsNewDefault()
    {
        _logic.SetDefault("rain", 1f);
        _logic.SwitchTo("bgmFunNightStage", 0.7f, true);

        // オーバーライド中にデフォルトを更新
        _logic.SetDefault("ocean", 0.9f);

        // リセット後は古い "rain" ではなく新しい "ocean" へ戻る
        var state = _logic.ResetToDefault();

        Assert.AreEqual("ocean", state.SoundId);
        Assert.AreEqual(0.9f, state.Volume);
        Assert.IsFalse(_logic.IsOverridden);
    }

    [Test]
    public void MultipleGimmickSwitches_LastOneWins()
    {
        _logic.SetDefault("rain", 1f);
        _logic.SwitchTo("bgmBrightPlains", 0.5f, true);
        _logic.SwitchTo("bgmATenseMoment", 0.8f, false);

        Assert.AreEqual("bgmATenseMoment", _logic.Current.SoundId);
        Assert.IsFalse(_logic.Current.Loop);
    }
}
