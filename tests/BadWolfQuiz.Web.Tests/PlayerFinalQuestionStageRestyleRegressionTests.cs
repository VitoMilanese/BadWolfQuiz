namespace BadWolfQuiz.Web.Tests;

public sealed class PlayerFinalQuestionStageRestyleRegressionTests
{
    [Fact]
    public void Player_final_stage_asset_is_registered_and_scoped_to_final_states()
    {
        var root = FindRepositoryRoot();
        var viewImports = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "_ViewImports.cshtml"));
        var tagHelper = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "TagHelpers",
            "PlayerFinalQuestionStageAssetsTagHelper.cs"));
        var css = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "css",
            "player-final-question-stage.css"));

        Assert.Contains(
            "PlayerFinalQuestionStageAssetsTagHelper",
            viewImports,
            StringComparison.Ordinal);
        Assert.Contains(
            "Attributes = \"data-final-status\"",
            tagHelper,
            StringComparison.Ordinal);
        Assert.Contains(
            "/css/player-final-question-stage.css?v=4",
            tagHelper,
            StringComparison.Ordinal);
        Assert.Contains(
            "/css/player-final-question-stage-stability.css?v=1",
            tagHelper,
            StringComparison.Ordinal);
        Assert.Contains(
            "/js/player-final-fallback-refresh.js?v=1",
            tagHelper,
            StringComparison.Ordinal);

        foreach (var status in new[]
                 {
                     "finalwagering",
                     "finalanswering",
                     "finaljudging",
                     "completed"
                 })
        {
            Assert.Contains(
                $"[data-final-status=\"{status}\"]",
                css,
                StringComparison.Ordinal);
            Assert.Contains($"\"{status}\"", tagHelper, StringComparison.Ordinal);
        }

        Assert.DoesNotContain(
            "[data-final-status=\"running\"]",
            css,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "[data-final-status=\"lobby\"]",
            css,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Player_final_stage_preserves_wager_answer_and_refresh_contracts()
    {
        var root = FindRepositoryRoot();
        var player = NormalizeLineEndings(File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "Player",
            "Lobby.cshtml")));
        var admissionCss = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "css",
            "player-admission-menu.css"));

        Assert.Contains("id=\"player-final-panel\"", player, StringComparison.Ordinal);
        Assert.Contains("@if (submission.Wager is null)", player, StringComparison.Ordinal);
        Assert.Contains("id=\"final-wager-form\"", player, StringComparison.Ordinal);
        Assert.Contains("data-final-wager-digit", player, StringComparison.Ordinal);
        Assert.Contains("data-final-wager-max", player, StringComparison.Ordinal);
        Assert.Contains(
            "GameSessionStatus.FinalAnswering &&\n                    submission.Answer is null",
            player,
            StringComparison.Ordinal);
        Assert.Contains("id=\"final-answer-form\"", player, StringComparison.Ordinal);
        Assert.Contains("id=\"final-answer\"", player, StringComparison.Ordinal);
        Assert.Contains("connection.invoke(\"SubmitFinalWager\"", player, StringComparison.Ordinal);
        Assert.Contains("connection.invoke(\"SubmitFinalAnswer\"", player, StringComparison.Ordinal);
        Assert.Contains("reloadForGameTransition", player, StringComparison.Ordinal);
        Assert.Contains("connection.on(\"FinalQuestionStateChanged\"", player, StringComparison.Ordinal);
        Assert.Contains("connection.on(\"FinalQuestionProgressChanged\"", player, StringComparison.Ordinal);

        Assert.Contains(
            ".player-lobby[data-final-status=\"finalanswering\"] #player-final-panel > .game-content-blocks { display: none; }",
            admissionCss,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            ".player-lobby[data-final-status=\"finaljudging\"] #player-final-panel > .game-content-blocks { display: none; }",
            admissionCss,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Player_final_stage_uses_fixed_viewport_and_responsive_completed_rating_layout()
    {
        var root = FindRepositoryRoot();
        var css = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "css",
            "player-final-question-stage.css"));

        Assert.Contains("position: fixed;", css, StringComparison.Ordinal);
        Assert.Contains("inset: var(--topbar-height, 60px) 0 0;", css, StringComparison.Ordinal);
        Assert.Contains("#player-final-panel", css, StringComparison.Ordinal);
        Assert.Contains("#final-wager-form .wager-keypad", css, StringComparison.Ordinal);
        Assert.Contains("#final-answer-form textarea", css, StringComparison.Ordinal);
        Assert.Contains("[data-final-status=\"completed\"]:has(> .quiz-rating-panel)", css, StringComparison.Ordinal);
        Assert.Contains("grid-template-columns: minmax(0, 1.15fr) minmax(300px, .85fr);", css, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 620px)", css, StringComparison.Ordinal);
        Assert.Contains("@media (max-height: 720px) and (min-width: 621px)", css, StringComparison.Ordinal);
        Assert.Contains("@media (prefers-reduced-motion: reduce)", css, StringComparison.Ordinal);
        Assert.Contains("> #game-timer", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Completed_player_rating_restores_mobile_safe_star_hit_targets()
    {
        var root = FindRepositoryRoot();
        var css = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "css",
            "player-final-question-stage.css"));

        Assert.Contains(
            ".player-lobby[data-final-status=\"completed\"] .star-rating label:not(.zero-rating)",
            css,
            StringComparison.Ordinal);
        Assert.Contains("min-inline-size: 44px;", css, StringComparison.Ordinal);
        Assert.Contains("min-block-size: 44px;", css, StringComparison.Ordinal);
        Assert.Contains("touch-action: manipulation;", css, StringComparison.Ordinal);
        Assert.Contains(
            ".player-lobby[data-final-status=\"completed\"] .star-rating input[type=\"radio\"]",
            css,
            StringComparison.Ordinal);
        Assert.Contains("pointer-events: none;", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Player_final_stage_avoids_document_wide_relational_relayout_and_blur_filters()
    {
        var root = FindRepositoryRoot();
        var css = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "css",
            "player-final-question-stage.css"));

        Assert.DoesNotContain(
            "body.gameplay-layout:has(.player-lobby",
            css,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "backdrop-filter",
            css,
            StringComparison.Ordinal);
        Assert.Contains(
            "overscroll-behavior: contain;",
            css,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Player_final_stage_isolates_unrelated_player_controllers_and_keeps_media_runtime_measurable()
    {
        var root = FindRepositoryRoot();
        var tagHelper = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "TagHelpers",
            "PlayerFinalQuestionStageAssetsTagHelper.cs"));
        var stabilityCss = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "css",
            "player-final-question-stage-stability.css"));

        Assert.Contains("window.badWolfPlayerFinalStage=true", tagHelper, StringComparison.Ordinal);
        Assert.Contains("window.badWolfAllPlayerQuestionInitialized=true", tagHelper, StringComparison.Ordinal);
        Assert.Contains("window.BadWolfAnonymousSharedWagerPlayerStarted=true", tagHelper, StringComparison.Ordinal);
        Assert.Contains("window.badWolfPeerRatedAllPlayerInitialized=true", tagHelper, StringComparison.Ordinal);
        Assert.Contains("window.badWolfPeerRatedPolishInitialized=true", tagHelper, StringComparison.Ordinal);

        Assert.Contains("> .player-media-settings", stabilityCss, StringComparison.Ordinal);
        Assert.Contains("display: block !important;", stabilityCss, StringComparison.Ordinal);
        Assert.Contains("left: -10000px !important;", stabilityCss, StringComparison.Ordinal);
        Assert.Contains("visibility: hidden !important;", stabilityCss, StringComparison.Ordinal);
        Assert.DoesNotContain("display: none !important;", stabilityCss, StringComparison.Ordinal);
    }

    private static string NormalizeLineEndings(string content) =>
        content.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src")) &&
                Directory.Exists(Path.Combine(directory.FullName, "tests")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
