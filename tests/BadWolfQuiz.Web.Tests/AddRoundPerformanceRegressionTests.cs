namespace BadWolfQuiz.Web.Tests;

public sealed class AddRoundPerformanceRegressionTests
{
    [Fact]
    public void Add_round_loads_only_the_latest_round_template_without_cartesian_eager_loading()
    {
        var root = FindRepositoryRoot();
        var editor = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "Admin",
            "Quizzes",
            "Editor.cshtml.cs"));

        var start = editor.IndexOf(
            "public async Task<IActionResult> OnPostAddRoundAsync()",
            StringComparison.Ordinal);
        var end = editor.IndexOf(
            "public async Task<IActionResult> OnPostReorderRoundsAsync()",
            start,
            StringComparison.Ordinal);

        Assert.True(start >= 0 && end > start);
        var method = editor[start..end];

        Assert.Contains("db.QuizRounds", method, StringComparison.Ordinal);
        Assert.Contains(".AsNoTracking()", method, StringComparison.Ordinal);
        Assert.Contains(".OrderByDescending(x => x.SortOrder)", method, StringComparison.Ordinal);
        Assert.Contains(".AsSplitQuery()", method, StringComparison.Ordinal);
        Assert.Contains("db.QuizRounds.Add(round)", method, StringComparison.Ordinal);
        Assert.DoesNotContain(".Include(x => x.Rounds)", method, StringComparison.Ordinal);
        Assert.DoesNotContain("quiz.Rounds.Count", method, StringComparison.Ordinal);
    }

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
