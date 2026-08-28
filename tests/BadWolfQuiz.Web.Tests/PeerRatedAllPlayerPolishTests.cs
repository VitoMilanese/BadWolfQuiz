using System.Reflection;
using BadWolfQuiz.Game.Definitions;
using BadWolfQuiz.Game.Runtime;
using BadWolfQuiz.Web.Services;

namespace BadWolfQuiz.Web.Tests;

public sealed class PeerRatedAllPlayerPolishTests
{
    [Fact]
    public void Peer_rated_question_has_no_separate_correct_answer()
    {
        var question = CreatePeerQuestion();

        Assert.False(question.HasCorrectAnswer);
        Assert.Equal(question.QuestionBlocks, question.AnswerBlocks);
        Assert.Equal(question.QuestionBlocks, question.RevealAnswerBlocks);
        Assert.Empty(question.StoredAnswerBlocks);
        Assert.Equal("Question", Assert.Single(question.AnswerBlocks).TextContent);
        Assert.DoesNotContain(
            question.AnswerBlocks,
            block => string.Equals(
                block.TextContent,
                "Reference answer",
                StringComparison.Ordinal));
    }

    [Fact]
    public void First_peer_review_answer_belongs_to_lowest_scoring_player()
    {
        var session = CreateSession();
        var high = session.AddPlayer("High");
        var low = session.AddPlayer("Low");
        var middle = session.AddPlayer("Middle");
        ApplyScore(high, 500);
        ApplyScore(low, -100);
        ApplyScore(middle, 120);
        session.Start();

        var question = session.SelectQuestion(100);
        var game = new GameSessionRegistration("ABC123", session, "host");
        var review = game.GetOrCreatePeerRatedAllPlayerReview(
            question,
            session.Players.Select(player => player.Id));

        Assert.Equal(
            new[] { low.Id, middle.Id, high.Id },
            review.ParticipantIds);
    }

    [Fact]
    public void Restored_peer_review_keeps_the_captured_score_order()
    {
        var session = CreateSession();
        var high = session.AddPlayer("High");
        var low = session.AddPlayer("Low");
        var middle = session.AddPlayer("Middle");
        ApplyScore(high, 500);
        ApplyScore(low, -100);
        ApplyScore(middle, 120);
        session.Start();

        var question = session.SelectQuestion(100);
        var game = new GameSessionRegistration("ABC123", session, "host");
        var review = game.GetOrCreatePeerRatedAllPlayerReview(
            question,
            session.Players.Select(player => player.Id));
        var snapshot = Assert.Single(game.CapturePeerRatedAllPlayerReviews());

        ApplyScore(high, -1000);
        var restored = new GameSessionRegistration("ABC123", session, "host");
        restored.RestorePeerRatedAllPlayerReviews([snapshot]);

        Assert.Equal(
            review.ParticipantIds,
            restored.PeerRatedAllPlayerReviews[100].ParticipantIds);
    }

