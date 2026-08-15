using BadWolfQuiz.Game.Definitions;
using BadWolfQuiz.Game.Runtime;

namespace BadWolfQuiz.Game.Tests;

public sealed class GameSessionTests
{
    private static readonly DateTimeOffset InitialTime =
        new(2026, 7, 29, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(0, 100)]
    [InlineData(1, 50)]
    [InlineData(2, 25)]
    public void Four_clue_question_reduces_correct_score_as_clues_are_revealed(
        int additionalClues,
        int expectedScore)
    {
        var session = CreateFourClueSession();
        var player = session.AddPlayer("Rose");
        session.Start();
        session.SelectQuestion(100);
        for (var index = 0; index < additionalClues; index++)
        {
            session.RevealNextClue(100);
        }
        Assert.Equal(
            expectedScore,
            session.Board.Questions.Single().CorrectAnswerValue);
        session.ActivateQuestionBuzzer(100);
        session.ClaimQuestionBuzzer(100, player.Id);

        var attempt = session.JudgeQuestionAnswer(100, player.Id, true);

        Assert.Equal(expectedScore, attempt.ScoreDelta);
    }

    [Fact]
    public void Four_clue_question_always_deducts_the_full_value_for_a_wrong_answer()
    {
        var session = CreateFourClueSession();
        var player = session.AddPlayer("Rose");
        session.Start();
        session.SelectQuestion(100);
        session.RevealNextClue(100);
        session.RevealNextClue(100);
        session.ActivateQuestionBuzzer(100);
        session.ClaimQuestionBuzzer(100, player.Id);

        var attempt = session.JudgeQuestionAnswer(100, player.Id, false);

        Assert.Equal(-100, attempt.ScoreDelta);
    }

    [Fact]
    public void Four_clue_question_accepts_video_clues_without_captions()
    {
        static ContentBlockSnapshot VideoClue(int id) => new(
            id,
            ContentBlockKind.YouTube,
            null,
            null,
            null,
            null,
            $"https://youtu.be/video-{id}",
            null,
            null,
            null,
            id,
            false);

        var question = new QuizQuestionSnapshot(
            100,
            10,
            0,
            100,
            false,
            "Connections",
            false,
            [VideoClue(1), VideoClue(2), VideoClue(3), VideoClue(4)],
            [],
            QuestionPresentationType.FourClues);

        Assert.All(
            question.QuestionBlocks,
            block =>
            {
                Assert.Equal(ContentBlockKind.YouTube, block.Kind);
                Assert.Null(block.TopCaption);
                Assert.Null(block.BottomCaption);
            });
    }

    [Fact]
    public void Restore_preserves_the_number_of_revealed_clues()
    {
        var session = CreateFourClueSession();
        session.AddPlayer("Rose");
        session.Start();
        session.SelectQuestion(100);
        session.RevealNextClue(100);

        var restored = GameSession.Restore(
            session.Quiz,
            session.Settings,
            session.CaptureState());

        Assert.Equal(
            3,
            restored.Board.Questions.Single().RevealedClueCount);
    }

    private static GameSession CreateFourClueSession()
    {
        static ContentBlockSnapshot Clue(int id) => new(
            id, ContentBlockKind.Text, $"Clue {id}", null, null, null,
            null, null, null, null, id, false);

        var question = new QuizQuestionSnapshot(
            100, 10, 0, 100, false, "Connections", false,
            [Clue(1), Clue(2), Clue(3), Clue(4)],
            [],
            QuestionPresentationType.FourClues);
        var quiz = new QuizSnapshot(
            1, "Four clues", [new QuizRoundSnapshot(1, "Round 1", 0, [question])]);
        return GameSession.Create(quiz, new ManualTimeProvider(InitialTime));
    }

