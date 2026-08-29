using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BadWolfQuiz.Web.Pages;

public sealed class MinigamesModel(MinigameCardSetStore cardSetStore) : PageModel
{
    public IReadOnlyList<MinigameCardDescriptor> Cards { get; private set; } = [];

    public long StateVersion { get; private set; }

    public string? HighlightedFileName { get; private set; }

    public void OnGet()
    {
        var state = cardSetStore.GetCurrent();
        StateVersion = state.Version;
        Cards = state.Cards;
        HighlightedFileName = Cards.Count == 0
            ? null
            : Cards[Random.Shared.Next(Cards.Count)].FileName;
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
