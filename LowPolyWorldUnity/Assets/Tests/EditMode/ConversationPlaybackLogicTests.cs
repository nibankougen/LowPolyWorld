using System.Collections.Generic;
using NUnit.Framework;

public class ConversationPlaybackLogicTests
{
    // ── テスト用ビルダー ──────────────────────────────────────────────────────

    private static GimmickTextJson[] T(string lang, string text) =>
        new[] { new GimmickTextJson { lang = lang, text = text } };

    private static ConversationEffectJson WorldSet(int index, int value) => new()
    {
        kind = "worldState",
        stateIndex = index,
        stateOp = "set",
        value = value,
    };

    private static ConversationLineJson Line(
        string id, string body, string gotoLineId = "",
        ConversationEffectJson onReach = null, ConversationChoiceJson[] choices = null) => new()
    {
        lineId = id,
        texts = T("", body),
        gotoLineId = gotoLineId,
        onReach = onReach ?? new ConversationEffectJson(),
        choices = choices ?? System.Array.Empty<ConversationChoiceJson>(),
    };

    private static ConversationChoiceJson Choice(
        string text, string gotoLineId, ConversationEffectJson effect = null) => new()
    {
        texts = T("", text),
        gotoLineId = gotoLineId,
        effect = effect ?? new ConversationEffectJson(),
    };

    private static ConversationJson Conv(params ConversationLineJson[] lines) =>
        new() { conversationId = "c1", name = "test", lines = lines };

    // ── 基本進行 ──────────────────────────────────────────────────────────────

    [Test]
    public void Start_EntersFirstLine()
    {
        var play = new ConversationPlaybackLogic(Conv(Line("l1", "やあ"), Line("l2", "またね")));
        var changes = play.Start();
        Assert.IsEmpty(changes);
        Assert.IsFalse(play.IsFinished);
        Assert.AreEqual("l1", play.Current.LineId);
        Assert.AreEqual("やあ", play.Current.Text);
    }

    [Test]
    public void Current_NullBeforeStart()
    {
        var play = new ConversationPlaybackLogic(Conv(Line("l1", "やあ")));
        Assert.IsNull(play.Current);
        Assert.IsFalse(play.CanAdvance);
    }

    [Test]
    public void Advance_GoesToNextLineInOrder()
    {
        var play = new ConversationPlaybackLogic(Conv(Line("l1", "1"), Line("l2", "2")));
        play.Start();
        Assert.IsTrue(play.CanAdvance);
        play.Advance();
        Assert.AreEqual("l2", play.Current.LineId);
    }

    [Test]
    public void Advance_PastLastLine_Finishes()
    {
        var play = new ConversationPlaybackLogic(Conv(Line("l1", "1"), Line("l2", "2")));
        play.Start();
        play.Advance(); // → l2
        play.Advance(); // l2 は最終行・次なし → 終了
        Assert.IsTrue(play.IsFinished);
        Assert.IsNull(play.Current);
        Assert.IsFalse(play.CanAdvance);
    }

    [Test]
    public void Advance_AfterFinish_IsNoOp()
    {
        var play = new ConversationPlaybackLogic(Conv(Line("l1", "1", gotoLineId: "end")));
        play.Start();
        play.Advance();
        Assert.IsTrue(play.IsFinished);
        Assert.IsEmpty(play.Advance());
        Assert.IsTrue(play.IsFinished);
    }

    [Test]
    public void EmptyConversation_FinishesOnStart()
    {
        var play = new ConversationPlaybackLogic(Conv());
        var changes = play.Start();
        Assert.IsEmpty(changes);
        Assert.IsTrue(play.IsFinished);
        Assert.IsNull(play.Current);
    }

    [Test]
    public void NullConversation_FinishesOnStart()
    {
        var play = new ConversationPlaybackLogic(null);
        play.Start();
        Assert.IsTrue(play.IsFinished);
    }

    // ── ジャンプ先 ────────────────────────────────────────────────────────────

    [Test]
    public void Goto_End_Finishes()
    {
        var play = new ConversationPlaybackLogic(Conv(Line("l1", "1", gotoLineId: "end"), Line("l2", "2")));
        play.Start();
        play.Advance();
        Assert.IsTrue(play.IsFinished, "gotoLineId=end は次行があっても終了する");
    }

    [Test]
    public void Goto_LineId_JumpsToThatLine()
    {
        var play = new ConversationPlaybackLogic(Conv(
            Line("l1", "1", gotoLineId: "l3"),
            Line("l2", "2"),
            Line("l3", "3")));
        play.Start();
        play.Advance();
        Assert.AreEqual("l3", play.Current.LineId);
    }

    [Test]
    public void Goto_UnknownLineId_Finishes()
    {
        var play = new ConversationPlaybackLogic(Conv(Line("l1", "1", gotoLineId: "nope")));
        play.Start();
        play.Advance();
        Assert.IsTrue(play.IsFinished);
    }

