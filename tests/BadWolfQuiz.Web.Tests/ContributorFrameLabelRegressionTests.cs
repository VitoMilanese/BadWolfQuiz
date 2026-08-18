namespace BadWolfQuiz.Web.Tests;

public sealed class ContributorFrameLabelRegressionTests
{
    [Fact]
    public void Frame_settings_only_show_label_on_selector_button()
    {
        var root = FindRepositoryRoot();
        var gameSettings = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "Admin",
            "Games",
            "_GameSettingsFields.cshtml"));
        var contributorSupport = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "TagHelpers",
            "ContributorSupportTagHelper.cs"));

        Assert.DoesNotContain(
            "<span>@ContributorLocalizer[\"ContributorFrame_Label\"]</span>",
            gameSettings,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "<span>{{html.Encode(localizer[\"ContributorFrame_Label\"].Value)}}</span>",
            contributorSupport,
            StringComparison.Ordinal);

        Assert.Contains(
            "data-open-contributor-frame-picker",
            gameSettings,
            StringComparison.Ordinal);
        Assert.Contains(
            "data-open-contributor-frame-picker",
            contributorSupport,
            StringComparison.Ordinal);
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
