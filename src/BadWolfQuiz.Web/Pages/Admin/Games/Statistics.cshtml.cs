using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BadWolfQuiz.Web.Pages.Admin.Games;

public sealed class StatisticsModel(PlayerStatisticsService statistics) : PageModel
{
    public IReadOnlyList<PlayerLifetimeStatistics> Players { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Players = await statistics.LoadAsync(cancellationToken);
    }
}
