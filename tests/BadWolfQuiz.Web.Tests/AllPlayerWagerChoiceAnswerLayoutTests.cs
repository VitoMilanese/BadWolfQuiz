namespace BadWolfQuiz.Web.Tests;

public sealed class AllPlayerWagerChoiceAnswerLayoutTests
{
    [Fact]
    public void Wager_choice_answer_uses_dedicated_non_squeezing_layout()
    {
        var script = ReadWebFile("wwwroot", "js", "all-player-question.js")
            .ReplaceLineEndings("\n");

        Assert.Contains("const hasWagerResults = (state.players ?? [])", script);
        Assert.Contains(".some(player => player.wagerSubmitted);", script);
        Assert.Contains("const isWagerMultipleChoiceAnswer =", script);
        Assert.Contains("isMultipleChoiceAnswer && hasWagerResults", script);
        Assert.Contains("all-player-wager-multiple-choice-answer", script);
        Assert.Contains(
            ".host-game-board.all-player-wager-multiple-choice-answer\n    .current-question-summary:not(.wager-mode)",
            script);
        Assert.Contains("max-height: min(24dvh, 12rem);", script);
        Assert.Contains("overflow-y: auto;", script);
        Assert.Contains("overflow-y: auto !important;", script);
        Assert.Contains("overscroll-behavior: contain;", script);
    }

    [Fact]
    public void Wager_choice_layout_does_not_change_normal_choice_answers()
    {
        var script = ReadWebFile("wwwroot", "js", "all-player-question.js")
            .ReplaceLineEndings("\n");

        Assert.Contains(
            "board.classList.toggle(\n                \"all-player-wager-multiple-choice-answer\",\n                isWagerMultipleChoiceAnswer);",
            script);
        Assert.Contains(
            "\"all-player-wager-multiple-choice-answer\",\n                    \"all-player-text-reviewing\"",
            script);
        Assert.DoesNotContain(
            ".host-game-board.all-player-multiple-choice-answer\n    .current-question-summary:not(.wager-mode) > .all-player-host-progress",
            script);
    }

    private static string ReadWebFile(params string[] parts)
    {
        var root = FindRepositoryRoot();
        return File.ReadAllText(Path.Combine(
            new[] { root, "src", "BadWolfQuiz.Web" }.Concat(parts).ToArray()));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "BadWolfQuiz.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
