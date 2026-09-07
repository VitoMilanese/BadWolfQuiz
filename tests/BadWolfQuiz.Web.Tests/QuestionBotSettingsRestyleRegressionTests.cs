namespace BadWolfQuiz.Web.Tests;

public sealed class QuestionBotSettingsRestyleRegressionTests
{
    [Fact]
    public void Question_bot_settings_uses_dedicated_standalone_and_embedded_presentation()
    {
        var markup = ReadWebFile("Pages", "Admin", "Settings", "QuestionBot.cshtml");

        Assert.Contains("~/css/question-bot-settings.css", markup, StringComparison.Ordinal);
        Assert.Contains("class=\"question-bot-settings-page", markup, StringComparison.Ordinal);
        Assert.Contains("class=\"question-bot-settings-hero\"", markup, StringComparison.Ordinal);
        Assert.Contains("class=\"question-bot-settings-card\"", markup, StringComparison.Ordinal);
        Assert.Contains("Layout = null", markup, StringComparison.Ordinal);
        Assert.Contains("~/css/site.css", markup, StringComparison.Ordinal);
        Assert.Contains("embedded ? \"is-embedded\" : null", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("form-card narrow", markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Question_bot_settings_preserves_save_and_dynamic_channel_contracts()
    {
        var markup = ReadWebFile("Pages", "Admin", "Settings", "QuestionBot.cshtml");

        Assert.Contains("asp-page-handler=\"Save\"", markup, StringComparison.Ordinal);
        Assert.Contains("asp-route-embedded=\"@embedded\"", markup, StringComparison.Ordinal);
        Assert.Contains("asp-for=\"GuildId\" id=\"questionBotGuild\"", markup, StringComparison.Ordinal);
        Assert.Contains("asp-for=\"ChannelId\" id=\"questionBotChannel\"", markup, StringComparison.Ordinal);
        Assert.Contains("?handler=Channels&guildId=${encodeURIComponent(guildSelect.value)}&embedded=@embedded.ToString().ToLowerInvariant()", markup, StringComparison.Ordinal);
        Assert.Contains("response.json()", markup, StringComparison.Ordinal);
        Assert.Contains("window.setTimeout(() => successMessage.remove(), 3000)", markup, StringComparison.Ordinal);
        Assert.Contains("TempData[\"QuestionBotSuccess\"]", markup, StringComparison.Ordinal);
        Assert.Contains("TempData[\"QuestionBotError\"]", markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Question_bot_settings_styles_cover_responsive_focus_embedded_and_native_option_contrast()
    {
        var styles = ReadWebFile("wwwroot", "css", "question-bot-settings.css")
            .ReplaceLineEndings("\n");

        Assert.Contains("body.portal-layout:has(.question-bot-settings-page) > .page-shell", styles, StringComparison.Ordinal);
        Assert.Contains(".question-bot-settings-page.is-embedded", styles, StringComparison.Ordinal);
        Assert.Contains(".question-bot-settings-field select:focus-visible", styles, StringComparison.Ordinal);
        Assert.Contains(".question-bot-settings-field select option", styles, StringComparison.Ordinal);
        Assert.Contains("background: Canvas;", styles, StringComparison.Ordinal);
        Assert.Contains("color: CanvasText;", styles, StringComparison.Ordinal);
        Assert.Contains("color: GrayText;", styles, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 900px)", styles, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 680px)", styles, StringComparison.Ordinal);
        Assert.Contains("@media (prefers-reduced-motion: reduce)", styles, StringComparison.Ordinal);
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