    [Fact]
    public void Restore_preserves_gameplay_state_but_stops_timers()
    {
        var session = CreateSession();
        var rose = session.AddPlayer("Rose");
        session.AddPlayer("Mickey");
        session.Start();
        session.SelectQuestion(100);
        session.ActivateQuestionBuzzer(100);
        session.ClaimQuestionBuzzer(100, rose.Id);
        session.JudgeQuestionAnswer(100, rose.Id, false);

        var restored = GameSession.Restore(
            session.Quiz,
            session.Settings,
            session.CaptureState());

        Assert.Equal(session.Id, restored.Id);
        Assert.Equal(GameSessionStatus.Running, restored.Status);
        Assert.Equal(2, restored.Players.Count);
        Assert.Equal(-100, restored.Players.Single(p => p.Id == rose.Id).Score);
        var question = restored.Board.Questions.Single(q => q.SourceQuestionId == 100);
        Assert.Equal(RuntimeQuestionStatus.Active, question.Status);
        Assert.Equal(QuestionBuzzerStatus.Inactive, question.BuzzerStatus);
        Assert.Single(question.AnswerAttempts);
        Assert.Equal(GameTimerStatus.Stopped, restored.Timer.Status);
        Assert.Equal(GameTimerStatus.Stopped, restored.AnswerTimer.Status);
    }

    [Fact]
    public void Restore_preserves_a_claimed_buzzer()
    {
        var session = CreateSession();
        var rose = session.AddPlayer("Rose");
        session.Start();
        session.SelectQuestion(100);
        session.ActivateQuestionBuzzer(100);
        session.ClaimQuestionBuzzer(100, rose.Id);

        var restored = GameSession.Restore(
            session.Quiz,
            session.Settings,
            session.CaptureState());

        var question = restored.Board.Questions.Single(q => q.SourceQuestionId == 100);
        Assert.Equal(QuestionBuzzerStatus.Claimed, question.BuzzerStatus);
        Assert.Equal(rose.Id, question.AnsweringPlayerId);
    }

    [Fact]
    public void Create_uses_game_specific_timer_settings()
    {
        var settings = new GameSessionSettings(
            TimeSpan.FromSeconds(45),
            TimeSpan.FromSeconds(12),
            GamePhaseStartMode.Manual,
            GamePhaseStartMode.Automatic);

        var session = GameSession.Create(
            CreateSession().Quiz,
            settings,
            new ManualTimeProvider(InitialTime));

        Assert.Same(settings, session.Settings);
        Assert.Equal(TimeSpan.FromSeconds(45), session.Timer.Duration);
        Assert.Equal(TimeSpan.FromSeconds(12), session.AnswerTimer.Duration);
    }

    [Fact]
    public void SelectQuestion_automatically_opens_buzzer_when_configured()
    {
        var settings = new GameSessionSettings(
            TimeSpan.FromSeconds(40),
            TimeSpan.FromSeconds(15),
            GamePhaseStartMode.Automatic,
            GamePhaseStartMode.Automatic);
        var session = GameSession.Create(
            CreateSession().Quiz,
            settings,
            new ManualTimeProvider(InitialTime));
        session.AddPlayer("Rose");
        session.Start();

        var question = session.SelectQuestion(100);

        Assert.Equal(RuntimeQuestionStatus.Active, question.Status);
        Assert.Equal(QuestionBuzzerStatus.Open, question.BuzzerStatus);
        Assert.Equal(GameTimerStatus.Running, session.Timer.Status);
        Assert.Equal(TimeSpan.FromSeconds(40), session.Timer.Remaining);
    }

