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
        bool isAnswerBlock)
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
            FileContentType = block.FileContentType,
            FileName = block.FileName,
            IsAnswerBlock = isAnswerBlock
        };
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
        if (Input.QuestionBlocks == null || Input.QuestionBlocks.Count == 0)
        {
            ModelState.AddModelError(
                $"{nameof(Input)}.{nameof(Input.QuestionBlocks)}",
                localizer["QuestionBlocksRequired"]);
        }

        if (Input.AnswerBlocks == null || Input.AnswerBlocks.Count == 0)
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
            .Include(x => x.FinalQuestionBlocks)
            .Include(x => x.FinalAnswerBlocks)
            .SingleOrDefaultAsync(x => x.Id == Input.Id);

        if (quiz is null)
        {
            return NotFound();
        }

        quiz.UpdatedAtUtc = DateTime.UtcNow;

        var submittedQuestionBlockIds = Input.QuestionBlocks
            .Where(x => x.Id.HasValue)
            .Select(x => x.Id!.Value)
            .ToHashSet();

        var questionBlocksToDelete = quiz.FinalQuestionBlocks
            .Where(x => !submittedQuestionBlockIds.Contains(x.Id))
            .ToList();

        db.RemoveRange(questionBlocksToDelete);

        foreach (var inputBlock in Input.QuestionBlocks.OrderBy(x => x.SortOrder))
        {
            FinalQuestionContentBlock entity;

            if (inputBlock.Id.HasValue)
            {
                entity = quiz.FinalQuestionBlocks
                    .Single(x => x.Id == inputBlock.Id.Value);
            }
            else
            {
                entity = new FinalQuestionContentBlock();
                quiz.FinalQuestionBlocks.Add(entity);
            }

            if (inputBlock.RemoveFile &&
                inputBlock.BlockType is ContentBlockType.Image or ContentBlockType.Audio)
            {
                entity.FileData = null;
                entity.FileContentType = null;
                entity.FileName = null;
            }

            if (inputBlock.UploadedFile is not null &&
                inputBlock.UploadedFile.Length > 0)
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

                    return Page();
                }
            }

            entity.SortOrder = inputBlock.SortOrder;
            entity.BlockType = inputBlock.BlockType;
            entity.TextContent = inputBlock.TextContent?.Trim();
            entity.TopCaption = inputBlock.TopCaption?.Trim();
            entity.BottomCaption = inputBlock.BottomCaption?.Trim();
            entity.ExternalUrl = inputBlock.ExternalUrl?.Trim();
            entity.AudioOnly = inputBlock.AudioOnly;
        }

        var submittedAnswerBlockIds = Input.AnswerBlocks
            .Where(x => x.Id.HasValue)
            .Select(x => x.Id!.Value)
            .ToHashSet();

        var answerBlocksToDelete = quiz.FinalAnswerBlocks
            .Where(x => !submittedAnswerBlockIds.Contains(x.Id))
            .ToList();

        db.RemoveRange(answerBlocksToDelete);

        foreach (var inputBlock in Input.AnswerBlocks.OrderBy(x => x.SortOrder))
        {
            FinalAnswerContentBlock entity;

            if (inputBlock.Id.HasValue)
            {
                entity = quiz.FinalAnswerBlocks
                    .Single(x => x.Id == inputBlock.Id.Value);
            }
            else
            {
                entity = new FinalAnswerContentBlock();
                quiz.FinalAnswerBlocks.Add(entity);
            }

            if (inputBlock.RemoveFile &&
                inputBlock.BlockType is ContentBlockType.Image or ContentBlockType.Audio)
            {
                entity.FileData = null;
                entity.FileContentType = null;
                entity.FileName = null;
            }

            if (inputBlock.UploadedFile is not null &&
                inputBlock.UploadedFile.Length > 0)
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

                    return Page();
                }
            }

            entity.SortOrder = inputBlock.SortOrder;
            entity.BlockType = inputBlock.BlockType;
            entity.TextContent = inputBlock.TextContent?.Trim();
            entity.TopCaption = inputBlock.TopCaption?.Trim();
            entity.BottomCaption = inputBlock.BottomCaption?.Trim();
            entity.ExternalUrl = inputBlock.ExternalUrl?.Trim();
            entity.AudioOnly = inputBlock.AudioOnly;
        }

        await db.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = localizer["Message_FinalQuestionSaved"].Value;
        //return RedirectToPage("Editor", new { id = Input.QuizId });
        return RedirectToPage(new { id = Input.Id });
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

    public async Task<IActionResult> OnGetQuestionBlockFileAsync(int id)
    {
        var block = await db.FinalQuestionContentBlocks
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id);

        if (block is null ||
            block.FileData is null ||
            block.FileData.Length == 0 ||
            string.IsNullOrWhiteSpace(block.FileContentType))
        {
            return NotFound();
        }

        return File(block.FileData, block.FileContentType, block.FileName);
    }

    public async Task<IActionResult> OnGetAnswerBlockFileAsync(int id)
    {
        var block = await db.FinalAnswerContentBlocks
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id);

        if (block is null ||
            block.FileData is null ||
            block.FileData.Length == 0 ||
            string.IsNullOrWhiteSpace(block.FileContentType))
        {
            return NotFound();
        }

        return File(block.FileData, block.FileContentType, block.FileName);
    }

    public async Task<IActionResult> OnGetQuestionBlockAudioAsync(int id)
    {
        var block = await db.FinalQuestionContentBlocks
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id);

        if (block is null ||
            block.FileData is null ||
            block.FileData.Length == 0 ||
            string.IsNullOrWhiteSpace(block.FileContentType))
        {
            return NotFound();
        }

        return File(
            block.FileData,
            block.FileContentType);
    }

    public async Task<IActionResult> OnGetAnswerBlockAudioAsync(int id)
    {
        var block = await db.FinalAnswerContentBlocks
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id);

        if (block is null ||
            block.FileData is null ||
            block.FileData.Length == 0 ||
            string.IsNullOrWhiteSpace(block.FileContentType))
        {
            return NotFound();
        }

        return File(
            block.FileData,
            block.FileContentType);
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

        public List<ContentBlockInputModel> QuestionBlocks { get; set; } = [];

        public List<ContentBlockInputModel> AnswerBlocks { get; set; } = [];
    }
}
