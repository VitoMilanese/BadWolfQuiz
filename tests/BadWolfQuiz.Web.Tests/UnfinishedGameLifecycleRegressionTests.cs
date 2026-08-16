namespace BadWolfQuiz.Web.Tests;

public sealed class UnfinishedGameLifecycleRegressionTests
{
    [Fact]
    public void Quiz_list_exposes_confirmed_replacement_and_unfinished_delete_actions()
    {
        var root = FindRepositoryRoot();
        var page = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "Admin",
            "Quizzes",
            "Index.cshtml"));
        var pageModel = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "Admin",
            "Quizzes",
            "Index.cshtml.cs"));
        var launcher = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Services",
            "GameSessionLauncher.cs"));
        var persistence = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Services",
            "ActiveGamePersistenceService.cs"));
        var availability = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Services",
            "ActiveGameAvailability.cs"));

        Assert.Contains("data-open-delete-unfinished-dialog", page, StringComparison.Ordinal);
        Assert.Contains("asp-page-handler=\"DeleteUnfinishedGame\"", page, StringComparison.Ordinal);
        Assert.Contains("data-open-replace-game-dialog", page, StringComparison.Ordinal);
        Assert.Contains("data-confirm-replace-game", page, StringComparison.Ordinal);
        Assert.Contains("name=\"replaceUnfinished\"", page, StringComparison.Ordinal);

        var unfinishedDelete = page.IndexOf(
            "data-open-delete-unfinished-dialog",
            StringComparison.Ordinal);
        var quizDelete = page.IndexOf(
            "data-open-delete-dialog",
            unfinishedDelete,
            StringComparison.Ordinal);
        Assert.True(unfinishedDelete >= 0 && quizDelete > unfinishedDelete);

        Assert.Contains("OnPostDeleteUnfinishedGameAsync", pageModel, StringComparison.Ordinal);
        Assert.Contains("activeGameStore.RemoveAsync", pageModel, StringComparison.Ordinal);
        Assert.Contains("bool replaceUnfinished", pageModel, StringComparison.Ordinal);
        Assert.Contains("ReplacementConfirmationRequired", pageModel, StringComparison.Ordinal);

        var deleteHandlerStart = pageModel.IndexOf(
            "OnPostDeleteUnfinishedGameAsync",
            StringComparison.Ordinal);
        var nextHandlerStart = pageModel.IndexOf(
            "OnPostArchiveMediaAsync",
            deleteHandlerStart,
            StringComparison.Ordinal);
        var deleteHandler = pageModel[deleteHandlerStart..nextHandlerStart];
        Assert.DoesNotContain("IsArchived =", deleteHandler, StringComparison.Ordinal);
        Assert.DoesNotContain("SaveChangesAsync", deleteHandler, StringComparison.Ordinal);

        Assert.Contains(
            "sessionRegistry.Create(snapshot, settings);",
            launcher,
            StringComparison.Ordinal);
        Assert.Contains(
            "registration.AssignHost(currentHost.RequiredId);",
            launcher,
            StringComparison.Ordinal);
        Assert.Contains(
            "question.Status != RuntimeQuestionStatus.Available",
            persistence,
            StringComparison.Ordinal);
        Assert.Contains(
            "question.Status != RuntimeQuestionStatus.Available",
            availability,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Russian_unfinished_game_resources_follow_project_convention()
    {
        var root = FindRepositoryRoot();
        var resourcePath = Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Resources",
            "Localization",
            "UnfinishedGameResource.ru.resx");
        var document = System.Xml.Linq.XDocument.Load(resourcePath);
        var values = document.Root!
            .Elements("data")
            .Select(data => data.Element("value")?.Value)
            .ToArray();

        Assert.NotEmpty(values);
        Assert.All(values, value => Assert.Equal("Україна", value));
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
