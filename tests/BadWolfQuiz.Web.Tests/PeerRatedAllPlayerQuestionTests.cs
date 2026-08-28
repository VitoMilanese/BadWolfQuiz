using System.Reflection;
using BadWolfQuiz.Game.Definitions;
using BadWolfQuiz.Game.Runtime;
using BadWolfQuiz.Web.Pages;
using BadWolfQuiz.Web.Services;

namespace BadWolfQuiz.Web.Tests;

public sealed class PeerRatedAllPlayerQuestionTests
{
    [Theory]
    [InlineData(0.0, 0)]
    [InlineData(2.0, 40)]
    [InlineData(2.1, 45)]
    [InlineData(2.49, 45)]
    [InlineData(2.5, 50)]
    [InlineData(2.51, 55)]
    [InlineData(2.99, 55)]
    [InlineData(3.0, 60)]
    [InlineData(5.0, 100)]
    public void Peer_rating_percentage_matches_increment_rules(
        double averageStars,
        int expectedPercentage)
    {
        Assert.Equal(
            expectedPercentage,
            PeerRatedAllPlayerScoring.CalculateRewardPercentage(averageStars));
    }

    [Fact]
    public void Peer_rating_points_are_derived_from_question_value()
    {
        Assert.Equal(450, PeerRatedAllPlayerScoring.CalculateAwardedPoints(1000, 2.2));
        Assert.Equal(500, PeerRatedAllPlayerScoring.CalculateAwardedPoints(1000, 2.5));
        Assert.Equal(550, PeerRatedAllPlayerScoring.CalculateAwardedPoints(1000, 2.8));
    }

    [Fact]
    public void Peer_rated_snapshot_forces_regular_non_buzzer_non_wager_behavior()
    {
        var question = CreatePeerQuestion(
            isSpecial: true,
            buzzerMode: QuestionBuzzerMode.Immediately,
            excludeFromRandomWagerSelection: false);

        Assert.Equal(
            QuestionPresentationType.AllPlayerPeerRatedText,
            question.PresentationType);
        Assert.False(question.IsSpecial);
        Assert.Equal(QuestionBuzzerMode.Disabled, question.BuzzerMode);
        Assert.Equal(0, question.BuzzDelaySeconds);
        Assert.False(question.IsEligibleForRandomWagerSelection);
    }

    [Fact]
    public void Peer_review_snapshot_preserves_answers_ratings_exclusions_and_position()
    {
        var session = CreateSession();
        var rose = session.AddPlayer("Rose");
        var jack = session.AddPlayer("Jack");
        var donna = session.AddPlayer("Donna");
        session.Start();
        var question = session.SelectQuestion(100);
        var game = new GameSessionRegistration("ABC123", session, "host");
        var review = game.GetOrCreatePeerRatedAllPlayerReview(
            question,
            session.Players.Select(player => player.Id));

        review.Answers[rose.Id] = "Rose answer";
        review.Answers[jack.Id] = "Jack answer";
        review.Answers[donna.Id] = "Donna answer";
        review.RatingsByAnswerPlayer[rose.Id] = new Dictionary<GamePlayerId, int>
        {
            [jack.Id] = 4,
            [donna.Id] = 5
        };
        review.ExcludedPlayerIds.Add(donna.Id);
        review.CompletedAnswerPlayerIds.Add(rose.Id);
        review.ReviewIndex = 1;

        var snapshot = Assert.Single(game.CapturePeerRatedAllPlayerReviews());
        var restored = new GameSessionRegistration("ABC123", session, "host");
        restored.RestorePeerRatedAllPlayerReviews([snapshot]);
        var restoredReview = restored.PeerRatedAllPlayerReviews[100];

        Assert.Equal(3, restoredReview.Answers.Count);
        Assert.Equal("Rose answer", restoredReview.Answers[rose.Id]);
        Assert.Contains(donna.Id, restoredReview.ExcludedPlayerIds);
        Assert.Contains(rose.Id, restoredReview.CompletedAnswerPlayerIds);
        Assert.Equal(1, restoredReview.ReviewIndex);
        Assert.Equal(4, restoredReview.RatingsByAnswerPlayer[rose.Id][jack.Id]);
        Assert.False(restoredReview.RatingsByAnswerPlayer[rose.Id].ContainsKey(donna.Id));
    }

    [Fact]
    public void Answer_collection_does_not_advance_review_index_past_players()
    {
        var session = CreateSession();
        var rose = session.AddPlayer("Rose");
        var jack = session.AddPlayer("Jack");
        var donna = session.AddPlayer("Donna");
        session.Start();
        var question = session.SelectQuestion(100);
        var game = new GameSessionRegistration("ABC123", session, "host");
        var review = game.GetOrCreatePeerRatedAllPlayerReview(
            question,
            session.Players.Select(player => player.Id));

        // Reproduce the broken state created by the old lifecycle normalizer.
        review.ReviewIndex = review.ParticipantIds.Count;
        InvokeEnsureLifecycle(game, question, review);
        Assert.Equal(0, review.ReviewIndex);

        review.Answers[rose.Id] = "Rose answer";
        InvokeEnsureLifecycle(game, question, review);
        Assert.Equal(0, review.ReviewIndex);

        review.Answers[jack.Id] = "Jack answer";
        review.Answers[donna.Id] = "Donna answer";
        InvokeEnsureLifecycle(game, question, review);

        Assert.Equal(0, review.ReviewIndex);
        Assert.Empty(review.CompletedAnswerPlayerIds);
    }