    [Test]
    public void Goto_BackwardLineId_AllowsLoopNavigation()
    {
        // 後方ジャンプは許可（無限ループ防止カウントは上位レイヤーの責務）。
        var play = new ConversationPlaybackLogic(Conv(Line("l1", "1"), Line("l2", "2", gotoLineId: "l1")));
        play.Start();
        play.Advance(); // → l2
        play.Advance(); // l2 → l1
        Assert.AreEqual("l1", play.Current.LineId);
        Assert.IsFalse(play.IsFinished);
    }

    // ── 選択肢 ────────────────────────────────────────────────────────────────

    [Test]
    public void ChoiceLine_CannotAdvance()
    {
        var play = new ConversationPlaybackLogic(Conv(
            Line("l1", "選んで", choices: new[] { Choice("A", "l2"), Choice("B", "end") }),
            Line("l2", "Aルート")));
        play.Start();
        Assert.IsFalse(play.CanAdvance);
        Assert.IsEmpty(play.Advance(), "選択肢のある行で Advance は無効");
        Assert.AreEqual("l1", play.Current.LineId);
        Assert.AreEqual(2, play.Current.Choices.Count);
        Assert.AreEqual("A", play.Current.Choices[0]);
    }

    [Test]
    public void Select_FollowsChoiceGoto()
    {
        var play = new ConversationPlaybackLogic(Conv(
            Line("l1", "選んで", choices: new[] { Choice("A", "l2"), Choice("B", "l3") }),
            Line("l2", "Aルート"),
            Line("l3", "Bルート")));
        play.Start();
        play.Select(1);
        Assert.AreEqual("l3", play.Current.LineId);
    }

    [Test]
    public void Select_ToEnd_Finishes()
    {
        var play = new ConversationPlaybackLogic(Conv(
            Line("l1", "選んで", choices: new[] { Choice("終わる", "end") })));
        play.Start();
        play.Select(0);
        Assert.IsTrue(play.IsFinished);
    }

    [Test]
    public void Select_OutOfRange_IsNoOp()
    {
        var play = new ConversationPlaybackLogic(Conv(
            Line("l1", "選んで", choices: new[] { Choice("A", "end") })));
        play.Start();
        Assert.IsEmpty(play.Select(5));
        Assert.IsEmpty(play.Select(-1));
        Assert.AreEqual("l1", play.Current.LineId);
    }

    [Test]
    public void Select_OnNonChoiceLine_IsNoOp()
    {
        var play = new ConversationPlaybackLogic(Conv(Line("l1", "1"), Line("l2", "2")));
        play.Start();
        Assert.IsEmpty(play.Select(0));
        Assert.AreEqual("l1", play.Current.LineId);
    }

    // ── ステート変更要求 ──────────────────────────────────────────────────────

    [Test]
    public void Start_EmitsFirstLineOnReachEffect()
    {
        var play = new ConversationPlaybackLogic(Conv(Line("l1", "1", onReach: WorldSet(2, 5))));
        var changes = play.Start();
        Assert.AreEqual(1, changes.Count);
        Assert.AreEqual("worldState", changes[0].Kind);
        Assert.AreEqual(2, changes[0].StateIndex);
        Assert.AreEqual(5, changes[0].Value);
    }

    [Test]
    public void NoneEffect_EmitsNothing()
    {
        var play = new ConversationPlaybackLogic(Conv(Line("l1", "1"))); // onReach kind="none"
        Assert.IsEmpty(play.Start());
    }

    [Test]
    public void Advance_EmitsTargetOnReachEffect()
    {
        var play = new ConversationPlaybackLogic(Conv(
            Line("l1", "1"),
            Line("l2", "2", onReach: WorldSet(0, 9))));
        play.Start();
        var changes = play.Advance();
        Assert.AreEqual(1, changes.Count);
        Assert.AreEqual(9, changes[0].Value);
    }

    [Test]
    public void Select_EmitsChoiceEffectThenTargetOnReach()
    {
        var play = new ConversationPlaybackLogic(Conv(
            Line("l1", "選んで", choices: new[] { Choice("A", "l2", WorldSet(1, 1)) }),
            Line("l2", "次", onReach: WorldSet(3, 7))));
        play.Start();
        var changes = play.Select(0);
        Assert.AreEqual(2, changes.Count);
        // 選択時の effect が先、到達先の onReach が後。
        Assert.AreEqual(1, changes[0].StateIndex);
        Assert.AreEqual(1, changes[0].Value);
        Assert.AreEqual(3, changes[1].StateIndex);
        Assert.AreEqual(7, changes[1].Value);
    }

