using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Localization;
using BadWolfQuiz.Web.Models;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
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
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string EntityTitle { get; private set; } = string.Empty;

    public bool IsCategory => Input.CategoryId.HasValue;

    public sealed class InputModel
    {
        public int QuizId { get; set; }
        public int RoundId { get; set; }
        public int? CategoryId { get; set; }
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
                .Include(x => x.DescriptionBlocks)
                .Include(x => x.Round)
                .SingleOrDefaultAsync(x => x.Id == categoryId.Value, cancellationToken);

            if (category is null)
            {
                return NotFound();
            }

            if (category.Round.Quiz.MediaState != QuizMediaState.Active)
            {
                TempData["ErrorMessage"] = localizer["MediaArchive_RestoreBeforeEditing"].Value;
                return RedirectToPage("Index");
            }

            Input = new InputModel
            {
                QuizId = category.Round.QuizId,
                RoundId = category.Round.Id,
                CategoryId = category.Id,
                Blocks = category.DescriptionBlocks
                    .OrderBy(x => x.SortOrder)
                    .Select(ToInputModel)
                    .ToList()
            };
            EntityTitle = category.Title;
            return Page();
        }

        if (!roundId.HasValue)
        {
            return BadRequest();
        }

        var round = await db.QuizRounds
            .AsNoTracking()
            .Include(x => x.DescriptionBlocks)
            .Include(x => x.Quiz)
            .SingleOrDefaultAsync(x => x.Id == roundId.Value, cancellationToken);

        if (round is null)
        {
            return NotFound();
        }

        if (round.Quiz.MediaState != QuizMediaState.Active)
        {
            TempData["ErrorMessage"] = localizer["MediaArchive_RestoreBeforeEditing"].Value;
            return RedirectToPage("Index");
        }

        Input = new InputModel
        {
            QuizId = round.QuizId,
            RoundId = round.Id,
            Blocks = round.DescriptionBlocks
                .OrderBy(x => x.SortOrder)
                .Select(ToInputModel)
                .ToList()
        };
        EntityTitle = round.Title;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!await db.Quizzes.AsNoTracking().AnyAsync(
                x => x.Id == Input.QuizId && x.MediaState == QuizMediaState.Active,
                cancellationToken))
        {
            TempData["ErrorMessage"] = localizer["MediaArchive_RestoreBeforeEditing"].Value;
            return RedirectToPage("Index");
        }

        Input.Blocks ??= new List<ContentBlockInputModel>();

        if (Input.CategoryId.HasValue)
        {
            var category = await db.QuizCategories
                .Include(x => x.DescriptionBlocks)
                .Include(x => x.Round)
                .SingleOrDefaultAsync(
                    x => x.Id == Input.CategoryId.Value &&
                         x.QuizRoundId == Input.RoundId &&
                         x.Round.QuizId == Input.QuizId,
                    cancellationToken);

            if (category is null)
            {
                return NotFound();
            }

            EntityTitle = category.Title;
            if (!await SyncBlocksAsync(category.DescriptionBlocks, cancellationToken))
            {
                return Page();
            }
        }
        else
        {
            var round = await db.QuizRounds
                .Include(x => x.DescriptionBlocks)
                .SingleOrDefaultAsync(
                    x => x.Id == Input.RoundId && x.QuizId == Input.QuizId,
                    cancellationToken);

            if (round is null)
            {
                return NotFound();
            }

            EntityTitle = round.Title;
            if (!await SyncBlocksAsync(round.DescriptionBlocks, cancellationToken))
            {
                return Page();
            }
        }

        var quiz = await db.Quizzes.SingleAsync(x => x.Id == Input.QuizId, cancellationToken);
        quiz.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        TempData["SuccessMessage"] = localizer["Success_Saved"].Value;
        return RedirectToPage(new
        {
            roundId = Input.CategoryId.HasValue ? (int?)null : Input.RoundId,
            categoryId = Input.CategoryId
        });
    }

    private async Task<bool> SyncBlocksAsync<TBlock>(
        ICollection<TBlock> existingBlocks,
        CancellationToken cancellationToken)
        where TBlock : ContentBlockBase, new()
    {
        var submittedIds = Input.Blocks
            .Where(x => x.Id.HasValue)
            .Select(x => x.Id!.Value)
            .ToHashSet();

        if (submittedIds.Any(id => existingBlocks.All(x => x.Id != id)))
        {
            ModelState.AddModelError(string.Empty, localizer["Error_Unexpected"]);
            return false;
        }

        db.RemoveRange(existingBlocks.Where(x => !submittedIds.Contains(x.Id)).ToList());

        var sortOrder = 1;
        foreach (var inputBlock in Input.Blocks)
        {
            TBlock entity;
            if (inputBlock.Id.HasValue)
            {
                entity = existingBlocks.Single(x => x.Id == inputBlock.Id.Value);
            }
            else
            {
                entity = new TBlock();
                existingBlocks.Add(entity);
            }

            if (inputBlock.RemoveFile &&
                inputBlock.BlockType is ContentBlockType.Image or ContentBlockType.Audio)
            {
                entity.FileData = null;
                entity.FileContentType = null;
                entity.FileName = null;
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

    private static ContentBlockInputModel ToInputModel(ContentBlockBase block) => new()
    {
        Id = block.Id,
        SortOrder = block.SortOrder,
        BlockType = block.BlockType,
        TextContent = block.TextContent,
        TopCaption = block.TopCaption,
        BottomCaption = block.BottomCaption,
        ExternalUrl = block.ExternalUrl,
        AudioOnly = block.AudioOnly,
        FileContentType = block.FileContentType,
        FileName = block.FileName
    };
}
