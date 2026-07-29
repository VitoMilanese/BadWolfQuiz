using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Localization;
using BadWolfQuiz.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace BadWolfQuiz.Web.Pages.Admin.Quizzes;

public sealed class EditorModel(
    QuizDbContext db,
    IStringLocalizer<SharedResource> localizer) : PageModel
{
    const int TemporarySortOrderOffset = 100000;

    [BindProperty]
    public AddRoundInputModel AddRound { get; set; } = new();

    [BindProperty]
    public RenameRoundInputModel RenameRound { get; set; } = new();

    [BindProperty]
    public RenameCategoryInputModel RenameCategory { get; set; } = new();

    [BindProperty]
    public DeleteRoundInputModel DeleteRound { get; set; } = new();

    [BindProperty]
    public RoundRowsInputModel RoundRows { get; set; } = new();

    [BindProperty]
    public ReorderRoundsInputModel ReorderRounds { get; set; } = new();

    [BindProperty]
    public ReorderCategoriesInputModel ReorderCategories { get; set; } = new();

    [BindProperty]
    public ExchangeCategoriesInputModel ExchangeCategories { get; set; } = new();

    public int SelectedRoundId { get; private set; }

    public Quiz Quiz { get; private set; } = null!;

    public sealed class AddRoundInputModel
    {
        public int QuizId { get; set; }

        public string? Title { get; set; }
    }

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

    public sealed class DeleteRoundInputModel
    {
        public int QuizId { get; set; }

        public int RoundId { get; set; }
    }

    public sealed class RoundRowsInputModel
    {
        public int QuizId { get; set; }

        public int RoundId { get; set; }

        public List<RoundRowInputModel> Rows { get; set; } = new();
    }

    public sealed class RoundRowInputModel
    {
        public int RowIndex { get; set; }

        public int Points { get; set; }
    }

    public sealed class ReorderRoundsInputModel
    {
        public int QuizId { get; set; }

        public List<int> RoundIds { get; set; } = new();
    }

    public sealed class ReorderCategoriesInputModel
    {
        public int QuizId { get; set; }

        public int RoundId { get; set; }

        public List<int> CategoryIds { get; set; } = new();
    }

    public sealed class ExchangeCategoriesInputModel
    {
        public int QuizId { get; set; }

        public int SourceCategoryId { get; set; }

        public int TargetCategoryId { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(
        int id,
        int? selectedRoundId = null)
    {
        var quiz = await db.Quizzes
            .AsNoTracking()
            .Include(x => x.Rounds)
                .ThenInclude(x => x.Rows)
            .Include(x => x.Rounds)
                .ThenInclude(x => x.Categories)
                    .ThenInclude(x => x.Questions)
                        .ThenInclude(x => x.QuestionBlocks)
            .SingleOrDefaultAsync(x => x.Id == id);

        if (quiz is null)
        {
            return NotFound();
        }

        var rounds = quiz.Rounds
            .OrderBy(x => x.SortOrder)
            .ToList();

        if (rounds.Count == 0)
        {
            return NotFound();
        }

        Quiz = quiz;

        SelectedRoundId =
            selectedRoundId.HasValue &&
            rounds.Any(x => x.Id == selectedRoundId.Value)
                ? selectedRoundId.Value
                : rounds[0].Id;

        var selectedRound = rounds.Single(x => x.Id == SelectedRoundId);

        RoundRows = new RoundRowsInputModel
        {
            QuizId = quiz.Id,
            RoundId = selectedRound.Id,
            Rows = selectedRound.Rows
                .OrderBy(x => x.RowIndex)
                .Select(x => new RoundRowInputModel
                {
                    RowIndex = x.RowIndex,
                    Points = x.Points
                })
                .ToList()
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAddRoundAsync()
    {
        var quiz = await db.Quizzes
            .Include(x => x.Rounds)
                .ThenInclude(x => x.Rows)
            .Include(x => x.Rounds)
                .ThenInclude(x => x.Categories)
            .SingleOrDefaultAsync(x => x.Id == AddRound.QuizId);

        if (quiz is null)
        {
            return NotFound();
        }

        var title = AddRound.Title?.Trim();

        if (string.IsNullOrWhiteSpace(title))
        {
            title = string.Format(
                localizer["Round_DefaultTitle"].Value,
                quiz.Rounds.Count + 1);
        }

        var nextSortOrder = quiz.Rounds.Count == 0
            ? 0
            : quiz.Rounds.Max(x => x.SortOrder) + 1;

        var templateRound = quiz.Rounds
            .OrderBy(x => x.SortOrder)
            .FirstOrDefault();

        var round = new QuizRound
        {
            QuizId = quiz.Id,
            Title = title,
            SortOrder = nextSortOrder,
            DefaultTimeLimitSeconds = templateRound?.DefaultTimeLimitSeconds ?? 30,
            DefaultBuzzMode = templateRound?.DefaultBuzzMode ?? BuzzActivationMode.Manual
        };

        var templateRows = templateRound?.Rows
            .OrderBy(x => x.RowIndex)
            .ToList();

        if (templateRows is { Count: > 0 })
        {
            // Increase the base point value for each subsequent round.
            var roundNumber = nextSortOrder + 1;
            var step = roundNumber * 200;

            foreach (var templateRow in templateRows)
            {
                round.Rows.Add(new QuizRoundRow
                {
                    RowIndex = templateRow.RowIndex,
                    Points = templateRow.RowIndex * step
                });
            }
        }
        else
        {
            for (var rowIndex = 1; rowIndex <= 5; rowIndex++)
            {
                round.Rows.Add(new QuizRoundRow
                {
                    RowIndex = rowIndex,
                    Points = rowIndex * 200
                });
            }
        }

        if (templateRound is not null)
        {
            foreach (var category in templateRound.Categories.OrderBy(x => x.SortOrder))
            {
                var newCategory = new QuizCategory
                {
                    Title = category.Title,
                    SortOrder = category.SortOrder
                };

                foreach (var roundRow in round.Rows.OrderBy(x => x.RowIndex))
                {
                    var question = new QuizQuestion
                    {
                        RowIndex = roundRow.RowIndex
                    };

                    question.QuestionBlocks.Add(new QuestionContentBlock
                    {
                        BlockType = ContentBlockType.Text,
                        SortOrder = 1
                    });

                    question.AnswerBlocks.Add(new AnswerContentBlock
                    {
                        BlockType = ContentBlockType.Text,
                        SortOrder = 1
                    });

                    newCategory.Questions.Add(question);
                }

                round.Categories.Add(newCategory);
            }
        }

        quiz.Rounds.Add(round);
        quiz.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync();

        TempData["SuccessMessage"] =
            localizer["QuizEditor_RoundCreated"].Value;

        return RedirectToPage(new
        {
            id = quiz.Id,
            selectedRoundId = round.Id
        });
    }

    public async Task<IActionResult> OnPostReorderRoundsAsync()
    {
        var quiz = await db.Quizzes
            .Include(x => x.Rounds)
            .SingleOrDefaultAsync(x => x.Id == ReorderRounds.QuizId);

        if (quiz is null)
        {
            return NotFound();
        }

        var submittedRoundIds = ReorderRounds.RoundIds
            .Distinct()
            .ToList();

        var existingRoundIds = quiz.Rounds
            .Select(x => x.Id)
            .ToHashSet();

        if (submittedRoundIds.Count != quiz.Rounds.Count ||
            submittedRoundIds.Any(x => !existingRoundIds.Contains(x)))
        {
            return BadRequest();
        }

        var roundsById = quiz.Rounds
            .ToDictionary(x => x.Id);

        await using var transaction = await db.Database.BeginTransactionAsync();

        // First move every round to a temporary, non-conflicting range.
        for (var index = 0; index < submittedRoundIds.Count; index++)
        {
            roundsById[submittedRoundIds[index]].SortOrder = TemporarySortOrderOffset + index;
        }

        await db.SaveChangesAsync();

        // Then assign the final order.
        for (var index = 0; index < submittedRoundIds.Count; index++)
        {
            roundsById[submittedRoundIds[index]].SortOrder = index;
        }

        quiz.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        return new JsonResult(new { success = true });
    }

    public async Task<IActionResult> OnPostReorderCategoriesAsync()
    {
        var quiz = await db.Quizzes
            .Include(x => x.Rounds)
                .ThenInclude(x => x.Categories)
            .SingleOrDefaultAsync(x => x.Id == ReorderCategories.QuizId);

        if (quiz is null)
        {
            return NotFound();
        }

        var round = quiz.Rounds
            .SingleOrDefault(x => x.Id == ReorderCategories.RoundId);

        if (round is null)
        {
            return NotFound();
        }

        var submittedCategoryIds = ReorderCategories.CategoryIds
            .Distinct()
            .ToList();

        var existingCategoryIds = round.Categories
            .Select(x => x.Id)
            .ToHashSet();

        if (submittedCategoryIds.Count != round.Categories.Count ||
            submittedCategoryIds.Any(x => !existingCategoryIds.Contains(x)))
        {
            return BadRequest();
        }

        var categoriesById = round.Categories
            .ToDictionary(x => x.Id);

        await using var transaction = await db.Database.BeginTransactionAsync();

        // First move every category to a temporary, non-conflicting range.
        for (var index = 0; index < submittedCategoryIds.Count; index++)
        {
            categoriesById[submittedCategoryIds[index]].SortOrder =
                TemporarySortOrderOffset + index;
        }

        await db.SaveChangesAsync();

        // Then assign the final order.
        for (var index = 0; index < submittedCategoryIds.Count; index++)
        {
            categoriesById[submittedCategoryIds[index]].SortOrder = index;
        }

        quiz.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        return new JsonResult(new { success = true });
    }

    public async Task<IActionResult> OnPostExchangeCategoriesAsync()
    {
        if (ExchangeCategories.SourceCategoryId <= 0 ||
            ExchangeCategories.TargetCategoryId <= 0 ||
            ExchangeCategories.SourceCategoryId ==
            ExchangeCategories.TargetCategoryId)
        {
            return BadRequest();
        }

        var quiz = await db.Quizzes
            .Include(x => x.Rounds)
                .ThenInclude(x => x.Categories)
            .SingleOrDefaultAsync(x =>
                x.Id == ExchangeCategories.QuizId);

        if (quiz is null)
        {
            return NotFound();
        }

        var categories = quiz.Rounds
            .SelectMany(x => x.Categories)
            .ToDictionary(x => x.Id);

        if (!categories.TryGetValue(
                ExchangeCategories.SourceCategoryId,
                out var sourceCategory) ||
            !categories.TryGetValue(
                ExchangeCategories.TargetCategoryId,
                out var targetCategory))
        {
            return NotFound();
        }

        if (sourceCategory.QuizRoundId ==
            targetCategory.QuizRoundId)
        {
            return BadRequest();
        }

        var sourceRoundId = sourceCategory.QuizRoundId;
        var sourceSortOrder = sourceCategory.SortOrder;

        var targetRoundId = targetCategory.QuizRoundId;
        var targetSortOrder = targetCategory.SortOrder;

        await using var transaction =
            await db.Database.BeginTransactionAsync();

        try
        {
            /*
             * Phase 1:
             * Free the original SortOrder values in both rounds.
             */
            sourceCategory.SortOrder =
                TemporarySortOrderOffset + sourceCategory.Id;

            targetCategory.SortOrder =
                TemporarySortOrderOffset + targetCategory.Id;

            await db.SaveChangesAsync();

            /*
             * Phase 2:
             * Move the categories to the opposite rounds while they
             * still have temporary SortOrder values.
             */
            sourceCategory.QuizRoundId = targetRoundId;
            targetCategory.QuizRoundId = sourceRoundId;

            await db.SaveChangesAsync();

            /*
             * Phase 3:
             * Assign the positions previously occupied by the other
             * category.
             */
            sourceCategory.SortOrder = targetSortOrder;
            targetCategory.SortOrder = sourceSortOrder;

            quiz.UpdatedAtUtc = DateTime.UtcNow;

            await db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

        return RedirectToPage(new
        {
            id = quiz.Id,
            selectedRoundId = sourceRoundId
        });
    }

    public async Task<IActionResult> OnPostSaveRoundRowsAsync()
    {
        var quiz = await db.Quizzes
            .Include(x => x.Rounds)
                .ThenInclude(x => x.Rows)
            .SingleOrDefaultAsync(x => x.Id == RoundRows.QuizId);

        if (quiz is null)
        {
            return NotFound();
        }

        var round = quiz.Rounds
            .SingleOrDefault(x => x.Id == RoundRows.RoundId);

        if (round is null)
        {
            return NotFound();
        }

        var submittedRows = RoundRows.Rows
            .GroupBy(x => x.RowIndex)
            .Select(x => x.First())
            .OrderBy(x => x.RowIndex)
            .ToList();

        if (submittedRows.Count == 0 ||
            submittedRows.Any(x => x.RowIndex <= 0 || x.Points < 0))
        {
            TempData["ErrorMessage"] =
                localizer["QuizEditor_InvalidRoundRows"].Value;

            return RedirectToPage(new
            {
                id = quiz.Id,
                selectedRoundId = round.Id
            });
        }

        foreach (var submittedRow in submittedRows)
        {
            var roundRow = round.Rows
                .SingleOrDefault(x => x.RowIndex == submittedRow.RowIndex);

            if (roundRow is null)
            {
                return BadRequest();
            }

            roundRow.Points = submittedRow.Points;
        }

        quiz.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync();

        TempData["SuccessMessage"] =
            localizer["QuizEditor_RoundRowsSaved"].Value;

        return RedirectToPage(new
        {
            id = quiz.Id,
            selectedRoundId = round.Id
        });
    }

    public async Task<IActionResult> OnPostRenameRoundAsync()
    {
        var round = await db.QuizRounds
            .Include(x => x.Quiz)
            .SingleOrDefaultAsync(x =>
                x.Id == RenameRound.RoundId &&
                x.QuizId == RenameRound.QuizId);

        if (round is null)
        {
            return NotFound();
        }

        var title = RenameRound.Title?.Trim();

        if (string.IsNullOrWhiteSpace(title))
        {
            TempData["ErrorMessage"] =
                localizer["QuizEditor_RoundTitleRequired"].Value;

            return RedirectToPage(new
            {
                id = round.QuizId,
                selectedRoundId = round.Id
            });
        }

        round.Title = title;
        round.Quiz.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync();

        TempData["SuccessMessage"] =
            localizer["QuizEditor_RoundRenamed"].Value;

        return RedirectToPage(new
        {
            id = round.QuizId,
            selectedRoundId = round.Id
        });
    }

    public async Task<IActionResult> OnPostDeleteRoundAsync()
    {
        var quiz = await db.Quizzes
            .Include(x => x.Rounds)
            .SingleOrDefaultAsync(x => x.Id == DeleteRound.QuizId);

        if (quiz is null)
        {
            return NotFound();
        }

        var orderedRounds = quiz.Rounds
            .OrderBy(x => x.SortOrder)
            .ToList();

        var roundIndex = orderedRounds.FindIndex(x => x.Id == DeleteRound.RoundId);

        if (roundIndex < 0)
        {
            return NotFound();
        }

        if (orderedRounds.Count <= 1)
        {
            TempData["ErrorMessage"] =
                localizer["QuizEditor_CannotDeleteOnlyRound"].Value;

            return RedirectToPage(new
            {
                id = quiz.Id,
                selectedRoundId = orderedRounds[0].Id
            });
        }

        var round = orderedRounds[roundIndex];

        var nextSelectedRoundId = roundIndex > 0
            ? orderedRounds[roundIndex - 1].Id
            : orderedRounds[roundIndex + 1].Id;

        db.QuizRounds.Remove(round);
        quiz.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync();

        var remainingRounds = await db.QuizRounds
            .Where(x => x.QuizId == quiz.Id)
            .OrderBy(x => x.SortOrder)
            .ToListAsync();

        for (var index = 0; index < remainingRounds.Count; index++)
        {
            remainingRounds[index].SortOrder = index;
        }

        await db.SaveChangesAsync();

        TempData["SuccessMessage"] =
            localizer["QuizEditor_RoundDeleted"].Value;

        return RedirectToPage(new
        {
            id = quiz.Id,
            selectedRoundId = nextSelectedRoundId
        });
    }

    public async Task<IActionResult> OnPostRenameCategoryAsync()
    {
        var category = await db.QuizCategories
            .Include(x => x.Round)
            .ThenInclude(x => x.Quiz)
            .SingleOrDefaultAsync(x =>
                x.Id == RenameCategory.CategoryId &&
                x.Round.QuizId == RenameCategory.QuizId);

        if (category is null)
        {
            return NotFound();
        }

        var title = RenameCategory.Title?.Trim();

        if (string.IsNullOrWhiteSpace(title))
        {
            TempData["ErrorMessage"] =
                localizer["QuizEditor_CategoryTitleRequired"].Value;

            return RedirectToPage(new
            {
                id = category.Round.QuizId,
                selectedRoundId = category.QuizRoundId
            });
        }

        category.Title = title;
        category.Round.Quiz.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync();

        TempData["SuccessMessage"] =
            localizer["QuizEditor_CategoryRenamed"].Value;

        return RedirectToPage(new
        {
            id = category.Round.QuizId,
            selectedRoundId = category.QuizRoundId,
            selectedCategoryId = category.Id
        });
    }
}
