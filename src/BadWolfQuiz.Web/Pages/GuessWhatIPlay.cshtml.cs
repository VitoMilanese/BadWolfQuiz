using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BadWolfQuiz.Web.Pages;

public sealed class GuessWhatIPlayModel(
    IDbContextFactory<QuizDbContext> dbFactory,
    IOptions<MinigameOptions> options) : PageModel
{
    public int AvailableCardCount { get; private set; }

    public int DefaultCardCount { get; private set; }

    private MinigameCatalogStore Catalog =>
        new(dbFactory, options.Value.CardCount);

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var counts = await Catalog.GetCountsAsync(cancellationToken);
        AvailableCardCount = counts.GameCount;
        DefaultCardCount = AvailableCardCount >= MinigameRoomStore.MinimumGameCardCount
            ? Math.Clamp(
                Catalog.DefaultCardCount,
                MinigameRoomStore.MinimumGameCardCount,
                AvailableCardCount)
            : 0;
    }

    public async Task<IActionResult> OnGetCardAsync(
        string? file,
        CancellationToken cancellationToken)
    {
        var image = await Catalog.GetGameImageAsync(file, cancellationToken);
        if (image is null)
        {
            return NotFound();
        }

        Response.Headers.CacheControl = "public, max-age=300";
        return File(image.Data, image.ContentType);
    }
}
