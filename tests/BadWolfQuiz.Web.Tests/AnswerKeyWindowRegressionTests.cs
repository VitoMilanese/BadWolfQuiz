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
            "/js/answer-key-window.js?v=1.22.0-281.1",
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
    public void Helper_opens_the_named_window_before_requesting_screen_details()
    {
        var script = File.ReadAllText(FindWebFile(
            "wwwroot",
            "js",
            "answer-key-window.js"));

        Assert.Contains(
            "const answerKeyWindowName = \"badwolf-answer-key\";",
            script,
            StringComparison.Ordinal);
        Assert.Contains("window.open(", script, StringComparison.Ordinal);
        Assert.Contains("window.getScreenDetails()", script, StringComparison.Ordinal);

        var openIndex = script.IndexOf("window.open(", StringComparison.Ordinal);
        var screenDetailsIndex = script.IndexOf(
            "window.getScreenDetails()",
            StringComparison.Ordinal);
        Assert.True(openIndex >= 0 && screenDetailsIndex > openIndex);

        var popupFailureIndex = script.IndexOf(
            "if (!answerKeyWindow)",
            StringComparison.Ordinal);
        var preventDefaultIndex = script.IndexOf(
            "event.preventDefault();",
            StringComparison.Ordinal);
        Assert.True(popupFailureIndex >= 0 && preventDefaultIndex > popupFailureIndex);
    }

    [Fact]
    public void Helper_targets_another_screen_and_falls_back_without_blocking_answer_key()
    {
        var script = File.ReadAllText(FindWebFile(
            "wwwroot",
            "js",
            "answer-key-window.js"));

        Assert.Contains("window.screen?.isExtended === false", script, StringComparison.Ordinal);
        Assert.Contains(
            "typeof window.getScreenDetails !== \"function\"",
            script,
            StringComparison.Ordinal);
        Assert.Contains("details?.currentScreen", script, StringComparison.Ordinal);
        Assert.Contains("details?.screens", script, StringComparison.Ordinal);
        Assert.Contains("screen?.availLeft", script, StringComparison.Ordinal);
        Assert.Contains("screen?.availTop", script, StringComparison.Ordinal);
        Assert.Contains("screen?.availWidth", script, StringComparison.Ordinal);
        Assert.Contains("screen?.availHeight", script, StringComparison.Ordinal);
        Assert.Contains("popupWindow.moveTo", script, StringComparison.Ordinal);
        Assert.Contains("popupWindow.resizeTo", script, StringComparison.Ordinal);
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
