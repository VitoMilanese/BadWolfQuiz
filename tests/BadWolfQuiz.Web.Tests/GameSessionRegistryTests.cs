using BadWolfQuiz.Game.Definitions;
using BadWolfQuiz.Game.Runtime;
using BadWolfQuiz.Web.Services;

namespace BadWolfQuiz.Web.Tests;

public sealed class GameSessionRegistryTests
{
    [Fact]
    public void Create_registers_runtime_session()
    {
        var registry = CreateRegistry("ABC123");
        var quiz = CreateQuiz();

        var game = registry.Create(quiz);

        Assert.Same(game, registry.Find(game.Session.Id));
        Assert.Same(game, registry.Find("abc123"));
        Assert.Equal("ABC123", game.PublicCode);
    }

    [Fact]
    public void Create_retries_when_public_code_already_exists()
    {
        var registry = CreateRegistry("ABC123", "ABC123", "WOLF42");

        registry.Create(CreateQuiz());
        var secondGame = registry.Create(CreateQuiz());

        Assert.Equal("WOLF42", secondGame.PublicCode);
    }

    [Fact]
    public void UpdateSettings_routes_lobby_settings_to_runtime_session()
    {
        var registry = CreateRegistry("ABC123");
        var game = registry.Create(CreateQuiz());
        var settings = new BadWolfQuiz.Game.Runtime.GameSessionSettings(
            TimeSpan.FromSeconds(50),
            TimeSpan.FromSeconds(11),
            BadWolfQuiz.Game.Runtime.GamePhaseStartMode.Automatic,
            BadWolfQuiz.Game.Runtime.GamePhaseStartMode.Manual);

        registry.UpdateSettings("ABC123", settings);

        Assert.Same(settings, game.Session.Settings);
        Assert.Equal(TimeSpan.FromSeconds(50), game.Session.Timer.Duration);
    }

    [Fact]
    public void JoinPlayer_adds_player_to_matching_game()
    {
        var registry = CreateRegistry("ABC123");
        var game = registry.Create(CreateQuiz());

        var result = registry.JoinPlayer(" abc123 ", "  Rose  ");

        Assert.Equal(PlayerJoinStatus.Success, result.Status);
        Assert.Same(game, result.Game);
        Assert.Equal("Rose", result.Player!.Name);
        Assert.Single(registry.GetPlayers(game));
    }

    [Fact]
    public void JoinPlayer_rejects_duplicate_name_ignoring_case()
    {
        var registry = CreateRegistry("ABC123");
        registry.Create(CreateQuiz());
        registry.JoinPlayer("ABC123", "Rose");

        var result = registry.JoinPlayer("ABC123", "rose");

        Assert.Equal(PlayerJoinStatus.NameAlreadyUsed, result.Status);
    }

    [Fact]
    public void JoinPlayer_returns_not_found_for_unknown_code()
    {
        var registry = CreateRegistry("ABC123");

        var result = registry.JoinPlayer("BAD999", "Rose");

        Assert.Equal(PlayerJoinStatus.GameNotFound, result.Status);
    }

    [Fact]
    public void JoinPlayer_returns_access_token_for_presence_connection()
    {
        var registry = CreateRegistry("ABC123");
        registry.Create(CreateQuiz());

        var joined = registry.JoinPlayer("ABC123", "Rose");
        var connected = registry.ConnectPlayer(
            "ABC123",
            joined.AccessToken!,
            "connection-1",
            true);

        Assert.NotNull(joined.AccessToken);
        Assert.NotNull(connected);
        Assert.Equal(
            PlayerPresenceStatus.Active,
            registry.GetPlayerLobbyEntries(joined.Game!).Single().Presence);
    }

    [Fact]
    public void Presence_changes_when_page_becomes_inactive_or_disconnects()
    {
        var registry = CreateRegistry("ABC123");
        registry.Create(CreateQuiz());
        var joined = registry.JoinPlayer("ABC123", "Rose");
        registry.ConnectPlayer("ABC123", joined.AccessToken!, "connection-1", true);

        registry.SetPlayerVisibility("connection-1", false);
        var inactive = registry.GetPlayerLobbyEntries(joined.Game!).Single();

        registry.DisconnectPlayer("connection-1");
        var disconnected = registry.GetPlayerLobbyEntries(joined.Game!).Single();

        Assert.Equal(PlayerPresenceStatus.Inactive, inactive.Presence);
        Assert.Equal(PlayerPresenceStatus.Disconnected, disconnected.Presence);
        Assert.Equal(0, disconnected.Score);
    }

