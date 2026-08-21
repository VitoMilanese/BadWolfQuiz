namespace BadWolfQuiz.Web.Tests;

public sealed class AnswerKeyWindowRegressionTests
{
    [Fact]
    public void Lobby_registers_the_dedicated_answer_key_window_helper()
    {
        var imports = File.ReadAllText(FindWebFile("Pages", "_ViewImports.cshtml"));
        var tagHelper = File.ReadAllText(FindWebFile(
            "TagHelpers",
            "AnswerKeyWindowAssetsTagHelper.cs"));

        Assert.Contains(
            "AnswerKeyWindowAssetsTagHelper, BadWolfQuiz.Web",
            imports,
            StringComparison.Ordinal);
        Assert.Contains("is not LobbyModel", tagHelper, StringComparison.Ordinal);
        Assert.Contains(
            "/js/answer-key-window.js?v=1.22.0-281.2",
            tagHelper,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Existing_answer_key_links_keep_the_native_blank_window_fallback()
    {
        var layout = File.ReadAllText(FindWebFile("Pages", "Shared", "_Layout.cshtml"));
        var lobby = File.ReadAllText(FindWebFile(
            "Pages",
            "Admin",
            "Games",
            "Lobby.cshtml"));

        Assert.Contains("asp-page=\"/Admin/Games/AnswerKey\"", layout, StringComparison.Ordinal);
        Assert.Contains("asp-page=\"/Admin/Games/AnswerKey\"", lobby, StringComparison.Ordinal);
        Assert.Contains("target=\"_blank\"", layout, StringComparison.Ordinal);
        Assert.Contains("target=\"_blank\"", lobby, StringComparison.Ordinal);
        Assert.Contains("rel=\"noopener\"", layout, StringComparison.Ordinal);
        Assert.Contains("rel=\"noopener\"", lobby, StringComparison.Ordinal);
    }

    [Fact]
    public void Helper_resolves_target_screen_before_opening_the_managed_popup()
    {
        var script = File.ReadAllText(FindWebFile(
            "wwwroot",
            "js",
            "answer-key-window.js"));

        Assert.Contains(
            "const answerKeyWindowName = \"badwolf-answer-key\";",
            script,
            StringComparison.Ordinal);
        Assert.Contains("window.getScreenDetails()", script, StringComparison.Ordinal);
        Assert.Contains(
            "const screenDetails = await getScreenDetailsForPlacement();",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "const targetBounds = getAvailableBounds(getOtherScreen(screenDetails));",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "openAnswerKeyWindow(anchor.href, targetBounds);",
            script,
            StringComparison.Ordinal);

        var detailsIndex = script.IndexOf(
            "const screenDetails = await getScreenDetailsForPlacement();",
            StringComparison.Ordinal);
        var openIndex = script.IndexOf(
            "openAnswerKeyWindow(anchor.href, targetBounds);",
            StringComparison.Ordinal);
        Assert.True(detailsIndex >= 0 && openIndex > detailsIndex);
    }

    [Fact]
    public void Managed_popup_uses_target_screen_coordinates_in_window_open_features()
    {
        var script = File.ReadAllText(FindWebFile(
            "wwwroot",
            "js",
            "answer-key-window.js"));

        Assert.Contains("`left=${Math.round(bounds.left)}`", script, StringComparison.Ordinal);
        Assert.Contains("`top=${Math.round(bounds.top)}`", script, StringComparison.Ordinal);
        Assert.Contains("`width=${Math.round(bounds.width)}`", script, StringComparison.Ordinal);
        Assert.Contains("`height=${Math.round(bounds.height)}`", script, StringComparison.Ordinal);
        Assert.Contains(
            "buildPopupFeatures(bounds));",
            script,
            StringComparison.Ordinal);
        Assert.Contains("popupWindow.moveTo", script, StringComparison.Ordinal);
        Assert.Contains("popupWindow.resizeTo", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Helper_targets_another_screen_and_preserves_native_fallbacks()
    {
        var script = File.ReadAllText(FindWebFile(
            "wwwroot",
            "js",
            "answer-key-window.js"));

        Assert.Contains(
            "typeof window.getScreenDetails !== \"function\"",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "windowManagementPermissionState === \"denied\"",
            script,
            StringComparison.Ordinal);
        Assert.Contains("details?.currentScreen", script, StringComparison.Ordinal);
        Assert.Contains("details?.screens", script, StringComparison.Ordinal);
        Assert.Contains("screen?.availLeft", script, StringComparison.Ordinal);
        Assert.Contains("screen?.availTop", script, StringComparison.Ordinal);
        Assert.Contains("screen?.availWidth", script, StringComparison.Ordinal);
        Assert.Contains("screen?.availHeight", script, StringComparison.Ordinal);
        Assert.Contains("event.preventDefault();", script, StringComparison.Ordinal);
        Assert.Contains("} catch {", script, StringComparison.Ordinal);
    }

    private static string FindWebFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidateParts = new[]
            {
                directory.FullName,
                "src",
                "BadWolfQuiz.Web"
            }.Concat(parts).ToArray();
            var candidate = Path.Combine(candidateParts);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"Could not locate BadWolfQuiz.Web file: {Path.Combine(parts)}");
    }
}
