namespace BadWolfQuiz.Web.Tests;

public sealed class HeaderSideMenuRegressionTests
{
    [Fact]
    public void Layout_uses_modal_side_drawer_for_the_main_header_menu()
    {
        var root = FindRepositoryRoot();
        var layout = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "Shared",
            "_Layout.cshtml"));

        Assert.Contains("data-open-header-menu", layout, StringComparison.Ordinal);
        Assert.Contains("<dialog id=\"header-menu-drawer\"", layout, StringComparison.Ordinal);
        Assert.Contains("data-header-side-menu", layout, StringComparison.Ordinal);
        Assert.Contains("data-close-header-menu", layout, StringComparison.Ordinal);
        Assert.Contains("~/css/header-side-menu.css", layout, StringComparison.Ordinal);
        Assert.Contains("~/js/header-side-menu.js", layout, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "<details class=\"action-menu header-action-menu\">",
            layout,
            StringComparison.Ordinal);

        Assert.Contains("Menu_Quizzes", layout, StringComparison.Ordinal);
        Assert.Contains("Menu_PublicQuizzes", layout, StringComparison.Ordinal);
        Assert.Contains("Menu_Minigames", layout, StringComparison.Ordinal);
        Assert.Contains("Menu_GameHistory", layout, StringComparison.Ordinal);
        Assert.Contains("Menu_PlayerStatistics", layout, StringComparison.Ordinal);
        Assert.Contains("Menu_Settings", layout, StringComparison.Ordinal);
        Assert.Contains("Menu_SignOut", layout, StringComparison.Ordinal);
    }

    [Fact]
    public void Drawer_assets_cover_modal_interaction_responsiveness_and_reduced_motion()
    {
        var root = FindRepositoryRoot();
        var css = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "css",
            "header-side-menu.css"));
        var js = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "js",
            "header-side-menu.js"));

        Assert.Contains(".header-side-menu::backdrop", css, StringComparison.Ordinal);
        Assert.Contains("transform: translateX(100%);", css, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 520px)", css, StringComparison.Ordinal);
        Assert.Contains("@media (prefers-reduced-motion: reduce)", css, StringComparison.Ordinal);
        Assert.Contains("overflow: hidden;", css, StringComparison.Ordinal);

        Assert.Contains("dialog.showModal();", js, StringComparison.Ordinal);
        Assert.Contains("event.target === dialog", js, StringComparison.Ordinal);
        Assert.Contains("dialog.addEventListener('cancel'", js, StringComparison.Ordinal);
        Assert.Contains("header-side-menu-open", js, StringComparison.Ordinal);
        Assert.Contains("aria-expanded", js, StringComparison.Ordinal);
        Assert.Contains("returnFocus.focus", js, StringComparison.Ordinal);
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
