using NUnit.Framework;

public class ReportModalLogicTests
{
    // ── 初期状態 ──────────────────────────────────────────────────────────────

    [Test]
    public void InitialState_NoReasonSelected()
    {
        var logic = new ReportModalLogic();
        Assert.IsNull(logic.SelectedReason);
    }

    [Test]
    public void InitialState_CanSubmit_IsFalse()
    {
        var logic = new ReportModalLogic();
        Assert.IsFalse(logic.CanSubmit);
    }

    [Test]
    public void InitialState_HideUser_IsTrue_WhenNotAlreadyHidden()
    {
        var logic = new ReportModalLogic(isAlreadyHidden: false);
        Assert.IsTrue(logic.HideUser);
    }

    [Test]
    public void InitialState_HideUser_IsFalse_WhenAlreadyHidden()
    {
        var logic = new ReportModalLogic(isAlreadyHidden: true);
        Assert.IsFalse(logic.HideUser);
    }

    [Test]
    public void ShowHideCheckbox_True_WhenNotAlreadyHidden()
    {
        var logic = new ReportModalLogic(isAlreadyHidden: false);
        Assert.IsTrue(logic.ShowHideCheckbox);
    }

    [Test]
    public void ShowHideCheckbox_False_WhenAlreadyHidden()
    {
        var logic = new ReportModalLogic(isAlreadyHidden: true);
        Assert.IsFalse(logic.ShowHideCheckbox);
    }

    // ── 理由選択（詳細テキスト不要） ──────────────────────────────────────────

    [Test]
    public void SelectReason_NonRequiredDetail_CanSubmit_IsTrue()
    {
        var logic = new ReportModalLogic();
        logic.SelectReason(ReportReason.Spam);
        Assert.IsTrue(logic.CanSubmit);
    }

    [Test]
    public void SelectReason_NonRequiredDetail_IsDetailRequired_IsFalse()
    {
        var logic = new ReportModalLogic();
        logic.SelectReason(ReportReason.HateSpeech);
        Assert.IsFalse(logic.IsDetailRequired);
    }

    [TestCase(ReportReason.Spam)]
    [TestCase(ReportReason.Harassment)]
    [TestCase(ReportReason.HateSpeech)]
    [TestCase(ReportReason.Inappropriate)]
    [TestCase(ReportReason.Violence)]
    [TestCase(ReportReason.Misinformation)]
    public void SelectReason_NonRequiredDetailReasons_CanSubmitWithoutDetail(ReportReason reason)
    {
        var logic = new ReportModalLogic();
        logic.SelectReason(reason);
        Assert.IsTrue(logic.CanSubmit);
        Assert.IsFalse(logic.IsDetailRequired);
    }

    // ── 理由選択（詳細テキスト必須） ──────────────────────────────────────────

    [TestCase(ReportReason.Impersonation)]
    [TestCase(ReportReason.Other)]
    public void SelectReason_RequiredDetail_CanSubmit_IsFalse_WithoutDetail(ReportReason reason)
    {
        var logic = new ReportModalLogic();
        logic.SelectReason(reason);
        Assert.IsFalse(logic.CanSubmit);
        Assert.IsTrue(logic.IsDetailRequired);
    }

    [TestCase(ReportReason.Impersonation)]
    [TestCase(ReportReason.Other)]
    public void SelectReason_RequiredDetail_WithDetail_CanSubmit_IsTrue(ReportReason reason)
    {
        var logic = new ReportModalLogic();
        logic.SelectReason(reason);
        logic.UpdateDetailText("詳細テキスト");
        Assert.IsTrue(logic.CanSubmit);
    }

    // ── 詳細テキスト更新 ──────────────────────────────────────────────────────

    [Test]
    public void UpdateDetailText_Null_SetsEmptyString()
    {
        var logic = new ReportModalLogic();
        logic.UpdateDetailText(null);
        Assert.AreEqual(string.Empty, logic.DetailText);
    }

    [Test]
    public void UpdateDetailText_EmptyString_StillRequiredDetail_CanSubmit_IsFalse()
    {
        var logic = new ReportModalLogic();
        logic.SelectReason(ReportReason.Other);
        logic.UpdateDetailText("");
        Assert.IsFalse(logic.CanSubmit);
    }

    [Test]
    public void UpdateDetailText_ClearAfterEntry_RequiredDetail_CanSubmit_IsFalse()
    {
        var logic = new ReportModalLogic();
        logic.SelectReason(ReportReason.Other);
        logic.UpdateDetailText("入力済み");
        logic.UpdateDetailText("");
        Assert.IsFalse(logic.CanSubmit);
    }

    // ── HideUser 切り替え ──────────────────────────────────────────────────────

    [Test]
    public void SetHideUser_False_WhenNotAlreadyHidden_Works()
    {
        var logic = new ReportModalLogic(isAlreadyHidden: false);
        logic.SetHideUser(false);
        Assert.IsFalse(logic.HideUser);
    }

    [Test]
    public void SetHideUser_WhenAlreadyHidden_DoesNothing()
    {
        var logic = new ReportModalLogic(isAlreadyHidden: true);
        logic.SetHideUser(true);
        Assert.IsFalse(logic.HideUser);
    }

    // ── 理由変更で詳細要否が切り替わる ────────────────────────────────────────

    [Test]
    public void ChangeReason_FromRequired_ToNonRequired_CanSubmitWithoutDetail()
    {
        var logic = new ReportModalLogic();
        logic.SelectReason(ReportReason.Other); // 必須
        logic.SelectReason(ReportReason.Spam);  // 不要に変更
        Assert.IsTrue(logic.CanSubmit);
        Assert.IsFalse(logic.IsDetailRequired);
    }

    // ── API 文字列変換 ─────────────────────────────────────────────────────────

    [TestCase(ReportReason.Spam, "spam")]
    [TestCase(ReportReason.Harassment, "harassment")]
    [TestCase(ReportReason.HateSpeech, "hate_speech")]
    [TestCase(ReportReason.Impersonation, "impersonation")]
    [TestCase(ReportReason.Inappropriate, "inappropriate")]
    [TestCase(ReportReason.Violence, "violence")]
    [TestCase(ReportReason.Misinformation, "misinformation")]
    [TestCase(ReportReason.Other, "other")]
    public void SelectedReasonApiString_ReturnsCorrectString(ReportReason reason, string expected)
    {
        var logic = new ReportModalLogic();
        logic.SelectReason(reason);
        Assert.AreEqual(expected, logic.SelectedReasonApiString());
    }
}
