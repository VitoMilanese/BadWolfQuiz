namespace BadWolfQuiz.Web.Tests;

public sealed class GlobalHostFrameSettingsRegressionTests
{
    [Fact]
    public void Global_host_settings_mount_avatar_frame_controls_into_redesigned_host_grid()
    {
        var settings = File.ReadAllText(FindWebFile(
            "Pages",
            "Admin",
            "Settings",
            "Index.cshtml"));
        var supportHelper = File.ReadAllText(FindWebFile(
            "TagHelpers",
            "ContributorSupportTagHelper.cs"));
        var settingsHelper = File.ReadAllText(FindWebFile(
            "TagHelpers",
            "ContributorGameSettingsTagHelper.cs"));

        Assert.Contains("host-settings-host-grid", settings, StringComparison.Ordinal);
        Assert.Contains("host-avatar-input-row", settings, StringComparison.Ordinal);
        Assert.Contains("contributor-host-frame-template", supportHelper, StringComparison.Ordinal);
        Assert.Contains(
            ".host-settings-form .host-settings-host-grid",
            settingsHelper,
            StringComparison.Ordinal);
        Assert.Contains("template.content.cloneNode(true)", settingsHelper, StringComparison.Ordinal);
        Assert.Contains("host-avatar-frame-global-settings", settingsHelper, StringComparison.Ordinal);
        Assert.Contains("frameRowHost.append(avatarField, fragment)", settingsHelper, StringComparison.Ordinal);
    }

    private static string FindWebFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                new[]
                {
                    directory.FullName,
                    "src",
                    "BadWolfQuiz.Web"
                }.Concat(parts).ToArray());

            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"Could not locate BadWolfQuiz.Web file: {string.Join('/', parts)}");
    }
}
