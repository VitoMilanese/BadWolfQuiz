using BadWolfQuiz.Web.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BadWolfQuiz.Web.Pages;

public sealed class CustomAchievementArtworkModel(QuizDbContext db) : PageModel
{
    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken)
    {
        var artwork = await db.HostCustomAchievements
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => item.ArtworkPng)
            .SingleOrDefaultAsync(cancellationToken);
        return artwork is null || artwork.Length == 0
            ? NotFound()
            : File(artwork, "image/png");
    }
}
