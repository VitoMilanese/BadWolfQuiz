using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace BadWolfQuiz.Web.Pages.Admin.Quizzes;

public sealed class RenameModel(
    QuizDbContext db,
    IStringLocalizer<SharedResource> localizer) : PageModel
{
    [BindProperty]
    public RenameRoundInputModel RenameRound { get; set; } = new();

    [BindProperty]
    public RenameCategoryInputModel RenameCategory { get; set; } = new();

    public sealed class RenameRoundInputModel
    {
        public int QuizId { get; set; }
        public int RoundId { get; set; }
        public string? Title { get; set; }
    }

    public sealed class RenameCategoryInputModel
    {
        public int QuizId { get; set; }
        public int CategoryId { get; set; }
        public string? Title { get; set; }
    }

    public IActionResult OnGet() => NotFound();

    public async Task<IActionResult> OnPostRoundAsync(
        CancellationToken cancellationToken)
    {
        var round = await db.QuizRounds
  .Include(x => x.Quiz)
  .SingleOrDefaultAsync(
      x => x.Id == RenameRound.RoundId &&
           x.QuizId == RenameRound.QuizId,
      cancellationToken);

        if (round is null)
        {
  return NotFound();
        }

        var title = RenameRound.Title?.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
  return BadRequest(new
  {
      success = false,
      error = localizer["QuizEditor_RoundTitleRequired"].Value
  });
        }

        round.Title = title;
        round.Quiz.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return new JsonResult(new
        {
  success = true,
  title = round.Title,
  message = localizer["QuizEditor_RoundRenamed"].Value
        });
    }

    public async Task<IActionResult> OnPostCategoryAsync(
        CancellationToken cancellationToken)
    {
        var category = await db.QuizCategories
  .Include(x => x.Round)
  .ThenInclude(x => x.Quiz)
  .SingleOrDefaultAsync(
      x => x.Id == RenameCategory.CategoryId &&
           x.Round.QuizId == RenameCategory.QuizId,
      cancellationToken);

        if (category is null)
        {
  return NotFound();
        }

        var title = RenameCategory.Title?.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
  return BadRequest(new
  {
      success = false,
      error = localizer["QuizEditor_CategoryTitleRequired"].Value
  });
        }

        category.Title = title;
        category.Round.Quiz.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return new JsonResult(new
        {
  success = true,
  title = category.Title,
  message = localizer["QuizEditor_CategoryRenamed"].Value
        });
    }
}
