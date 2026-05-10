using NUnit.Framework;

public class PhotoModeLogicTests
{
    // ── 初期状態 ──────────────────────────────────────────────────────────────

    [Test]
    public void InitialState_IsNormal()
    {
        var logic = new PhotoModeLogic();
        Assert.AreEqual(PhotoModeState.Normal, logic.State);
    }

    [Test]
    public void InitialState_IsPhotoMode_IsFalse()
    {
        var logic = new PhotoModeLogic();
        Assert.IsFalse(logic.IsPhotoMode);
    }

    // ── Normal → Photo ────────────────────────────────────────────────────────

    [Test]
    public void Enter_FromNormal_TransitionsToPhoto()
    {
        var logic = new PhotoModeLogic();
        var result = logic.Enter();
        Assert.IsTrue(result);
        Assert.AreEqual(PhotoModeState.Photo, logic.State);
        Assert.IsTrue(logic.IsPhotoMode);
    }

    [Test]
    public void Enter_WhenAlreadyPhoto_ReturnsFalse()
    {
        var logic = new PhotoModeLogic();
        logic.Enter();
        var result = logic.Enter();
        Assert.IsFalse(result);
        Assert.AreEqual(PhotoModeState.Photo, logic.State);
    }

    // ── Photo → Normal ────────────────────────────────────────────────────────

    [Test]
    public void Exit_FromPhoto_TransitionsToNormal()
    {
        var logic = new PhotoModeLogic();
        logic.Enter();
        var result = logic.Exit();
        Assert.IsTrue(result);
        Assert.AreEqual(PhotoModeState.Normal, logic.State);
        Assert.IsFalse(logic.IsPhotoMode);
    }

    [Test]
    public void Exit_WhenAlreadyNormal_ReturnsFalse()
    {
        var logic = new PhotoModeLogic();
        var result = logic.Exit();
        Assert.IsFalse(result);
        Assert.AreEqual(PhotoModeState.Normal, logic.State);
    }

    // ── 繰り返し遷移 ──────────────────────────────────────────────────────────

    [Test]
    public void EnterExit_CanRepeat()
    {
        var logic = new PhotoModeLogic();
        logic.Enter();
        logic.Exit();
        var result = logic.Enter();
        Assert.IsTrue(result);
        Assert.AreEqual(PhotoModeState.Photo, logic.State);
    }
}
