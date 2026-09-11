using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BadWolfQuiz.Web.Pages;

public sealed class CustomAchievementMetadataModel(QuizDbContext db) : PageModel
{
    public async Task<IActionResult> OnGetAsync(string? codes, CancellationToken cancellationToken)
    {
        var ids = (codes ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(code => HostCustomAchievementService.TryParseCode(code, out var id) ? id : (Guid?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .Take(100)
            .ToArray();
        if (ids.Length == 0)
        {
            return new JsonResult(new { achievements = Array.Empty<object>() });
        }

        var items = await db.HostCustomAchievements
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(item => ids.Contains(item.Id))
            .Select(item => new
            {
                item.Id,
                item.Name,
                item.Description,
                item.Target,
                item.IsDeleted
            })
            .ToListAsync(cancellationToken);

        return new JsonResult(new
        {
            achievements = items.Select(item => new
            {
                code = HostCustomAchievementService.BuildCode(item.Id),
                item.Name,
                item.Description,
                item.Target,
                artworkUrl = $"/AchievementArtwork/{item.Id:D}",
                item.IsDeleted,
                isHostDefined = true
            })
        });
    }
}
