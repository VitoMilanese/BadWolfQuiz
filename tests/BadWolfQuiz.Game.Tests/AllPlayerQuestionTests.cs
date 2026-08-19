using BadWolfQuiz.Game.Definitions;
using BadWolfQuiz.Game.Runtime;

namespace BadWolfQuiz.Game.Tests;

public sealed class AllPlayerQuestionTests
{
    [Fact]
    public void Text_mode_requires_one_non_empty_text_answer()
    {
        var question = CreateQuestion(
            QuestionPresentationType.AllPlayerText,
            [TextBlock(10, "Kyiv")]);

        Assert.False(question.IsSpecial);
        Assert.False(question.ExcludeFromRandomWagerSelection);
        Assert.True(question.IsEligibleForRandomWagerSelection);
        Assert.Single(question.AnswerBlocks);

        Assert.Throws<ArgumentException>(() => CreateQuestion(
            QuestionPresentationType.AllPlayerText,
            [TextBlock(10, "Kyiv"), TextBlock(11, "Lviv")]));
    }

    [Fact]
    public void Multiple_choice_accepts_two_to_four_text_or_image_options()
    {
        var question = CreateQuestion(
            QuestionPresentationType.AllPlayerMultipleChoice,
            [TextBlock(10, "Red"), ImageBlock(11)]);

        Assert.False(question.IsSpecial);
        Assert.True(question.IsEligibleForRandomWagerSelection);
        Assert.Equal(2, question.AnswerBlocks.Count);
        Assert.Equal(ContentBlockKind.Image, question.AnswerBlocks[1].Kind);

        Assert.Throws<ArgumentException>(() => CreateQuestion(
            QuestionPresentationType.AllPlayerMultipleChoice,
            [TextBlock(10, "Red")]));
        Assert.Throws<ArgumentException>(() => CreateQuestion(
            QuestionPresentationType.AllPlayerMultipleChoice,
            [TextBlock(10, "Red"), TextBlock(11, "red")]));
        Assert.Throws<ArgumentException>(() => CreateQuestion(
            QuestionPresentationType.AllPlayerMultipleChoice,
            [TextBlock(10, "Red"), AudioBlock(11)]));
    }

    [Fact]
    public void Selecting_non_wager_all_player_question_does_not_open_buzzer()
    {
        var session = CreateSession(QuestionPresentationType.AllPlayerText);
        session.AddPlayer("Rose");
        session.Start();

        var question = session.SelectQuestion(100);

        Assert.True(question.IsAllPlayerQuestion);
        Assert.False(question.IsSpecial);
        Assert.Equal(RuntimeQuestionStatus.Active, question.Status);
        Assert.Equal(QuestionBuzzerStatus.Closed, question.BuzzerStatus);
    }

    [Fact]
    public void Every_player_submits_an_individual_wager_before_host_reveals_question()
    {
        var session = CreateSession(
            QuestionPresentationType.AllPlayerText,
            isSpecial: true);
        var rose = session.AddPlayer("Rose");
        var jack = session.AddPlayer("Jack");
        session.Start();

        var selected = session.SelectQuestion(100);
        Assert.Equal(RuntimeQuestionStatus.AwaitingWager, selected.Status);

        session.SubmitAllPlayerQuestionWager(100, rose.Id, 75);
        Assert.Equal(RuntimeQuestionStatus.AwaitingWager, selected.Status);
        Assert.Single(selected.AllPlayerWagers);

        session.SubmitAllPlayerQuestionWager(100, jack.Id, 35);
        Assert.Equal(RuntimeQuestionStatus.AwaitingWager, selected.Status);
        Assert.Equal(2, selected.AllPlayerWagers.Count);

        var active = session.StartAllPlayerQuestionAfterWagers(100);

        Assert.Equal(RuntimeQuestionStatus.Active, active.Status);
        Assert.Equal(75, active.AllPlayerWagers.Single(item =>
            item.PlayerId == rose.Id).Amount);
        Assert.Equal(35, active.AllPlayerWagers.Single(item =>
            item.PlayerId == jack.Id).Amount);
        Assert.Null(active.Wager);
        Assert.Equal(GameTimerStatus.Running, session.AnswerTimer.Status);
    }