    [Fact]
    public void Client_and_api_cover_separate_voting_and_results_flow()
    {
        var script = File.ReadAllText(FindRepoFile(
            "src", "BadWolfQuiz.Web", "wwwroot", "js",
            "peer-rated-all-player-question.js"));
        var api = File.ReadAllText(FindRepoFile(
            "src", "BadWolfQuiz.Web", "Pages",
            "PeerRatedAllPlayerQuestion.cshtml.cs"));

        Assert.Contains("option[value=\"5\"]", script, StringComparison.Ordinal);
        Assert.Contains("star-rating", script, StringComparison.Ordinal);
        Assert.Contains("zeroStars", script, StringComparison.Ordinal);
        Assert.Contains("post(\"Exclude\"", script, StringComparison.Ordinal);
        Assert.Contains("post(\"NextResult\"", script, StringComparison.Ordinal);
        Assert.Contains("post(\"ReturnToBoard\"", script, StringComparison.Ordinal);
        Assert.Contains("text.showResults", script, StringComparison.Ordinal);
        Assert.Contains("text.nextResult", script, StringComparison.Ordinal);
        Assert.Contains("state.phase === \"results\"", script, StringComparison.Ordinal);
        Assert.Contains("renderRaterList(sidebar, state, false)", script, StringComparison.Ordinal);

        Assert.Contains("player.Id == requestedAuthorId", api, StringComparison.Ordinal);
        Assert.Contains("RemoveRatingsByPlayer", api, StringComparison.Ordinal);
        Assert.Contains("BeginResults", api, StringComparison.Ordinal);
        Assert.Contains("OnPostNextResult", api, StringComparison.Ordinal);
        Assert.Contains("phase = answering ? \"answering\" : showingResults ? \"results\" : \"rating\"", api, StringComparison.Ordinal);
        Assert.Contains("ResolveQuestionWithoutCorrectAnswer", api, StringComparison.Ordinal);
        Assert.Contains("CloseQuestionAnswer", api, StringComparison.Ordinal);
    }

    [Fact]
    public void Host_peer_review_uses_stable_right_sidebar_and_reserves_scrollbar_lane()
    {
        var script = File.ReadAllText(FindRepoFile(
            "src", "BadWolfQuiz.Web", "wwwroot", "js",
            "peer-rated-all-player-question.js"));

        Assert.Contains(".peer-rated-host-ui", script, StringComparison.Ordinal);
        Assert.Contains("position: absolute", script, StringComparison.Ordinal);
        Assert.Contains("peer-rated-host-sidebar", script, StringComparison.Ordinal);
        Assert.Contains("peer-rated-host-content-reserved", script, StringComparison.Ordinal);
        Assert.Contains("findScrollbarOwner", script, StringComparison.Ordinal);
        Assert.Contains("scrollbarReserve", script, StringComparison.Ordinal);
        Assert.Contains("--peer-rated-stage-right-gap", script, StringComparison.Ordinal);
        Assert.Contains("hostControllers.has(code)", script, StringComparison.Ordinal);
        Assert.DoesNotContain("peer-rated-host-review {", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Active_game_snapshot_persists_peer_review_state()
    {
        var store = File.ReadAllText(FindRepoFile(
            "src", "BadWolfQuiz.Web", "Services", "ActiveGameStore.cs"));
        var persistence = File.ReadAllText(FindRepoFile(
            "src", "BadWolfQuiz.Web", "Services",
            "ActiveGamePersistenceService.cs"));

        Assert.Contains("PeerRatedAllPlayerReviews", store, StringComparison.Ordinal);
        Assert.Contains("CapturePeerRatedAllPlayerReviews", persistence, StringComparison.Ordinal);
        Assert.Contains("RestorePeerRatedAllPlayerReviews", persistence, StringComparison.Ordinal);
    }

    private static void InvokeEnsureLifecycle(
        GameSessionRegistration game,
        RuntimeQuestion question,
        PeerRatedAllPlayerReviewState review)
    {
        var method = typeof(PeerRatedAllPlayerQuestionModel).GetMethod(
            "EnsureLifecycle",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        method.Invoke(null, [game, question, review]);
    }

    private static GameSession CreateSession()
    {
        var question = CreatePeerQuestion();
        var quiz = new QuizSnapshot(
            1,
            "Peer rating",
            [new QuizRoundSnapshot(1, "Round", 0, [question])]);
        return GameSession.Create(quiz);
    }

    private static QuizQuestionSnapshot CreatePeerQuestion(
        bool isSpecial = false,
        QuestionBuzzerMode buzzerMode = QuestionBuzzerMode.UseGameSetting,
        bool excludeFromRandomWagerSelection = false) => new(
            sourceQuestionId: 100,
            sourceCategoryId: 10,
            rowIndex: 0,
            points: 1000,
            isSpecial: isSpecial,
            categoryTitle: "Category",
            excludeFromRandomWagerSelection: excludeFromRandomWagerSelection,
            questionBlocks:
            [
                new ContentBlockSnapshot(
                    1,
                    ContentBlockKind.Text,
                    "Question",
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
                    "Reference answer",
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
            presentationType: QuestionPresentationType.AllPlayerPeerRatedText,
            buzzerMode: buzzerMode,
            buzzDelaySeconds: 5);

    private static string FindRepoFile(params string[] relativeParts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var parts = new string[relativeParts.Length + 1];
            parts[0] = directory.FullName;
            Array.Copy(relativeParts, 0, parts, 1, relativeParts.Length);
            var candidate = Path.Combine(parts);
            if (File.Exists(candidate))
            {
                return candidate;
            }
            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"Could not locate repository file: {Path.Combine(relativeParts)}");
    }
}
