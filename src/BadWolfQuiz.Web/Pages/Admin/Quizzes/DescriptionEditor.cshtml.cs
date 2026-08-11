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
        public List<ContentBlockInputModel> Blocks { get; set; } = new();
    }

    public async Task<IActionResult> OnGetAsync(int? roundId, int? categoryId, CancellationToken cancellationToken)
    {
        if (categoryId.HasValue)
        {
            var category = await db.QuizCategories.AsNoTracking().Include(x => x.DescriptionBlocks).Include(x => x.Round).ThenInclude(x => x.Quiz).SingleOrDefaultAsync(x => x.Id == categoryId.Value, cancellationToken);
            if (category is null) return NotFound();
            if (category.Round.Quiz.MediaState != QuizMediaState.Active)
            {
                TempData["ErrorMessage"] = localizer["MediaArchive_RestoreBeforeEditing"].Value;
                return RedirectToPage("Index");
            }
            Input = new InputModel { QuizId = category.Round.QuizId, RoundId = category.Round.Id, CategoryId = category.Id, Blocks = category.DescriptionBlocks.OrderBy(x => x.SortOrder).Select(x => ToInputModel(x, "CategoryDescriptionBlockFile", "CategoryDescriptionBlockAudio")).ToList() };
            EntityTitle = category.Title;
            PreviewTitle = await BuildPreviewTitleAsync(category.Round.Id, category.Id, category.Title, cancellationToken);
            return Page();
        }
        if (!roundId.HasValue) return BadRequest();
        var round = await db.QuizRounds.AsNoTracking().Include(x => x.DescriptionBlocks).Include(x => x.Quiz).SingleOrDefaultAsync(x => x.Id == roundId.Value, cancellationToken);
        if (round is null) return NotFound();
        if (round.Quiz.MediaState != QuizMediaState.Active)
        {
            TempData["ErrorMessage"] = localizer["MediaArchive_RestoreBeforeEditing"].Value;
            return RedirectToPage("Index");
        }
        Input = new InputModel { QuizId = round.QuizId, RoundId = round.Id, Blocks = round.DescriptionBlocks.OrderBy(x => x.SortOrder).Select(x => ToInputModel(x, "RoundDescriptionBlockFile", "RoundDescriptionBlockAudio")).ToList() };
        EntityTitle = round.Title;
        PreviewTitle = await BuildPreviewTitleAsync(round.Id, null, round.Title, cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!await db.Quizzes.AsNoTracking().AnyAsync(x => x.Id == Input.QuizId && x.MediaState == QuizMediaState.Active, cancellationToken))
        {
            TempData["ErrorMessage"] = localizer["MediaArchive_RestoreBeforeEditing"].Value;
            return RedirectToPage("Index");
        }
        Input.Blocks ??= new List<ContentBlockInputModel>();
        if (Input.CategoryId.HasValue)
        {
            var category = await db.QuizCategories.Include(x => x.DescriptionBlocks).Include(x => x.Round).SingleOrDefaultAsync(x => x.Id == Input.CategoryId.Value && x.QuizRoundId == Input.RoundId && x.Round.QuizId == Input.QuizId, cancellationToken);
            if (category is null) return NotFound();
            EntityTitle = category.Title;
            PreviewTitle = await BuildPreviewTitleAsync(category.Round.Id, category.Id, category.Title, cancellationToken);
            if (!await SyncBlocksAsync(category.DescriptionBlocks, cancellationToken))
            {
                ApplyStoredHandlers(Input.Blocks, "CategoryDescriptionBlockFile", "CategoryDescriptionBlockAudio");
                return Page();
            }
        }
        else
        {
            var round = await db.QuizRounds.Include(x => x.DescriptionBlocks).SingleOrDefaultAsync(x => x.Id == Input.RoundId && x.QuizId == Input.QuizId, cancellationToken);
            if (round is null) return NotFound();
            EntityTitle = round.Title;
            PreviewTitle = await BuildPreviewTitleAsync(round.Id, null, round.Title, cancellationToken);
            if (!await SyncBlocksAsync(round.DescriptionBlocks, cancellationToken))
            {
                ApplyStoredHandlers(Input.Blocks, "RoundDescriptionBlockFile", "RoundDescriptionBlockAudio");
                return Page();
            }
        }
        var quiz = await db.Quizzes.SingleAsync(x => x.Id == Input.QuizId, cancellationToken);
        quiz.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return RedirectToPage(new { roundId = Input.CategoryId.HasValue ? (int?)null : Input.RoundId, categoryId = Input.CategoryId, saved = true });
    }

    public PartialViewResult OnGetContentBlock(string fieldPrefix, ContentBlockType blockType, int index)
    {
        var model = new ContentBlockInputModel { BlockType = blockType, SortOrder = index + 1 };
        var viewData = new ViewDataDictionary<ContentBlockInputModel>(ViewData, model);
        viewData.TemplateInfo.HtmlFieldPrefix = $"{fieldPrefix}[{index}]";
        return new PartialViewResult { ViewName = "Shared/_ContentBlockCard", ViewData = viewData };
    }

    public Task<IActionResult> OnGetRoundDescriptionBlockFileAsync(int id) => GetStoredBlockFileAsync(db.RoundDescriptionContentBlocks, id, false);
    public Task<IActionResult> OnGetRoundDescriptionBlockAudioAsync(int id) => GetStoredBlockFileAsync(db.RoundDescriptionContentBlocks, id, true);
    public Task<IActionResult> OnGetCategoryDescriptionBlockFileAsync(int id) => GetStoredBlockFileAsync(db.CategoryDescriptionContentBlocks, id, false);
    public Task<IActionResult> OnGetCategoryDescriptionBlockAudioAsync(int id) => GetStoredBlockFileAsync(db.CategoryDescriptionContentBlocks, id, true);

    private async Task<IActionResult> GetStoredBlockFileAsync<TBlock>(DbSet<TBlock> blocks, int id, bool inline) where TBlock : ContentBlockBase
    {
        var block = await blocks.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
        if (block is null || block.FileData is null || block.FileData.Length == 0 || string.IsNullOrWhiteSpace(block.FileContentType)) return NotFound();
        return inline ? File(block.FileData, block.FileContentType) : File(block.FileData, block.FileContentType, block.FileName);
    }

    private async Task<bool> SyncBlocksAsync<TBlock>(ICollection<TBlock> existingBlocks, CancellationToken cancellationToken) where TBlock : ContentBlockBase, new()
    {
        var submittedIds = Input.Blocks.Where(x => x.Id.HasValue).Select(x => x.Id!.Value).ToHashSet();
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
            if (inputBlock.Id.HasValue) entity = existingBlocks.Single(x => x.Id == inputBlock.Id.Value);
            else { entity = new TBlock(); existingBlocks.Add(entity); }
            if (inputBlock.RemoveFile && inputBlock.BlockType is ContentBlockType.Image or ContentBlockType.Audio)
            {
                entity.FileData = null; entity.FileContentType = null; entity.FileName = null;
            }
            if (inputBlock.UploadedFile is not null && inputBlock.UploadedFile.Length > 0)
            {
                try
                {
                    var media = await mediaUploadProcessor.ProcessContentBlockAsync(inputBlock.UploadedFile, inputBlock.BlockType, premiumHostAccess.IsPremium(currentHost.RequiredId), cancellationToken);
                    entity.FileData = media.Data; entity.FileContentType = media.ContentType; entity.FileName = media.FileName;
                }
                catch (MediaUploadException exception)
                {
                    ModelState.AddModelError(string.Empty, localizer[exception.ResourceKey, exception.ResourceArguments]);
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

    private async Task<string> BuildPreviewTitleAsync(int roundId, int? categoryId, string? title, CancellationToken cancellationToken)
    {
        var trimmedTitle = title?.Trim() ?? string.Empty;
        var isNumericTitle = trimmedTitle.Length > 0 && trimmedTitle.All(char.IsDigit);
        if (categoryId.HasValue)
        {
            var categoryLabel = localizer["Label_Category"].Value;
            if (isNumericTitle) return $"{categoryLabel} {trimmedTitle}";
            if (!string.IsNullOrWhiteSpace(trimmedTitle))
                return StartsWithLabel(trimmedTitle, categoryLabel) ? trimmedTitle : $"{categoryLabel}: {trimmedTitle}";
            var categoryIds = await db.QuizCategories.AsNoTracking().Where(x => x.QuizRoundId == roundId).OrderBy(x => x.SortOrder).ThenBy(x => x.Id).Select(x => x.Id).ToListAsync(cancellationToken);
            var position = categoryIds.IndexOf(categoryId.Value) + 1;
            return $"{categoryLabel} {Math.Max(position, 1)}";
        }
        var roundLabel = localizer["Label_Round"].Value;
        if (isNumericTitle) return $"{roundLabel} {trimmedTitle}";
        if (!string.IsNullOrWhiteSpace(trimmedTitle)) return trimmedTitle;
        var quizId = await db.QuizRounds.AsNoTracking().Where(x => x.Id == roundId).Select(x => x.QuizId).SingleAsync(cancellationToken);
        var roundIds = await db.QuizRounds.AsNoTracking().Where(x => x.QuizId == quizId).OrderBy(x => x.SortOrder).ThenBy(x => x.Id).Select(x => x.Id).ToListAsync(cancellationToken);
        var roundPosition = roundIds.IndexOf(roundId) + 1;
        return $"{roundLabel} {Math.Max(roundPosition, 1)}";
    }

    private static bool StartsWithLabel(string title, string label)
    {
        if (!title.StartsWith(label, StringComparison.CurrentCultureIgnoreCase)) return false;
        if (title.Length == label.Length) return true;
        var next = title[label.Length];
        return char.IsWhiteSpace(next) || char.IsPunctuation(next) || char.IsDigit(next);
    }

    private static ContentBlockInputModel ToInputModel(ContentBlockBase block, string fileHandler, string audioHandler) => new()
    {
        Id = block.Id, SortOrder = block.SortOrder, BlockType = block.BlockType, TextContent = block.TextContent, TopCaption = block.TopCaption, BottomCaption = block.BottomCaption, ExternalUrl = block.ExternalUrl, AudioOnly = block.AudioOnly, FileContentType = block.FileContentType, FileName = block.FileName, StoredFileHandler = fileHandler, StoredAudioHandler = audioHandler
    };

    private static void ApplyStoredHandlers(IEnumerable<ContentBlockInputModel> blocks, string fileHandler, string audioHandler)
    {
        foreach (var block in blocks) { block.StoredFileHandler = fileHandler; block.StoredAudioHandler = audioHandler; }
    }
}