    [Fact]
    public void Host_cannot_reveal_wager_question_before_every_player_has_wagered()
    {
        var session = CreateSession(
            QuestionPresentationType.AllPlayerMultipleChoice,
            isSpecial: true);
        var rose = session.AddPlayer("Rose");
        session.AddPlayer("Jack");
        session.Start();
        session.SelectQuestion(100);
        session.SubmitAllPlayerQuestionWager(100, rose.Id, 5);

        Assert.Throws<GameRuleViolationException>(() =>
            session.StartAllPlayerQuestionAfterWagers(100));
    }

    [Fact]
    public void Manual_wager_timer_starts_only_when_host_requests_it()
    {
        var settings = new GameSessionSettings(
            GameSession.DefaultBuzzerDuration,
            GameSession.DefaultAnswerDuration,
            GamePhaseStartMode.Manual,
            GamePhaseStartMode.Manual,
            allowNegativeScoreFinalPlayers: true);
        var session = CreateSession(
            QuestionPresentationType.AllPlayerText,
            isSpecial: true,
            settings: settings);
        var rose = session.AddPlayer("Rose");
        session.Start();
        session.SelectQuestion(100);
        session.SubmitAllPlayerQuestionWager(100, rose.Id, 5);
        session.StartAllPlayerQuestionAfterWagers(100);

        Assert.Equal(GameTimerStatus.Stopped, session.AnswerTimer.Status);

        session.StartWagerAnswerTimer(100);

        Assert.Equal(GameTimerStatus.Running, session.AnswerTimer.Status);
    }

    [Fact]
    public void Random_wager_selection_can_choose_all_player_question()
    {
        var question = CreateQuestion(
            QuestionPresentationType.AllPlayerMultipleChoice,
            [TextBlock(10, "A"), TextBlock(11, "B")]);
        var quiz = new QuizSnapshot(
            1,
            "Random all-player wager",
            [new QuizRoundSnapshot(
                1,
                "Round",
                0,
                [question],
                useRandomWagerQuestions: true,
                randomWagerQuestionCount: 1)]);
        var session = GameSession.Create(quiz);

        var runtimeQuestion = Assert.Single(session.Board.Questions);

        Assert.True(runtimeQuestion.IsAllPlayerQuestion);
        Assert.True(runtimeQuestion.IsSpecial);
    }

    [Fact]
    public void Wager_scoring_uses_each_players_own_stake()
    {
        var session = CreateSession(
            QuestionPresentationType.AllPlayerMultipleChoice,
            isSpecial: true);
        var rose = session.AddPlayer("Rose");
        var jack = session.AddPlayer("Jack");
        session.Start();
        session.SelectQuestion(100);
        session.SubmitAllPlayerQuestionWager(100, rose.Id, 80);
        session.SubmitAllPlayerQuestionWager(100, jack.Id, 25);
        var question = session.StartAllPlayerQuestionAfterWagers(100);

        var correct = session.AddQuestionAnswerHistoryEntry(
            100,
            rose.Id,
            isCorrect: true,
            value: question.AllPlayerWagers.Single(item =>
                item.PlayerId == rose.Id).Amount,
            resolveQuestionIfAvailable: false);
        var incorrect = session.AddQuestionAnswerHistoryEntry(
            100,
            jack.Id,
            isCorrect: false,
            value: question.AllPlayerWagers.Single(item =>
                item.PlayerId == jack.Id).Amount,
            resolveQuestionIfAvailable: false);

        Assert.Equal(80, correct.ScoreDelta);
        Assert.Equal(-25, incorrect.ScoreDelta);
        Assert.Equal(80, rose.Score);
        Assert.Equal(-25, jack.Score);
    }