    [Fact]
    public void Manual_wager_timer_waits_for_explicit_start()
    {
        var settings = new GameSessionSettings(
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(18),
            GamePhaseStartMode.Manual,
            GamePhaseStartMode.Manual);
        var session = GameSession.Create(
            CreateSession().Quiz,
            settings,
            new ManualTimeProvider(InitialTime));
        session.AddPlayer("Rose");
        session.Start();
        session.SelectQuestion(101);

        session.SubmitQuestionWager(101, 100);

        Assert.Equal(GameTimerStatus.Stopped, session.AnswerTimer.Status);

        var timer = session.StartWagerAnswerTimer(101);

        Assert.Same(session.AnswerTimer, timer);
        Assert.Equal(GameTimerStatus.Running, timer.Status);
        Assert.Equal(TimeSpan.FromSeconds(18), timer.Remaining);
    }

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
    public void UpdateSettings_rebuilds_timers_before_game_starts()
    {
        var session = CreateSession();
        var settings = new GameSessionSettings(
            TimeSpan.FromSeconds(55),
            TimeSpan.FromSeconds(14),
            GamePhaseStartMode.Automatic,
            GamePhaseStartMode.Manual);

        session.UpdateSettings(settings);

        Assert.Same(settings, session.Settings);
        Assert.Equal(TimeSpan.FromSeconds(55), session.Timer.Duration);
        Assert.Equal(TimeSpan.FromSeconds(14), session.AnswerTimer.Duration);
    }

    [Fact]
    public void Start_allows_host_card_with_name_only()
    {
        var settings = new GameSessionSettings(
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(10),
            GamePhaseStartMode.Manual,
            GamePhaseStartMode.Automatic,
            displayHostCard: true,
            hostName: "Host");
        var session = GameSession.Create(
            CreateSession().Quiz,
            settings,
            new ManualTimeProvider(InitialTime));
        session.AddPlayer("Rose");

        session.Start();

        Assert.Equal(GameSessionStatus.Running, session.Status);
        Assert.True(session.Settings.HasHostCard);
    }

