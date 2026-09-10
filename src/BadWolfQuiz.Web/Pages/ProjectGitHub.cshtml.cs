using System.Security.Claims;
using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace BadWolfQuiz.Web.Pages;

public sealed class ProjectGitHubModel(
    QuizDbContext db,
    IOptions<ProjectOptions> projectOptions) : PageModel
{
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var target = projectOptions.Value.GetGitHubUrl();
        if (target is null)
        {
            return RedirectToPage("/About");
        }

        var accountId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrWhiteSpace(accountId))
        {
            await new PlayerAchievementService(db).UnlockAccountAsync(
                accountId,
                "GitHubVisitor",
                cancellationToken: cancellationToken);
        }

        return Redirect(target);
    }
}
