using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Localization;
using BadWolfQuiz.Web.Models;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace BadWolfQuiz.Web.Pages.Admin.Quizzes;

public sealed class DescriptionEditorModel(
    QuizDbContext db,
    CurrentHost currentHost,
    MediaUploadProcessor mediaUploadProcessor,
    PremiumHostAccess premiumHostAccess,
    IStringLocalizer<SharedResource> localizer) : PageModel
{
    private const string DefaultCategoryColor = "#2563EB";

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string EntityTitle { get; private set; } = string.Empty;
    public string PreviewTitle { get; private set; } = string.Empty;

    public bool IsCategory => Input.CategoryId.HasValue;

    public sealed class InputModel
    {
        public int QuizId { get; set; }
        public int RoundId { get; set; }
        public int? CategoryId { get; set; }
        public QuizCategoryColorMode ColorMode { get; set; } = QuizCategoryColorMode.Automatic;
        public string CustomColor { get; set; } = DefaultCategoryColor;
        public List<ContentBlockInputModel> Blocks { get; set; } = new();
    }

    public async Task<IActionResult> OnGetAsync(
        int? roundId,
        int? categoryId,
        CancellationToken cancellationToken)
    {
        if (categoryId.HasValue)
        {
            var category = await db.QuizCategories
                .AsNoTracking()
                .Where(x => x.Id == categoryId.Value)
                .Select(x => new
                {
                    x.Id,
                    x.Title,
                    x.ColorMode,
                    x.CustomColor,
                    RoundId = x.QuizRoundId,
                    QuizId = x.Round.QuizId,
                    MediaState = x.Round.Quiz.MediaState
                })
                .SingleOrDefaultAsync(cancellationToken);

            if (category is null)
            {
                return NotFound();
            }

            if (category.MediaState != QuizMediaState.Active)
            {
                TempData["ErrorMessage"] =
                    localizer["MediaArchive_RestoreBeforeEditing"].Value;
                return RedirectToPage("Index");
            }

            var blocks = await db.CategoryDescriptionContentBlocks
                .AsNoTracking()
                .Where(x => x.QuizCategoryId == category.Id)
                .OrderBy(x => x.SortOrder)
                .Select(x => new ContentBlockInputModel
                {
                    Id = x.Id,
                    SortOrder = x.SortOrder,
                    BlockType = x.BlockType,
                    TextContent = x.TextContent,
                    TopCaption = x.TopCaption,
                    BottomCaption = x.BottomCaption,
                    ExternalUrl = x.ExternalUrl,
                    AudioOnly = x.AudioOnly,
                    FileContentType = x.FileContentType,
                    FileName = x.FileName,
                    StoredFileHandler = "CategoryDescriptionBlockFile",
                    StoredAudioHandler = "CategoryDescriptionBlockAudio"
                })
                .ToListAsync(cancellationToken);

            Input = new InputModel
            {
                QuizId = category.QuizId,
                RoundId = category.RoundId,
                CategoryId = category.Id,
                ColorMode = category.ColorMode,
                CustomColor = category.CustomColor ?? DefaultCategoryColor,
                Blocks = blocks
            };
            EntityTitle = category.Title;
            PreviewTitle = await BuildPreviewTitleAsync(
                category.RoundId,
                category.Id,
                category.Title,
                cancellationToken);
            return Page();
        }

        if (!roundId.HasValue)
        {
            return BadRequest();
        }

        var round = await db.QuizRounds
            .AsNoTracking()
            .Where(x => x.Id == roundId.Value)
            .Select(x => new
            {
                x.Id,
                x.Title,
                x.QuizId,
                MediaState = x.Quiz.MediaState
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (round is null)
        {
            return NotFound();
        }

        if (round.MediaState != QuizMediaState.Active)
        {
            TempData["ErrorMessage"] =
                localizer["MediaArchive_RestoreBeforeEditing"].Value;
            return RedirectToPage("Index");
        }

        var roundBlocks = await db.RoundDescriptionContentBlocks
            .AsNoTracking()
            .Where(x => x.QuizRoundId == round.Id)
            .OrderBy(x => x.SortOrder)
            .Select(x => new ContentBlockInputModel
            {
                Id = x.Id,
                SortOrder = x.SortOrder,
                BlockType = x.BlockType,
                TextContent = x.TextContent,
                TopCaption = x.TopCaption,
                BottomCaption = x.BottomCaption,
                ExternalUrl = x.ExternalUrl,
                AudioOnly = x.AudioOnly,
                FileContentType = x.FileContentType,
                FileName = x.FileName,
                StoredFileHandler = "RoundDescriptionBlockFile",
                StoredAudioHandler = "RoundDescriptionBlockAudio"
            })
            .ToListAsync(cancellationToken);

        Input = new InputModel
        {
            QuizId = round.QuizId,
            RoundId = round.Id,
            Blocks = roundBlocks
        };
        EntityTitle = round.Title;
        PreviewTitle = await BuildPreviewTitleAsync(
            round.Id,
            null,
            round.Title,
            cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        Input.Blocks ??= new List<ContentBlockInputModel>();

        var quiz = await db.Quizzes.SingleOrDefaultAsync(
            x => x.Id == Input.QuizId,
            cancellationToken);
        if (quiz is null || quiz.MediaState != QuizMediaState.Active)
        {
            TempData["ErrorMessage"] = localizer["MediaArchive_RestoreBeforeEditing"].Value;
            return RedirectToPage("Index");
        }

        if (Input.CategoryId.HasValue)
        {
            var category = await db.QuizCategories
                .SingleOrDefaultAsync(
                    x => x.Id == Input.CategoryId.Value &&
                        x.QuizRoundId == Input.RoundId &&
                        x.Round.QuizId == Input.QuizId,
                    cancellationToken);
            if (category is null) return NotFound();

            EntityTitle = category.Title;
            PreviewTitle = await BuildPreviewTitleAsync(
                category.QuizRoundId,
                category.Id,
                category.Title,
                cancellationToken);

            var normalizedCustomColor = NormalizeCategoryCustomColor(Input.CustomColor);
            if (!Enum.IsDefined(Input.ColorMode) ||
                (Input.ColorMode == QuizCategoryColorMode.Custom && normalizedCustomColor is null))
            {
                ModelState.AddModelError(
                    "Input.CustomColor",
                    localizer["CategoryColor_Invalid"].Value);
                ApplyStoredHandlers(
                    Input.Blocks,
                    "CategoryDescriptionBlockFile",
                    "CategoryDescriptionBlockAudio");
                return Page();
            }

            category.ColorMode = Input.ColorMode;
            category.CustomColor = normalizedCustomColor;

            var existingBlocks = await GetCategoryDescriptionBlockEditMetadataQuery(
                    db,
                    category.Id)
                .ToDictionaryAsync(x => x.Id, cancellationToken);
            if (!await SyncBlocksAsync(
                    db.CategoryDescriptionContentBlocks,
                    existingBlocks,
                    id => new CategoryDescriptionContentBlock
                    {
                        Id = id,
                        QuizCategoryId = category.Id
                    },
                    cancellationToken))
            {
                ApplyStoredHandlers(
                    Input.Blocks,
                    "CategoryDescriptionBlockFile",
                    "CategoryDescriptionBlockAudio");
                return Page();
            }
        }
        else
        {
            var round = await db.QuizRounds
                .SingleOrDefaultAsync(
                    x => x.Id == Input.RoundId && x.QuizId == Input.QuizId,
                    cancellationToken);
            if (round is null) return NotFound();

            EntityTitle = round.Title;
            PreviewTitle = await BuildPreviewTitleAsync(
                round.Id,
                null,
                round.Title,
                cancellationToken);

            var existingBlocks = await GetRoundDescriptionBlockEditMetadataQuery(
                    db,
                    round.Id)
                .ToDictionaryAsync(x => x.Id, cancellationToken);
            if (!await SyncBlocksAsync(
                    db.RoundDescriptionContentBlocks,
                    existingBlocks,
                    id => new RoundDescriptionContentBlock
                    {
                        Id = id,
                        QuizRoundId = round.Id
                    },
                    cancellationToken))
            {
                ApplyStoredHandlers(
                    Input.Blocks,
                    "RoundDescriptionBlockFile",
                    "RoundDescriptionBlockAudio");
                return Page();
            }
        }

        quiz.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return RedirectToPage(new
        {
            roundId = Input.CategoryId.HasValue ? (int?)null : Input.RoundId,
            categoryId = Input.CategoryId,
            saved = true
        });
    }

    public async Task<IActionResult> OnPostRenameAsync(
        int quizId,
        int roundId,
        int? categoryId,
        string? title,
        CancellationToken cancellationToken)
    {
        var trimmedTitle = title?.Trim();
        if (string.IsNullOrWhiteSpace(trimmedTitle))
        {
            return BadRequest(new
            {
                success = false,
                error = localizer[
                    categoryId.HasValue
                        ? "QuizEditor_CategoryTitleRequired"
                        : "QuizEditor_RoundTitleRequired"].Value
            });
        }

        string previewTitle;
        if (categoryId.HasValue)
        {
            var category = await db.QuizCategories
                .Include(x => x.Round)
                    .ThenInclude(x => x.Quiz)
                .SingleOrDefaultAsync(x =>
                    x.Id == categoryId.Value &&
                    x.QuizRoundId == roundId &&
                    x.Round.QuizId == quizId &&
                    x.Round.Quiz.MediaState == QuizMediaState.Active,
                    cancellationToken);

            if (category is null)
            {
                return NotFound();
            }

            category.Title = trimmedTitle;
            category.Round.Quiz.UpdatedAtUtc = DateTime.UtcNow;
            previewTitle = await BuildPreviewTitleAsync(
                roundId,
                category.Id,
                trimmedTitle,
                cancellationToken);
        }
        else
        {
            var round = await db.QuizRounds
                .Include(x => x.Quiz)
                .SingleOrDefaultAsync(x =>
                    x.Id == roundId &&
                    x.QuizId == quizId &&
                    x.Quiz.MediaState == QuizMediaState.Active,
                    cancellationToken);

            if (round is null)
            {
                return NotFound();
            }

            round.Title = trimmedTitle;
            round.Quiz.UpdatedAtUtc = DateTime.UtcNow;
            previewTitle = await BuildPreviewTitleAsync(
                round.Id,
                null,
                trimmedTitle,
                cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);

        return new JsonResult(new
        {
            success = true,
            title = trimmedTitle,
            previewTitle
        });
    }

    public PartialViewResult OnGetContentBlock(
        string fieldPrefix,
        ContentBlockType blockType,
        int index)
    {
        var model = new ContentBlockInputModel
        {
            BlockType = blockType,
            SortOrder = index + 1
        };
        var viewData = new ViewDataDictionary<ContentBlockInputModel>(ViewData, model);
        viewData.TemplateInfo.HtmlFieldPrefix = $"{fieldPrefix}[{index}]";
        return new PartialViewResult
        {
            ViewName = "Shared/_ContentBlockCard",
            ViewData = viewData
        };
    }

    public Task<IActionResult> OnGetRoundDescriptionBlockFileAsync(int id) =>
        GetStoredBlockFileAsync(db.RoundDescriptionContentBlocks, id, false);

    public Task<IActionResult> OnGetRoundDescriptionBlockAudioAsync(int id) =>
        GetStoredBlockFileAsync(db.RoundDescriptionContentBlocks, id, true);

    public Task<IActionResult> OnGetCategoryDescriptionBlockFileAsync(int id) =>
        GetStoredBlockFileAsync(db.CategoryDescriptionContentBlocks, id, false);

    public Task<IActionResult> OnGetCategoryDescriptionBlockAudioAsync(int id) =>
        GetStoredBlockFileAsync(db.CategoryDescriptionContentBlocks, id, true);

    private async Task<IActionResult> GetStoredBlockFileAsync<TBlock>(
        DbSet<TBlock> blocks,
        int id,
        bool inline)
        where TBlock : ContentBlockBase
    {
        var block = await blocks
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id);
        if (block is null ||
            block.FileData is null ||
            block.FileData.Length == 0 ||
            string.IsNullOrWhiteSpace(block.FileContentType))
        {
            return NotFound();
        }

        return inline
            ? File(block.FileData, block.FileContentType)
            : File(block.FileData, block.FileContentType, block.FileName);
    }

    internal sealed record EditableDescriptionBlockSnapshot(
        int Id,
        int SortOrder,
        ContentBlockType BlockType,
        string? TextContent,
        string? TopCaption,
        string? BottomCaption,
        string? ExternalUrl,
        bool AudioOnly,
        string? FileContentType,
        string? FileName);

    internal static IQueryable<EditableDescriptionBlockSnapshot>
        GetCategoryDescriptionBlockEditMetadataQuery(
            QuizDbContext db,
            int categoryId) =>
        db.CategoryDescriptionContentBlocks
            .AsNoTracking()
            .Where(x => x.QuizCategoryId == categoryId)
            .Select(x => new EditableDescriptionBlockSnapshot(
                x.Id,
                x.SortOrder,
                x.BlockType,
                x.TextContent,
                x.TopCaption,
                x.BottomCaption,
                x.ExternalUrl,
                x.AudioOnly,
                x.FileContentType,
                x.FileName));

    internal static IQueryable<EditableDescriptionBlockSnapshot>
        GetRoundDescriptionBlockEditMetadataQuery(
            QuizDbContext db,
            int roundId) =>
        db.RoundDescriptionContentBlocks
            .AsNoTracking()
            .Where(x => x.QuizRoundId == roundId)
            .Select(x => new EditableDescriptionBlockSnapshot(
                x.Id,
                x.SortOrder,
                x.BlockType,
                x.TextContent,
                x.TopCaption,
                x.BottomCaption,
                x.ExternalUrl,
                x.AudioOnly,
                x.FileContentType,
                x.FileName));

    internal static CategoryDescriptionContentBlock
        AttachCategoryDescriptionBlockForUpdate(
            QuizDbContext db,
            int categoryId,
            EditableDescriptionBlockSnapshot snapshot)
    {
        var entity = new CategoryDescriptionContentBlock
        {
            Id = snapshot.Id,
            QuizCategoryId = categoryId
        };
        ApplyEditableSnapshot(entity, snapshot);
        db.CategoryDescriptionContentBlocks.Attach(entity);
        return entity;
    }

    private async Task<bool> SyncBlocksAsync<TBlock>(
        DbSet<TBlock> blockSet,
        IReadOnlyDictionary<int, EditableDescriptionBlockSnapshot> existingBlocks,
        Func<int, TBlock> createBlock,
        CancellationToken cancellationToken)
        where TBlock : ContentBlockBase
    {
        var submittedIdValues = Input.Blocks
            .Where(x => x.Id.HasValue)
            .Select(x => x.Id!.Value)
            .ToList();
        var submittedIds = submittedIdValues.ToHashSet();
        if (submittedIds.Count != submittedIdValues.Count ||
            submittedIds.Any(id => !existingBlocks.ContainsKey(id)))
        {
            ModelState.AddModelError(string.Empty, localizer["Error_Unexpected"]);
            return false;
        }

        foreach (var existing in existingBlocks.Values
                     .Where(x => !submittedIds.Contains(x.Id)))
        {
            blockSet.Remove(createBlock(existing.Id));
        }

        var sortOrder = 1;
        foreach (var inputBlock in Input.Blocks)
        {
            TBlock entity;
            if (inputBlock.Id.HasValue)
            {
                entity = createBlock(inputBlock.Id.Value);
                ApplyEditableSnapshot(entity, existingBlocks[inputBlock.Id.Value]);
                blockSet.Attach(entity);
            }
            else
            {
                entity = createBlock(0);
                blockSet.Add(entity);
            }

            if (inputBlock.RemoveFile &&
                inputBlock.BlockType is ContentBlockType.Image or ContentBlockType.Audio)
            {
                ClearStoredFile(entity);
            }

            if (inputBlock.UploadedFile is not null && inputBlock.UploadedFile.Length > 0)
            {
                try
                {
                    var media = await mediaUploadProcessor.ProcessContentBlockAsync(
                        inputBlock.UploadedFile,
                        inputBlock.BlockType,
                        premiumHostAccess.IsPremium(currentHost.RequiredId),
                        cancellationToken);
                    entity.FileData = media.Data;
                    entity.FileContentType = media.ContentType;
                    entity.FileName = media.FileName;
                }
                catch (MediaUploadException exception)
                {
                    ModelState.AddModelError(
                        string.Empty,
                        localizer[exception.ResourceKey, exception.ResourceArguments]);
                    return false;
                }
            }

            entity.SortOrder = sortOrder++;
            entity.BlockType = inputBlock.BlockType;
            entity.TextContent = inputBlock.TextContent?.Trim();
            entity.TopCaption = inputBlock.TopCaption?.Trim();
            entity.BottomCaption = inputBlock.BottomCaption?.Trim();
            entity.ExternalUrl = inputBlock.ExternalUrl?.Trim();
            entity.AudioOnly = inputBlock.AudioOnly;
        }

        return true;
    }

    private static void ApplyEditableSnapshot(
        ContentBlockBase entity,
        EditableDescriptionBlockSnapshot snapshot)
    {
        entity.SortOrder = snapshot.SortOrder;
        entity.BlockType = snapshot.BlockType;
        entity.TextContent = snapshot.TextContent;
        entity.TopCaption = snapshot.TopCaption;
        entity.BottomCaption = snapshot.BottomCaption;
        entity.ExternalUrl = snapshot.ExternalUrl;
        entity.AudioOnly = snapshot.AudioOnly;
        entity.FileContentType = snapshot.FileContentType;
        entity.FileName = snapshot.FileName;
    }

    private void ClearStoredFile(ContentBlockBase entity)
    {
        entity.FileData = null;
        entity.FileContentType = null;
        entity.FileName = null;

        var entry = db.Entry(entity);
        if (entry.State == EntityState.Added)
        {
            return;
        }

        entry.Property(nameof(ContentBlockBase.FileData)).IsModified = true;
        entry.Property(nameof(ContentBlockBase.FileContentType)).IsModified = true;
        entry.Property(nameof(ContentBlockBase.FileName)).IsModified = true;
    }

    private async Task<string> BuildPreviewTitleAsync(
        int roundId,
        int? categoryId,
        string? title,
        CancellationToken cancellationToken)
    {
        var trimmedTitle = title?.Trim() ?? string.Empty;
        var isNumericTitle = trimmedTitle.Length > 0 && trimmedTitle.All(char.IsDigit);
        if (categoryId.HasValue)
        {
            if (!string.IsNullOrWhiteSpace(trimmedTitle))
            {
                return trimmedTitle;
            }

            var categoryLabel = localizer["Label_Category"].Value;
            var categoryIds = await db.QuizCategories
                .AsNoTracking()
                .Where(x => x.QuizRoundId == roundId)
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.Id)
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);
            var position = categoryIds.IndexOf(categoryId.Value) + 1;
            return $"{categoryLabel} {Math.Max(position, 1)}";
        }

        var roundLabel = localizer["Label_Round"].Value;
        if (isNumericTitle)
        {
            return $"{roundLabel} {trimmedTitle}";
        }

        if (!string.IsNullOrWhiteSpace(trimmedTitle))
        {
            return trimmedTitle;
        }

        var quizId = await db.QuizRounds
            .AsNoTracking()
            .Where(x => x.Id == roundId)
            .Select(x => x.QuizId)
            .SingleAsync(cancellationToken);
        var roundIds = await db.QuizRounds
            .AsNoTracking()
            .Where(x => x.QuizId == quizId)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Id)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
        var roundPosition = roundIds.IndexOf(roundId) + 1;
        return $"{roundLabel} {Math.Max(roundPosition, 1)}";
    }

    private static string? NormalizeCategoryCustomColor(string? value)
    {
        var normalized = value?.Trim().ToUpperInvariant();
        if (normalized is null ||
            normalized.Length != 7 ||
            normalized[0] != '#' ||
            normalized.Skip(1).Any(character => !Uri.IsHexDigit(character)))
        {
            return null;
        }

        return normalized;
    }

    private static void ApplyStoredHandlers(
        IEnumerable<ContentBlockInputModel> blocks,
        string fileHandler,
        string audioHandler)
    {
        foreach (var block in blocks)
        {
            block.StoredFileHandler = fileHandler;
            block.StoredAudioHandler = audioHandler;
        }
    }
}
