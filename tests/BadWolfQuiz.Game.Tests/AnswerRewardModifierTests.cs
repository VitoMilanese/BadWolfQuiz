using BadWolfQuiz.Game.Definitions;
using BadWolfQuiz.Game.Runtime;

namespace BadWolfQuiz.Game.Tests;

public sealed class AnswerRewardModifierTests
{
    [Theory]
    [InlineData(100, AnswerRewardModifier.Double, 200)]
    [InlineData(100, AnswerRewardModifier.Half, 50)]
    [InlineData(101, AnswerRewardModifier.Half, 51)]
    [InlineData(1, AnswerRewardModifier.Half, 1)]
    public void Enabled_correct_answer_modifier_is_explicit_and_survives_recovery(
        int points,
        AnswerRewardModifier modifier,
        int expectedScore)
    {
        var session = CreateSession(points, allowAnswerRewardModifiers: true);
        var player = session.AddPlayer("Wolf");
        StartAnswering(session, player);

        var attempt = session.JudgeQuestionAnswer(100, player.Id, true, modifier);
        var restored = GameSession.Restore(
            session.Quiz,
            session.Settings,
            session.CaptureState());
        var restoredQuestion = restored.Board.Questions.Single();
        var restoredAttempt = restoredQuestion.AnswerAttempts.Single();

        Assert.Equal(expectedScore, attempt.ScoreDelta);
        Assert.Equal(expectedScore, player.Score);
        Assert.Equal(points, session.Board.Questions.Single().Points);
        Assert.Equal(modifier, attempt.RewardModifier);
        Assert.Equal(modifier, restoredAttempt.RewardModifier);
        Assert.True(restoredQuestion.AllowAnswerRewardModifiers);
    }

