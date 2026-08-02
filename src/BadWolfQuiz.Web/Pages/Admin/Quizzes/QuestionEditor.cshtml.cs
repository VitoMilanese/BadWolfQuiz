using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Models;
using BadWolfQuiz.Web.Localization;
using BadWolfQuiz.Game.Definitions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using System.ComponentModel.DataAnnotations;

namespace BadWolfQuiz.Web.Pages.Admin.Quizzes;

public sealed class QuestionEditorModel(QuizDbContext db, IStringLocalizer<SharedResource> localizer) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var question = await db.QuizQuestions
            .Include(x => x.Category)
                .ThenInclude(x => x.Round)
            .Include(x => x.QuestionBlocks)
            .Include(x => x.AnswerBlocks)
            .SingleOrDefaultAsync(x => x.Id == id);

        if (question is null)
        {
            return NotFound();
        }

        var questionBlock = question.QuestionBlocks.OrderBy(x => x.SortOrder).FirstOrDefault();
        var answerBlock = question.AnswerBlocks.OrderBy(x => x.SortOrder).FirstOrDefault();

        Input = new InputModel
        {
            Id = question.Id,
            QuizId = question.Category.Round.QuizId,
            RoundId = question.Category.Round.Id,
            IsSpecial = question.IsSpecial,
            PresentationType = question.PresentationType,
            ExcludeFromRandomWagerSelection =
                question.ExcludeFromRandomWagerSelection,
            BuzzModeOverride = question.BuzzModeOverride
        };

        Input.QuestionBlocks = question.QuestionBlocks
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
                IsAnswerBlock = false
            })
            .ToList();

        Input.AnswerBlocks = question.AnswerBlocks
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
                IsAnswerBlock = true
            })
            .ToList();

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
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

        if (Input.PresentationType == QuestionPresentationType.FourClues)
        {
            if (Input.QuestionBlocks?.Count != 4)
            {
                ModelState.AddModelError(
                    $"{nameof(Input)}.{nameof(Input.QuestionBlocks)}",
                    localizer["FourClues_ExactlyFourRequired"]);
            }
            else if (Input.QuestionBlocks.Any(block =>
                block.BlockType is ContentBlockType.Video or ContentBlockType.YouTube))
            {
                ModelState.AddModelError(
                    $"{nameof(Input)}.{nameof(Input.QuestionBlocks)}",
                    localizer["FourClues_UnsupportedMedia"]);
            }
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var question = await db.QuizQuestions
            .Include(x => x.QuestionBlocks)
            .Include(x => x.AnswerBlocks)
            .SingleOrDefaultAsync(x => x.Id == Input.Id);

        if (question is null)
        {
            return NotFound();
        }

        question.PresentationType = Input.PresentationType;
        question.IsSpecial = Input.PresentationType == QuestionPresentationType.Standard && Input.IsSpecial;
        question.ExcludeFromRandomWagerSelection =
            Input.ExcludeFromRandomWagerSelection;
        question.BuzzModeOverride = question.IsSpecial
            ? BuzzActivationMode.Disabled
            : Input.BuzzModeOverride;
        question.UpdatedAtUtc = DateTime.UtcNow;

        var submittedQuestionBlockIds = Input.QuestionBlocks
            .Where(x => x.Id.HasValue)
            .Select(x => x.Id!.Value)
            .ToHashSet();

        var questionBlocksToDelete = question.QuestionBlocks
            .Where(x => !submittedQuestionBlockIds.Contains(x.Id))
            .ToList();

        db.RemoveRange(questionBlocksToDelete);

        const int maxFileSizeMb = 10;
        const long maxFileSize = 10 * 1024 * 1024;

        foreach (var inputBlock in Input.QuestionBlocks.OrderBy(x => x.SortOrder))
        {
            QuestionContentBlock entity;

            if (inputBlock.Id.HasValue)
            {
                entity = question.QuestionBlocks
                    .Single(x => x.Id == inputBlock.Id.Value);
            }
            else
            {
                entity = new QuestionContentBlock();
                question.QuestionBlocks.Add(entity);
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
                if (inputBlock.BlockType == ContentBlockType.Image &&
                    !inputBlock.UploadedFile.ContentType.StartsWith(
                        "image/",
                        StringComparison.OrdinalIgnoreCase))
                {
                    ModelState.AddModelError(
                        string.Empty,
                        localizer["InvalidImageFile"]);

                    return Page();
                }

                if (inputBlock.BlockType == ContentBlockType.Audio &&
                    !inputBlock.UploadedFile.ContentType.StartsWith(
                        "audio/",
                        StringComparison.OrdinalIgnoreCase))
                {
                    ModelState.AddModelError(
                        string.Empty,
                        localizer["InvalidAudioFile"]);

                    return Page();
                }

                if (inputBlock.UploadedFile.Length > maxFileSize)
                {
                    ModelState.AddModelError(
                        string.Empty,
                        localizer["FileSizeLimitExceeded", maxFileSizeMb]);

                    return Page();
                }

                await using var memoryStream = new MemoryStream();

                await inputBlock.UploadedFile.CopyToAsync(memoryStream);

                entity.FileData = memoryStream.ToArray();
                entity.FileContentType = inputBlock.UploadedFile.ContentType;
                entity.FileName = Path.GetFileName(
                    inputBlock.UploadedFile.FileName);
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

        var answerBlocksToDelete = question.AnswerBlocks
            .Where(x => !submittedAnswerBlockIds.Contains(x.Id))
            .ToList();

        db.RemoveRange(answerBlocksToDelete);

        foreach (var inputBlock in Input.AnswerBlocks.OrderBy(x => x.SortOrder))
        {
            AnswerContentBlock entity;

            if (inputBlock.Id.HasValue)
            {
                entity = question.AnswerBlocks
                    .Single(x => x.Id == inputBlock.Id.Value);
            }
            else
            {
                entity = new AnswerContentBlock();
                question.AnswerBlocks.Add(entity);
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
                if (inputBlock.BlockType == ContentBlockType.Image &&
                    !inputBlock.UploadedFile.ContentType.StartsWith(
                        "image/",
                        StringComparison.OrdinalIgnoreCase))
                {
                    ModelState.AddModelError(
                        string.Empty,
                        localizer["InvalidImageFile"]);

                    return Page();
                }

                if (inputBlock.BlockType == ContentBlockType.Audio &&
                    !inputBlock.UploadedFile.ContentType.StartsWith(
                        "audio/",
                        StringComparison.OrdinalIgnoreCase))
                {
                    ModelState.AddModelError(
                        string.Empty,
                        localizer["InvalidAudioFile"]);

                    return Page();
                }

                if (inputBlock.UploadedFile.Length > maxFileSize)
                {
                    ModelState.AddModelError(
                        string.Empty,
                        localizer["FileSizeLimitExceeded", maxFileSizeMb]);

                    return Page();
                }

                await using var memoryStream = new MemoryStream();

                await inputBlock.UploadedFile.CopyToAsync(memoryStream);

                entity.FileData = memoryStream.ToArray();
                entity.FileContentType = inputBlock.UploadedFile.ContentType;
                entity.FileName = Path.GetFileName(
                    inputBlock.UploadedFile.FileName);
            }

            entity.SortOrder = inputBlock.SortOrder;
            entity.BlockType = inputBlock.BlockType;
            entity.TextContent = inputBlock.TextContent?.Trim();
            entity.TopCaption = inputBlock.TopCaption?.Trim();
            entity.BottomCaption = inputBlock.BottomCaption?.Trim();
            entity.ExternalUrl = inputBlock.ExternalUrl?.Trim();
            entity.AudioOnly = inputBlock.AudioOnly;
        }

        await db.SaveChangesAsync();
        TempData["SuccessMessage"] = localizer["Message_QuestionSaved"].Value;
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
        var block = await db.QuestionContentBlocks
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
        var block = await db.AnswerContentBlocks
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
        var block = await db.QuestionContentBlocks
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
        var block = await db.AnswerContentBlocks
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
        public int RoundId { get; set; }

        [Display(Name = "Label_SpecialQuestion")]
        public bool IsSpecial { get; set; }

        [Display(Name = "Label_QuestionType")]
        public QuestionPresentationType PresentationType { get; set; }

        [Display(Name = "Label_ExcludeFromRandomWagerSelection")]
        public bool ExcludeFromRandomWagerSelection { get; set; }

        [Display(Name = "Label_BuzzMode")]
        public BuzzActivationMode BuzzModeOverride { get; set; }

        public List<ContentBlockInputModel> QuestionBlocks { get; set; } = [];

        public List<ContentBlockInputModel> AnswerBlocks { get; set; } = [];
    }
}