    [Fact]
    public void Connected_player_can_change_avatar()
    {
        var registry = CreateRegistry("ABC123");
        registry.Create(CreateQuiz());
        var joined = registry.JoinPlayer("ABC123", "Rose");
        registry.ConnectPlayer(
            "ABC123",
            joined.AccessToken!,
            "connection-1",
            true);

        var result = registry.SetPlayerAvatar("connection-1", "F/17.png");

        Assert.NotNull(result);
        Assert.Equal("F/17.png", joined.Player!.AvatarId);
    }

    [Fact]
    public void Running_game_accepts_new_player_pending_host_approval()
    {
        var registry = CreateRegistry("ABC123");
        var game = registry.Create(CreateQuiz());
        registry.JoinPlayer("ABC123", "Rose");
        registry.StartGame("ABC123");

        var joined = registry.JoinPlayer("ABC123", "Mickey");
        var connected = registry.ConnectPlayer(
            "ABC123",
            joined.AccessToken!,
            "connection-2",
            true);

        Assert.Equal(PlayerJoinStatus.Success, joined.Status);
        Assert.Equal(0, joined.Player!.Score);
        Assert.True(connected!.RequiresApproval);

        var pending = registry.GetPlayerLobbyEntries(game)
            .Single(player => player.Id == joined.Player.Id);

        Assert.Equal(PlayerPresenceStatus.RejoinPending, pending.Presence);

        registry.ApprovePlayerRejoin("ABC123", joined.Player.Id);

        var approved = registry.GetPlayerLobbyEntries(game)
            .Single(player => player.Id == joined.Player.Id);

        Assert.Equal(PlayerPresenceStatus.Active, approved.Presence);
        Assert.Equal(0, approved.Score);
    }

    [Fact]
    public void Running_game_rejects_new_player_when_late_joining_is_disabled()
    {
        var registry = CreateRegistry("ABC123");
        var game = registry.Create(CreateQuiz());
        registry.JoinPlayer("ABC123", "Rose");
        registry.StartGame("ABC123");
        registry.ToggleNewPlayerJoining("ABC123");

        var result = registry.JoinPlayer("ABC123", "Mickey");

        Assert.Equal(PlayerJoinStatus.GameAlreadyStarted, result.Status);
        Assert.DoesNotContain(game.Session.Players, player => player.Name == "Mickey");
    }

    [Fact]
    public void Host_can_reopen_new_player_joining_during_a_running_game()
    {
        var registry = CreateRegistry("ABC123");
        registry.Create(CreateQuiz());
        registry.JoinPlayer("ABC123", "Rose");
        registry.StartGame("ABC123");
        registry.ToggleNewPlayerJoining("ABC123");

        var allowsNewPlayers = registry.ToggleNewPlayerJoining("ABC123");
        var result = registry.JoinPlayer("ABC123", "Mickey");

        Assert.True(allowsNewPlayers);
        Assert.Equal(PlayerJoinStatus.Success, result.Status);
    }

    [Fact]
    public void Removing_player_removes_card_and_revokes_access()
    {
        var registry = CreateRegistry("ABC123");
        var game = registry.Create(CreateQuiz());
        var rose = registry.JoinPlayer("ABC123", "Rose");
        var mickey = registry.JoinPlayer("ABC123", "Mickey");
        registry.ConnectPlayer("ABC123", rose.AccessToken!, "rose-connection", true);
        registry.ConnectPlayer("ABC123", mickey.AccessToken!, "mickey-connection", true);

        var removal = registry.RemovePlayer("ABC123", rose.Player!.Id);

        Assert.Equal("rose-connection", Assert.Single(removal!.ConnectionIds));
        Assert.DoesNotContain(game.Session.Players, player => player.Id == rose.Player.Id);
        Assert.DoesNotContain(
            registry.GetPlayerLobbyEntries(game),
            player => player.Id == rose.Player.Id);
        Assert.Equal(mickey.Player!.Id, game.Session.ActivePlayerId);
        Assert.Null(registry.ConnectPlayer(
            "ABC123",
            rose.AccessToken!,
            "rose-reconnect",
            true));
    }

