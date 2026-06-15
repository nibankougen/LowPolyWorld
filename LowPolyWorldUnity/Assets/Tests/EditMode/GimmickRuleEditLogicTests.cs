using NUnit.Framework;

public class GimmickRuleEditLogicTests
{
    private static GimmickRuleEditLogic NewEditor()
    {
        return new GimmickRuleEditLogic(new GimmickRule { ruleId = "r1", label = "テスト" });
    }

    // ── 追加 / デフォルト種別 ──────────────────────────────────────────────────

    [Test]
    public void AddTrigger_DefaultsToFirstType_AndSyncsRule()
    {
        var ed = NewEditor();
        var t = ed.AddTrigger();
        Assert.AreEqual("roomStart", t.type);
        Assert.AreEqual(1, ed.Triggers.Count);
        Assert.AreEqual(1, ed.Rule.triggers.Length, "Rule 配列へ即時反映される");
        Assert.AreSame(t, ed.Rule.triggers[0]);
    }

    [Test]
    public void AddTrigger_UnknownTypeFallsBackToDefault()
    {
        var ed = NewEditor();
        var t = ed.AddTrigger("bogus");
        Assert.AreEqual("roomStart", t.type);
    }

    [Test]
    public void AddCondition_AddAction_UseFirstTypes()
    {
        var ed = NewEditor();
        Assert.AreEqual("worldState", ed.AddCondition().type);
        Assert.AreEqual("setWorldState", ed.AddAction().type);
        Assert.AreEqual(1, ed.Rule.conditions.Length);
        Assert.AreEqual(1, ed.Rule.actions.Length);
    }

    [Test]
    public void AddAction_WithExplicitType()
    {
        var ed = NewEditor();
        var a = ed.AddAction("showMessage");
        Assert.AreEqual("showMessage", a.type);
    }

    // ── 上限 ──────────────────────────────────────────────────────────────────

    [Test]
    public void AddTrigger_EnforcesMax20()
    {
        var ed = NewEditor();
        for (int i = 0; i < GimmickRuleEditLogic.MaxTriggers; i++)
            Assert.IsNotNull(ed.AddTrigger());
        Assert.IsFalse(ed.CanAddTrigger);
        Assert.IsNull(ed.AddTrigger(), "20 個を超えて追加できない");
        Assert.AreEqual(20, ed.Triggers.Count);
    }

    [Test]
    public void AddCondition_EnforcesMax20()
    {
        var ed = NewEditor();
        for (int i = 0; i < GimmickRuleEditLogic.MaxConditions; i++)
            ed.AddCondition();
        Assert.IsFalse(ed.CanAddCondition);
        Assert.IsNull(ed.AddCondition());
    }

    [Test]
    public void AddAction_EnforcesMax20()
    {
        var ed = NewEditor();
        for (int i = 0; i < GimmickRuleEditLogic.MaxActions; i++)
            ed.AddAction();
        Assert.IsFalse(ed.CanAddAction);
        Assert.IsNull(ed.AddAction());
    }

    // ── 種別変更 ──────────────────────────────────────────────────────────────

    [Test]
    public void SetTriggerType_ChangesKnownType()
    {
        var ed = NewEditor();
        ed.AddTrigger();
        Assert.IsTrue(ed.SetTriggerType(0, "timerReached"));
        Assert.AreEqual("timerReached", ed.Triggers[0].type);
        Assert.AreEqual("timerReached", ed.Rule.triggers[0].type);
    }

    [Test]
    public void SetTriggerType_RejectsUnknownTypeAndBadIndex()
    {
        var ed = NewEditor();
        ed.AddTrigger();
        Assert.IsFalse(ed.SetTriggerType(0, "nope"));
        Assert.AreEqual("roomStart", ed.Triggers[0].type, "拒否時は変更されない");
        Assert.IsFalse(ed.SetTriggerType(5, "objectTap"), "範囲外インデックスは false");
    }

    [Test]
    public void SetActionType_ChangesKnownType()
    {
        var ed = NewEditor();
        ed.AddAction();
        Assert.IsTrue(ed.SetActionType(0, "teleportPlayer"));
        Assert.AreEqual("teleportPlayer", ed.Actions[0].type);
    }

    // ── 並び替え ──────────────────────────────────────────────────────────────

    [Test]
    public void MoveAction_ReordersAndClamps()
    {
        var ed = NewEditor();
        var a = ed.AddAction("setWorldState");
        var b = ed.AddAction("timerStart");
        var c = ed.AddAction("showMessage");

        Assert.IsTrue(ed.MoveAction(2, 0));
        Assert.AreSame(c, ed.Actions[0]);
        Assert.AreSame(a, ed.Actions[1]);
        Assert.AreSame(b, ed.Actions[2]);
        // Rule 配列も同順
        Assert.AreSame(c, ed.Rule.actions[0]);

        // to が範囲外でも末尾へクランプ
        Assert.IsTrue(ed.MoveAction(0, 99));
        Assert.AreSame(c, ed.Actions[2]);

        // from 範囲外は false
        Assert.IsFalse(ed.MoveAction(9, 0));
    }

