namespace BadWolfQuiz.Web.Tests;

public sealed class HostQuestionControlsRegressionTests
{
    [Fact]
    public void Quick_score_keeps_one_question_selector_and_hides_unplayed_questions_by_default()
    {
        var script = ReadScript();

        Assert.DoesNotContain("data-quick-score-round", script, StringComparison.Ordinal);
        Assert.DoesNotContain("data-quick-score-category", script, StringComparison.Ordinal);
        Assert.DoesNotContain("data-quick-score-question-value", script, StringComparison.Ordinal);
        Assert.Contains("data-quick-score-question", script, StringComparison.Ordinal);
        Assert.Contains("data-quick-score-show-unplayed", script, StringComparison.Ordinal);
        Assert.Contains(
            "Показати ще не зіграні питання",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "question.played || showUnplayed.checked",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "sourceSelect.replaceChildren(...visibleQuestions.map(createOption));",
            script,
            StringComparison.Ordinal);
        Assert.Contains("showUnplayed.checked = false;", script, StringComparison.Ordinal);
        Assert.Contains("renderQuestions(false);", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Quick_score_initializes_when_persistent_shell_inserts_gameplay_dom()
    {
        var script = ReadScript();

        Assert.Contains("const getMetadataUrl = () =>", script, StringComparison.Ordinal);
        Assert.DoesNotContain("const metadataUrl = gameId", script, StringComparison.Ordinal);
        Assert.Contains(
            "const metadataUrl = getMetadataUrl();",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "sourceSelect.replaceChildren();",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "const initializeCurrentDom = () =>",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "new MutationObserver(() => initializeCurrentDom()).observe(",
            script,
            StringComparison.Ordinal);
        Assert.Contains("document.documentElement", script, StringComparison.Ordinal);
        Assert.Contains("subtree: true", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Quick_score_metadata_orders_played_questions_newest_first_and_unplayed_last()
    {
        var endpoint = Read(
            FindRepositoryRoot(),
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "Admin",
            "Games",
            "QuickScoreQuestions.cshtml.cs");

        Assert.Contains(
            "GetQuestionOpenSequence(",
            endpoint,
            StringComparison.Ordinal);
        Assert.Contains(
            ".Where(item =>\n                    item.OpenSequence.HasValue ||\n                    addableRoundIds.Contains(item.Question.SourceRoundId))",
            endpoint.Replace("\r\n", "\n", StringComparison.Ordinal),
            StringComparison.Ordinal);
        Assert.Contains(
            ".OrderBy(item => item.OpenSequence.HasValue ? 0 : 1)",
            endpoint,
            StringComparison.Ordinal);
        Assert.Contains(
            ".ThenByDescending(item => item.OpenSequence ?? long.MinValue)",
            endpoint,
            StringComparison.Ordinal);
        Assert.Contains(
            "played = item.OpenSequence.HasValue",
            endpoint,
            StringComparison.Ordinal);
        Assert.Contains(
            ".Take(game.Session.CurrentRoundIndex + 1)",
            endpoint,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Question_open_sequence_tracks_actual_open_states_and_is_persisted()
    {
        var root = FindRepositoryRoot();
        var registration = Read(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Services",
            "GameSessionRegistration.cs");
        var store = Read(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Services",
            "ActiveGameStore.cs");
        var persistence = Read(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Services",
            "ActiveGamePersistenceService.cs");

        Assert.Contains("TrackQuestionOpenings();", registration, StringComparison.Ordinal);
        Assert.Contains("RuntimeQuestionStatus.Selected", registration, StringComparison.Ordinal);
        Assert.Contains("RuntimeQuestionStatus.AwaitingWager", registration, StringComparison.Ordinal);
        Assert.Contains("RuntimeQuestionStatus.Active", registration, StringComparison.Ordinal);
        Assert.Contains("RuntimeQuestionStatus.ShowingAnswer", registration, StringComparison.Ordinal);
        Assert.DoesNotContain("RuntimeQuestionStatus.Resolved", registration, StringComparison.Ordinal);
        Assert.Contains("QuestionOpenSequenceState", store, StringComparison.Ordinal);
        Assert.Contains(
            "game.CaptureQuestionOpenSequence()",
            persistence,
            StringComparison.Ordinal);
        Assert.Contains(
            "game.RestoreQuestionOpenSequence(snapshot.QuestionOpenSequence);",
            persistence,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Resolved_question_context_menu_is_immediate_and_has_no_close_slot()
    {
        var script = ReadScript();

        Assert.Contains(
            ".host-board-question.status-resolved[data-question-resolved]",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "event.stopImmediatePropagation();",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "closeAction.style.setProperty(\"display\", \"none\");",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "menu.style.gridTemplateColumns = \"42px\";",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "menu.style.removeProperty(\"grid-template-columns\");",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "closeAction?.style.removeProperty(\"display\");",
            script,
            StringComparison.Ordinal);
        Assert.DoesNotContain("queueMicrotask", script, StringComparison.Ordinal);
        Assert.Contains(
            "installResolvedQuestionGiftMenu();",
            script,
            StringComparison.Ordinal);
        Assert.Contains("previewQuestionId", script, StringComparison.Ordinal);
        Assert.Contains("giftResolve.checked = false;", script, StringComparison.Ordinal);
        Assert.Contains("giftResolveOption.hidden = true;", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Global_gameplay_loader_loads_current_host_question_controls_revision()
    {
        var root = FindRepositoryRoot();
        var loader = Read(
            root,
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "js",
            "quick-timer-controls.js");

        Assert.Contains(
            "badWolfHostQuestionControlsLoaderInstalled",
            loader,
            StringComparison.Ordinal);
        Assert.Contains(
            "/js/host-question-controls.js?v=6",
            loader,
            StringComparison.Ordinal);
        Assert.Contains(
            "script.dataset.hostQuestionControls = \"\";",
            loader,
            StringComparison.Ordinal);
    }

    private static string ReadScript()
    {
        var root = FindRepositoryRoot();
        return Read(
            root,
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "js",
            "host-question-controls.js");
    }

    private static string Read(string root, params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { root }.Concat(parts).ToArray()));

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