    [Fact]
    public void Running_game_requires_host_approval_before_player_rejoins()
    {
        var registry = CreateRegistry("ABC123");
        var game = registry.Create(CreateQuiz());
        var joined = registry.JoinPlayer("ABC123", "Rose");
        registry.ConnectPlayer("ABC123", joined.AccessToken!, "connection-1", true);
        registry.StartGame("ABC123");
        registry.DisconnectPlayer("connection-1");

        var reconnect = registry.ConnectPlayer(
            "ABC123",
            joined.AccessToken!,
            "connection-2",
            true);

        Assert.True(reconnect!.RequiresApproval);
        Assert.Equal(
            PlayerPresenceStatus.RejoinPending,
            registry.GetPlayerLobbyEntries(game).Single().Presence);

        var approval = registry.ApprovePlayerRejoin(
            "ABC123",
            joined.Player!.Id);

        Assert.Equal("connection-2", Assert.Single(approval!.ConnectionIds));
        var restored = Assert.Single(registry.GetPlayerLobbyEntries(game));
        Assert.Equal(PlayerPresenceStatus.Active, restored.Presence);
        Assert.Equal(joined.Player.Id, restored.Id);
        Assert.Equal(joined.Player.Score, restored.Score);
    }

    [Fact]
    public void Running_game_keeps_approval_during_overlapping_page_transition()
    {
        var registry = CreateRegistry("ABC123");
        registry.Create(CreateQuiz());
        var joined = registry.JoinPlayer("ABC123", "Rose");
        registry.ConnectPlayer("ABC123", joined.AccessToken!, "old-page", true);
        registry.StartGame("ABC123");

        var replacement = registry.ConnectPlayer(
            "ABC123",
            joined.AccessToken!,
            "new-page",
            true);

        Assert.False(replacement!.RequiresApproval);
        Assert.Equal(
            PlayerPresenceStatus.Active,
            registry.GetPlayerLobbyEntries(replacement.Game).Single().Presence);
    }

    [Fact]
    public void Running_game_preserves_approval_with_single_use_transition_token()
    {
        var registry = CreateRegistry("ABC123");
        registry.Create(CreateQuiz());
        var joined = registry.JoinPlayer("ABC123", "Rose");
        registry.ConnectPlayer("ABC123", joined.AccessToken!, "old-page", true);
        registry.StartGame("ABC123");
        var transitionToken = registry.CreatePlayerTransitionToken("old-page");
        registry.DisconnectPlayer("old-page");

        var replacement = registry.ConnectPlayer(
            "ABC123",
            joined.AccessToken!,
            "new-page",
            true,
            transitionToken);
        registry.DisconnectPlayer("new-page");
        var reusedToken = registry.ConnectPlayer(
            "ABC123",
            joined.AccessToken!,
            "later-page",
            true,
            transitionToken);

        Assert.NotNull(transitionToken);
        Assert.False(replacement!.RequiresApproval);
        Assert.True(reusedToken!.RequiresApproval);
    }

    [Fact]
    public void SelectQuestion_routes_command_to_runtime_session()
    {
        var registry = CreateRegistry("ABC123");
        var game = registry.Create(CreateQuiz());
        registry.JoinPlayer("ABC123", "Rose");
        registry.StartGame("ABC123");

        var question = registry.SelectQuestion("ABC123", 1);

        Assert.NotNull(question);
        Assert.Equal(
            BadWolfQuiz.Game.Runtime.RuntimeQuestionStatus.Selected,
            question.Status);
        Assert.Same(
            game.Session.Board.Questions.Single(),
            question);
    }

    [Fact]
    public void SubmitQuestionWager_routes_command_to_runtime_session()
    {
        var registry = CreateRegistry("ABC123");
        var game = registry.Create(CreateWagerQuiz());
        var joined = registry.JoinPlayer("ABC123", "Rose");
        registry.StartGame("ABC123");
        registry.SelectQuestion("ABC123", 1);

        var question = registry.SubmitQuestionWager(
            "ABC123",
            1,
            100);

        Assert.NotNull(question);
        Assert.Equal(
            BadWolfQuiz.Game.Runtime.RuntimeQuestionStatus.Active,
            question.Status);
        Assert.Equal(joined.Player.Id, question.Wager!.PlayerId);
        Assert.Equal(100, question.Wager.Amount);
        Assert.Same(game.Session.Board.Questions.Single(), question);
    }

