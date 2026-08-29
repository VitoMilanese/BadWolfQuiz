namespace BadWolfQuiz.Web.Tests;

public sealed class QuestionCopyImmediateRefreshTests
{
    [Fact]
    public void Quiz_editor_loads_same_round_copy_refresh_before_copy_action()
    {
        var loader = ReadWebFile("wwwroot", "js", "editor-save-overlay.js");

        var refreshIndex = loader.IndexOf(
            "/js/question-copy-board-refresh.js",
            StringComparison.Ordinal);
        var actionIndex = loader.IndexOf(
            "/js/question-copy-action.js",
            StringComparison.Ordinal);

        Assert.True(refreshIndex >= 0);
        Assert.True(actionIndex > refreshIndex);
        Assert.Contains(
            "questionCopyBoardRefreshScript.async = false",
            loader,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Same_round_copy_refreshes_the_board_without_page_reload()
    {
        var script = ReadWebFile(
            "wwwroot",
            "js",
            "question-copy-board-refresh.js");

        Assert.Contains(
            "badwolf:question-copy-succeeded",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "Number(copyResult?.quizId) !== currentQuizId",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "Number(copyResult?.roundId) !== currentRoundId",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "new DOMParser().parseFromString(html, \"text/html\")",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "currentCell.replaceChildren(",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "const rowWasAdded = nextPointRows.length > currentPointRows.length;",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "currentPoints.replaceChildren(",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "currentColumn.insertBefore(",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "reloadQuestionCopyAction();",
            script,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "window.location.reload()",
            script,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Refreshed_question_cells_keep_delete_copy_and_drag_interactions()
    {
        var script = ReadWebFile(
            "wwwroot",
            "js",
            "question-copy-board-refresh.js");

        Assert.Contains(
            "questionCopyRefreshDynamic",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "deleteQuestionForm?.addEventListener(\"submit\"",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "typeof window.exchangeQuestions !== \"function\"",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "window.badWolfQuestionCopyActionInitialized = false",
            script,
            StringComparison.Ordinal);
    }

    private static string ReadWebFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                new[] { directory.FullName, "src", "BadWolfQuiz.Web" }
                    .Concat(parts)
                    .ToArray());
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"Could not locate {Path.Combine(parts)}");
    }
}
