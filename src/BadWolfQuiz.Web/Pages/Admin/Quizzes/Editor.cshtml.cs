using BadWolfQuiz.Game.Definitions;
using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Localization;
using BadWolfQuiz.Web.Models;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace BadWolfQuiz.Web.Pages.Admin.Quizzes;

public sealed class EditorModel(
    QuizDbContext db,
    GameSessionLauncher gameSessionLauncher,
    IStringLocalizer<SharedResource> localizer,
    IOptions<QuizEditorOptions> editorOptions) : PageModel
{
    const int TemporarySortOrderOffset = 100000;

    public int MinimumCategoryCount => editorOptions.Value.MinimumCategoryCount;
    public int MaximumCategoryCount => editorOptions.Value.MaximumCategoryCount;
    public int MinimumQuestionCount => editorOptions.Value.MinimumQuestionCount;
    public int MaximumQuestionCount => editorOptions.Value.MaximumQuestionCount;

    public static int CalculateDefaultPoints(int roundNumber, int rowNumber)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(roundNumber);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rowNumber);
        return checked(200 * roundNumber * rowNumber);
    }

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

    [BindProperty]
    public ExchangeQuestionsInputModel ExchangeQuestions { get; set; } = new();

    [BindProperty]
    public DeleteQuestionInputModel DeleteQuestion { get; set; } = new();

    [BindProperty]
    public DeleteFinalQuestionInputModel DeleteFinalQuestion { get; set; } = new();

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

        public bool UseRandomWagerQuestions { get; set; }

        public int RandomWagerQuestionCount { get; set; }

        public bool UseRandomAnonymousSharedWagerQuestions { get; set; }

        public int RandomAnonymousSharedWagerQuestionCount { get; set; }

        public int QuestionCount { get; set; }

        public int CategoryCount { get; set; }

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

    public sealed class ExchangeQuestionsInputModel
    {
        public int QuizId { get; set; }

        public int SourceQuestionId { get; set; }

        public int TargetQuestionId { get; set; }
    }

    public sealed class DeleteQuestionInputModel
    {
        public int QuizId { get; set; }

        public int QuestionId { get; set; }
    }

    public sealed class DeleteFinalQuestionInputModel
    {
        public int QuizId { get; set; }

        public int RoundId { get; set; }
    }

    private sealed class EditorContentBlockData
    {
        public int Id { get; set; }
        public int ParentId { get; set; }
        public ContentBlockType BlockType { get; set; }
        public string? TextContent { get; set; }
        public string? MediaPath { get; set; }
        public string? ExternalUrl { get; set; }
        public int SortOrder { get; set; }
        public bool HasFileData { get; set; }
    }

    private static TBlock CreateLightweightBlock<TBlock>(
        EditorContentBlockData source)
        where TBlock : ContentBlockBase, new()
    {
        var block = new TBlock
        {
            Id = source.Id,
            BlockType = source.BlockType,
            TextContent = source.TextContent,
            MediaPath = source.MediaPath,
            ExternalUrl = source.ExternalUrl,
            SortOrder = source.SortOrder,
            FileData = source.HasFileData ? new byte[1] : null
        };

        return block;
    }

    public async Task<IActionResult> OnGetAsync(
        int id,
        int? selectedRoundId = null)
    {
        var mediaState = await db.Quizzes.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => (QuizMediaState?)x.MediaState)
            .SingleOrDefaultAsync();
        if (mediaState != QuizMediaState.Active)
        {
            TempData["ErrorMessage"] = localizer["MediaArchive_RestoreBeforeEditing"].Value;
            return RedirectToPage("Index");
        }

        // Load only the structural data needed for tabs, the visible board and
        // move/exchange dialogs. Content blocks are loaded separately below so
        // large FileData BLOB values are never materialized by the editor GET.
        var quiz = await db.Quizzes
            .AsNoTracking()
            .Include(x => x.Rounds)
                .ThenInclude(x => x.Rows)
            .Include(x => x.Rounds)
                .ThenInclude(x => x.Categories)
                    .ThenInclude(x => x.Questions)
            .AsSplitQuery()
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

        SelectedRoundId =
            selectedRoundId.HasValue &&
            rounds.Any(x => x.Id == selectedRoundId.Value)
                ? selectedRoundId.Value
                : rounds[0].Id;

        var selectedRound = rounds.Single(x => x.Id == SelectedRoundId);
        var selectedQuestions = selectedRound.Categories
            .SelectMany(x => x.Questions)
            .ToDictionary(x => x.Id);
        var selectedQuestionIds = selectedQuestions.Keys.ToArray();

        if (selectedQuestionIds.Length > 0)
        {
            var questionBlocks = await db.QuestionContentBlocks
                .AsNoTracking()
                .Where(x => selectedQuestionIds.Contains(x.QuizQuestionId))
                .OrderBy(x => x.QuizQuestionId)
                .ThenBy(x => x.SortOrder)
                .Select(x => new EditorContentBlockData
                {
                    Id = x.Id,
                    ParentId = x.QuizQuestionId,
                    BlockType = x.BlockType,
                    TextContent = x.TextContent,
                    MediaPath = x.MediaPath,
                    ExternalUrl = x.ExternalUrl,
                    SortOrder = x.SortOrder,
                    HasFileData = x.FileData != null
                })
                .ToListAsync();

            foreach (var data in questionBlocks)
            {
                if (!selectedQuestions.TryGetValue(data.ParentId, out var question))
                {
                    continue;
                }

                var block = CreateLightweightBlock<QuestionContentBlock>(data);
                block.QuizQuestionId = data.ParentId;
                question.QuestionBlocks.Add(block);
            }

            var answerBlocks = await db.AnswerContentBlocks
                .AsNoTracking()
                .Where(x => selectedQuestionIds.Contains(x.QuizQuestionId))
                .OrderBy(x => x.QuizQuestionId)
                .ThenBy(x => x.SortOrder)
                .Select(x => new EditorContentBlockData
                {
                    Id = x.Id,
                    ParentId = x.QuizQuestionId,
                    BlockType = x.BlockType,
                    TextContent = x.TextContent,
                    MediaPath = x.MediaPath,
                    ExternalUrl = x.ExternalUrl,
                    SortOrder = x.SortOrder,
                    HasFileData = x.FileData != null
                })
                .ToListAsync();

            foreach (var data in answerBlocks)
            {
                if (!selectedQuestions.TryGetValue(data.ParentId, out var question))
                {
                    continue;
                }

                var block = CreateLightweightBlock<AnswerContentBlock>(data);
                block.QuizQuestionId = data.ParentId;
                question.AnswerBlocks.Add(block);
            }
        }

        var finalQuestionBlocks = await db.FinalQuestionContentBlocks
            .AsNoTracking()
            .Where(x => x.QuizId == quiz.Id)
            .OrderBy(x => x.SortOrder)
            .Select(x => new EditorContentBlockData
            {
                Id = x.Id,
                ParentId = x.QuizId,
                BlockType = x.BlockType,
                TextContent = x.TextContent,
                MediaPath = x.MediaPath,
                ExternalUrl = x.ExternalUrl,
                SortOrder = x.SortOrder,
                HasFileData = x.FileData != null
            })
            .ToListAsync();

        foreach (var data in finalQuestionBlocks)
        {
            var block = CreateLightweightBlock<FinalQuestionContentBlock>(data);
            block.QuizId = quiz.Id;
            quiz.FinalQuestionBlocks.Add(block);
        }

        var finalAnswerBlocks = await db.FinalAnswerContentBlocks
            .AsNoTracking()
            .Where(x => x.QuizId == quiz.Id)
            .OrderBy(x => x.SortOrder)
            .Select(x => new EditorContentBlockData
            {
                Id = x.Id,
                ParentId = x.QuizId,
                BlockType = x.BlockType,
                TextContent = x.TextContent,
                MediaPath = x.MediaPath,
                ExternalUrl = x.ExternalUrl,
                SortOrder = x.SortOrder,
                HasFileData = x.FileData != null
            })
            .ToListAsync();

        foreach (var data in finalAnswerBlocks)
        {
            var block = CreateLightweightBlock<FinalAnswerContentBlock>(data);
            block.QuizId = quiz.Id;
            quiz.FinalAnswerBlocks.Add(block);
        }

        Quiz = quiz;

        RoundRows = new RoundRowsInputModel
        {
            QuizId = quiz.Id,
            RoundId = selectedRound.Id,
            UseRandomWagerQuestions = selectedRound.UseRandomWagerQuestions,
            RandomWagerQuestionCount =
                selectedRound.RandomWagerQuestionCount,
            UseRandomAnonymousSharedWagerQuestions =
                selectedRound.UseRandomAnonymousSharedWagerQuestions,
            RandomAnonymousSharedWagerQuestionCount =
                selectedRound.RandomAnonymousSharedWagerQuestionCount,
            QuestionCount = selectedRound.Rows.Count,
            CategoryCount = selectedRound.Categories.Count,
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
                    .ThenInclude(x => x.DescriptionBlocks)
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
            .LastOrDefault();

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
        var questionCount = Math.Clamp(
            templateRows?.Count ?? editorOptions.Value.MinimumQuestionCount,
            editorOptions.Value.MinimumQuestionCount,
            editorOptions.Value.MaximumQuestionCount);
        var roundNumber = quiz.Rounds.Count + 1;

        for (var rowIndex = 1; rowIndex <= questionCount; rowIndex++)
        {
            round.Rows.Add(new QuizRoundRow
            {
                RowIndex = rowIndex,
                Points = CalculateDefaultPoints(roundNumber, rowIndex)
            });
        }

        if (templateRound is not null)
        {
            foreach (var category in templateRound.Categories
                .OrderBy(x => x.SortOrder)
                .Take(editorOptions.Value.MaximumCategoryCount))
            {
                var newCategory = new QuizCategory
                {
                    Title = category.Title,
                    SortOrder = category.SortOrder
                };

                CategoryDescriptionBlockCloner.CopyDescriptions(
                    category,
                    newCategory);

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

        while (round.Categories.Count < editorOptions.Value.MinimumCategoryCount)
        {
            var categoryNumber = round.Categories.Count + 1;
            var category = new QuizCategory
            {
                Title = localizer["Default_CategoryTitle", categoryNumber],
                SortOrder = categoryNumber
            };

            foreach (var roundRow in round.Rows.OrderBy(x => x.RowIndex))
            {
                var question = new QuizQuestion { RowIndex = roundRow.RowIndex };
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
                category.Questions.Add(question);
            }

            round.Categories.Add(category);
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

    public async Task<IActionResult> OnPostExchangeQuestionsAsync()
    {
        if (ExchangeQuestions.SourceQuestionId <= 0 ||
            ExchangeQuestions.TargetQuestionId <= 0 ||
            ExchangeQuestions.SourceQuestionId ==
            ExchangeQuestions.TargetQuestionId)
        {
            return BadRequest();
        }

        var quiz = await db.Quizzes
            .Include(x => x.Rounds)
                .ThenInclude(x => x.Categories)
                    .ThenInclude(x => x.Questions)
            .SingleOrDefaultAsync(x => x.Id == ExchangeQuestions.QuizId);

        if (quiz is null)
        {
            return NotFound();
        }

        var questions = quiz.Rounds
            .SelectMany(x => x.Categories)
            .SelectMany(x => x.Questions)
            .ToDictionary(x => x.Id);

        if (!questions.TryGetValue(
                ExchangeQuestions.SourceQuestionId,
                out var sourceQuestion) ||
            !questions.TryGetValue(
                ExchangeQuestions.TargetQuestionId,
                out var targetQuestion))
        {
            return NotFound();
        }

        var categories = quiz.Rounds
            .SelectMany(x => x.Categories)
            .ToDictionary(x => x.Id);

        var sourceRoundId = categories[sourceQuestion.QuizCategoryId]
            .QuizRoundId;
        var sourceCategoryId = sourceQuestion.QuizCategoryId;
        var sourceRowIndex = sourceQuestion.RowIndex;
        var targetCategoryId = targetQuestion.QuizCategoryId;
        var targetRowIndex = targetQuestion.RowIndex;

        await using var transaction =
            await db.Database.BeginTransactionAsync();

        try
        {
            sourceQuestion.RowIndex =
                TemporarySortOrderOffset + sourceQuestion.Id;
            await db.SaveChangesAsync();

            targetQuestion.QuizCategoryId = sourceCategoryId;
            targetQuestion.RowIndex = sourceRowIndex;
            await db.SaveChangesAsync();

            sourceQuestion.QuizCategoryId = targetCategoryId;
            sourceQuestion.RowIndex = targetRowIndex;
            sourceQuestion.UpdatedAtUtc = DateTime.UtcNow;
            targetQuestion.UpdatedAtUtc = DateTime.UtcNow;
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

    public async Task<IActionResult> OnPostDeleteQuestionAsync()
    {
        var question = await db.QuizQuestions
            .Include(x => x.Category)
                .ThenInclude(x => x.Round)
                    .ThenInclude(x => x.Quiz)
            .SingleOrDefaultAsync(x =>
                x.Id == DeleteQuestion.QuestionId &&
                x.Category.Round.QuizId == DeleteQuestion.QuizId);

        if (question is null)
        {
            return NotFound();
        }

        await using var transaction = await db.Database.BeginTransactionAsync();

        await db.QuestionContentBlocks
            .Where(x => x.QuizQuestionId == question.Id)
            .ExecuteDeleteAsync();
        await db.AnswerContentBlocks
            .Where(x => x.QuizQuestionId == question.Id)
            .ExecuteDeleteAsync();

        db.QuestionContentBlocks.Add(new QuestionContentBlock
        {
            QuizQuestionId = question.Id,
            BlockType = ContentBlockType.Text,
            SortOrder = 1
        });
        db.AnswerContentBlocks.Add(new AnswerContentBlock
        {
            QuizQuestionId = question.Id,
            BlockType = ContentBlockType.Text,
            SortOrder = 1
        });

        var now = DateTime.UtcNow;
        question.UpdatedAtUtc = now;
        question.Category.Round.Quiz.UpdatedAtUtc = now;

        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        if (IsAjaxRequest())
        {
            return new JsonResult(new
            {
                success = true,
                message = localizer["QuizEditor_QuestionDeleted"].Value
            });
        }

        TempData["SuccessMessage"] =
            localizer["QuizEditor_QuestionDeleted"].Value;

        return RedirectToPage(new
        {
            id = question.Category.Round.QuizId,
            selectedRoundId = question.Category.QuizRoundId
        });
    }

    public async Task<IActionResult> OnPostDeleteFinalQuestionAsync()
    {
        var quiz = await db.Quizzes
            .SingleOrDefaultAsync(x => x.Id == DeleteFinalQuestion.QuizId);

        if (quiz is null)
        {
            return NotFound();
        }

        await using var transaction = await db.Database.BeginTransactionAsync();

        await db.FinalQuestionContentBlocks
            .Where(x => x.QuizId == quiz.Id)
            .ExecuteDeleteAsync();
        await db.FinalAnswerContentBlocks
            .Where(x => x.QuizId == quiz.Id)
            .ExecuteDeleteAsync();

        quiz.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        TempData["SuccessMessage"] =
            localizer["Message_FinalQuestionDeleted"].Value;

        return RedirectToPage(new
        {
            id = quiz.Id,
            selectedRoundId = DeleteFinalQuestion.RoundId
        });
    }

    public async Task<IActionResult> OnPostSaveRoundRowsAsync(
        bool play,
        CancellationToken cancellationToken)
    {
        var quiz = await db.Quizzes
            .Include(x => x.Rounds)
                .ThenInclude(x => x.Rows)
            .Include(x => x.Rounds)
                .ThenInclude(x => x.Categories)
                    .ThenInclude(x => x.Questions)
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

        if (RoundRows.QuestionCount < MinimumQuestionCount ||
            RoundRows.QuestionCount > MaximumQuestionCount ||
            RoundRows.CategoryCount < MinimumCategoryCount ||
            RoundRows.CategoryCount > MaximumCategoryCount ||
            submittedRows.Count == 0 ||
            submittedRows.Any(x => x.RowIndex <= 0 || x.Points <= 0))
        {
            if (IsAjaxRequest() && !play)
            {
                return BadRequest(new
                {
                    success = false,
                    error = localizer["QuizEditor_InvalidRoundRows"].Value
                });
            }
            TempData["ErrorMessage"] =
                localizer["QuizEditor_InvalidRoundRows"].Value;

            return RedirectToPage(new
            {
                id = quiz.Id,
                selectedRoundId = round.Id
            });
        }

        var removedRowIndexes = round.Rows
            .Where(row => row.RowIndex > RoundRows.QuestionCount)
            .Select(row => row.RowIndex)
            .ToHashSet();

        if (removedRowIndexes.Count > 0)
        {
            db.RemoveRange(round.Categories
                .SelectMany(category => category.Questions)
                .Where(question => removedRowIndexes.Contains(question.RowIndex)));
            db.RemoveRange(round.Rows
                .Where(row => removedRowIndexes.Contains(row.RowIndex)));
        }

        var roundNumber = quiz.Rounds
            .OrderBy(item => item.SortOrder)
            .ToList()
            .FindIndex(item => item.Id == round.Id) + 1;

        for (var rowIndex = 1; rowIndex <= RoundRows.QuestionCount; rowIndex++)
        {
            if (round.Rows.All(row => row.RowIndex != rowIndex))
            {
                round.Rows.Add(new QuizRoundRow
                {
                    RowIndex = rowIndex,
                    Points = CalculateDefaultPoints(roundNumber, rowIndex)
                });
            }

            foreach (var category in round.Categories)
            {
                if (category.Questions.Any(question =>
                    question.RowIndex == rowIndex))
                {
                    continue;
                }

                var question = new QuizQuestion { RowIndex = rowIndex };
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
                category.Questions.Add(question);
            }
        }

        var orderedCategories = round.Categories
            .OrderBy(category => category.SortOrder)
            .ToList();
        var categoriesToRemove = orderedCategories
            .Skip(RoundRows.CategoryCount)
            .ToList();

        if (categoriesToRemove.Count > 0)
        {
            db.RemoveRange(categoriesToRemove);
        }

        var nextCategorySortOrder = orderedCategories.Count == 0
            ? 1
            : orderedCategories.Max(category => category.SortOrder) + 1;

        while (orderedCategories.Count < RoundRows.CategoryCount)
        {
            var categoryNumber = orderedCategories.Count + 1;
            var category = new QuizCategory
            {
                Title = localizer["Default_CategoryTitle", categoryNumber],
                SortOrder = nextCategorySortOrder++
            };

            foreach (var roundRow in round.Rows
                .Where(row => row.RowIndex <= RoundRows.QuestionCount)
                .OrderBy(row => row.RowIndex))
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
                category.Questions.Add(question);
            }

            round.Categories.Add(category);
            orderedCategories.Add(category);
        }

        var eligibleQuestions = round.Categories
            .Where(category => !categoriesToRemove.Contains(category))
            .SelectMany(category => category.Questions)
            .Where(question =>
                question.RowIndex <= RoundRows.QuestionCount &&
                !question.ExcludeFromRandomWagerSelection)
            .ToList();
        var eligibleQuestionCount = eligibleQuestions.Count;
        var eligibleAnonymousSharedQuestionCount = eligibleQuestions.Count(
            question => QuestionWagerModes.GetContentPresentationType(
                question.PresentationType) == QuestionPresentationType.Standard);

        if (RoundRows.UseRandomWagerQuestions &&
            (RoundRows.RandomWagerQuestionCount < 0 ||
             RoundRows.RandomWagerQuestionCount > eligibleQuestionCount))
        {
            return RandomWagerValidationError(
                "QuizEditor_InvalidRandomWagerCount",
                eligibleQuestionCount);
        }

        if (RoundRows.UseRandomAnonymousSharedWagerQuestions &&
            (RoundRows.RandomAnonymousSharedWagerQuestionCount < 0 ||
             RoundRows.RandomAnonymousSharedWagerQuestionCount >
                eligibleAnonymousSharedQuestionCount))
        {
            return RandomWagerValidationError(
                "QuizEditor_InvalidRandomAnonymousSharedWagerCount",
                eligibleAnonymousSharedQuestionCount);
        }

        var requestedRandomWagerCount =
            (RoundRows.UseRandomWagerQuestions
                ? RoundRows.RandomWagerQuestionCount
                : 0) +
            (RoundRows.UseRandomAnonymousSharedWagerQuestions
                ? RoundRows.RandomAnonymousSharedWagerQuestionCount
                : 0);
        if (requestedRandomWagerCount > eligibleQuestionCount)
        {
            return RandomWagerValidationError(
                "QuizEditor_InvalidCombinedRandomWagerCount",
                eligibleQuestionCount);
        }

        round.UseRandomWagerQuestions =
            RoundRows.UseRandomWagerQuestions;
        round.RandomWagerQuestionCount =
            Math.Max(0, RoundRows.RandomWagerQuestionCount);
        round.UseRandomAnonymousSharedWagerQuestions =
            RoundRows.UseRandomAnonymousSharedWagerQuestions;
        round.RandomAnonymousSharedWagerQuestionCount =
            Math.Max(0, RoundRows.RandomAnonymousSharedWagerQuestionCount);

        IActionResult RandomWagerValidationError(
            string resourceKey,
            int maximum)
        {
            var error = localizer[resourceKey, maximum].Value;
            if (IsAjaxRequest() && !play)
            {
                return BadRequest(new { success = false, error });
            }

            TempData["ErrorMessage"] = error;
            return RedirectToPage(new
            {
                id = quiz.Id,
                selectedRoundId = round.Id
            });
        }

        foreach (var submittedRow in submittedRows)
        {
            if (submittedRow.RowIndex > RoundRows.QuestionCount)
            {
                continue;
            }

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

        if (play)
        {
            try
            {
                var game = await gameSessionLauncher.CreateAsync(
                    quiz.Id,
                    cancellationToken);

                if (game is null)
                {
                    return NotFound();
                }

                return RedirectToPage(
                    "/Admin/Games/Lobby",
                    new { id = game.Session.Id.Value });
            }
            catch (ArgumentException)
            {
                TempData["ErrorMessage"] =
                    localizer["Error_QuizCannotStart"].Value;
                return RedirectToPage(new
                {
                    id = quiz.Id,
                    selectedRoundId = round.Id
                });
            }
            catch (InvalidOperationException)
            {
                TempData["ErrorMessage"] =
                    localizer["Error_QuizCannotStart"].Value;
                return RedirectToPage(new
                {
                    id = quiz.Id,
                    selectedRoundId = round.Id
                });
            }
        }

        if (IsAjaxRequest())
        {
            return new JsonResult(new
            {
                success = true,
                message = localizer["QuizEditor_RoundRowsSaved"].Value
            });
        }

        TempData["SuccessMessage"] =
            localizer["QuizEditor_RoundRowsSaved"].Value;

        return RedirectToPage(new
        {
            id = quiz.Id,
            selectedRoundId = round.Id
        });
    }

    private bool IsAjaxRequest() => string.Equals(
        Request.Headers["X-Requested-With"],
        "XMLHttpRequest",
        StringComparison.OrdinalIgnoreCase);

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