    [Fact]
    public void ClaimQuestionBuzzer_records_players_within_one_second_of_winner()
    {
        var timeProvider = new TestTimeProvider();
        var registry = new GameSessionRegistry(
            new StubGameCodeGenerator(["ABC123"]),
            timeProvider);
        registry.Create(CreateQuiz());
        var rose = registry.JoinPlayer("ABC123", "Rose");
        var mickey = registry.JoinPlayer("ABC123", "Mickey");
        registry.ConnectPlayer("ABC123", rose.AccessToken!, "rose-connection", true);
        registry.ConnectPlayer("ABC123", mickey.AccessToken!, "mickey-connection", true);
        registry.StartGame("ABC123");
        registry.SelectQuestion("ABC123", 1);
        registry.ActivateQuestionBuzzer("ABC123", 1);

        var winner = registry.ClaimQuestionBuzzer("rose-connection", 1);
        timeProvider.Advance(TimeSpan.FromMilliseconds(275));
        var late = registry.ClaimQuestionBuzzer("mickey-connection", 1);
        var race = winner!.Game.BuzzerRace!;

        Assert.True(winner.IsWinner);
        Assert.False(late!.IsWinner);
        Assert.Equal(rose.Player!.Id, race.WinnerPlayerId);
        var latePlayer = Assert.Single(race.LatePlayers);
        Assert.Equal(mickey.Player!.Id, latePlayer.PlayerId);
        Assert.Equal(275, latePlayer.DelayMilliseconds);

        registry.JudgeQuestionAnswer("ABC123", 1, rose.Player.Id, false);

        Assert.Null(winner.Game.BuzzerRace);
    }

    [Fact]
    public void ClaimQuestionBuzzer_ignores_players_after_one_second_window()
    {
        var timeProvider = new TestTimeProvider();
        var registry = new GameSessionRegistry(
            new StubGameCodeGenerator(["ABC123"]),
            timeProvider);
        registry.Create(CreateQuiz());
        var rose = registry.JoinPlayer("ABC123", "Rose");
        var mickey = registry.JoinPlayer("ABC123", "Mickey");
        registry.ConnectPlayer("ABC123", rose.AccessToken!, "rose-connection", true);
        registry.ConnectPlayer("ABC123", mickey.AccessToken!, "mickey-connection", true);
        registry.StartGame("ABC123");
        registry.SelectQuestion("ABC123", 1);
        registry.ActivateQuestionBuzzer("ABC123", 1);
        registry.ClaimQuestionBuzzer("rose-connection", 1);
        timeProvider.Advance(TimeSpan.FromMilliseconds(1001));

        var late = registry.ClaimQuestionBuzzer("mickey-connection", 1);

        Assert.Null(late);
        Assert.Empty(registry.Find("ABC123")!.BuzzerRace!.LatePlayers);
    }

    [Fact]
    public void ClaimQuestionBuzzer_accepts_approved_player_on_next_question()
    {
        var registry = CreateRegistry("ABC123");
        registry.Create(CreateTwoQuestionQuiz());
        var joined = registry.JoinPlayer("ABC123", "Rose");
        registry.ConnectPlayer(
            "ABC123",
            joined.AccessToken!,
            "connection-1",
            true);
        registry.StartGame("ABC123");

        registry.SelectQuestion("ABC123", 1);
        registry.ActivateQuestionBuzzer("ABC123", 1);
        var firstClaim = registry.ClaimQuestionBuzzer("connection-1", 1);
        registry.JudgeQuestionAnswer(
            "ABC123",
            1,
            joined.Player!.Id,
            true);
        registry.CloseQuestionAnswer("ABC123", 1);

        registry.SelectQuestion("ABC123", 2);
        registry.ActivateQuestionBuzzer("ABC123", 2);
        registry.SetPlayerVisibility("connection-1", false);
        var secondClaim = registry.ClaimQuestionBuzzer("connection-1", 2);

        Assert.True(firstClaim!.IsWinner);
        Assert.True(secondClaim!.IsWinner);
        Assert.Equal(2, secondClaim.Question.SourceQuestionId);
    }

