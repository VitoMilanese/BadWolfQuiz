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
        Assert.Equal(player.Id, session.ActivePlayerId);
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
    public void AddPlayer_accepts_new_player_after_game_has_started()
    {
        var session = CreateSession();
        session.AddPlayer("Rose");
        session.Start();

        var player = session.AddPlayer("Mickey");

        Assert.Equal("Mickey", player.Name);
        Assert.Equal(0, player.Score);
        Assert.Equal(2, session.Players.Count);
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
    public void SelectQuestion_marks_regular_question_as_selected()
    {
        var session = CreateSession();
        session.AddPlayer("Rose");
        session.Start();

        var question = session.SelectQuestion(100);

        Assert.Equal(RuntimeQuestionStatus.Selected, question.Status);
    }

    [Fact]
    public void SelectQuestion_moves_special_question_to_wager()
    {
        var session = CreateSession();
        session.AddPlayer("Rose");
        session.Start();

        var question = session.SelectQuestion(101);

        Assert.Equal(RuntimeQuestionStatus.AwaitingWager, question.Status);
    }

    [Fact]
    public void SubmitQuestionWager_activates_question_and_records_wager()
    {
        var timeProvider = new ManualTimeProvider(InitialTime);
        var session = CreateSession(timeProvider);
        var player = session.AddPlayer("Rose");
        session.Start();
        session.SelectQuestion(101);
        timeProvider.Advance(TimeSpan.FromSeconds(7));

        var question = session.SubmitQuestionWager(101, 150);

        Assert.Equal(RuntimeQuestionStatus.Active, question.Status);
        Assert.Equal(player.Id, question.SelectedByPlayerId);
        Assert.Equal(player.Id, question.Wager!.PlayerId);
        Assert.Equal(150, question.Wager.Amount);
        Assert.Equal(InitialTime.AddSeconds(7), question.Wager.SubmittedAtUtc);
    }

    [Fact]
    public void SubmitQuestionWager_rejects_regular_question()
    {
        var session = CreateSession();
        var player = session.AddPlayer("Rose");
        session.Start();
        session.SelectQuestion(100);

        Assert.Throws<GameRuleViolationException>(
            () => session.SubmitQuestionWager(100, 100));
    }

    [Fact]
    public void SubmitQuestionWager_enforces_board_based_limits()
    {
        var session = CreateSession();
        var player = session.AddPlayer("Rose");
        session.Start();
        session.SelectQuestion(101);

        var limits = session.GetQuestionWagerLimits(101);

        Assert.Equal(5, limits.Minimum);
        Assert.Equal(200, limits.Maximum);
        Assert.Throws<GameRuleViolationException>(
            () => session.SubmitQuestionWager(101, 4));
        Assert.Throws<GameRuleViolationException>(
            () => session.SubmitQuestionWager(101, 201));
    }

    [Fact]
    public void QuestionWager_maximum_uses_player_score_when_it_is_higher()
    {
        var session = CreateSession();
        var player = session.AddPlayer("Rose");
        session.AdjustPlayerScore(player.Id, 2700);
        session.Start();
        session.SelectQuestion(101);

        var limits = session.GetQuestionWagerLimits(101);
        var question = session.SubmitQuestionWager(101, 2700);

        Assert.Equal(2700, limits.Maximum);
        Assert.Equal(2700, question.Wager!.Amount);
    }

    [Fact]
    public void ActivePlayer_can_be_changed_without_changing_question_selector()
    {
        var session = CreateSession();
        var rose = session.AddPlayer("Rose");
        var mickey = session.AddPlayer("Mickey");
        session.Start();
        var question = session.SelectQuestion(101);

        session.SetActivePlayer(mickey.Id);

        Assert.Equal(mickey.Id, session.ActivePlayerId);
        Assert.Equal(rose.Id, question.SelectedByPlayerId);

        session.SubmitQuestionWager(101, 100);

        Assert.Equal(rose.Id, question.Wager!.PlayerId);
    }

    [Fact]
    public void AdjustPlayerScore_allows_negative_scores()
    {
        var session = CreateSession();
        var player = session.AddPlayer("Rose");

        session.AdjustPlayerScore(player.Id, -250);

        Assert.Equal(-250, player.Score);
    }

    [Fact]
    public void SelectQuestion_rejects_selection_before_game_starts()
    {
        var session = CreateSession();

        Assert.Throws<GameRuleViolationException>(
            () => session.SelectQuestion(100));
    }

    [Fact]
    public void SelectQuestion_rejects_another_question_until_current_is_resolved()
    {
        var session = CreateSession();
        session.AddPlayer("Rose");
        session.Start();
        session.SelectQuestion(100);

        Assert.Throws<GameRuleViolationException>(
            () => session.SelectQuestion(101));
    }

    [Fact]
    public void Create_selects_configured_number_of_random_wager_questions()
    {
        var quiz = new QuizSnapshot(
            1,
            "Random wagers",
            [
                new QuizRoundSnapshot(
                    1,
                    "Round 1",
                    0,
                    [
                        new QuizQuestionSnapshot(
                            100, 10, 0, 100, true, "Science", true),
                        new QuizQuestionSnapshot(
                            101, 10, 1, 200, false, "Science"),
                        new QuizQuestionSnapshot(
                            102, 10, 2, 300, false, "Science"),
                        new QuizQuestionSnapshot(
                            103, 10, 3, 400, false, "Science")
                    ],
                    useRandomWagerQuestions: true,
                    randomWagerQuestionCount: 2)
            ]);

        var session = GameSession.Create(quiz);

        Assert.Equal(
            2,
            session.Board.Questions.Count(question => question.IsSpecial));
        Assert.False(
            session.Board.Questions
                .Single(question => question.SourceQuestionId == 100)
                .IsSpecial);
    }

    [Fact]
    public void QuizSnapshot_rejects_random_wager_count_above_eligible_questions()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new QuizRoundSnapshot(
                1,
                "Round 1",
                0,
                [
                    new QuizQuestionSnapshot(
                        100, 10, 0, 100, false, "Science", true)
                ],
                useRandomWagerQuestions: true,
                randomWagerQuestionCount: 1));
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
                        new QuizQuestionSnapshot(100, 10, 0, 100, false, "Science"),
                        new QuizQuestionSnapshot(101, 10, 1, 200, true, "Science")
                    ])
            ]);

        return GameSession.Create(
            quiz,
            timeProvider ?? new ManualTimeProvider(InitialTime));
    }
}
