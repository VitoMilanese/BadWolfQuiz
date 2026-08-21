using BadWolfQuiz.Game.Definitions;
using BadWolfQuiz.Game.Runtime;

namespace BadWolfQuiz.Game.Tests;

public sealed class QuestionBuzzerModeTests
{
    [Theory]
    [InlineData(QuestionPresentationType.Standard)]
    [InlineData(QuestionPresentationType.FourClues)]
    [InlineData(QuestionPresentationType.HostMultipleChoice)]
    public void Immediately_opens_buzzer_for_every_buzzer_based_type(
        QuestionPresentationType presentationType)
    {
        var session = CreateSession(
            QuestionBuzzerMode.Immediately,
            presentationType);

        var question = session.SelectQuestion(100);

        Assert.Equal(QuestionBuzzerStatus.Open, question.BuzzerStatus);
    }

    [Theory]
    [InlineData(QuestionBuzzerMode.Manual)]
    [InlineData(QuestionBuzzerMode.Disabled)]
    public void Non_automatic_modes_leave_initial_buzzer_inactive(
        QuestionBuzzerMode mode)
    {
        var session = CreateSession(mode);

        var question = session.SelectQuestion(100);

        Assert.Equal(QuestionBuzzerStatus.Inactive, question.BuzzerStatus);
    }

    [Fact]
    public void Legacy_disabled_mode_is_treated_as_manual_for_buzzer_questions()
    {
        var session = CreateSession(QuestionBuzzerMode.Disabled);
        session.SelectQuestion(100);

        var policy = QuestionBuzzerPolicy.Get(session, 100);

        Assert.Equal(QuestionBuzzerMode.Manual, policy.Mode);
    }

    [Fact]
    public void After_media_waits_when_initial_media_exists()
    {
        var session = CreateSession(
            QuestionBuzzerMode.AfterMedia,
            hasInitialMedia: true);

        var question = session.SelectQuestion(100);

        Assert.Equal(QuestionBuzzerStatus.Inactive, question.BuzzerStatus);
    }

    [Fact]
    public void After_media_opens_immediately_when_question_has_no_initial_media()
    {
        var session = CreateSession(QuestionBuzzerMode.AfterMedia);

        var question = session.SelectQuestion(100);

        Assert.Equal(QuestionBuzzerStatus.Open, question.BuzzerStatus);
    }

    [Theory]
    [InlineData(0, QuestionBuzzerStatus.Open)]
    [InlineData(5, QuestionBuzzerStatus.Inactive)]
    public void After_delay_respects_zero_vs_positive_delay(
        int delaySeconds,
        QuestionBuzzerStatus expected)
    {
        var session = CreateSession(
            QuestionBuzzerMode.AfterDelay,
            buzzDelaySeconds: delaySeconds);

        var question = session.SelectQuestion(100);

        Assert.Equal(expected, question.BuzzerStatus);
    }

    [Theory]
    [InlineData(GamePhaseStartMode.Automatic, QuestionBuzzerStatus.Open)]
    [InlineData(GamePhaseStartMode.Manual, QuestionBuzzerStatus.Inactive)]
    public void Use_game_setting_preserves_legacy_runtime_fallback(
        GamePhaseStartMode startMode,
        QuestionBuzzerStatus expected)
    {
        var session = CreateSession(
            QuestionBuzzerMode.UseGameSetting,
            gameStartMode: startMode);

        var question = session.SelectQuestion(100);

        Assert.Equal(expected, question.BuzzerStatus);
    }

    [Fact]
    public void Wager_question_never_uses_normal_buzzer_policy()
    {
        var session = CreateSession(
            QuestionBuzzerMode.Immediately,
            isSpecial: true);

        var question = session.SelectQuestion(100);

        Assert.NotEqual(QuestionBuzzerStatus.Open, question.BuzzerStatus);
        Assert.Equal(RuntimeQuestionStatus.AwaitingWager, question.Status);
    }

    private static GameSession CreateSession(
        QuestionBuzzerMode mode,
        QuestionPresentationType presentationType =
            QuestionPresentationType.Standard,
        bool hasInitialMedia = false,
        int buzzDelaySeconds = 0,
        bool isSpecial = false,
        GamePhaseStartMode gameStartMode = GamePhaseStartMode.Manual)
    {
        var questionBlocks = presentationType == QuestionPresentationType.FourClues
            ? Enumerable.Range(0, 4)
                .Select(index => index == 0 && hasInitialMedia
                    ? Audio(10 + index, index)
                    : Text(10 + index, index, $"Clue {index + 1}"))
                .ToArray()
            : new[]
            {
                hasInitialMedia
                    ? Audio(10, 0)
                    : Text(10, 0, "Question")
            };

        IReadOnlyList<ContentBlockSnapshot> answerBlocks =
            presentationType == QuestionPresentationType.HostMultipleChoice
                ? Enumerable.Range(0, 4)
                    .Select(index => Text(
                        20 + index,
                        index,
                        $"Option {index + 1}"))
                    .ToArray()
                : new[] { Text(20, 0, "Answer") };

        var definition = new QuizQuestionSnapshot(
            100,
            200,
            0,
            100,
            isSpecial,
            "Category",
            false,
            questionBlocks,
            answerBlocks,
            presentationType,
            mode,
            buzzDelaySeconds);
        var round = new QuizRoundSnapshot(
            300,
            "Round",
            0,
            new[] { definition });
        var quiz = new QuizSnapshot(400, "Quiz", new[] { round });
        var settings = new GameSessionSettings(
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(10),
            gameStartMode,
            GamePhaseStartMode.Automatic);
        var session = GameSession.Create(quiz, settings);
        session.AddPlayer("Player");
        session.Start();
        return session;
    }

    private static ContentBlockSnapshot Text(
        int id,
        int sortOrder,
        string text) => new(
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

    private static ContentBlockSnapshot Audio(
        int id,
        int sortOrder) => new(
            id,
            ContentBlockKind.Audio,
            null,
            null,
            null,
            null,
            null,
            new byte[] { 1 },
            "audio/mpeg",
            "audio.mp3",
            sortOrder,
            true);
}
