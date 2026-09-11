using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BadWolfQuiz.Web.Pages;

[Authorize]
public sealed class AchievementsModel(
    QuizDbContext db,
    CurrentHost currentHost,
    IOptions<FooterOptions> footerOptions) : PageModel
{
    public IReadOnlyList<PlayerAchievementProgress> Achievements { get; private set; } = [];

    public int UnlockedCount => Achievements.Count(item => item.IsUnlocked);

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var accountId = currentHost.RequiredId;
        var displayName = await db.Hosts
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(host => host.Id == accountId)
            .Select(host => host.DisplayName)
            .SingleOrDefaultAsync(cancellationToken);
        var isContributor = ContributorRecognition.IsContributor(
            footerOptions.Value,
            displayName);

        Achievements = await new PlayerAchievementService(db).LoadForPlayerAsync(
            hostId: null,
            playerName: string.Empty,
            currentGameCode: null,
            accountId: accountId,
            isContributor: isContributor,
            cancellationToken: cancellationToken);
    }
}