    [Test]
    public void MoveTrigger_SameIndexIsNoOpButTrue()
    {
        var ed = NewEditor();
        ed.AddTrigger();
        ed.AddTrigger();
        Assert.IsTrue(ed.MoveTrigger(1, 1));
        Assert.AreEqual(2, ed.Triggers.Count);
    }

    // ── 削除 ──────────────────────────────────────────────────────────────────

    [Test]
    public void RemoveCondition_RemovesByIndex()
    {
        var ed = NewEditor();
        var c0 = ed.AddCondition("worldState");
        ed.AddCondition("playerCount");
        Assert.IsTrue(ed.RemoveCondition(0));
        Assert.AreEqual(1, ed.Conditions.Count);
        Assert.AreEqual("playerCount", ed.Conditions[0].type);
        Assert.AreNotSame(c0, ed.Conditions[0]);
        Assert.IsFalse(ed.RemoveCondition(5));
        Assert.AreEqual(1, ed.Rule.conditions.Length);
    }

    // ── 文字メッセージ編集 ────────────────────────────────────────────────────

    [Test]
    public void SetActionMessage_AddsAndUpdatesPerLanguage()
    {
        var ed = NewEditor();
        ed.AddAction("showMessage");

        Assert.IsTrue(ed.SetActionMessage(0, "", "こんにちは"));
        Assert.IsTrue(ed.SetActionMessage(0, "en", "Hello"));
        Assert.AreEqual(2, ed.Actions[0].texts.Length);

        // 同一言語は上書き（重複追加しない）
        Assert.IsTrue(ed.SetActionMessage(0, "en", "Hi"));
        Assert.AreEqual(2, ed.Actions[0].texts.Length);
        var en = System.Array.Find(ed.Actions[0].texts, t => t.lang == "en");
        Assert.AreEqual("Hi", en.text);
    }

    [Test]
    public void SetActionMessage_ClampsTo80Chars()
    {
        var ed = NewEditor();
        ed.AddAction("showMessage");
        ed.SetActionMessage(0, "", new string('あ', 100));
        Assert.AreEqual(80, ed.Actions[0].texts[0].text.Length);
    }

    [Test]
    public void SetActionMessage_RejectsNonMessageActionAndEmptyText()
    {
        var ed = NewEditor();
        ed.AddAction("setWorldState");
        Assert.IsFalse(ed.SetActionMessage(0, "", "x"), "showMessage 以外は拒否");

        ed.AddAction("showMessage");
        Assert.IsFalse(ed.SetActionMessage(1, "", ""), "空テキストは拒否");
    }

    [Test]
    public void RemoveActionMessage_RemovesLanguageEntry()
    {
        var ed = NewEditor();
        ed.AddAction("showMessage");
        ed.SetActionMessage(0, "", "デフォルト");
        ed.SetActionMessage(0, "en", "Hello");

        Assert.IsTrue(ed.RemoveActionMessage(0, "en"));
        Assert.AreEqual(1, ed.Actions[0].texts.Length);
        Assert.AreEqual("", ed.Actions[0].texts[0].lang);
        Assert.IsFalse(ed.RemoveActionMessage(0, "en"), "存在しない言語は false");
    }

    // ── 既存ルールの読み込み + ラウンドトリップ ────────────────────────────────

    [Test]
    public void Constructor_LoadsExistingRuleArrays()
    {
        var rule = new GimmickRule
        {
            triggers = new[] { new GimmickTrigger { type = "objectTap" } },
            conditions = new[] { new GimmickCondition { type = "playerCount" } },
            actions = new[] { new GimmickAction { type = "setWorldState" } },
        };
        var ed = new GimmickRuleEditLogic(rule);
        Assert.AreEqual(1, ed.Triggers.Count);
        Assert.AreEqual("objectTap", ed.Triggers[0].type);
        Assert.AreEqual("playerCount", ed.Conditions[0].type);
        Assert.AreEqual("setWorldState", ed.Actions[0].type);

        // 編集後も同一 Rule インスタンスへ反映される
        ed.AddTrigger("respawn");
        Assert.AreEqual(2, rule.triggers.Length);
    }

    [Test]
    public void Constructor_RejectsNull()
    {
        Assert.Throws<System.ArgumentNullException>(() => new GimmickRuleEditLogic(null));
    }
}