    [Theory]
    [InlineData(AnswerRewardModifier.Double, 104)]
    [InlineData(AnswerRewardModifier.Half, 26)]
    public void Modifier_uses_reward_after_hint_and_timer_reductions(
        AnswerRewardModifier modifier,
        int expectedScore)
    {
        var timeProvider = new ManualTimeProvider(
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var source = CreateSession(100, allowAnswerRewardModifiers: true);
        var settings = new GameSessionSettings(
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(30),
            GamePhaseStartMode.Manual,
            GamePhaseStartMode.Automatic,
            answerRewardDecayEnabled: true,
            answerRewardDecayStartAfterSeconds: 10,
            answerRewardDecayMinimumPercent: 25);
        var session = GameSession.Create(source.Quiz, settings, timeProvider);
        var player = session.AddPlayer("Wolf");

        session.Start();
        var question = session.SelectQuestion(100);
        question.RevealNextHint(4);
        session.ActivateQuestionBuzzer(100);
        session.ClaimQuestionBuzzer(100, player.Id);
        timeProvider.Advance(TimeSpan.FromSeconds(20));

        Assert.Equal(88, question.CorrectAnswerValue);
        Assert.Equal(52, session.GetCurrentCorrectAnswerValue(100));

        var attempt = session.JudgeQuestionAnswer(
            100,
            player.Id,
            true,
            modifier);

        Assert.Equal(expectedScore, attempt.ScoreDelta);
        Assert.Equal(expectedScore, player.Score);
        Assert.Equal(100, question.Points);
    }

    [Theory]
    [InlineData(AnswerRewardModifier.Double, 26)]
    [InlineData(AnswerRewardModifier.Half, 7)]
    public void Modifier_uses_reward_after_four_clue_and_timer_reductions(
        AnswerRewardModifier modifier,
        int expectedScore)
    {
        var timeProvider = new ManualTimeProvider(
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var quiz = new QuizSnapshot(
            1,
            "Reward modifiers",
            [
                new QuizRoundSnapshot(
                    1,
                    "Round 1",
                    0,
                    [
                        new QuizQuestionSnapshot(
                            100,
                            10,
                            0,
                            100,
                            false,
                            "General",
                            false,
                            [
                                TextBlock(1, "Clue 1", 0),
                                TextBlock(2, "Clue 2", 1),
                                TextBlock(3, "Clue 3", 2),
                                TextBlock(4, "Clue 4", 3)
                            ],
                            [TextBlock(5, "Answer", 0)],
                            QuestionPresentationType.FourClues,
                            allowAnswerRewardModifiers: true)
                    ])
            ]);
        var settings = new GameSessionSettings(
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(30),
            GamePhaseStartMode.Manual,
            GamePhaseStartMode.Automatic,
            answerRewardDecayEnabled: true,
            answerRewardDecayStartAfterSeconds: 10,
            answerRewardDecayMinimumPercent: 25);
        var session = GameSession.Create(quiz, settings, timeProvider);
        var player = session.AddPlayer("Wolf");

        session.Start();
        var question = session.SelectQuestion(100);
        session.RevealNextClue(100);
        session.ActivateQuestionBuzzer(100);
        session.ClaimQuestionBuzzer(100, player.Id);
        timeProvider.Advance(TimeSpan.FromSeconds(29));

        Assert.Equal(50, question.CorrectAnswerValue);
        Assert.Equal(13, session.GetCurrentCorrectAnswerValue(100));

        var attempt = session.JudgeQuestionAnswer(
            100,
            player.Id,
            true,
            modifier);

        Assert.Equal(expectedScore, attempt.ScoreDelta);
        Assert.Equal(expectedScore, player.Score);
        Assert.Equal(100, question.Points);
    }

    [Fact]
    public void Disabled_question_rejects_non_normal_correct_reward_modifier_without_score_change()
    {
        var session = CreateSession(100, allowAnswerRewardModifiers: false);
        var player = session.AddPlayer("Wolf");
        StartAnswering(session, player);

        Assert.Throws<GameRuleViolationException>(() =>
            session.JudgeQuestionAnswer(
                100,
                player.Id,
                true,
                AnswerRewardModifier.Double));

        Assert.Equal(0, player.Score);
        Assert.Empty(session.Board.Questions.Single().AnswerAttempts);
    }

    [Fact]
    public void Incorrect_answer_ignores_reward_modifier_and_keeps_normal_penalty()
    {
        var session = CreateSession(101, allowAnswerRewardModifiers: true);
        var player = session.AddPlayer("Wolf");
        StartAnswering(session, player);

        var attempt = session.JudgeQuestionAnswer(
            100,
            player.Id,
            false,
            AnswerRewardModifier.Double);

        Assert.Equal(-101, attempt.ScoreDelta);
        Assert.Equal(-101, player.Score);
        Assert.Equal(AnswerRewardModifier.Normal, attempt.RewardModifier);
    }

    [Fact]
    public void Modified_reward_cannot_be_applied_twice_to_the_same_answer()
    {
        var session = CreateSession(100, allowAnswerRewardModifiers: true);
        var player = session.AddPlayer("Wolf");
        StartAnswering(session, player);

        session.JudgeQuestionAnswer(
            100,
            player.Id,
            true,
            AnswerRewardModifier.Double);

        Assert.Throws<GameRuleViolationException>(() =>
            session.JudgeQuestionAnswer(
                100,
                player.Id,
                true,
                AnswerRewardModifier.Double));
        Assert.Equal(200, player.Score);
        Assert.Single(session.Board.Questions.Single().AnswerAttempts);
    }

    private static GameSession CreateSession(
        int points,
        bool allowAnswerRewardModifiers)
    {
        var clue = new ContentBlockSnapshot(
            1, ContentBlockKind.Text, "Question", null, null, null,
            null, null, null, null, 0, false);
        var answer = new ContentBlockSnapshot(
            2, ContentBlockKind.Text, "Answer", null, null, null,
            null, null, null, null, 0, false);
        var question = new QuizQuestionSnapshot(
            100, 10, 0, points, false, "General", false,
            [clue], [answer], QuestionPresentationType.Standard,
            allowAnswerRewardModifiers: allowAnswerRewardModifiers);
        var quiz = new QuizSnapshot(
            1,
            "Reward modifiers",
            [new QuizRoundSnapshot(1, "Round 1", 0, [question])]);
        return GameSession.Create(quiz);
    }

    private static ContentBlockSnapshot TextBlock(
        int id,
        string text,
        int sortOrder) => new(
        id,
        ContentBlockKind.Text,
        text,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        sortOrder,
        false);

    private static void StartAnswering(GameSession session, GamePlayer player)
    {
        session.Start();
        session.SelectQuestion(100);
        session.ActivateQuestionBuzzer(100);
        session.ClaimQuestionBuzzer(100, player.Id);
    }
}
