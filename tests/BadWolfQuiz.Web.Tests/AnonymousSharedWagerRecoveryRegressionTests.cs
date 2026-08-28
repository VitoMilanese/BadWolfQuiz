namespace BadWolfQuiz.Web.Tests;

public sealed class AnonymousSharedWagerRecoveryRegressionTests
{
    [Fact]
    public void Active_game_snapshot_persists_private_shared_wager_state()
    {
        var root = FindRepositoryRoot();
        var store = File.ReadAllText(Path.Combine(
            root, "src", "BadWolfQuiz.Web", "Services", "ActiveGameStore.cs"));
        var persistence = File.ReadAllText(Path.Combine(
            root, "src", "BadWolfQuiz.Web", "Services", "ActiveGamePersistenceService.cs"));

        Assert.Contains("AnonymousSharedWagerState? AnonymousSharedWager = null", store);
        Assert.Contains("AnonymousSharedWagerWebStore.Capture(game)", persistence);
        Assert.Contains("AnonymousSharedWagerWebStore.Restore(", persistence);
        Assert.Contains("snapshot.AnonymousSharedWager", persistence);
    }

    [Fact]
    public void Recovery_field_is_optional_for_legacy_active_game_json()
    {
        var root = FindRepositoryRoot();
        var store = File.ReadAllText(Path.Combine(
            root, "src", "BadWolfQuiz.Web", "Services", "ActiveGameStore.cs"));

        Assert.Contains("AnonymousSharedWagerState? AnonymousSharedWager = null", store);
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
