using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace BadWolfQuiz.Web.Pages;

public sealed class AboutModel(
    IOptions<ProjectOptions> projectOptions,
    IOptions<DiscordInviteOptions> discordInviteOptions) : PageModel
{
    public string? GitHubUrl { get; private set; }
    public string? DiscordInviteUrl { get; private set; }

    public void OnGet()
    {
        GitHubUrl = projectOptions.Value.GetGitHubUrl();
        DiscordInviteUrl = discordInviteOptions.Value.GetInviteUrl();
    }
}
