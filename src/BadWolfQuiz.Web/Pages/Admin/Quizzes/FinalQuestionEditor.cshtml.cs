using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Models;
using BadWolfQuiz.Web.Localization;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using System.ComponentModel.DataAnnotations;

namespace BadWolfQuiz.Web.Pages.Admin.Quizzes;

public sealed class FinalQuestionEditorModel(
    QuizDbContext db,
    CurrentHost currentHost,
    MediaUploadProcessor mediaUploadProcessor,
    PremiumHostAccess premiumHostAccess,
    IStringLocalizer<SharedResource> localizer) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var quiz = await db.Quizzes
            .Include(x => x.FinalDescriptionBlocks)
            .Include(x => x.FinalQuestionBlocks)
            .Include(x => x.FinalAnswerBlocks)
            .SingleOrDefaultAsync(x => x.Id == id);

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
            QuizId = quiz.Id,
            DescriptionBlocks = quiz.FinalDescriptionBlocks
                .OrderBy(x => x.SortOrder)
                .Select(x => CreateInputBlock(
                    x,
                    false,
                    "DescriptionBlockFile",
                    "DescriptionBlockAudio"))
                .ToList(),
            QuestionBlocks = quiz.FinalQuestionBlocks
                .OrderBy(x => x.SortOrder)
                .Select(x => CreateInputBlock(x, false))
                .ToList(),
            AnswerBlocks = quiz.FinalAnswerBlocks
                .OrderBy(x => x.SortOrder)
                .Select(x => CreateInputBlock(x, true))
                .ToList()
        };

        return Page();
    }

    private static ContentBlockInputModel CreateInputBlock(
        ContentBlockBase block,
        bool isAnswerBlock,
        string? storedFileHandler = null,
        string? storedAudioHandler = null)
    {
        return new ContentBlockInputModel
        {
            Id = block.Id,
            SortOrder = block.SortOrder,
            BlockType = block.BlockType,
            TextContent = block.TextContent,
            TopCaption = block.TopCaption,
            BottomCaption = block.BottomCaption,
            ExternalUrl = block.ExternalUrl,
            AudioOnly = block.AudioOnly,
            Autoplay = block.Autoplay,
            FileContentType = block.FileContentType,
            FileName = block.FileName,
            IsAnswerBlock = isAnswerBlock,
            StoredFileHandler = storedFileHandler,
            StoredAudioHandler = storedAudioHandler
        };
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        Input.DescriptionBlocks ??= [];
        Input.QuestionBlocks ??= [];
        Input.AnswerBlocks ??= [];
        ApplyDescriptionStoredHandlers(Input.DescriptionBlocks);

        if (!await db.Quizzes.AsNoTracking().AnyAsync(
            x => x.Id == Input.QuizId && x.MediaState == QuizMediaState.Active,
            cancellationToken))
        {
            TempData["ErrorMessage"] = localizer["MediaArchive_RestoreBeforeEditing"].Value;
            return RedirectToPage("Index");
        }

        if (Input.QuestionBlocks.Count == 0)
        {
            ModelState.AddModelError(
                $"{nameof(Input)}.{nameof(Input.QuestionBlocks)}",
                localizer["QuestionBlocksRequired"]);
        }

        if (Input.AnswerBlocks.Count == 0)
        {
            ModelState.AddModelError(
                $"{nameof(Input)}.{nameof(Input.AnswerBlocks)}",
                localizer["AnswerBlocksRequired"]);
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var quiz = await db.Quizzes
            .Include(x => x.FinalDescriptionBlocks)
            .Include(x => x.FinalQuestionBlocks)
            .Include(x => x.FinalAnswerBlocks)
            .SingleOrDefaultAsync(x => x.Id == Input.Id);

        if (quiz is null)
        {
            return NotFound();
        }

        if (!await SyncBlocksAsync(
                quiz.FinalDescriptionBlocks,
                Input.DescriptionBlocks,
                cancellationToken) ||
            !await SyncBlocksAsync(
                quiz.FinalQuestionBlocks,
                Input.QuestionBlocks,
                cancellationToken) ||
            !await SyncBlocksAsync(
                quiz.FinalAnswerBlocks,
                Input.AnswerBlocks,
                cancellationToken))
        {
            return Page();
        }

        quiz.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = localizer["Message_FinalQuestionSaved"].Value;
        return RedirectToPage(new { id = Input.Id });
    }

    private async Task<bool> SyncBlocksAsync<TBlock>(
        ICollection<TBlock> existingBlocks,
        IReadOnlyList<ContentBlockInputModel> inputBlocks,
        CancellationToken cancellationToken)
        where TBlock : ContentBlockBase, new()
    {
        var submittedIds = inputBlocks
            .Where(x => x.Id.HasValue)
            .Select(x => x.Id!.Value)
            .ToHashSet();

        if (submittedIds.Any(id => existingBlocks.All(x => x.Id != id)))
        {
            ModelState.AddModelError(string.Empty, localizer["Error_Unexpected"]);
            return false;
        }

        db.RemoveRange(existingBlocks
            .Where(x => !submittedIds.Contains(x.Id))
            .ToList());

        foreach (var inputBlock in inputBlocks.OrderBy(x => x.SortOrder))
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

            entity.SortOrder = inputBlock.SortOrder;
            entity.BlockType = inputBlock.BlockType;
            entity.TextContent = inputBlock.TextContent?.Trim();
            entity.TopCaption = inputBlock.TopCaption?.Trim();
            entity.BottomCaption = inputBlock.BottomCaption?.Trim();
            entity.ExternalUrl = inputBlock.ExternalUrl?.Trim();
            entity.AudioOnly = inputBlock.AudioOnly;
            entity.Autoplay = inputBlock.Autoplay &&
                inputBlock.BlockType is ContentBlockType.Audio or ContentBlockType.Video or ContentBlockType.YouTube;
        }

        return true;
    }

    public PartialViewResult OnGetContentBlock(
        string fieldPrefix,
        ContentBlockType blockType,
        int index)
    {
        var model = new ContentBlockInputModel
        {
            BlockType = blockType,
            SortOrder = index
        };

        var viewData =
            new Microsoft.AspNetCore.Mvc.ViewFeatures.ViewDataDictionary<ContentBlockInputModel>(
                ViewData,
                model);

        viewData.TemplateInfo.HtmlFieldPrefix = $"{fieldPrefix}[{index}]";

        return new PartialViewResult
        {
            ViewName = "Shared/_ContentBlockCard",
            ViewData = viewData
        };
    }

    public Task<IActionResult> OnGetDescriptionBlockFileAsync(int id) =>
        GetStoredBlockFileAsync(db.FinalDescriptionContentBlocks, id, false);

    public Task<IActionResult> OnGetDescriptionBlockAudioAsync(int id) =>
        GetStoredBlockFileAsync(db.FinalDescriptionContentBlocks, id, true);

    public Task<IActionResult> OnGetQuestionBlockFileAsync(int id) =>
        GetStoredBlockFileAsync(db.FinalQuestionContentBlocks, id, false);

    public Task<IActionResult> OnGetAnswerBlockFileAsync(int id) =>
        GetStoredBlockFileAsync(db.FinalAnswerContentBlocks, id, false);

    public Task<IActionResult> OnGetQuestionBlockAudioAsync(int id) =>
        GetStoredBlockFileAsync(db.FinalQuestionContentBlocks, id, true);

    public Task<IActionResult> OnGetAnswerBlockAudioAsync(int id) =>
        GetStoredBlockFileAsync(db.FinalAnswerContentBlocks, id, true);

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

    private static void ApplyDescriptionStoredHandlers(
        IEnumerable<ContentBlockInputModel> blocks)
    {
        foreach (var block in blocks)
        {
            block.StoredFileHandler = "DescriptionBlockFile";
            block.StoredAudioHandler = "DescriptionBlockAudio";
        }
    }

    public sealed class InputModel
    {
        public int Id { get; set; }
        public int QuizId { get; set; }

        [Display(Name = "Label_SpecialQuestion")]
        public bool IsSpecial { get; set; }

        [Display(Name = "Label_ExcludeFromRandomWagerSelection")]
        public bool ExcludeFromRandomWagerSelection { get; set; }

        [Display(Name = "Label_BuzzMode")]
        public BuzzActivationMode BuzzModeOverride { get; set; }

        public List<ContentBlockInputModel> DescriptionBlocks { get; set; } = [];
        public List<ContentBlockInputModel> QuestionBlocks { get; set; } = [];
        public List<ContentBlockInputModel> AnswerBlocks { get; set; } = [];
    }
}
