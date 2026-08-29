using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BadWolfQuiz.Web.Pages;

public sealed class MinigamesModel(MinigameCardSetStore cardSetStore) : PageModel
{
    public int AvailableCardCount { get; private set; }

    public int DefaultCardCount { get; private set; }

    public void OnGet()
    {
        AvailableCardCount = cardSetStore.AvailableCardCount;
        DefaultCardCount = AvailableCardCount >= MinigameRoomStore.MinimumGameCardCount
            ? Math.Clamp(
                cardSetStore.DefaultCardCount,
                MinigameRoomStore.MinimumGameCardCount,
                AvailableCardCount)
            : 0;
    }

    public IActionResult OnGetCard(string? file)
    {
        if (!cardSetStore.TryResolveCard(
                file,
                out var physicalPath,
                out var contentType))
        {
            return NotFound();
        }

        Response.Headers.CacheControl = "public, max-age=300";
        return PhysicalFile(physicalPath, contentType);
    }
}