    [Test]
    public void Select_ChoiceEffectEmittedEvenWhenFinishing()
    {
        var play = new ConversationPlaybackLogic(Conv(
            Line("l1", "選んで", choices: new[] { Choice("終了", "end", WorldSet(4, 2)) })));
        play.Start();
        var changes = play.Select(0);
        Assert.IsTrue(play.IsFinished);
        Assert.AreEqual(1, changes.Count);
        Assert.AreEqual(4, changes[0].StateIndex);
    }

    // ── 多言語フォールバック ──────────────────────────────────────────────────

    [Test]
    public void Resolve_PrefersViewerLanguage()
    {
        var line = new ConversationLineJson
        {
            lineId = "l1",
            texts = new[]
            {
                new GimmickTextJson { lang = "", text = "デフォルト" },
                new GimmickTextJson { lang = "ja", text = "日本語" },
                new GimmickTextJson { lang = "en", text = "English" },
            },
        };
        var play = new ConversationPlaybackLogic(Conv(line), viewerLang: "ja");
        play.Start();
        Assert.AreEqual("日本語", play.Current.Text);
    }

    [Test]
    public void Resolve_FallsBackToEnglishThenDefault()
    {
        var line = new ConversationLineJson
        {
            lineId = "l1",
            texts = new[]
            {
                new GimmickTextJson { lang = "", text = "デフォルト" },
                new GimmickTextJson { lang = "en", text = "English" },
            },
        };
        // fr は未設定 → 英語優先でフォールバック。
        var play = new ConversationPlaybackLogic(Conv(line), viewerLang: "fr");
        play.Start();
        Assert.AreEqual("English", play.Current.Text);

        // 英語も無ければデフォルト（lang="")。
        var line2 = new ConversationLineJson
        {
            lineId = "l1",
            texts = new[] { new GimmickTextJson { lang = "", text = "デフォルト" } },
        };
        var play2 = new ConversationPlaybackLogic(Conv(line2), viewerLang: "fr");
        play2.Start();
        Assert.AreEqual("デフォルト", play2.Current.Text);
    }

    [Test]
    public void Resolve_FallsBackToFirstNonEmpty()
    {
        var line = new ConversationLineJson
        {
            lineId = "l1",
            texts = new[] { new GimmickTextJson { lang = "de", text = "Deutsch" } },
        };
        var play = new ConversationPlaybackLogic(Conv(line), viewerLang: "fr");
        play.Start();
        Assert.AreEqual("Deutsch", play.Current.Text);
    }

    [Test]
    public void Resolve_SpeakerOptionalEmptyWhenUnset()
    {
        var play = new ConversationPlaybackLogic(Conv(Line("l1", "本文のみ")));
        play.Start();
        Assert.AreEqual("", play.Current.Speaker);
    }

    [Test]
    public void Resolve_SpeakerResolvedFromLibrary()
    {
        var line = Line("l1", "やあ");
        line.speakerId = "spk_1";
        var speakers = new[]
        {
            new SpeakerJson
            {
                speakerId = "spk_1",
                names = new[]
                {
                    new GimmickTextJson { lang = "ja", text = "村人" },
                    new GimmickTextJson { lang = "en", text = "Villager" },
                },
            },
        };

        var ja = new ConversationPlaybackLogic(Conv(line), "ja", speakers);
        ja.Start();
        Assert.AreEqual("村人", ja.Current.Speaker);

        var en = new ConversationPlaybackLogic(Conv(line), "en", speakers);
        en.Start();
        Assert.AreEqual("Villager", en.Current.Speaker);
    }

    [Test]
    public void Resolve_UnknownSpeakerId_IsEmpty()
    {
        var line = Line("l1", "やあ");
        line.speakerId = "missing";
        var play = new ConversationPlaybackLogic(Conv(line)); // 話者定義を渡さない
        play.Start();
        Assert.AreEqual("", play.Current.Speaker);
    }

    [Test]
    public void Resolve_ChoiceTextsResolvedForViewer()
    {
        var choice = new ConversationChoiceJson
        {
            texts = new[]
            {
                new GimmickTextJson { lang = "ja", text = "はい" },
                new GimmickTextJson { lang = "en", text = "Yes" },
            },
            gotoLineId = "end",
        };
        var play = new ConversationPlaybackLogic(
            Conv(Line("l1", "？", choices: new[] { choice })), viewerLang: "en");
        play.Start();
        Assert.AreEqual("Yes", play.Current.Choices[0]);
    }

    // ── 置き換え再生（同一プレイヤーで会話が再生中に新会話）の前提: 別インスタンス生成 ──

    [Test]
    public void Start_CalledTwice_DoesNotReenterFirstLine()
    {
        var play = new ConversationPlaybackLogic(Conv(Line("l1", "1", onReach: WorldSet(0, 1)), Line("l2", "2")));
        play.Start();
        play.Advance(); // → l2
        var again = play.Start(); // 2 回目の Start は無効
        Assert.IsEmpty(again);
        Assert.AreEqual("l2", play.Current.LineId);
    }
}
