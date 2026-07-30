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
        Assert.Equal(GameTimerStatus.Running, session.AnswerTimer.Status);
        Assert.Equal(
            GameSession.DefaultAnswerDuration,
            session.AnswerTimer.Remaining);
    }

    [Fact]
    public void Wager_answer_timer_expiration_records_incorrect_answer()
    {
        var timeProvider = new ManualTimeProvider(InitialTime);
        var session = CreateSession(timeProvider);
        var player = session.AddPlayer("Rose");
        session.Start();
        var question = session.SelectQuestion(101);
        session.SubmitQuestionWager(101, 150);
        timeProvider.Advance(GameSession.DefaultAnswerDuration);

        var outcome = session.ProcessQuestionTimers();

        Assert.Equal(QuestionTimerOutcome.AnswerExpired, outcome);
        Assert.Equal(-150, player.Score);
        Assert.Equal(RuntimeQuestionStatus.ShowingAnswer, question.Status);
        Assert.Equal(GameTimerStatus.Stopped, session.AnswerTimer.Status);
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
    public void ActivePlayer_can_be_changed_without_changing_regular_question_selector()
    {
        var session = CreateSession();
        var rose = session.AddPlayer("Rose");
        var mickey = session.AddPlayer("Mickey");
        session.Start();
        var question = session.SelectQuestion(100);

        session.SetActivePlayer(mickey.Id);

        Assert.Equal(mickey.Id, session.ActivePlayerId);
        Assert.Equal(rose.Id, question.SelectedByPlayerId);
    }

    [Fact]
    public void ActivePlayer_cannot_change_while_wager_question_is_in_progress()
    {
        var session = CreateSession();
        var rose = session.AddPlayer("Rose");
        var mickey = session.AddPlayer("Mickey");
        session.Start();
        session.SelectQuestion(101);

        Assert.True(session.IsActivePlayerChangeLocked);
        Assert.Throws<GameRuleViolationException>(
            () => session.SetActivePlayer(mickey.Id));
        Assert.Throws<GameRuleViolationException>(
            () => session.SelectRandomActivePlayer());

        session.SubmitQuestionWager(101, 100);

        Assert.Throws<GameRuleViolationException>(
            () => session.SetActivePlayer(mickey.Id));
        Assert.Equal(rose.Id, session.ActivePlayerId);
    }


    [Fact]
    public void ActivateQuestionBuzzer_opens_regular_question_buzzer()
    {
        var session = CreateSession();
        session.AddPlayer("Rose");
        session.Start();
        var question = session.SelectQuestion(100);

        session.ActivateQuestionBuzzer(100);

        Assert.Equal(RuntimeQuestionStatus.Active, question.Status);
        Assert.Equal(QuestionBuzzerStatus.Open, question.BuzzerStatus);
        Assert.Null(question.AnsweringPlayerId);
    }

    [Fact]
    public void ClaimQuestionBuzzer_accepts_first_eligible_player()
    {
        var session = CreateSession();
        var rose = session.AddPlayer("Rose");
        var mickey = session.AddPlayer("Mickey");
        session.Start();
        var question = session.SelectQuestion(100);
        session.ActivateQuestionBuzzer(100);

        session.ClaimQuestionBuzzer(100, rose.Id);

        Assert.Equal(QuestionBuzzerStatus.Claimed, question.BuzzerStatus);
        Assert.Equal(rose.Id, question.AnsweringPlayerId);
        Assert.Throws<GameRuleViolationException>(
            () => session.ClaimQuestionBuzzer(100, mickey.Id));
    }

    [Fact]
    public void Wager_question_rejects_buzzer_activation()
    {
        var session = CreateSession();
        session.AddPlayer("Rose");
        session.Start();
        session.SelectQuestion(101);

        Assert.Throws<GameRuleViolationException>(
            () => session.ActivateQuestionBuzzer(101));
    }

    [Fact]
    public void Buzzer_and_answer_timers_preserve_the_buzzer_window()
    {
        var timeProvider = new ManualTimeProvider(InitialTime);
        var session = CreateSession(timeProvider);
        var rose = session.AddPlayer("Rose");
        session.Start();
        session.SelectQuestion(100);

        session.ActivateQuestionBuzzer(100);
        timeProvider.Advance(TimeSpan.FromSeconds(7));
        session.ClaimQuestionBuzzer(100, rose.Id);

        Assert.True(session.Timer.IsPaused);
        Assert.Equal(TimeSpan.FromSeconds(23), session.Timer.Remaining);
        Assert.Equal(GameTimerStatus.Running, session.AnswerTimer.Status);

        timeProvider.Advance(GameSession.DefaultAnswerDuration);

        Assert.Equal(
            QuestionTimerOutcome.AnswerExpired,
            session.ProcessQuestionTimers());
        Assert.Equal(-100, rose.Score);
        Assert.Equal(GameTimerStatus.Running, session.Timer.Status);
        Assert.Equal(TimeSpan.FromSeconds(23), session.Timer.Remaining);
    }

    [Fact]
    public void Active_question_timer_can_be_paused_and_resumed()
    {
        var timeProvider = new ManualTimeProvider(InitialTime);
        var session = CreateSession(timeProvider);
        session.AddPlayer("Rose");
        session.Start();
        session.SelectQuestion(100);
        session.ActivateQuestionBuzzer(100);
        timeProvider.Advance(TimeSpan.FromSeconds(4));

        var paused = session.PauseQuestionTimer();
        timeProvider.Advance(TimeSpan.FromSeconds(5));

        Assert.Same(session.Timer, paused);
        Assert.Equal(TimeSpan.FromSeconds(26), session.Timer.Remaining);

        var resumed = session.ResumeQuestionTimer();
        timeProvider.Advance(TimeSpan.FromSeconds(1));

        Assert.Same(session.Timer, resumed);
        Assert.Equal(TimeSpan.FromSeconds(25), session.Timer.Remaining);
    }

    [Fact]
    public void Buzzer_timer_expiration_shows_the_answer()
    {
        var timeProvider = new ManualTimeProvider(InitialTime);
        var session = CreateSession(timeProvider);
        session.AddPlayer("Rose");
        session.Start();
        var question = session.SelectQuestion(100);
        session.ActivateQuestionBuzzer(100);
        timeProvider.Advance(GameSession.DefaultBuzzerDuration);

        var outcome = session.ProcessQuestionTimers();

        Assert.Equal(QuestionTimerOutcome.BuzzerExpired, outcome);
        Assert.Equal(RuntimeQuestionStatus.ShowingAnswer, question.Status);
        Assert.Equal(GameTimerStatus.Stopped, session.Timer.Status);
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
    public void JudgeQuestionAnswer_applies_regular_scores_and_transfers_selection()
    {
        var session = CreateSession();
        var rose = session.AddPlayer("Rose");
        var mickey = session.AddPlayer("Mickey");
        session.Start();
        var question = session.SelectQuestion(100);

        var wrong = session.JudgeQuestionAnswer(100, rose.Id, false);
        var correct = session.JudgeQuestionAnswer(100, mickey.Id, true);

        Assert.Equal(-100, wrong.ScoreDelta);
        Assert.Equal(100, correct.ScoreDelta);
        Assert.Equal(-100, rose.Score);
        Assert.Equal(100, mickey.Score);
        Assert.Equal(mickey.Id, session.ActivePlayerId);
        Assert.Equal(RuntimeQuestionStatus.ShowingAnswer, question.Status);
        Assert.Equal(2, question.AnswerAttempts.Count);
    }

    [Fact]
    public void JudgeQuestionAnswer_rejects_duplicate_regular_attempt()
    {
        var session = CreateSession();
        var player = session.AddPlayer("Rose");
        session.Start();
        session.SelectQuestion(100);
        session.JudgeQuestionAnswer(100, player.Id, false);

        Assert.Throws<GameRuleViolationException>(
            () => session.JudgeQuestionAnswer(100, player.Id, false));
        Assert.Equal(-100, player.Score);
    }

    [Fact]
    public void ResolveQuestionWithoutCorrectAnswer_keeps_active_player()
    {
        var session = CreateSession();
        var rose = session.AddPlayer("Rose");
        session.AddPlayer("Mickey");
        session.Start();
        var question = session.SelectQuestion(100);
        session.JudgeQuestionAnswer(100, rose.Id, false);

        session.ResolveQuestionWithoutCorrectAnswer(100);

        Assert.Equal(RuntimeQuestionStatus.ShowingAnswer, question.Status);
        Assert.Equal(rose.Id, session.ActivePlayerId);
    }

    [Theory]
    [InlineData(true, 150)]
    [InlineData(false, -150)]
    public void JudgeQuestionAnswer_applies_wager_and_resolves(
        bool isCorrect,
        int expectedScore)
    {
        var session = CreateSession();
        var player = session.AddPlayer("Rose");
        session.Start();
        var question = session.SelectQuestion(101);
        session.SubmitQuestionWager(101, 150);

        var attempt = session.JudgeQuestionAnswer(
            101,
            player.Id,
            isCorrect);

        Assert.Equal(expectedScore, attempt.ScoreDelta);
        Assert.Equal(expectedScore, player.Score);
        Assert.Equal(RuntimeQuestionStatus.ShowingAnswer, question.Status);
        Assert.False(session.IsActivePlayerChangeLocked);
    }

    [Fact]
    public void JudgeQuestionAnswer_rejects_other_player_for_wager_question()
    {
        var session = CreateSession();
        session.AddPlayer("Rose");
        var mickey = session.AddPlayer("Mickey");
        session.Start();
        session.SelectQuestion(101);
        session.SubmitQuestionWager(101, 100);

        Assert.Throws<GameRuleViolationException>(
            () => session.JudgeQuestionAnswer(101, mickey.Id, true));
        Assert.Equal(0, mickey.Score);
    }

    [Fact]
    public void CloseQuestionAnswer_returns_to_resolved_board_state()
    {
        var session = CreateSession();
        var player = session.AddPlayer("Rose");
        session.Start();
        var question = session.SelectQuestion(100);
        session.JudgeQuestionAnswer(100, player.Id, true);

        session.CloseQuestionAnswer(100);

        Assert.Equal(RuntimeQuestionStatus.Resolved, question.Status);
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
    public void QuizSnapshot_allows_zero_random_wager_questions()
    {
        var round = new QuizRoundSnapshot(
            1,
            "Round 1",
            0,
            [
                new QuizQuestionSnapshot(
                    100, 10, 0, 100, false, "Science")
            ],
            useRandomWagerQuestions: true,
            randomWagerQuestionCount: 0);

        var session = GameSession.Create(
            new QuizSnapshot(1, "No wagers", [round]));

        Assert.Empty(session.Board.Questions.Where(question => question.IsSpecial));
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

    [Fact]
    public void AdvanceToNextRound_requires_completed_current_round()
    {
        var session = CreateMultiRoundSession();
        session.AddPlayer("Rose");
        session.Start();

        Assert.Throws<GameRuleViolationException>(
            session.AdvanceToNextRound);
    }

    [Fact]
    public void AdvanceToNextRound_selects_lowest_score_player()
    {
        var session = CreateMultiRoundSession();
        var rose = session.AddPlayer("Rose");
        var mickey = session.AddPlayer("Mickey");
        session.AdjustPlayerScore(rose.Id, 300);
        session.AdjustPlayerScore(mickey.Id, -100);
        session.Start();
        session.SelectQuestion(100);
        session.JudgeQuestionAnswer(100, rose.Id, true);
        session.CloseQuestionAnswer(100);

        var round = session.AdvanceToNextRound();

        Assert.Equal(2, session.CurrentRoundNumber);
        Assert.Equal(2, round.SourceRoundId);
        Assert.Equal(mickey.Id, session.ActivePlayerId);
        Assert.Throws<GameRuleViolationException>(
            () => session.SelectQuestion(100));
        Assert.Equal(
            RuntimeQuestionStatus.Selected,
            session.SelectQuestion(200).Status);
    }

    [Fact]
    public void AdvanceToNextRound_breaks_score_tie_by_weakest_round_results()
    {
        var timeProvider = new ManualTimeProvider(InitialTime);
        var session = CreateMultiRoundSession(timeProvider);
        var rose = session.AddPlayer("Rose");
        timeProvider.Advance(TimeSpan.FromSeconds(1));
        var mickey = session.AddPlayer("Mickey");
        session.Start();
        session.SelectQuestion(100);
        session.JudgeQuestionAnswer(100, rose.Id, true);
        session.CloseQuestionAnswer(100);
        session.AdjustPlayerScore(rose.Id, -100);

        session.AdvanceToNextRound();

        Assert.Equal(mickey.Id, session.ActivePlayerId);
    }

    [Fact]
    public void Current_round_standings_use_full_tie_breaking_before_last_round()
    {
        var session = CreateMultiRoundSession();
        var rose = session.AddPlayer("Rose");
        var mickey = session.AddPlayer("Mickey");
        session.Start();
        session.SelectQuestion(100);
        session.JudgeQuestionAnswer(100, rose.Id, true);
        session.CloseQuestionAnswer(100);
        session.AdjustPlayerScore(rose.Id, -100);

        var standings = session.GetCurrentRoundStandings();

        Assert.True(session.HasNextRound);
        Assert.Equal(rose.Id, standings[0].PlayerId);
        Assert.Equal(1, standings[0].TotalCorrectAnswers);
        Assert.Equal(mickey.Id, standings[1].PlayerId);
    }

    [Fact]
    public void Final_standings_use_last_round_score_gain_after_score()
    {
        var session = CreateMultiRoundSession();
        var rose = session.AddPlayer("Rose");
        var mickey = session.AddPlayer("Mickey");
        session.Start();
        session.SelectQuestion(100);
        session.JudgeQuestionAnswer(100, rose.Id, true);
        session.CloseQuestionAnswer(100);
        session.AdvanceToNextRound();
        session.SelectQuestion(200);
        session.JudgeQuestionAnswer(200, mickey.Id, true);
        session.CloseQuestionAnswer(200);
        session.AdjustPlayerScore(rose.Id, 0);
        session.AdjustPlayerScore(mickey.Id, -100);

        var standings = session.GetFinalStandings();

        Assert.Equal(100, rose.Score);
        Assert.Equal(100, mickey.Score);
        Assert.Equal(mickey.Id, standings[0].PlayerId);
        Assert.Equal(100, standings[0].ScoreGain);
        Assert.True(standings[0].IsWinner);
    }

    [Fact]
    public void Final_standings_use_correct_answers_before_attempts()
    {
        var session = CreateMultiRoundSession();
        var rose = session.AddPlayer("Rose");
        var mickey = session.AddPlayer("Mickey");
        session.Start();
        session.SelectQuestion(100);
        session.JudgeQuestionAnswer(100, rose.Id, true);
        session.CloseQuestionAnswer(100);
        session.AdjustPlayerScore(rose.Id, -100);
        session.AdvanceToNextRound();
        session.SelectQuestion(200);
        session.JudgeQuestionAnswer(200, rose.Id, true);
        session.CloseQuestionAnswer(200);
        session.AdjustPlayerScore(rose.Id, -200);

        var standings = session.GetFinalStandings();

        Assert.Equal(0, rose.Score);
        Assert.Equal(0, mickey.Score);
        Assert.Equal(rose.Id, standings[0].PlayerId);
        Assert.Equal(2, standings[0].TotalCorrectAnswers);
        Assert.Equal(mickey.Id, standings[1].PlayerId);
    }

    private static GameSession CreateMultiRoundSession(
        ManualTimeProvider? timeProvider = null)
    {
        var quiz = new QuizSnapshot(
            1,
            "Multi-round Quiz",
            [
                new QuizRoundSnapshot(
                    1,
                    "Round 1",
                    0,
                    [new QuizQuestionSnapshot(100, 10, 0, 100, false, "Science")]),
                new QuizRoundSnapshot(
                    2,
                    "Round 2",
                    1,
                    [new QuizQuestionSnapshot(200, 20, 0, 200, false, "History")])
            ]);

        return GameSession.Create(
            quiz,
            timeProvider ?? new ManualTimeProvider(InitialTime));
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