    [Fact]
    public void UpdateSettings_rebuilds_timers_during_regular_play()
    {
        var session = CreateSession();
        session.AddPlayer("Rose");
        session.Start();
        var settings = new GameSessionSettings(
            TimeSpan.FromSeconds(45),
            TimeSpan.FromSeconds(12),
            GamePhaseStartMode.Automatic,
            GamePhaseStartMode.Manual);

        session.UpdateSettings(settings);

        Assert.Same(settings, session.Settings);
        Assert.Equal(TimeSpan.FromSeconds(45), session.Timer.Duration);
        Assert.Equal(TimeSpan.FromSeconds(12), session.AnswerTimer.Duration);
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
    public void Start_allows_session_without_players()
    {
        var session = CreateSession();

        session.Start();

        Assert.Equal(GameSessionStatus.Running, session.Status);
        Assert.Equal(InitialTime, session.StartedAtUtc);
        Assert.Empty(session.Players);
    }

    [Fact]
    public void Player_can_join_after_empty_lobby_game_starts()
    {
        var session = CreateSession();
        session.Start();

        var player = session.AddPlayer("Rose");

        Assert.Same(player, Assert.Single(session.Players));
        Assert.Equal(player.Id, session.ActivePlayerId);
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

        Assert.Equal(QuestionTimerOutcome.AnswerExpired, outcome.Outcome);
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
        session.AddPlayer("Mickey");
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
            session.ProcessQuestionTimers().Outcome);
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

        Assert.Equal(QuestionTimerOutcome.BuzzerExpired, outcome.Outcome);
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
    public void AdjustQuestionAnswerHistoryEntry_accumulates_on_existing_entry()
    {
        var session = CreateSession();
        var player = session.AddPlayer("Rose");
        session.Start();
        session.AddQuestionAnswerHistoryEntry(100, player.Id, true, 100);

        var increased = session.AdjustQuestionAnswerHistoryEntry(
            100, player.Id, 25);
        var decreased = session.AdjustQuestionAnswerHistoryEntry(
            100, player.Id, -40);

        var question = session.Board.Questions.Single(item =>
            item.SourceQuestionId == 100);
        Assert.Single(question.AnswerAttempts);
        Assert.Equal(increased.Id, decreased.Id);
        Assert.Equal(85, decreased.ScoreDelta);
        Assert.Equal(85, player.Score);
    }

    [Fact]
    public void AdjustQuestionAnswerHistoryEntry_can_cross_zero_without_duplicate_entry()
    {
        var session = CreateSession();
        var player = session.AddPlayer("Rose");
        session.Start();
        session.AddQuestionAnswerHistoryEntry(100, player.Id, false, 100);

        var updated = session.AdjustQuestionAnswerHistoryEntry(
            100, player.Id, 150);

        var question = session.Board.Questions.Single(item =>
            item.SourceQuestionId == 100);
        Assert.Single(question.AnswerAttempts);
        Assert.True(updated.IsCorrect);
        Assert.Equal(50, updated.ScoreDelta);
        Assert.Equal(50, player.Score);
    }

    [Fact]
    public void UpdateQuestionAnswerHistoryEntry_recalculates_players_and_attempt()
    {
        var session = CreateSession();
        var rose = session.AddPlayer("Rose");
        var mickey = session.AddPlayer("Mickey");
        session.Start();
        session.SelectQuestion(100);
        var original = session.JudgeQuestionAnswer(100, rose.Id, false);

        var updated = session.UpdateQuestionAnswerHistoryEntry(
            100,
            original.Id,
            mickey.Id,
            true,
            350);

        Assert.Equal(0, rose.Score);
        Assert.Equal(350, mickey.Score);
        Assert.Equal(mickey.Id, updated.PlayerId);
        Assert.True(updated.IsCorrect);
        Assert.Equal(350, updated.ScoreDelta);
        Assert.Equal(original.Id, updated.Id);
    }

    [Fact]
    public void AddQuestionAnswerHistoryEntry_updates_score_and_standings()
    {
        var session = CreateSession();
        var rose = session.AddPlayer("Rose");
        var mickey = session.AddPlayer("Mickey");
        session.Start();
        session.SelectQuestion(100);
        session.JudgeQuestionAnswer(100, rose.Id, false);
        session.ResolveQuestionWithoutCorrectAnswer(100);
        session.CloseQuestionAnswer(100);
        session.SelectQuestion(101);
        session.SubmitQuestionWager(101, 5);
        session.JudgeQuestionAnswer(101, rose.Id, false);
        session.CloseQuestionAnswer(101);

        var added = session.AddQuestionAnswerHistoryEntry(
            100,
            mickey.Id,
            true,
            100);
        var standings = session.GetFinalStandings();

        Assert.Equal(100, mickey.Score);
        Assert.Equal(100, added.ScoreDelta);
        Assert.Equal(mickey.Id, standings[0].PlayerId);
        Assert.Equal(1, standings[0].TotalCorrectAnswers);
    }

    [Fact]
    public void AddQuestionAnswerHistoryEntry_accepts_unopened_current_round_question()
    {
        var session = CreateMultiRoundSession();
        var player = session.AddPlayer("Rose");
        session.Start();

        var added = session.AddQuestionAnswerHistoryEntry(
            100,
            player.Id,
            true,
            100);

        var question = session.Board.Questions.Single(item =>
            item.SourceQuestionId == 100);
        Assert.Equal(100, added.ScoreDelta);
        Assert.Equal(RuntimeQuestionStatus.Resolved, question.Status);
    }

    [Fact]
    public void AddQuestionAnswerHistoryEntry_rejects_future_round_question()
    {
        var session = CreateMultiRoundSession();
        var player = session.AddPlayer("Rose");
        session.Start();

        Assert.Throws<GameRuleViolationException>(() =>
            session.AddQuestionAnswerHistoryEntry(
                200,
                player.Id,
                true,
                200));
    }

    [Fact]
    public void Editing_previous_round_history_does_not_change_current_round_gain()
    {
        var session = CreateMultiRoundSession();
        var rose = session.AddPlayer("Rose");
        var mickey = session.AddPlayer("Mickey");
        session.Start();
        session.SelectQuestion(100);
        var attempt = session.JudgeQuestionAnswer(100, rose.Id, true);
        session.CloseQuestionAnswer(100);
        session.AdvanceToNextRound();
        session.SelectQuestion(200);
        session.JudgeQuestionAnswer(200, mickey.Id, true);
        session.CloseQuestionAnswer(200);

        session.UpdateQuestionAnswerHistoryEntry(
            100,
            attempt.Id,
            rose.Id,
            true,
            500);
        var standings = session.GetFinalStandings();
        var roseStanding = standings.Single(item => item.PlayerId == rose.Id);

        Assert.Equal(500, rose.Score);
        Assert.Equal(0, roseStanding.ScoreGain);
    }

    [Fact]
    public void RemoveQuestionAnswerHistoryEntry_reverses_score_and_statistics()
    {
        var session = CreateSession();
        var rose = session.AddPlayer("Rose");
        var mickey = session.AddPlayer("Mickey");
        session.Start();
        session.SelectQuestion(100);
        var wrong = session.JudgeQuestionAnswer(100, rose.Id, false);
        session.JudgeQuestionAnswer(100, mickey.Id, true);

        var removed = session.RemoveQuestionAnswerHistoryEntry(100, wrong.Id);

        Assert.Equal(wrong, removed);
        Assert.Equal(0, rose.Score);
        Assert.DoesNotContain(
            session.Board.Questions.Single(question =>
                question.SourceQuestionId == 100).AnswerAttempts,
            attempt => attempt.Id == wrong.Id);
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

    [Fact]
    public void Final_question_includes_negative_score_players_by_default()
    {
        var session = CreateFinalSession();
        var positive = session.AddPlayer("Rose");
        var negative = session.AddPlayer("Mickey");
        session.AdjustPlayerScore(negative.Id, -100);
        session.Start();
        session.SelectQuestion(100);
        session.JudgeQuestionAnswer(100, positive.Id, true);
        session.CloseQuestionAnswer(100);

        var final = session.StartFinalQuestion();

        Assert.Contains(final.Submissions, item => item.PlayerId == negative.Id);
    }

    [Fact]
    public void Final_question_can_exclude_negative_score_players()
    {
        var settings = new GameSessionSettings(
            GameSession.DefaultBuzzerDuration,
            GameSession.DefaultAnswerDuration,
            GamePhaseStartMode.Manual,
            GamePhaseStartMode.Automatic,
            allowNegativeScoreFinalPlayers: false);
        var session = CreateFinalSession(settings: settings);
        var positive = session.AddPlayer("Rose");
        var negative = session.AddPlayer("Mickey");
        session.AdjustPlayerScore(negative.Id, -100);
        session.Start();
        session.SelectQuestion(100);
        session.JudgeQuestionAnswer(100, positive.Id, true);
        session.CloseQuestionAnswer(100);

        var final = session.StartFinalQuestion();

        Assert.DoesNotContain(
            final.Submissions,
            item => item.PlayerId == negative.Id);
        Assert.Contains(
            final.Submissions,
            item => item.PlayerId == positive.Id);
    }

    [Fact]
    public void Final_question_runs_private_wager_answer_and_judging_flow()
    {
        var timeProvider = new ManualTimeProvider(InitialTime);
        var session = CreateFinalSession(timeProvider);
        var rose = session.AddPlayer("Rose");
        var mickey = session.AddPlayer("Mickey");
        session.AdjustPlayerScore(rose.Id, 300);
        session.AdjustPlayerScore(mickey.Id, 150);
        session.Start();
        session.SelectQuestion(100);
        session.JudgeQuestionAnswer(100, rose.Id, true);
        session.CloseQuestionAnswer(100);

        var final = session.StartFinalQuestion();
        session.SubmitFinalWager(rose.Id, 250);
        session.SubmitFinalWager(mickey.Id, 100);
        session.LockFinalWagers();
        session.SubmitFinalAnswer(rose.Id, "Bad Wolf");
        session.SubmitFinalAnswer(mickey.Id, "Torchwood");
        session.LockFinalAnswers();
        session.JudgeFinalAnswer(rose.Id, true);
        session.JudgeFinalAnswer(mickey.Id, false);

        Assert.Equal(FinalQuestionStatus.Completed, final.Status);
        Assert.Equal(GameSessionStatus.Completed, session.Status);
        Assert.Equal(650, rose.Score);
        Assert.Equal(50, mickey.Score);
        Assert.True(final.Submissions.Single(x => x.PlayerId == rose.Id).IsCorrect);
        Assert.False(final.Submissions.Single(x => x.PlayerId == mickey.Id).IsCorrect);
    }

    [Fact]
    public void Final_question_enforces_minimum_and_default_maximum_wager()
    {
        var session = CreateFinalSession();
        var player = session.AddPlayer("Rose");
        session.AdjustPlayerScore(player.Id, 200);
        session.Start();
        session.SelectQuestion(100);
        session.JudgeQuestionAnswer(100, player.Id, true);
        session.CloseQuestionAnswer(100);
        var final = session.StartFinalQuestion();

        Assert.Equal(
            FinalQuestion.DefaultMaximumWager,
            final.Submissions.Single().MaximumWager);
        Assert.Throws<GameRuleViolationException>(
            () => session.SubmitFinalAnswer(player.Id, "Too early"));
        Assert.Throws<GameRuleViolationException>(
            () => session.SubmitFinalWager(player.Id, 4));
        Assert.Throws<GameRuleViolationException>(
            () => session.SubmitFinalWager(player.Id, 1001));

        session.SubmitFinalWager(player.Id, 1000);

        Assert.Throws<GameRuleViolationException>(
            () => session.SubmitFinalWager(player.Id, 100));
        Assert.Throws<GameRuleViolationException>(
            session.LockFinalAnswers);
    }

    [Fact]
    public void Final_question_maximum_uses_player_score_above_default_maximum()
    {
        var session = CreateFinalSession();
        var player = session.AddPlayer("Rose");
        session.AdjustPlayerScore(player.Id, 2000);
        session.Start();
        session.SelectQuestion(100);
        session.JudgeQuestionAnswer(100, player.Id, true);
        session.CloseQuestionAnswer(100);

        var final = session.StartFinalQuestion();

        Assert.Equal(2100, final.Submissions.Single().MaximumWager);
        Assert.Throws<GameRuleViolationException>(
            () => session.SubmitFinalWager(player.Id, 2101));
        Assert.Equal(
            2100,
            session.SubmitFinalWager(player.Id, 2100).Wager!.Amount);
    }

    private static GameSession CreateFinalSession(
        ManualTimeProvider? timeProvider = null,
        GameSessionSettings? settings = null)
    {
        var quiz = new QuizSnapshot(
            1,
            "Final Quiz",
            [
                new QuizRoundSnapshot(
                    1,
                    "Round 1",
                    0,
                    [new QuizQuestionSnapshot(100, 10, 0, 100, false, "Science")])
            ],
            new FinalQuestionSnapshot(
                questionBlocks:
                [
                    new ContentBlockSnapshot(
                        1,
                        ContentBlockKind.Text,
                        "Who is the Bad Wolf?",
                        null,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null,
                        0,
                        false)
                ],
                answerBlocks:
                [
                    new ContentBlockSnapshot(
                        2,
                        ContentBlockKind.Text,
                        "Rose Tyler",
                        null,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null,
                        0,
                        false)
                ]));

        var effectiveTimeProvider =
            timeProvider ?? new ManualTimeProvider(InitialTime);

        return settings is null
            ? GameSession.Create(quiz, effectiveTimeProvider)
            : GameSession.Create(quiz, settings, effectiveTimeProvider);
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

    private static GameSession CreateRewardDecaySession(
        ManualTimeProvider timeProvider,
        int answerDurationSeconds = 30,
        int startAfterSeconds = 10,
        int minimumPercent = 25)
    {
        var source = CreateSession();

        var settings = new GameSessionSettings(
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(answerDurationSeconds),
            GamePhaseStartMode.Manual,
            GamePhaseStartMode.Automatic,
            answerRewardDecayEnabled: true,
            answerRewardDecayStartAfterSeconds: startAfterSeconds,
            answerRewardDecayMinimumPercent: minimumPercent);

        return GameSession.Create(
            source.Quiz,
            settings,
            timeProvider);
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(9, 100)]
    [InlineData(10, 96)]
    [InlineData(11, 93)]
    [InlineData(20, 59)]
    [InlineData(25, 40)]
    [InlineData(28, 29)]
    [InlineData(29, 25)]
    public void Answer_reward_decay_reduces_correct_answer_value_linearly(
        int elapsedSeconds,
        int expectedReward)
    {
        var timeProvider = new ManualTimeProvider(InitialTime);
        var session = CreateRewardDecaySession(timeProvider);
        var player = session.AddPlayer("Rose");

        session.Start();
        session.SelectQuestion(100);
        session.ActivateQuestionBuzzer(100);
        session.ClaimQuestionBuzzer(100, player.Id);

        timeProvider.Advance(TimeSpan.FromSeconds(elapsedSeconds));

        Assert.Equal(
            expectedReward,
            session.GetCurrentCorrectAnswerValue(100));
    }

    [Fact]
    public void Correct_answer_awards_current_decayed_reward()
    {
        var timeProvider = new ManualTimeProvider(InitialTime);
        var session = CreateRewardDecaySession(timeProvider);
        var player = session.AddPlayer("Rose");

        session.Start();
        session.SelectQuestion(100);
        session.ActivateQuestionBuzzer(100);
        session.ClaimQuestionBuzzer(100, player.Id);

        timeProvider.Advance(TimeSpan.FromSeconds(20));

        var expectedReward =
            session.GetCurrentCorrectAnswerValue(100);

        var attempt = session.JudgeQuestionAnswer(
            100,
            player.Id,
            true);

        Assert.Equal(expectedReward, attempt.ScoreDelta);
        Assert.Equal(expectedReward, player.Score);
    }

    [Fact]
    public void Answer_reward_decay_does_not_reduce_wrong_answer_penalty()
    {
        var timeProvider = new ManualTimeProvider(InitialTime);
        var session = CreateRewardDecaySession(timeProvider);
        var player = session.AddPlayer("Rose");

        session.Start();
        session.SelectQuestion(100);
        session.ActivateQuestionBuzzer(100);
        session.ClaimQuestionBuzzer(100, player.Id);

        timeProvider.Advance(TimeSpan.FromSeconds(25));

        Assert.True(session.GetCurrentCorrectAnswerValue(100) < 100);

        var attempt = session.JudgeQuestionAnswer(
            100,
            player.Id,
            false);

        Assert.Equal(-100, attempt.ScoreDelta);
        Assert.Equal(-100, player.Score);
    }

    [Fact]
    public void Answer_reward_decay_resets_when_buzzer_reopens()
    {
        var timeProvider = new ManualTimeProvider(InitialTime);
        var session = CreateRewardDecaySession(timeProvider);
        var rose = session.AddPlayer("Rose");
        var mickey = session.AddPlayer("Mickey");

        session.Start();
        session.SelectQuestion(100);
        session.ActivateQuestionBuzzer(100);
        session.ClaimQuestionBuzzer(100, rose.Id);

        timeProvider.Advance(TimeSpan.FromSeconds(25));

        Assert.True(session.GetCurrentCorrectAnswerValue(100) < 100);

        session.JudgeQuestionAnswer(
            100,
            rose.Id,
            false);

        Assert.Equal(
            100,
            session.GetCurrentCorrectAnswerValue(100));

        session.ClaimQuestionBuzzer(100, mickey.Id);

        Assert.Equal(
            100,
            session.GetCurrentCorrectAnswerValue(100));
    }

    [Fact]
    public void Pausing_answer_timer_freezes_reward_decay()
    {
        var timeProvider = new ManualTimeProvider(InitialTime);
        var session = CreateRewardDecaySession(timeProvider);
        var player = session.AddPlayer("Rose");

        session.Start();
        session.SelectQuestion(100);
        session.ActivateQuestionBuzzer(100);
        session.ClaimQuestionBuzzer(100, player.Id);

        timeProvider.Advance(TimeSpan.FromSeconds(20));

        var rewardBeforePause =
            session.GetCurrentCorrectAnswerValue(100);

        session.PauseQuestionTimer();

        timeProvider.Advance(TimeSpan.FromSeconds(30));

        Assert.Equal(
            rewardBeforePause,
            session.GetCurrentCorrectAnswerValue(100));

        session.ResumeQuestionTimer();

        timeProvider.Advance(TimeSpan.FromSeconds(1));

        Assert.True(
            session.GetCurrentCorrectAnswerValue(100) <
            rewardBeforePause);
    }

    [Fact]
    public void Disabled_answer_reward_decay_keeps_full_reward()
    {
        var timeProvider = new ManualTimeProvider(InitialTime);
        var source = CreateSession();

        var settings = new GameSessionSettings(
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(30),
            GamePhaseStartMode.Manual,
            GamePhaseStartMode.Automatic,
            answerRewardDecayEnabled: false);

        var session = GameSession.Create(
            source.Quiz,
            settings,
            timeProvider);

        var player = session.AddPlayer("Rose");

        session.Start();
        session.SelectQuestion(100);
        session.ActivateQuestionBuzzer(100);
        session.ClaimQuestionBuzzer(100, player.Id);

        timeProvider.Advance(TimeSpan.FromSeconds(29));

        Assert.Equal(
            100,
            session.GetCurrentCorrectAnswerValue(100));
    }

    [Fact]
    public void Answer_reward_decay_does_not_apply_to_wager_questions()
    {
        var timeProvider = new ManualTimeProvider(InitialTime);
        var session = CreateRewardDecaySession(timeProvider);
        var player = session.AddPlayer("Rose");

        session.Start();
        session.SelectQuestion(101);
        session.SubmitQuestionWager(101, 150);

        timeProvider.Advance(TimeSpan.FromSeconds(25));

        Assert.Equal(
            150,
            session.GetCurrentCorrectAnswerValue(101));

        var attempt = session.JudgeQuestionAnswer(
            101,
            player.Id,
            true);

        Assert.Equal(150, attempt.ScoreDelta);
    }

    [Fact]
    public void Answer_reward_decay_uses_four_clue_adjusted_value_as_its_base()
    {
        var timeProvider = new ManualTimeProvider(InitialTime);

        var settings = new GameSessionSettings(
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(30),
            GamePhaseStartMode.Manual,
            GamePhaseStartMode.Automatic,
            answerRewardDecayEnabled: true,
            answerRewardDecayStartAfterSeconds: 10,
            answerRewardDecayMinimumPercent: 25);

        var source = CreateFourClueSession();
        var session = GameSession.Create(
            source.Quiz,
            settings,
            timeProvider);

        var player = session.AddPlayer("Rose");

        session.Start();
        session.SelectQuestion(100);
        session.RevealNextClue(100);

        Assert.Equal(
            50,
            session.Board.Questions.Single().CorrectAnswerValue);

        session.ActivateQuestionBuzzer(100);
        session.ClaimQuestionBuzzer(100, player.Id);

        timeProvider.Advance(TimeSpan.FromSeconds(29));

        Assert.Equal(
            13,
            session.GetCurrentCorrectAnswerValue(100));
    }
}
