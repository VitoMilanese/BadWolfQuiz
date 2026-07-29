using BadWolfQuiz.Game.Definitions;
using BadWolfQuiz.Game.Runtime;

namespace BadWolfQuiz.Game.Tests;

public sealed class GameSessionTests
{
    private static readonly DateTimeOffset InitialTime =
        new(2026, 7, 29, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_builds_lobby_session_and_runtime_board()
    {
        var session = CreateSession();

        Assert.Equal(GameSessionStatus.Lobby, session.Status);
        Assert.Empty(session.Players);
        Assert.Equal(2, session.Board.Questions.Count);
        Assert.All(
            session.Board.Questions,
            question => Assert.Equal(RuntimeQuestionStatus.Available, question.Status));
    }

    [Fact]
    public void AddPlayer_trims_name_and_adds_player_to_lobby()
    {
        var session = CreateSession();

        var player = session.AddPlayer("  Rose  ");

        Assert.Equal("Rose", player.Name);
        Assert.Equal(InitialTime, player.JoinedAtUtc);
        Assert.Same(player, Assert.Single(session.Players));
    }

    [Fact]
    public void AddPlayer_rejects_duplicate_name_ignoring_case()
    {
        var session = CreateSession();
        session.AddPlayer("Rose");

        var exception = Assert.Throws<GameRuleViolationException>(
            () => session.AddPlayer("rose"));

        Assert.Contains("already joined", exception.Message);
    }

    [Fact]
    public void Start_rejects_session_without_players()
    {
        var session = CreateSession();

        Assert.Throws<GameRuleViolationException>(() => session.Start());
    }

    [Fact]
    public void Start_moves_lobby_to_running()
    {
        var timeProvider = new ManualTimeProvider(InitialTime);
        var session = CreateSession(timeProvider);
        session.AddPlayer("Rose");
        timeProvider.Advance(TimeSpan.FromSeconds(5));

        session.Start();

        Assert.Equal(GameSessionStatus.Running, session.Status);
        Assert.Equal(InitialTime.AddSeconds(5), session.StartedAtUtc);
    }

    [Fact]
    public void AddPlayer_rejects_join_after_game_has_started()
    {
        var session = CreateSession();
        session.AddPlayer("Rose");
        session.Start();

        Assert.Throws<GameRuleViolationException>(() => session.AddPlayer("Mickey"));
    }

    [Fact]
    public void Start_rejects_second_start()
    {
        var session = CreateSession();
        session.AddPlayer("Rose");
        session.Start();

        Assert.Throws<GameRuleViolationException>(() => session.Start());
    }

    [Fact]
    public void QuizSnapshot_copies_source_collections()
    {
        var questions = new List<QuizQuestionSnapshot>
        {
            new(100, 10, 0, 100, false)
        };
        var round = new QuizRoundSnapshot(1, "Round 1", 0, questions);
        var rounds = new List<QuizRoundSnapshot> { round };
        var quiz = new QuizSnapshot(1, "Quiz", rounds);

        questions.Add(new QuizQuestionSnapshot(101, 10, 1, 200, false));
        rounds.Clear();

        Assert.Single(quiz.Rounds);
        Assert.Single(quiz.Rounds[0].Questions);
    }

    private static GameSession CreateSession(ManualTimeProvider? timeProvider = null)
    {
        var quiz = new QuizSnapshot(
            1,
            "Test Quiz",
            [
                new QuizRoundSnapshot(
                    1,
                    "Round 1",
                    0,
                    [
                        new QuizQuestionSnapshot(100, 10, 0, 100, false),
                        new QuizQuestionSnapshot(101, 10, 1, 200, true)
                    ])
            ]);

        return GameSession.Create(
            quiz,
            timeProvider ?? new ManualTimeProvider(InitialTime));
    }
}
