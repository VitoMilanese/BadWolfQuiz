namespace BadWolfQuiz.Web.Tests;

public sealed class DiscordSettingsRestyleRegressionTests
{
    [Fact]
    public void Discord_settings_use_dedicated_portal_presentation()
    {
        var markup = ReadWebFile("Pages", "Admin", "Settings", "Discord.cshtml");

        Assert.Contains("~/css/discord-settings.css", markup, StringComparison.Ordinal);
        Assert.Contains("class=\"discord-settings discord-settings-page", markup, StringComparison.Ordinal);
        Assert.Contains("class=\"discord-settings-hero\"", markup, StringComparison.Ordinal);
        Assert.Contains("class=\"discord-settings-overview", markup, StringComparison.Ordinal);
        Assert.Contains("class=\"discord-settings-summary\"", markup, StringComparison.Ordinal);
        Assert.Contains("class=\"discord-settings-workspace\"", markup, StringComparison.Ordinal);
        Assert.Contains("class=\"discord-settings-card discord-settings-channel-card\"", markup, StringComparison.Ordinal);
        Assert.Contains("class=\"discord-settings-card discord-settings-automation-card\"", markup, StringComparison.Ordinal);
        Assert.Contains("class=\"discord-settings-card discord-settings-control-card\"", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("class=\"content-panel narrow discord-settings\"", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("<style>", markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Existing_discord_actions_and_localized_copy_are_preserved()
    {
        var markup = ReadWebFile("Pages", "Admin", "Settings", "Discord.cshtml");

        Assert.Contains("Localizer[\"Discord_SettingsTitle\"]", markup, StringComparison.Ordinal);
        Assert.Contains("Localizer[\"Discord_NotEnabled\"]", markup, StringComparison.Ordinal);
        Assert.Contains("Localizer[\"Discord_ConnectDescription\"]", markup, StringComparison.Ordinal);
        Assert.Contains("Localizer[\"Discord_Account\"]", markup, StringComparison.Ordinal);
        Assert.Contains("Localizer[\"Discord_Server\"]", markup, StringComparison.Ordinal);
        Assert.Contains("Localizer[\"Discord_VoiceChannel\"]", markup, StringComparison.Ordinal);
        Assert.Contains("Localizer[\"Discord_Status\"]", markup, StringComparison.Ordinal);
        Assert.Contains("Localizer[\"Discord_BotStatus\"]", markup, StringComparison.Ordinal);
        Assert.Contains("Localizer[\"Discord_MutePermission\"]", markup, StringComparison.Ordinal);
        Assert.Contains("Localizer[\"Discord_AutoMute\"]", markup, StringComparison.Ordinal);
        Assert.Contains("Localizer[\"Discord_AutoMuteHint\"]", markup, StringComparison.Ordinal);
        Assert.Contains("Localizer[\"Discord_Test\"]", markup, StringComparison.Ordinal);
        Assert.Contains("Localizer[\"Discord_Disconnect\"]", markup, StringComparison.Ordinal);

        Assert.Contains("asp-page-handler=\"Connect\"", markup, StringComparison.Ordinal);
        Assert.Contains("asp-page-handler=\"Save\"", markup, StringComparison.Ordinal);
        Assert.Contains("asp-page-handler=\"SaveAutomaticMute\"", markup, StringComparison.Ordinal);
        Assert.Contains("asp-page-handler=\"Disconnect\"", markup, StringComparison.Ordinal);
        Assert.Contains("formtarget=\"_blank\"", markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Embedded_discord_settings_keep_dialog_contract_without_duplicate_hero()
    {
        var markup = ReadWebFile("Pages", "Admin", "Settings", "Discord.cshtml");
        var model = ReadWebFile("Pages", "Admin", "Settings", "Discord.cshtml.cs");

        Assert.Contains("@(Model.Embedded ? \"is-embedded\" : null)", markup, StringComparison.Ordinal);
        Assert.Contains("@if (!Model.Embedded)", markup, StringComparison.Ordinal);
        Assert.Contains("<input asp-for=\"Embedded\" type=\"hidden\" />", markup, StringComparison.Ordinal);
        Assert.Contains("[BindProperty(SupportsGet = true)]", model, StringComparison.Ordinal);
        Assert.Contains("public bool Embedded { get; set; }", model, StringComparison.Ordinal);
        Assert.Contains("ViewData[\"EmbeddedDiscordSettings\"] = Embedded;", model, StringComparison.Ordinal);
        Assert.Contains("return RedirectToPage(new { embedded = Embedded });", model, StringComparison.Ordinal);
    }

    [Fact]
    public void Dynamic_channel_auto_mute_and_test_behavior_remain_wired()
    {
        var markup = ReadWebFile("Pages", "Admin", "Settings", "Discord.cshtml");

        Assert.Contains("data-discord-guild", markup, StringComparison.Ordinal);
        Assert.Contains("data-discord-channel", markup, StringComparison.Ordinal);
        Assert.Contains("data-discord-auto-mute-form", markup, StringComparison.Ordinal);
        Assert.Contains("data-discord-status", markup, StringComparison.Ordinal);
        Assert.Contains("data-discord-test", markup, StringComparison.Ordinal);
        Assert.Contains("?handler=Channels&guildId=${encodeURIComponent(guild.value)}", markup, StringComparison.Ordinal);
        Assert.Contains("?handler=SaveAutomaticMute&ajax=true", markup, StringComparison.Ordinal);
        Assert.Contains("badwolfquiz:discord-auto-mute-changed", markup, StringComparison.Ordinal);
        Assert.Contains("?handler=Test", markup, StringComparison.Ordinal);
        Assert.Contains("RequestVerificationToken", markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Dedicated_styles_cover_viewport_embedded_responsive_and_accessibility_layouts()
    {
        var styles = ReadWebFile("wwwroot", "css", "discord-settings.css")
            .ReplaceLineEndings("\n");

        Assert.Contains("body.portal-layout:has(.discord-settings-page:not(.is-embedded)) > .page-shell", styles, StringComparison.Ordinal);
        Assert.Contains("width: 100%;", styles, StringComparison.Ordinal);
        Assert.Contains("padding-inline: 0;", styles, StringComparison.Ordinal);
        Assert.Contains(".discord-settings-page {", styles, StringComparison.Ordinal);
        Assert.Contains("width: min(100%, 1540px);", styles, StringComparison.Ordinal);
        Assert.Contains("margin-inline: auto;", styles, StringComparison.Ordinal);
        Assert.Contains(".discord-settings-hero {", styles, StringComparison.Ordinal);
        Assert.Contains(".discord-settings-summary {", styles, StringComparison.Ordinal);
        Assert.Contains("grid-template-columns: repeat(3, minmax(0, 1fr));", styles, StringComparison.Ordinal);
        Assert.Contains(".discord-settings-workspace {", styles, StringComparison.Ordinal);
        Assert.Contains(".discord-settings-page.is-embedded {", styles, StringComparison.Ordinal);
        Assert.Contains("body.embedded-discord-settings:has(.discord-settings-page) > .page-shell", styles, StringComparison.Ordinal);
        Assert.Contains(".discord-settings-page.is-embedded .discord-settings-workspace", styles, StringComparison.Ordinal);
        Assert.Contains("grid-template-columns: minmax(0, 1fr);", styles, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 980px)", styles, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 700px)", styles, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 480px)", styles, StringComparison.Ordinal);
        Assert.Contains(":focus-visible", styles, StringComparison.Ordinal);
        Assert.Contains("@media (prefers-reduced-motion: reduce)", styles, StringComparison.Ordinal);
    }

    [Fact]
    public void Page_model_keeps_existing_discord_handlers()
    {
        var model = ReadWebFile("Pages", "Admin", "Settings", "Discord.cshtml.cs");

        Assert.Contains("public IActionResult OnPostConnect()", model, StringComparison.Ordinal);
        Assert.Contains("public async Task<IActionResult> OnGetCallbackAsync", model, StringComparison.Ordinal);
        Assert.Contains("public JsonResult OnGetChannels(string guildId)", model, StringComparison.Ordinal);
        Assert.Contains("public async Task<IActionResult> OnPostSaveAsync", model, StringComparison.Ordinal);
        Assert.Contains("public async Task<IActionResult> OnPostDisconnectAsync", model, StringComparison.Ordinal);
        Assert.Contains("public async Task<IActionResult> OnPostSaveAutomaticMuteAsync", model, StringComparison.Ordinal);
        Assert.Contains("public async Task<JsonResult> OnPostTestAsync", model, StringComparison.Ordinal);
        Assert.Contains("await repository.SaveSelectionAsync", model, StringComparison.Ordinal);
        Assert.Contains("await repository.SaveAutomaticMuteAsync", model, StringComparison.Ordinal);
        Assert.Contains("await repository.DeleteAsync", model, StringComparison.Ordinal);
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
