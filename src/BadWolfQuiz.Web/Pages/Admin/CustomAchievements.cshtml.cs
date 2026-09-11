using System.ComponentModel.DataAnnotations;
using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Models;
using BadWolfQuiz.Web.Localization;
using BadWolfQuiz.Web.Pages.Admin.Quizzes;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using SkiaSharp;

namespace BadWolfQuiz.Web.Pages.Admin;

[Authorize]
[RequestSizeLimit(3 * 1024 * 1024)]
public sealed class CustomAchievementsModel(
    QuizDbContext db,
    CurrentHost currentHost,
    IStringLocalizer<HostCustomAchievementEditorResource> localizer) : PageModel
{
    public IReadOnlyList<HostCustomAchievement> Achievements { get; private set; } = [];

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public async Task OnGetAsync(Guid? edit, CancellationToken cancellationToken)
    {
        await LoadListAsync(cancellationToken);
        if (!edit.HasValue)
        {
            return;
        }

        var achievement = await new HostCustomAchievementService(db).LoadDefinitionAsync(
            edit.Value,
            currentHost.RequiredId,
            cancellationToken: cancellationToken);
        if (achievement is null)
        {
            return;
        }

        Input = new InputModel
        {
            Id = achievement.Id,
            Name = achievement.Name,
            Description = achievement.Description,
            Target = achievement.Target,
            Tags = achievement.Tags
                .OrderBy(tag => tag.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(tag => tag.Name)
                .ToList()
        };
    }

    public async Task<IActionResult> OnGetTagSuggestionsAsync(
        string? query,
        CancellationToken cancellationToken)
    {
        var suggestions = await QuestionTagSuggestionQuery.GetAsync(
            db,
            query,
            cancellationToken);
        return new JsonResult(suggestions);
    }

    public async Task<IActionResult> OnPostSaveAsync(CancellationToken cancellationToken)
    {
        var hostId = currentHost.RequiredId;
        HostCustomAchievement? achievement = null;
        if (Input.Id.HasValue)
        {
            achievement = await db.HostCustomAchievements
                .IgnoreQueryFilters()
                .Include(item => item.Tags)
                .SingleOrDefaultAsync(
                    item => item.Id == Input.Id.Value && item.HostId == hostId && !item.IsDeleted,
                    cancellationToken);
            if (achievement is null)
            {
                return NotFound();
            }
        }

        IReadOnlyList<HostCustomAchievementTagInput> tags = [];
        try
        {
            tags = HostCustomAchievementService.NormalizeTags(Input.Tags);
        }
        catch (ArgumentException)
        {
            ModelState.AddModelError(
                "Input.Tags",
                localizer["TagsInvalid", HostCustomAchievementService.MaximumTags]);
        }

        byte[]? artwork = null;
        if (Input.Artwork is not null && Input.Artwork.Length > 0)
        {
            if (Input.Artwork.Length > HostCustomAchievementService.MaximumArtworkBytes)
            {
                ModelState.AddModelError("Input.Artwork", localizer["ArtworkTooLarge"]);
            }
            else
            {
                await using var stream = new MemoryStream();
                await Input.Artwork.CopyToAsync(stream, cancellationToken);
                artwork = stream.ToArray();
                using var bitmap = SKBitmap.Decode(artwork);
                if (bitmap is null ||
                    !HostCustomAchievementService.HasPngSignature(artwork) ||
                    bitmap.Width != HostCustomAchievementService.ArtworkSize ||
                    bitmap.Height != HostCustomAchievementService.ArtworkSize)
                {
                    ModelState.AddModelError(
                        "Input.Artwork",
                        localizer["ArtworkInvalid"]);
                    artwork = null;
                }
            }
        }
        else if (achievement is null)
        {
            ModelState.AddModelError("Input.Artwork", localizer["ArtworkRequired"]);
        }

        if (!ModelState.IsValid)
        {
            await LoadListAsync(cancellationToken);
            return Page();
        }

        if (achievement is null)
        {
            achievement = new HostCustomAchievement
            {
                HostId = hostId,
                CreatedAtUtc = DateTime.UtcNow
            };
            db.HostCustomAchievements.Add(achievement);
        }

        achievement.Name = Input.Name.Trim();
        achievement.Description = Input.Description.Trim();
        achievement.Target = Input.Target;
        achievement.UpdatedAtUtc = DateTime.UtcNow;
        if (artwork is not null)
        {
            achievement.ArtworkPng = artwork;
        }

        achievement.Tags.Clear();
        foreach (var tag in tags)
        {
            achievement.Tags.Add(new HostCustomAchievementTag
            {
                Name = tag.Name,
                NormalizedName = tag.NormalizedName
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        TempData["CustomAchievementSaved"] = true;
        return RedirectToPage(new { edit = achievement.Id });
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await new HostCustomAchievementService(db).DeleteAsync(id, currentHost.RequiredId, cancellationToken);
        return RedirectToPage();
    }

    private async Task LoadListAsync(CancellationToken cancellationToken) =>
        Achievements = await new HostCustomAchievementService(db)
            .LoadDefinitionsAsync(currentHost.RequiredId, cancellationToken);

    public sealed class InputModel
    {
        public Guid? Id { get; set; }

        [Required, StringLength(160)]
        public string Name { get; set; } = string.Empty;

        [Required, StringLength(1000)]
        public string Description { get; set; } = string.Empty;

        [Range(1, HostCustomAchievementService.MaximumTarget)]
        public int Target { get; set; } = 1;

        public List<string> Tags { get; set; } = [];

        public IFormFile? Artwork { get; set; }
    }
}
