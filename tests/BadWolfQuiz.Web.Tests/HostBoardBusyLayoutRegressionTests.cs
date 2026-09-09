namespace BadWolfQuiz.Web.Tests;

public sealed class HostBoardBusyLayoutRegressionTests
{
    [Fact]
    public void Host_board_keeps_host_card_spacing_and_border_box_while_question_is_loading()
    {
        var styles = File.ReadAllText(FindWebFile(
            "wwwroot",
            "css",
            "player-regular-gameplay-host-followup.css"));
        var helper = File.ReadAllText(FindWebFile(
            "TagHelpers",
            "AnonymousSharedWagerAssetsTagHelper.cs"));

        Assert.Contains(
            "> .host-board-layout[aria-busy=\"true\"]:not([hidden])",
            styles,
            StringComparison.Ordinal);
        Assert.Contains(
            "box-sizing: border-box !important;",
            styles,
            StringComparison.Ordinal);
        Assert.Contains(
            ".host-board-layout[aria-busy=\"true\"]:not([hidden]):has(> .board-host-sidebar)",
            styles,
            StringComparison.Ordinal);
        Assert.Contains(
            "padding-left: calc(var(--board-host-width, 190px) + 24px) !important;",
            styles,
            StringComparison.Ordinal);
        Assert.Contains(
            "/css/player-regular-gameplay-host-followup.css?v=6",
            helper,
            StringComparison.Ordinal);
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