    [Fact]
    public void Client_hides_buzzer_answer_editor_and_legacy_host_controls()
    {
        var script = File.ReadAllText(FindRepoFile(
            "src", "BadWolfQuiz.Web", "wwwroot", "js",
            "peer-rated-all-player-polish.js"));

        Assert.Contains(
            ".player-lobby.peer-rated-player-active .player-buzzer-panel",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            ".player-buzzer-panel[data-hidden-by-peer-rated=\"true\"]",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            ".host-game-board.peer-rated-question-shell-active .question-controls",
            script,
            StringComparison.Ordinal);
        Assert.Contains("answer-blocks", script, StringComparison.Ordinal);
        Assert.Contains(
            "[data-open-question-preview=\"answer\"]",
            script,
            StringComparison.Ordinal);
        Assert.Contains("presentation.value === \"5\"", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Polish_observer_does_not_watch_attributes_or_self_trigger_on_class_hidden_changes()
    {
        var script = File.ReadAllText(FindRepoFile(
            "src", "BadWolfQuiz.Web", "wwwroot", "js",
            "peer-rated-all-player-polish.js"));

        Assert.Contains("const setClassState", script, StringComparison.Ordinal);
        Assert.Contains("if (!buzzer.hidden)", script, StringComparison.Ordinal);
        Assert.Contains("childList: true", script, StringComparison.Ordinal);
        Assert.Contains("subtree: true", script, StringComparison.Ordinal);
        Assert.DoesNotContain("attributes: true", script, StringComparison.Ordinal);
        Assert.DoesNotContain("attributeFilter:", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Return_to_board_hides_the_resolved_question_until_the_host_shell_refreshes()
    {
        var script = File.ReadAllText(FindRepoFile(
            "src", "BadWolfQuiz.Web", "wwwroot", "js",
            "peer-rated-all-player-polish.js"));

        Assert.Contains(
            ".host-game-board.peer-rated-returning-to-board .current-question-summary",
            script,
            StringComparison.Ordinal);
        Assert.Contains("const setReturningToBoard", script, StringComparison.Ordinal);
        Assert.Contains("setReturningToBoard(true)", script, StringComparison.Ordinal);
        Assert.Contains("if (!response.ok)", script, StringComparison.Ordinal);
        Assert.Contains("window.location.reload()", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Initial_peer_sidebar_reserves_the_bottom_player_card_strip()
    {
        var script = File.ReadAllText(FindRepoFile(
            "src", "BadWolfQuiz.Web", "wwwroot", "js",
            "peer-rated-all-player-polish.js"));

        Assert.Contains(".game-scoreboard", script, StringComparison.Ordinal);
        Assert.Contains("summaryRect.bottom - scoreboardRect.top", script, StringComparison.Ordinal);
        Assert.Contains("ui.style.bottom", script, StringComparison.Ordinal);
        Assert.Contains("ResizeObserver", script, StringComparison.Ordinal);
        Assert.Contains("schedulePeerRatedHostBounds", script, StringComparison.Ordinal);
        Assert.Contains("window.setTimeout", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Premature_peer_host_overlay_is_removed_until_question_summary_exists()
    {
        var guard = File.ReadAllText(FindRepoFile(
            "src", "BadWolfQuiz.Web", "wwwroot", "js",
            "peer-rated-host-mount-guard.js"));

        Assert.Contains(".current-question-summary", guard, StringComparison.Ordinal);
        Assert.Contains(".peer-rated-host-ui", guard, StringComparison.Ordinal);
        Assert.Contains("summary.contains(ui)", guard, StringComparison.Ordinal);
        Assert.Contains("ui.remove()", guard, StringComparison.Ordinal);
        Assert.Contains("badwolf:host-gameplay-updated", guard, StringComparison.Ordinal);
        Assert.Contains("MutationObserver", guard, StringComparison.Ordinal);
    }

    [Fact]
    public void Client_forces_board_refresh_and_question_only_resolved_preview()
    {
        var script = File.ReadAllText(FindRepoFile(
            "src", "BadWolfQuiz.Web", "wwwroot", "js",
            "peer-rated-all-player-polish.js"));
        var metadata = File.ReadAllText(FindRepoFile(
            "src", "BadWolfQuiz.Web", "Pages",
            "PeerRatedQuestionMetadata.cshtml.cs"));

        Assert.Contains("handler\") === \"ReturnToBoard\"", script, StringComparison.Ordinal);
        Assert.Contains("window.location.reload()", script, StringComparison.Ordinal);
        Assert.Contains("previewAnswer", script, StringComparison.Ordinal);
        Assert.Contains("removePreviewAnswerParameter", script, StringComparison.Ordinal);
        Assert.Contains("rewardTemplate", script, StringComparison.Ordinal);
        Assert.Contains("AllPlayerPeerRatedText", metadata, StringComparison.Ordinal);
        Assert.Contains("hasCorrectAnswer", metadata, StringComparison.Ordinal);
    }

    [Fact]
    public void Answer_key_uses_question_content_and_question_media_for_peer_rating()
    {
        var model = File.ReadAllText(FindRepoFile(
            "src", "BadWolfQuiz.Web", "Pages", "Admin", "Games",
            "AnswerKey.cshtml.cs"));
        var script = File.ReadAllText(FindRepoFile(
            "src", "BadWolfQuiz.Web", "wwwroot", "js",
            "peer-rated-all-player-polish.js"));

        Assert.Contains("IsPeerRatedQuestion", model, StringComparison.Ordinal);
        Assert.Contains("? question.QuestionBlocks", model, StringComparison.Ordinal);
        Assert.Contains("DeferredGameMediaRole.Question", model, StringComparison.Ordinal);
        Assert.Contains("syncAnswerKey", script, StringComparison.Ordinal);
        Assert.Contains("answer-key-header-context h2", script, StringComparison.Ordinal);
        Assert.Contains("answerKeyText.question", script, StringComparison.Ordinal);
        Assert.Contains("answerKeyText.showQuestion", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Peer_assets_load_mount_guard_immediately_after_the_runtime_script()
    {
        var tagHelper = File.ReadAllText(FindRepoFile(
            "src", "BadWolfQuiz.Web", "TagHelpers",
            "PeerRatedAllPlayerAssetsTagHelper.cs"));

        var runtime = tagHelper.IndexOf(
            "peer-rated-all-player-question.js?v=2",
            StringComparison.Ordinal);
        var mountGuard = tagHelper.IndexOf(
            "peer-rated-host-mount-guard.js?v=1",
            StringComparison.Ordinal);
        var rating = tagHelper.IndexOf(
            "peer-rated-all-player-rating-confirmation.js?v=2",
            StringComparison.Ordinal);
        var polish = tagHelper.IndexOf(
            "peer-rated-all-player-polish.js?v=3",
            StringComparison.Ordinal);

        Assert.True(runtime >= 0);
        Assert.True(mountGuard > runtime);
        Assert.True(rating > mountGuard);
        Assert.True(polish > rating);
    }

    private static void ApplyScore(GamePlayer player, int points)
    {
        var method = typeof(GamePlayer).GetMethod(
            "ApplyScore",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method.Invoke(player, [points]);
    }

    private static GameSession CreateSession()
    {
        var quiz = new QuizSnapshot(
            1,
            "Peer rating",
            [new QuizRoundSnapshot(1, "Round", 0, [CreatePeerQuestion()])]);
        return GameSession.Create(quiz);
    }

    private static QuizQuestionSnapshot CreatePeerQuestion() => new(
        sourceQuestionId: 100,
        sourceCategoryId: 10,
        rowIndex: 0,
        points: 200,
        isSpecial: false,
        categoryTitle: "Category",
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
        presentationType: QuestionPresentationType.AllPlayerPeerRatedText);

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
