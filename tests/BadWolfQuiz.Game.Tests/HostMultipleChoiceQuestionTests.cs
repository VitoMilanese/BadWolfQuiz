using BadWolfQuiz.Game.Definitions;
using BadWolfQuiz.Game.Runtime;

namespace BadWolfQuiz.Game.Tests;

public sealed class HostMultipleChoiceQuestionTests
{
    private static readonly DateTimeOffset InitialTime =
        new(2026, 8, 20, 0, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(4, 4, 100)]
    [InlineData(4, 3, 50)]
    [InlineData(5, 5, 100)]
    [InlineData(5, 4, 75)]
    [InlineData(5, 3, 50)]
    [InlineData(6, 6, 100)]
    [InlineData(6, 5, 84)]
    [InlineData(6, 4, 67)]
    [InlineData(6, 3, 50)]
    [InlineData(7, 7, 100)]
    [InlineData(7, 6, 88)]
    [InlineData(7, 5, 75)]
    [InlineData(7, 4, 63)]
    [InlineData(7, 3, 50)]
    [InlineData(10, 10, 100)]
    [InlineData(10, 9, 93)]
    [InlineData(10, 3, 50)]
    public void Reward_percentage_uses_equal_steps_from_full_value_to_half_value(
        int originalOptionCount,
        int remainingOptionCount,
        int expectedPercentage)
    {
        Assert.Equal(
            expectedPercentage,
            RuntimeQuestion.CalculateHostMultipleChoiceRewardPercentage(
                originalOptionCount,
                remainingOptionCount));
    }

    [Fact]
    public void Snapshot_requires_four_to_ten_short_distinct_text_options_and_disables_wager()
    {
        var question = CreateQuestion(4, isSpecial: true);

        Assert.Equal(QuestionPresentationType.HostMultipleChoice, question.PresentationType);
        Assert.False(question.IsSpecial);
        Assert.True(question.ExcludeFromRandomWagerSelection);
        Assert.False(question.IsEligibleForRandomWagerSelection);
        Assert.Equal(4, question.AnswerBlocks.Count);

        Assert.Throws<ArgumentException>(() => CreateQuestion(3));
        Assert.Throws<ArgumentException>(() => CreateQuestion(11));

        var tooLong = CreateOptions(4).ToArray();
        tooLong[1] = TextBlock(202, new string('x', 21), 1);
        Assert.Throws<ArgumentException>(() => CreateQuestion(tooLong));

        var duplicate = CreateOptions(4).ToArray();
        duplicate[2] = TextBlock(203, duplicate[1].TextContent!, 2);
        Assert.Throws<ArgumentException>(() => CreateQuestion(duplicate));
    }

    [Fact]
    public void Wrong_option_deducts_current_value_removes_option_and_reopens_buzzer()
    {
        var session = CreateSession(5);
        var first = session.AddPlayer("Rose");
        var second = session.AddPlayer("Bad Wolf");
        session.Start();
        session.SelectQuestion(100);
        session.ActivateQuestionBuzzer(100);
        session.ClaimQuestionBuzzer(100, first.Id);

        var wrong = session.SelectHostMultipleChoiceOption(100, first.Id, 202);
        var question = session.Board.Questions.Single();

        Assert.False(wrong.IsCorrect);
        Assert.Equal(-100, wrong.Attempt.ScoreDelta);
        Assert.Equal(-100, first.Score);
        Assert.DoesNotContain(202, question.RemainingHostMultipleChoiceOptionIds);
        Assert.Equal(4, question.RemainingHostMultipleChoiceOptionIds.Count);
        Assert.Equal(75, question.HostMultipleChoiceRewardPercentage);
        Assert.Equal(75, question.CorrectAnswerValue);
        Assert.Equal(QuestionBuzzerStatus.Open, question.BuzzerStatus);
        Assert.Throws<GameRuleViolationException>(() =>
            session.ClaimQuestionBuzzer(100, first.Id));

        session.ClaimQuestionBuzzer(100, second.Id);
        var correct = session.SelectHostMultipleChoiceOption(100, second.Id, 201);

        Assert.True(correct.IsCorrect);
        Assert.Equal(75, correct.Attempt.ScoreDelta);
        Assert.Equal(75, second.Score);
        Assert.Equal(second.Id, session.ActivePlayerId);
        Assert.Equal(RuntimeQuestionStatus.ShowingAnswer, question.Status);
        Assert.Equal(QuestionBuzzerStatus.Closed, question.BuzzerStatus);
    }