    [Fact]
    public void JudgeQuestionAnswer_routes_score_and_resolution()
    {
        var registry = CreateRegistry("ABC123");
        var game = registry.Create(CreateQuiz());
        var player = registry.JoinPlayer("ABC123", "Rose").Player!;
        registry.StartGame("ABC123");
        registry.SelectQuestion("ABC123", 1);

        var attempt = registry.JudgeQuestionAnswer(
            "ABC123",
            1,
            player.Id,
            false);
        var question = registry.ResolveQuestionWithoutCorrectAnswer(
            "ABC123",
            1);

        Assert.Equal(-100, attempt!.ScoreDelta);
        Assert.Equal(-100, player.Score);
        Assert.Equal(
            BadWolfQuiz.Game.Runtime.RuntimeQuestionStatus.ShowingAnswer,
            question!.Status);

        var closed = registry.CloseQuestionAnswer("ABC123", 1);

        Assert.Equal(
            BadWolfQuiz.Game.Runtime.RuntimeQuestionStatus.Resolved,
            closed!.Status);
        Assert.Same(game.Session.Board.Questions.Single(), closed);
    }

    [Fact]
    public void ActivePlayer_commands_route_to_runtime_session()
    {
        var registry = CreateRegistry("ABC123");
        var game = registry.Create(CreateQuiz());
        var rose = registry.JoinPlayer("ABC123", "Rose").Player!;
        var mickey = registry.JoinPlayer("ABC123", "Mickey").Player!;

        Assert.Equal(rose.Id, game.Session.ActivePlayerId);

        registry.SetActivePlayer("ABC123", mickey.Id);

        Assert.Equal(mickey.Id, game.Session.ActivePlayerId);

        var selected = registry.SelectRandomActivePlayer("ABC123");

        Assert.Contains(
            selected!.Id,
            new[] { rose.Id, mickey.Id });
        Assert.Equal(selected.Id, game.Session.ActivePlayerId);
    }

    [Fact]
    public void Create_copies_custom_settings_into_runtime_session()
    {
        var registry = CreateRegistry("ABC123");
        var settings = new BadWolfQuiz.Game.Runtime.GameSessionSettings(
            TimeSpan.FromSeconds(45),
            TimeSpan.FromSeconds(12),
            BadWolfQuiz.Game.Runtime.GamePhaseStartMode.Automatic,
            BadWolfQuiz.Game.Runtime.GamePhaseStartMode.Manual);

        var game = registry.Create(CreateQuiz(), settings);

        Assert.Same(settings, game.Session.Settings);
        Assert.Equal(TimeSpan.FromSeconds(45), game.Session.Timer.Duration);
        Assert.Equal(TimeSpan.FromSeconds(12), game.Session.AnswerTimer.Duration);
    }

    [Fact]
    public void Final_question_routes_private_player_actions_by_approved_connection()
    {
        var registry = CreateRegistry("ABC123");
        var game = registry.Create(CreateFinalQuiz());
        var joined = registry.JoinPlayer("ABC123", "Rose");
        registry.ConnectPlayer(
            "ABC123",
            joined.AccessToken!,
            "connection-1",
            true);
        CompleteOnlyQuestion(registry, joined.Player!);

        registry.StartFinalQuestion("ABC123");
        var wager = registry.SubmitFinalWager("connection-1", 250);
        registry.LockFinalWagers("ABC123");
        var answer = registry.SubmitFinalAnswer(
            "connection-1",
            "Bad Wolf");
        registry.LockFinalAnswers("ABC123");
        registry.JudgeFinalAnswer(
            "ABC123",
            joined.Player!.Id,
            true);

        Assert.Equal(250, wager!.Submission.Wager!.Amount);
        Assert.Equal("Bad Wolf", answer!.Submission.Answer!.Text);
        Assert.Equal(
            BadWolfQuiz.Game.Runtime.GameSessionStatus.Completed,
            game.Session.Status);
        Assert.Equal(350, joined.Player.Score);
    }

