namespace BadWolfQuiz.Web.Tests;

public sealed class AnonymousSharedWagerEditorUiRegressionTests
{
    [Fact]
    public void Question_editor_places_wager_mode_between_wager_options_and_buzzer_mode()
    {
        var page = ReadWebFile("Pages", "Admin", "Quizzes", "QuestionEditor.cshtml");
        var model = ReadWebFile("Pages", "Admin", "Quizzes", "QuestionEditor.cshtml.cs");
        var imports = ReadWebFile("Pages", "_ViewImports.cshtml");

        var special = page.IndexOf("Input.IsSpecial", StringComparison.Ordinal);
        var exclusion = page.IndexOf("Input.ExcludeFromRandomWagerSelection", StringComparison.Ordinal);
        var wagerMode = page.IndexOf("Input.WagerMode", StringComparison.Ordinal);
        var buzzer = page.IndexOf("Input.BuzzModeOverride", StringComparison.Ordinal);

        Assert.True(special >= 0 && special < exclusion);
        Assert.True(exclusion < wagerMode);
        Assert.True(wagerMode < buzzer);
        Assert.Contains("QuestionWagerModes.GetMode", model);
        Assert.Contains("QuestionWagerModes.Apply", model);
        Assert.DoesNotContain("AnonymousSharedWagerEditorTagHelper", imports);
    }

    [Fact]
    public void Round_editor_exposes_independent_anonymous_random_wager_controls()
    {
        var page = ReadWebFile("Pages", "Admin", "Quizzes", "Editor.cshtml");
        var model = ReadWebFile("Pages", "Admin", "Quizzes", "Editor.cshtml.cs");
        var board = ReadGameFile("Runtime", "GameBoard.cs");

        Assert.Contains("RoundRows.UseRandomWagerQuestions", page);
        Assert.Contains("RoundRows.UseRandomAnonymousSharedWagerQuestions", page);
        Assert.Contains("RoundRows.RandomAnonymousSharedWagerQuestionCount", page);
        Assert.Contains("requestedRandomWagerCount", model);
        Assert.Contains("!anonymousShared.Contains", board);
        Assert.Contains("QuestionWagerModes.AnonymousShared", board);
    }

    private static string ReadWebFile(params string[] parts) =>
        ReadRepositoryFile(new[] { "src", "BadWolfQuiz.Web" }.Concat(parts).ToArray());

    private static string ReadGameFile(params string[] parts) =>
        ReadRepositoryFile(new[] { "src", "BadWolfQuiz.Game" }.Concat(parts).ToArray());

    private static string ReadRepositoryFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate)) return File.ReadAllText(candidate);
            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not locate {Path.Combine(parts)}");
    }
}
