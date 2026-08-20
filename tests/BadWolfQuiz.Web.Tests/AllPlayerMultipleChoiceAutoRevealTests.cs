using BadWolfQuiz.Game.Definitions;
using BadWolfQuiz.Game.Runtime;
using BadWolfQuiz.Web.Services;

namespace BadWolfQuiz.Web.Tests;

public sealed class AllPlayerMultipleChoiceAutoRevealTests
{
    [Fact]
    public void Multiple_choice_reveals_only_after_every_current_player_submits()
    {
        var game = CreateGame(QuestionPresentationType.AllPlayerMultipleChoice);
        var rose = game.Session.AddPlayer("Rose");
        var jack = game.Session.AddPlayer("Jack");
        game.Session.Start();
        var question = game.Session.SelectQuestion(100);

        game.Session.AddQuestionAnswerHistoryEntry(
            100,
            rose.Id,
            isCorrect: true,
            value: question.Points,
            resolveQuestionIfAvailable: false);
        game.MarkPersistenceChanged();

        Assert.Equal(RuntimeQuestionStatus.Active, question.Status);

        game.Session.AddQuestionAnswerHistoryEntry(
            100,
            jack.Id,
            isCorrect: false,
            value: 0,
            resolveQuestionIfAvailable: false);
        game.MarkPersistenceChanged();

        Assert.Equal(RuntimeQuestionStatus.ShowingAnswer, question.Status);
        Assert.Equal(GameTimerStatus.Stopped, game.Session.Timer.Status);
        Assert.Equal(GameTimerStatus.Stopped, game.Session.AnswerTimer.Status);
    }

    [Fact]
    public void Text_question_remains_host_controlled_after_every_player_submits()
    {
        var game = CreateGame(QuestionPresentationType.AllPlayerText);
        var rose = game.Session.AddPlayer("Rose");
        var jack = game.Session.AddPlayer("Jack");
        game.Session.Start();
        var question = game.Session.SelectQuestion(100);

        game.Session.AddQuestionAnswerHistoryEntry(
            100,
            rose.Id,
            isCorrect: false,
            value: 0,
            resolveQuestionIfAvailable: false);
        game.MarkPersistenceChanged();
        game.Session.AddQuestionAnswerHistoryEntry(
            100,
            jack.Id,
            isCorrect: false,
            value: 0,
            resolveQuestionIfAvailable: false);
        game.MarkPersistenceChanged();

        Assert.Equal(RuntimeQuestionStatus.Active, question.Status);
    }

    [Fact]
    public void Wager_multiple_choice_waits_only_for_players_who_submitted_wagers()
    {
        var game = CreateGame(
            QuestionPresentationType.AllPlayerMultipleChoice,
            isSpecial: true);
        var rose = game.Session.AddPlayer("Rose");
        var jack = game.Session.AddPlayer("Jack");
        game.Session.Start();
        game.Session.SelectQuestion(100);
        game.Session.SubmitAllPlayerQuestionWager(100, rose.Id, 80);
        game.Session.SubmitAllPlayerQuestionWager(100, jack.Id, 25);
        var question = game.Session.StartAllPlayerQuestionAfterWagers(100);

        _ = game.Session.AddPlayer("Late player");

        game.Session.AddQuestionAnswerHistoryEntry(
            100,
            rose.Id,
            isCorrect: true,
            value: 80,
            resolveQuestionIfAvailable: false);
        game.MarkPersistenceChanged();

        Assert.Equal(RuntimeQuestionStatus.Active, question.Status);

        game.Session.AddQuestionAnswerHistoryEntry(
            100,
            jack.Id,
            isCorrect: false,
            value: 25,
            resolveQuestionIfAvailable: false);
        game.MarkPersistenceChanged();

        Assert.Equal(RuntimeQuestionStatus.ShowingAnswer, question.Status);
        Assert.Equal(GameTimerStatus.Stopped, game.Session.AnswerTimer.Status);
    }

    private static GameSessionRegistration CreateGame(
        QuestionPresentationType type,
        bool isSpecial = false)
    {
        var answers = type == QuestionPresentationType.AllPlayerMultipleChoice
            ? new[] { TextBlock(10, "A"), TextBlock(11, "B") }
            : new[] { TextBlock(10, "Answer") };
        var question = new QuizQuestionSnapshot(
            100,
            10,
            0,
            200,
            isSpecial,
            "Category",
            false,
            [TextBlock(1, "Question")],
            answers,
            type);
        var quiz = new QuizSnapshot(
            1,
            "All players",
            [new QuizRoundSnapshot(1, "Round", 0, [question])]);
        return new GameSessionRegistration(
            "ABCDE",
            GameSession.Create(quiz),
            "host");
    }

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
}
