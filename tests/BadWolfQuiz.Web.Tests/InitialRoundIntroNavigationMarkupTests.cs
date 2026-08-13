namespace BadWolfQuiz.Web.Tests;

public sealed class InitialRoundIntroNavigationMarkupTests
{
    [Fact]
    public void Standalone_first_round_intro_advances_frames_without_reload_and_fits_viewport()
    {
        var markup = File.ReadAllText(FindWebFile(
            "Pages",
            "Admin",
            "Games",
            "RunningRoundIntro.cshtml"));

        Assert.Contains("main.page-shell:has(> .game-intro-page)", markup);
        Assert.Contains("height: calc(100dvh - var(--topbar-height, 0px));", markup);
        Assert.Contains("main.page-shell:has(> .game-intro-page) > .game-intro-page", markup);
        Assert.Contains("const loadFrame = async targetUrl =>", markup);
        Assert.Contains("\"X-Requested-With\": \"XMLHttpRequest\"", markup);
        Assert.Contains("currentPage.replaceWith(document.importNode(nextPage, true));", markup);
        Assert.Contains("history.replaceState(", markup);
        Assert.DoesNotContain("window.location.href = next.href;", markup);
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
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"Could not locate BadWolfQuiz.Web file: {Path.Combine(parts)}");
    }
}