    [Fact]
    public void Final_question_rejects_player_action_without_approved_connection()
    {
        var registry = CreateRegistry("ABC123");
        registry.Create(CreateFinalQuiz());
        var joined = registry.JoinPlayer("ABC123", "Rose");
        registry.ConnectPlayer(
            "ABC123",
            joined.AccessToken!,
            "connection-1",
            true);
        CompleteOnlyQuestion(registry, joined.Player!);
        registry.StartFinalQuestion("ABC123");
        registry.DisconnectPlayer("connection-1");

        var result = registry.SubmitFinalWager("connection-1", 100);

        Assert.Null(result);
    }

    [Fact]
    public void Final_question_excludes_negative_player_when_game_setting_is_disabled()
    {
        var settings = new BadWolfQuiz.Game.Runtime.GameSessionSettings(
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(10),
            BadWolfQuiz.Game.Runtime.GamePhaseStartMode.Manual,
            BadWolfQuiz.Game.Runtime.GamePhaseStartMode.Automatic,
            allowNegativeScoreFinalPlayers: false);
        var registry = CreateRegistry("ABC123");
        var game = registry.Create(CreateFinalQuiz(), settings);
        var positive = registry.JoinPlayer("ABC123", "Rose").Player!;
        var negative = registry.JoinPlayer("ABC123", "Mickey").Player!;
        registry.AdjustPlayerScore("ABC123", negative.Id, -1000);
        CompleteOnlyQuestion(registry, positive);

        var final = registry.StartFinalQuestion("ABC123");

        Assert.Contains(
            final!.Submissions,
            item => item.PlayerId == positive.Id);
        Assert.DoesNotContain(
            final.Submissions,
            item => item.PlayerId == negative.Id);
        Assert.Equal(-1000, negative.Score);
        Assert.False(game.Session.Settings.AllowNegativeScoreFinalPlayers);
    }

    private static GameSessionRegistry CreateRegistry(params string[] codes)
    {
        return new GameSessionRegistry(new StubGameCodeGenerator(codes));
    }

    private static QuizSnapshot CreateQuiz()
    {
        return new QuizSnapshot(
            1,
            "Test Quiz",
            [
                new QuizRoundSnapshot(
                    1,
                    "Round 1",
                    0,
                    [new QuizQuestionSnapshot(1, 1, 0, 100, false)])
            ]);
    }

    private static QuizSnapshot CreateTwoQuestionQuiz()
    {
        return new QuizSnapshot(
            1,
            "Two Question Quiz",
            [
                new QuizRoundSnapshot(
                    1,
                    "Round 1",
                    0,
                    [
                        new QuizQuestionSnapshot(1, 1, 0, 100, false),
                        new QuizQuestionSnapshot(2, 1, 1, 200, false)
                    ])
            ]);
    }

    private static QuizSnapshot CreateWagerQuiz()
    {
        return new QuizSnapshot(
            1,
            "Wager Quiz",
            [
                new QuizRoundSnapshot(
                    1,
                    "Round 1",
                    0,
                    [new QuizQuestionSnapshot(1, 1, 0, 100, true)])
            ]);
    }

    private static QuizSnapshot CreateFinalQuiz()
    {
        return new QuizSnapshot(
            1,
            "Final Quiz",
            [
                new QuizRoundSnapshot(
                    1,
                    "Round 1",
                    0,
                    [new QuizQuestionSnapshot(1, 1, 0, 100, false)])
            ],
            new FinalQuestionSnapshot(
                [CreateTextBlock(1, "Who are you?")],
                [CreateTextBlock(2, "Bad Wolf")]));
    }

    private static ContentBlockSnapshot CreateTextBlock(int id, string text)
    {
        return new ContentBlockSnapshot(
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
            0,
            false);
    }

    private static void CompleteOnlyQuestion(
        GameSessionRegistry registry,
        BadWolfQuiz.Game.Runtime.GamePlayer player)
    {
        registry.StartGame("ABC123");
        registry.SelectQuestion("ABC123", 1);
        registry.JudgeQuestionAnswer("ABC123", 1, player.Id, true);
        registry.CloseQuestionAnswer("ABC123", 1);
    }

    private sealed class TestTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan duration)
        {
            _utcNow += duration;
        }
    }

    private sealed class StubGameCodeGenerator(IEnumerable<string> codes) : IGameCodeGenerator
    {
        private readonly Queue<string> _codes = new(codes);

        public string Create()
        {
            return _codes.Dequeue();
        }
    }
}