    [Fact]
    public void Non_wager_all_player_incorrect_answer_does_not_deduct_points()
    {
        var session = CreateSession(
            QuestionPresentationType.AllPlayerMultipleChoice);
        var rose = session.AddPlayer("Rose");
        session.Start();
        session.SelectQuestion(100);

        var incorrect = session.AddQuestionAnswerHistoryEntry(
            100,
            rose.Id,
            isCorrect: false,
            value: 0,
            resolveQuestionIfAvailable: false);

        Assert.Equal(0, incorrect.ScoreDelta);
        Assert.Equal(0, rose.Score);
    }

    [Fact]
    public void Partial_player_wagers_survive_session_state_restore()
    {
        var session = CreateSession(
            QuestionPresentationType.AllPlayerText,
            isSpecial: true);
        var rose = session.AddPlayer("Rose");
        session.AddPlayer("Jack");
        session.Start();
        session.SelectQuestion(100);
        session.SubmitAllPlayerQuestionWager(100, rose.Id, 45);

        var restored = GameSession.Restore(
            session.Quiz,
            session.Settings,
            session.CaptureState());
        var question = restored.Board.Questions.Single(item =>
            item.SourceQuestionId == 100);

        var wager = Assert.Single(question.AllPlayerWagers);
        Assert.Equal(rose.Id, wager.PlayerId);
        Assert.Equal(45, wager.Amount);
        Assert.Equal(RuntimeQuestionStatus.AwaitingWager, question.Status);
    }

    [Fact]
    public void Text_answer_can_be_recorded_for_zero_points_and_judged_later()
    {
        var session = CreateSession(QuestionPresentationType.AllPlayerText);
        var rose = session.AddPlayer("Rose");
        session.Start();
        session.SelectQuestion(100);

        var submitted = session.AddQuestionAnswerHistoryEntry(
            100,
            rose.Id,
            isCorrect: false,
            value: 0,
            resolveQuestionIfAvailable: false);

        var judged = session.UpdateQuestionAnswerHistoryEntry(
            100,
            submitted.Id,
            rose.Id,
            isCorrect: true,
            value: 200);

        Assert.True(judged.IsCorrect);
        Assert.Equal(200, judged.ScoreDelta);
        Assert.Equal(200, rose.Score);
    }

    private static GameSession CreateSession(
        QuestionPresentationType type,
        bool isSpecial = false,
        bool excludeFromRandomWagerSelection = false,
        GameSessionSettings? settings = null)
    {
        var answers = type == QuestionPresentationType.AllPlayerMultipleChoice
            ? new[] { TextBlock(10, "A"), TextBlock(11, "B") }
            : new[] { TextBlock(10, "Answer") };
        var question = CreateQuestion(
            type,
            answers,
            isSpecial: isSpecial,
            excludeFromRandomWagerSelection: excludeFromRandomWagerSelection);
        var quiz = new QuizSnapshot(
            1,
            "All players",
            [new QuizRoundSnapshot(1, "Round", 0, [question])]);
        return GameSession.Create(
            quiz,
            settings ?? GameSessionSettings.Default);
    }

    private static QuizQuestionSnapshot CreateQuestion(
        QuestionPresentationType type,
        IReadOnlyList<ContentBlockSnapshot> answers,
        IReadOnlyList<ContentBlockSnapshot>? questionBlocks = null,
        bool isSpecial = false,
        bool excludeFromRandomWagerSelection = false) => new(
            100,
            10,
            0,
            200,
            isSpecial,
            "Category",
            excludeFromRandomWagerSelection,
            questionBlocks ?? [TextBlock(1, "Question")],
            answers,
            type);

    private static ContentBlockSnapshot TextBlock(int id, string text) => new(
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
        id,
        false);

    private static ContentBlockSnapshot ImageBlock(int id) => new(
        id,
        ContentBlockKind.Image,
        null,
        null,
        null,
        null,
        null,
        [1, 2, 3],
        "image/png",
        $"option-{id}.png",
        id,
        false);

    private static ContentBlockSnapshot AudioBlock(int id) => new(
        id,
        ContentBlockKind.Audio,
        null,
        null,
        null,
        null,
        null,
        [1, 2, 3],
        "audio/mpeg",
        $"audio-{id}.mp3",
        id,
        true);
}