    [Fact]
    public void Buzzer_timeout_eliminates_random_incorrect_options_until_two_remain()
    {
        var timeProvider = new ManualTimeProvider(InitialTime);
        var session = CreateSession(5, timeProvider);
        session.AddPlayer("Rose");
        session.Start();
        session.SelectQuestion(100);
        session.ActivateQuestionBuzzer(100);
        var question = session.Board.Questions.Single();

        for (var expectedRemaining = 4; expectedRemaining >= 2; expectedRemaining--)
        {
            timeProvider.Advance(TimeSpan.FromSeconds(31));
            var tick = session.ProcessQuestionTimers();

            Assert.Equal(
                QuestionTimerOutcome.HostMultipleChoiceOptionEliminated,
                tick.Outcome);
            Assert.NotEqual(
                question.HostMultipleChoiceCorrectOptionId,
                tick.RemovedHostMultipleChoiceOptionId);
            Assert.Equal(
                expectedRemaining,
                question.RemainingHostMultipleChoiceOptionIds.Count);
        }

        Assert.Equal(RuntimeQuestionStatus.ShowingAnswer, question.Status);
        Assert.Equal(QuestionBuzzerStatus.Closed, question.BuzzerStatus);
        Assert.Contains(
            question.HostMultipleChoiceCorrectOptionId,
            question.RemainingHostMultipleChoiceOptionIds);
    }

    [Fact]
    public void Recovery_preserves_eliminated_options_and_dynamic_value()
    {
        var session = CreateSession(6);
        var player = session.AddPlayer("Rose");
        session.Start();
        session.SelectQuestion(100);
        session.ActivateQuestionBuzzer(100);
        session.ClaimQuestionBuzzer(100, player.Id);
        session.SelectHostMultipleChoiceOption(100, player.Id, 202);

        var before = session.Board.Questions.Single();
        var restored = GameSession.Restore(
            session.Quiz,
            session.Settings,
            session.CaptureState());
        var after = restored.Board.Questions.Single();

        Assert.Equal(
            before.RemainingHostMultipleChoiceOptionIds,
            after.RemainingHostMultipleChoiceOptionIds);
        Assert.Equal(84, after.HostMultipleChoiceRewardPercentage);
        Assert.Equal(84, after.CorrectAnswerValue);
        Assert.DoesNotContain(202, after.RemainingHostMultipleChoiceOptionIds);
    }

    private static GameSession CreateSession(
        int optionCount,
        TimeProvider? timeProvider = null)
    {
        var round = new QuizRoundSnapshot(
            10,
            "Round",
            0,
            [CreateQuestion(optionCount)]);
        return GameSession.Create(
            new QuizSnapshot(1, "Quiz", [round]),
            timeProvider);
    }

    private static QuizQuestionSnapshot CreateQuestion(
        int optionCount,
        bool isSpecial = false) =>
        CreateQuestion(CreateOptions(optionCount), isSpecial);

    private static QuizQuestionSnapshot CreateQuestion(
        IReadOnlyList<ContentBlockSnapshot> options,
        bool isSpecial = false) =>
        new(
            100,
            20,
            0,
            100,
            isSpecial,
            "Category",
            false,
            [TextBlock(101, "Question", 0)],
            options,
            QuestionPresentationType.HostMultipleChoice);

    private static IReadOnlyList<ContentBlockSnapshot> CreateOptions(int count) =>
        Enumerable.Range(0, count)
            .Select(index => TextBlock(
                201 + index,
                index == 0 ? "Correct" : $"Wrong {index}",
                index))
            .ToArray();

    private static ContentBlockSnapshot TextBlock(
        int id,
        string text,
        int sortOrder) =>
        new(
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
}
