using BadWolfQuiz.Web.Pages;
using BadWolfQuiz.Web.Services;
using Microsoft.Extensions.Options;

namespace BadWolfQuiz.Web.Tests;

public sealed class AboutSocialLinksTests
{
    [Fact]
    public void Configured_github_and_discord_urls_are_exposed()
    {
        const string gitHubUrl = "https://github.com/VitoMilanese/BadWolfQuiz";
        const string discordUrl = "https://discord.gg/example";
        var model = CreateModel(
            $"  {gitHubUrl}  ",
            $"  {discordUrl}  ");

        model.OnGet();

        Assert.Equal(gitHubUrl, model.GitHubUrl);
        Assert.Equal(discordUrl, model.DiscordInviteUrl);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not a url")]
    [InlineData("javascript:alert(1)")]
    [InlineData("/relative")]
    public void Missing_or_invalid_github_url_is_hidden(string? value)
    {
        var model = CreateModel(value, "https://discord.gg/example");

        model.OnGet();

        Assert.Null(model.GitHubUrl);
        Assert.Equal("https://discord.gg/example", model.DiscordInviteUrl);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not a url")]
    [InlineData("javascript:alert(1)")]
    [InlineData("/relative")]
    public void Missing_or_invalid_discord_url_is_hidden(string? value)
    {
        var model = CreateModel(
            "https://github.com/VitoMilanese/BadWolfQuiz",
            value);

        model.OnGet();

        Assert.Equal("https://github.com/VitoMilanese/BadWolfQuiz", model.GitHubUrl);
        Assert.Null(model.DiscordInviteUrl);
    }

    [Fact]
    public void About_uses_icon_only_conditional_social_links_and_keeps_license_button()
    {
        var markup = File.ReadAllText(FindAboutView());

        Assert.Contains("@if (Model.GitHubUrl is not null)", markup, StringComparison.Ordinal);
        Assert.Contains("@if (Model.DiscordInviteUrl is not null)", markup, StringComparison.Ordinal);
        Assert.Contains("href=\"@Model.GitHubUrl\"", markup, StringComparison.Ordinal);
        Assert.Contains("href=\"@Model.DiscordInviteUrl\"", markup, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"GitHub\"", markup, StringComparison.Ordinal);
        Assert.Contains("title=\"GitHub\"", markup, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Discord\"", markup, StringComparison.Ordinal);
        Assert.Contains("title=\"Discord\"", markup, StringComparison.Ordinal);
        Assert.Contains("class=\"button button-secondary about-social-link\"", markup, StringComparison.Ordinal);
        Assert.Contains("asp-page=\"/License\"", markup, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "https://github.com/VitoMilanese/BadWolfQuiz",
            markup,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "ProductVersionLocalizer[\"RepositoryLink\"]",
            markup,
            StringComparison.Ordinal);
    }

    private static AboutModel CreateModel(string? gitHubUrl, string? discordUrl)
        => new(
            Options.Create(new ProjectOptions { GitHubUrl = gitHubUrl }),
            Options.Create(new DiscordInviteOptions { InviteUrl = discordUrl }));

    private static string FindAboutView()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "src",
                "BadWolfQuiz.Web",
                "Pages",
                "About.cshtml");
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate About.cshtml from the test output directory.");
    }
}
