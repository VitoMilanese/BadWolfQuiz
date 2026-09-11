using System.ComponentModel.DataAnnotations;
using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Localization;
using BadWolfQuiz.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace BadWolfQuiz.Web.Pages.Admin.Quizzes;

public sealed class QuizMetadataEditorModel(
    QuizDbContext db,
    IStringLocalizer<SharedResource> localizer) : PageModel
{
    public const int MaximumTagCount = 10;
    private const int MaximumTagLength = 100;

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public bool Saved { get; private set; }

    public sealed class InputModel
    {
        public int Id { get; set; }
        public int? SelectedRoundId { get; set; }

        [Required, MaxLength(160)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Description { get; set; }

        public List<string> Tags { get; set; } = [];
    }

    public async Task<IActionResult> OnGetAsync(
        int id,
        int? selectedRoundId = null,
        bool saved = false,
        CancellationToken cancellationToken = default)
    {
        // Deliberately lightweight: only scalar quiz metadata and quiz-level tags.
        // Never materialize rounds, questions, content blocks, or media BLOBs here.
        var quiz = await db.Quizzes
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new
            {
                x.Id,
                x.Title,
                x.Description,
                x.MediaState,
                Tags = x.Tags
                    .OrderBy(tag => tag.Name)
                    .Select(tag => tag.Name)
                    .ToList()
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (quiz is null)
        {
            return NotFound();
        }

        if (quiz.MediaState != QuizMediaState.Active)
        {
            TempData["ErrorMessage"] = localizer["MediaArchive_RestoreBeforeEditing"].Value;
            return RedirectToPage("Index");
        }

        Input = new InputModel
        {
            Id = quiz.Id,
            SelectedRoundId = selectedRoundId,
            Title = quiz.Title,
            Description = quiz.Description,
            Tags = quiz.Tags
        };
        Saved = saved;
        return Page();
    }

    public async Task<IActionResult> OnGetTagSuggestionsAsync(
        int id,
        string? query,
        CancellationToken cancellationToken)
    {
        var canEdit = await db.Quizzes
            .AsNoTracking()
            .AnyAsync(
                quiz => quiz.Id == id && quiz.MediaState == QuizMediaState.Active,
                cancellationToken);
        if (!canEdit)
        {
            return NotFound();
        }

        return new JsonResult(await QuizTagSuggestionQuery.GetAsync(
            db,
            query,
            cancellationToken));
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        Input.Title = Input.Title?.Trim() ?? string.Empty;
        Input.Description = string.IsNullOrWhiteSpace(Input.Description)
            ? null
            : Input.Description.Trim();
        Input.Tags = NormalizeTags(Input.Tags);

        if (Input.Tags.Count > MaximumTagCount)
        {
            ModelState.AddModelError(
                "Input.Tags",
                localizer["Validation_QuizTagLimit", MaximumTagCount].Value);
        }

        if (Input.Tags.Any(tag => tag.Length > MaximumTagLength))
        {
            ModelState.AddModelError(
                "Input.Tags",
                localizer["Validation_QuizTagTooLong"].Value);
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        // Saving also stays narrow: the quiz row plus at most ten tag rows.
        var quiz = await db.Quizzes
            .Include(x => x.Tags)
            .SingleOrDefaultAsync(x => x.Id == Input.Id, cancellationToken);
        if (quiz is null)
        {
            return NotFound();
        }

        if (quiz.MediaState != QuizMediaState.Active)
        {
            TempData["ErrorMessage"] = localizer["MediaArchive_RestoreBeforeEditing"].Value;
            return RedirectToPage("Index");
        }

        quiz.Title = Input.Title;
        quiz.Description = Input.Description;
        SynchronizeTags(quiz, Input.Tags);
        quiz.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return RedirectToPage(
            "QuizMetadataEditor",
            new
            {
                id = quiz.Id,
                selectedRoundId = Input.SelectedRoundId,
                saved = true
            });
    }

    private static List<string> NormalizeTags(IEnumerable<string>? tags)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in tags ?? [])
        {
            var tag = value?.Trim();
            if (string.IsNullOrWhiteSpace(tag))
            {
                continue;
            }

            if (seen.Add(NormalizeTagName(tag)))
            {
                result.Add(tag);
            }
        }

        return result;
    }

    private static string NormalizeTagName(string tag) =>
        tag.Trim().ToUpperInvariant();

    private static void SynchronizeTags(Quiz quiz, IReadOnlyCollection<string> tags)
    {
        var desired = tags.ToDictionary(NormalizeTagName, tag => tag, StringComparer.Ordinal);
        foreach (var existing in quiz.Tags.ToArray())
        {
            if (!desired.TryGetValue(existing.NormalizedName, out var displayName))
            {
                quiz.Tags.Remove(existing);
                continue;
            }

            existing.Name = displayName;
        }

        var existingNames = quiz.Tags
            .Select(tag => tag.NormalizedName)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var pair in desired)
        {
            if (existingNames.Add(pair.Key))
            {
                quiz.Tags.Add(new QuizTag
                {
                    Name = pair.Value,
                    NormalizedName = pair.Key
                });
            }
        }
    }
}
