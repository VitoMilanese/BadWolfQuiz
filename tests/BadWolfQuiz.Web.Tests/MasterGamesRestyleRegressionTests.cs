namespace BadWolfQuiz.Web.Tests;

public sealed class MasterGamesRestyleRegressionTests
{
    [Fact]
    public void Master_games_uses_dedicated_dashboard_presentation()
    {
        var markup = ReadWebFile("Pages", "Admin", "MasterGames.cshtml");

        Assert.Contains("@model BadWolfQuiz.Web.Pages.Admin.MasterGamesModel", markup, StringComparison.Ordinal);
        Assert.Contains("~/css/master-games.css", markup, StringComparison.Ordinal);
        Assert.Contains("class=\"master-games-page\"", markup, StringComparison.Ordinal);
        Assert.Contains("class=\"master-games-hero\"", markup, StringComparison.Ordinal);
        Assert.Contains("class=\"master-games-content\"", markup, StringComparison.Ordinal);
        Assert.Contains("class=\"master-games-list\"", markup, StringComparison.Ordinal);
        Assert.Contains("class=\"master-games-card", markup, StringComparison.Ordinal);
        Assert.Contains("class=\"master-games-scoreboard\"", markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Existing_localized_labels_and_live_game_data_remain_visible()
    {
        var markup = ReadWebFile("Pages", "Admin", "MasterGames.cshtml");

        Assert.Contains("Localizer[\"Admin_Eyebrow\"]", markup, StringComparison.Ordinal);
        Assert.Contains("Localizer[\"MasterGames_Title\"]", markup, StringComparison.Ordinal);
        Assert.Contains("Localizer[\"MasterGames_Empty\"]", markup, StringComparison.Ordinal);
        Assert.Contains("Localizer[\"MasterGames_InLobby\"]", markup, StringComparison.Ordinal);
        Assert.Contains("Localizer[\"MasterGames_Playing\"]", markup, StringComparison.Ordinal);
        Assert.Contains("Localizer[\"MasterGames_SessionId\"]", markup, StringComparison.Ordinal);
        Assert.Contains("Localizer[\"MasterGames_HostId\"]", markup, StringComparison.Ordinal);
        Assert.Contains("Localizer[\"MasterGames_HostName\"]", markup, StringComparison.Ordinal);
        Assert.Contains("Localizer[\"MasterGames_Round\"]", markup, StringComparison.Ordinal);
        Assert.Contains("Localizer[\"MasterGames_Players\"]", markup, StringComparison.Ordinal);
        Assert.Contains("@game.SessionId", markup, StringComparison.Ordinal);
        Assert.Contains("@game.HostId", markup, StringComparison.Ordinal);
        Assert.Contains("@game.HostName", markup, StringComparison.Ordinal);
        Assert.Contains("@game.RoundTitle", markup, StringComparison.Ordinal);
        Assert.Contains("@player.Name", markup, StringComparison.Ordinal);
        Assert.Contains("@player.Score", markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Join_code_and_create_game_contracts_are_preserved()
    {
        var markup = ReadWebFile("Pages", "Admin", "MasterGames.cshtml");

        Assert.Contains("data-open-join-code", markup, StringComparison.Ordinal);
        Assert.Contains("data-game-code=\"@game.PublicCode\"", markup, StringComparison.Ordinal);
        Assert.Contains("data-qr-url=\"@Url.Page(\"/Admin/MasterGames\", \"JoinQrCode\", new { id = game.SessionId })\"", markup, StringComparison.Ordinal);
        Assert.Contains("asp-page-handler=\"CreateGame\"", markup, StringComparison.Ordinal);
        Assert.Contains("name=\"quizId\"", markup, StringComparison.Ordinal);
        Assert.Contains("id=\"join-code-dialog\"", markup, StringComparison.Ordinal);
        Assert.Contains("data-join-qr-code", markup, StringComparison.Ordinal);
        Assert.Contains("data-join-code-value", markup, StringComparison.Ordinal);
        Assert.Contains("dialog?.showModal();", markup, StringComparison.Ordinal);
        Assert.Contains("dialog?.close()", markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Dedicated_styles_cover_dashboard_cards_dialog_and_responsiveness()
    {
        var styles = ReadWebFile("wwwroot", "css", "master-games.css")
            .ReplaceLineEndings("\n");

        Assert.Contains("body.portal-layout:has(.master-games-page) > .page-shell", styles, StringComparison.Ordinal);
        Assert.Contains(".master-games-hero {", styles, StringComparison.Ordinal);
        Assert.Contains(".master-games-list {", styles, StringComparison.Ordinal);
        Assert.Contains(".master-games-card {", styles, StringComparison.Ordinal);
        Assert.Contains(".master-games-details {", styles, StringComparison.Ordinal);
        Assert.Contains(".master-games-scoreboard {", styles, StringComparison.Ordinal);
        Assert.Contains(".master-games-join-dialog {", styles, StringComparison.Ordinal);
        Assert.Contains("overflow-wrap: anywhere;", styles, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 1180px)", styles, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 820px)", styles, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 620px)", styles, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 420px)", styles, StringComparison.Ordinal);
        Assert.Contains(":focus-visible", styles, StringComparison.Ordinal);
        Assert.Contains("@media (prefers-reduced-motion: reduce)", styles, StringComparison.Ordinal);
    }

    [Fact]
    public void Page_model_preserves_master_host_filtering_and_qr_behavior()
    {
        var model = ReadWebFile("Pages", "Admin", "MasterGames.cshtml.cs");

        Assert.Contains("if (!IsMasterHost())", model, StringComparison.Ordinal);
        Assert.Contains("GameHub.IsHostConnected(game)", model, StringComparison.Ordinal);
        Assert.Contains("sessionRegistry.HasConnectedPlayer(game)", model, StringComparison.Ordinal);
        Assert.Contains("public async Task<IActionResult> OnPostCreateGameAsync", model, StringComparison.Ordinal);
        Assert.Contains("gameSessionLauncher.CreateAsync(quizId, cancellationToken)", model, StringComparison.Ordinal);
        Assert.Contains("public IActionResult OnGetJoinQrCode(Guid id)", model, StringComparison.Ordinal);
        Assert.Contains("joinUrlBuilder.Build(Request, game.PublicCode)", model, StringComparison.Ordinal);
        Assert.Contains("generator.CreateQrCode(", model, StringComparison.Ordinal);
        Assert.Contains("return File(qrCode.GetGraphic(16), \"image/png\");", model, StringComparison.Ordinal);
    }

    private static string ReadWebFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                new[] { directory.FullName, "src", "BadWolfQuiz.Web" }
                    .Concat(parts)
                    .ToArray());
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not locate web file: {string.Join('/', parts)}");
    }
}
