using NUnit.Framework;

public class TextStampLogicTests
{
    // ── 初期状態（未編集）────────────────────────────────────────────────────

    [Test]
    public void InitialState_IsNotEdited()
    {
        var logic = new TextStampLogic();
        Assert.AreEqual(TextStampEditState.NotEdited, logic.EditState);
    }

    [Test]
    public void InitialText_IsEmpty()
    {
        var logic = new TextStampLogic();
        Assert.AreEqual(string.Empty, logic.Text);
    }

    // ── 未編集 → 編集中 ──────────────────────────────────────────────────────

    [Test]
    public void BeginEditing_FromNotEdited_TransitionsToEditing()
    {
        var logic = new TextStampLogic();
        var result = logic.BeginEditing();
        Assert.IsTrue(result);
        Assert.AreEqual(TextStampEditState.Editing, logic.EditState);
    }

    [Test]
    public void BeginEditing_WhenAlreadyEditing_ReturnsFalse()
    {
        var logic = new TextStampLogic();
        logic.BeginEditing();
        var result = logic.BeginEditing();
        Assert.IsFalse(result);
        Assert.AreEqual(TextStampEditState.Editing, logic.EditState);
    }

    // ── 編集中のテキスト更新 ─────────────────────────────────────────────────

    [Test]
    public void UpdateText_WhileEditing_SetsText()
    {
        var logic = new TextStampLogic();
        logic.BeginEditing();
        var result = logic.UpdateText("Hello");
        Assert.IsTrue(result);
        Assert.AreEqual("Hello", logic.Text);
    }

    [Test]
    public void UpdateText_NotEditing_ReturnsFalse()
    {
        var logic = new TextStampLogic();
        var result = logic.UpdateText("Hello");
        Assert.IsFalse(result);
        Assert.AreEqual(string.Empty, logic.Text);
    }

    [Test]
    public void UpdateText_Null_SetsEmptyString()
    {
        var logic = new TextStampLogic();
        logic.BeginEditing();
        logic.UpdateText(null);
        Assert.AreEqual(string.Empty, logic.Text);
    }

    // ── 編集中 → 完了 ────────────────────────────────────────────────────────

    [Test]
    public void CompleteEditing_FromEditing_TransitionsToCompleted()
    {
        var logic = new TextStampLogic();
        logic.BeginEditing();
        logic.UpdateText("Hello");
        var result = logic.CompleteEditing();
        Assert.IsTrue(result);
        Assert.AreEqual(TextStampEditState.Completed, logic.EditState);
    }

    [Test]
    public void CompleteEditing_PreservesText()
    {
        var logic = new TextStampLogic();
        logic.BeginEditing();
        logic.UpdateText("Hello");
        logic.CompleteEditing();
        Assert.AreEqual("Hello", logic.Text);
    }

    [Test]
    public void CompleteEditing_NotEditing_ReturnsFalse()
    {
        var logic = new TextStampLogic();
        var result = logic.CompleteEditing();
        Assert.IsFalse(result);
    }

    // ── 完了 → 再編集（スタンプ再タップ）────────────────────────────────────

    [Test]
    public void BeginEditing_FromCompleted_TransitionsBackToEditing()
    {
        var logic = new TextStampLogic();
        logic.BeginEditing();
        logic.UpdateText("Hello");
        logic.CompleteEditing();

        var result = logic.BeginEditing();

        Assert.IsTrue(result);
        Assert.AreEqual(TextStampEditState.Editing, logic.EditState);
    }

    [Test]
    public void ReEdit_CanUpdateTextAgain()
    {
        var logic = new TextStampLogic();
        logic.BeginEditing();
        logic.UpdateText("Hello");
        logic.CompleteEditing();
        logic.BeginEditing();

        logic.UpdateText("World");
        logic.CompleteEditing();

        Assert.AreEqual("World", logic.Text);
    }

    // ── 完全な遷移フロー ─────────────────────────────────────────────────────

    [Test]
    public void FullFlow_NotEdited_Editing_Completed_Editing()
    {
        var logic = new TextStampLogic();

        Assert.AreEqual(TextStampEditState.NotEdited, logic.EditState);

        logic.BeginEditing();
        Assert.AreEqual(TextStampEditState.Editing, logic.EditState);

        logic.UpdateText("Hello");
        logic.CompleteEditing();
        Assert.AreEqual(TextStampEditState.Completed, logic.EditState);
        Assert.AreEqual("Hello", logic.Text);

        logic.BeginEditing(); // 再編集
        Assert.AreEqual(TextStampEditState.Editing, logic.EditState);

        logic.UpdateText("World");
        logic.CompleteEditing();
        Assert.AreEqual(TextStampEditState.Completed, logic.EditState);
        Assert.AreEqual("World", logic.Text);
    }
}
