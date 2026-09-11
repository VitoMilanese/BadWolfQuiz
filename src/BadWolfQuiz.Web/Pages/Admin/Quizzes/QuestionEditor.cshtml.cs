using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Models;
using BadWolfQuiz.Web.Localization;
using BadWolfQuiz.Web.Services;
using BadWolfQuiz.Game.Definitions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using System.ComponentModel.DataAnnotations;

namespace BadWolfQuiz.Web.Pages.Admin.Quizzes;

public sealed class QuestionEditorModel(
    QuizDbContext db,
    CurrentHost currentHost,
    MediaUploadProcessor mediaUploadProcessor,
    PremiumHostAccess premiumHostAccess,
    IStringLocalizer<SharedResource> localizer) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public int? NextQuestionId { get; private set; }


    public async Task<IActionResult> OnGetTagSuggestionsAsync(
    string? query,
    CancellationToken cancellationToken)
{
    var suggestions = await QuestionTagSuggestionQuery.GetAsync(
        db,
        query,
        cancellationToken);
    return new JsonResult(suggestions);
}

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var canEdit = await db.QuizQuestions.AsNoTracking().AnyAsync(x =>
            x.Id == id && x.Category.Round.Quiz.MediaState == QuizMediaState.Active);
        if (!canEdit)
        {
            TempData["ErrorMessage"] = localizer["MediaArchive_RestoreBeforeEditing"].Value;
            return RedirectToPage("Index");
        }

        var question = await db.QuizQuestions
            .AsNoTracking()
            .AsSplitQuery()
            .Include(x => x.Category)
                .ThenInclude(x => x.Round)
            .Include(x => x.QuestionBlocks)
            .Include(x => x.AnswerBlocks)
            .Include(x => x.Tags)
            .SingleOrDefaultAsync(x => x.Id == id);

        if (question is null)
        {
            return NotFound();
        }

        var storedPresentationType =
            AllPlayerQuestionCompatibility.ResolveStoredPresentationType(question);
        var wagerMode = QuestionWagerModes.GetMode(storedPresentationType);
        var presentationType =
            QuestionWagerModes.GetContentPresentationType(storedPresentationType);

        Input = new InputModel
        {
            Id = question.Id,
            QuizId = question.Category.Round.QuizId,
            RoundId = question.Category.Round.Id,
            IsSpecial = question.IsSpecial,
            WagerMode = wagerMode,
            PresentationType = presentationType,
            AllPlayerMode = AllPlayerQuestionCompatibility.GetMode(
                presentationType),
            ExcludeFromRandomWagerSelection =
                question.ExcludeFromRandomWagerSelection,
            AllowAnswerRewardModifiers = question.AllowAnswerRewardModifiers,
            BuzzModeOverride = question.BuzzModeOverride,
            BuzzDelaySeconds = question.BuzzDelaySeconds,
            Tags = question.Tags.OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(x => x.Name)
                .ToList()
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
                Autoplay = x.Autoplay,
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
                Autoplay = x.Autoplay,
                FileContentType = x.FileContentType,
                FileName = x.FileName,
                IsAnswerBlock = true
            })
            .ToList();

        NormalizeAnswerOptionsStructure(
            presentationType,
            Input.AnswerBlocks,
            wrapLegacyBlocks: true);

        NextQuestionId = await db.QuizQuestions
            .AsNoTracking()
            .Where(x =>
                x.QuizCategoryId == question.QuizCategoryId &&
                x.RowIndex > question.RowIndex)
            .OrderBy(x => x.RowIndex)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync();

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        Input.PresentationType =
            AllPlayerQuestionCompatibility.ResolvePostedPresentationType(
                Input.PresentationType,
                Input.AllPlayerMode);
        if (Input.PresentationType != QuestionPresentationType.Standard)
        {
            Input.WagerMode = QuestionWagerMode.Normal;
        }

        NormalizeAnswerOptionsStructure(
            Input.PresentationType,
            Input.AnswerBlocks,
            wrapLegacyBlocks: true);

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

        if (Input.PresentationType == QuestionPresentationType.FourClues)
        {
            if (Input.QuestionBlocks?.Count != 4)
            {
                ModelState.AddModelError(
                    $"{nameof(Input)}.{nameof(Input.QuestionBlocks)}",
                    localizer["FourClues_ExactlyFourRequired"]);
            }
        }

        if (Input.PresentationType == QuestionPresentationType.AllPlayerMultipleChoice)
        {
            ValidateAllPlayerMultipleChoiceAnswerOptions();
        }
        else if (Input.PresentationType == QuestionPresentationType.HostMultipleChoice)
        {
            ValidateHostMultipleChoiceAnswerOptions();
        }

        Input.Tags = NormalizeTags(Input.Tags);
        if (Input.Tags.Any(tag => tag.Length > 100))
        {
            ModelState.AddModelError(
                $"{nameof(Input)}.{nameof(Input.Tags)}",
                localizer["QuestionTags_MaxLength"]);
        }

        if (!ModelState.IsValid)
        {
            if (IsAjaxRequest())
            {
                return AjaxValidationError();
            }
            return Page();
        }

        var question = await db.QuizQuestions
            .Include(x => x.Tags)
            .SingleOrDefaultAsync(
                x => x.Id == Input.Id &&
                    x.Category.Round.QuizId == Input.QuizId,
                cancellationToken);

        if (question is null)
        {
            return NotFound();
        }

        var questionBlockSnapshots = await GetQuestionBlockEditMetadataQuery(
                db,
                question.Id)
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        var answerBlockSnapshots = await GetAnswerBlockEditMetadataQuery(
                db,
                question.Id)
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var isAllPlayer = Input.PresentationType is
            QuestionPresentationType.AllPlayerText or
            QuestionPresentationType.AllPlayerMultipleChoice;
        var isHostMultipleChoice =
            Input.PresentationType == QuestionPresentationType.HostMultipleChoice;
        var answerLayout = GetAnswerOptionsLayout();
        var answerOptionSet = answerLayout.Options.ToHashSet();

        question.PresentationType = QuestionWagerModes.Apply(
            Input.PresentationType,
            Input.IsSpecial ? Input.WagerMode : QuestionWagerMode.Normal);
        question.IsSpecial =
            Input.PresentationType != QuestionPresentationType.FourClues &&
            Input.PresentationType != QuestionPresentationType.HostMultipleChoice &&
            Input.IsSpecial;
        question.ExcludeFromRandomWagerSelection =
            isHostMultipleChoice || Input.ExcludeFromRandomWagerSelection;
        question.AllowAnswerRewardModifiers = Input.AllowAnswerRewardModifiers;
        question.BuzzModeOverride = question.IsSpecial || isAllPlayer
            ? BuzzActivationMode.Disabled
            : Input.BuzzModeOverride;
        question.BuzzDelaySeconds = question.IsSpecial || isAllPlayer
            ? 0
            : Math.Max(0, Input.BuzzDelaySeconds);
        question.UpdatedAtUtc = DateTime.UtcNow;
        SynchronizeTags(question, Input.Tags);

        var submittedQuestionBlockIds = Input.QuestionBlocks
            .Where(x => x.Id.HasValue)
            .Select(x => x.Id!.Value)
            .ToHashSet();

        if (submittedQuestionBlockIds.Count !=
                Input.QuestionBlocks.Count(x => x.Id.HasValue) ||
            submittedQuestionBlockIds.Any(id =>
                !questionBlockSnapshots.ContainsKey(id)))
        {
            ModelState.AddModelError(string.Empty, localizer["Error_Unexpected"]);
            if (IsAjaxRequest())
            {
                return AjaxValidationError();
            }
            return Page();
        }

        foreach (var snapshot in questionBlockSnapshots.Values
                     .Where(x => !submittedQuestionBlockIds.Contains(x.Id)))
        {
            db.QuestionContentBlocks.Remove(new QuestionContentBlock
            {
                Id = snapshot.Id,
                QuizQuestionId = question.Id
            });
        }

        var persistedQuestionBlocks =
            new List<QuestionContentBlock>(Input.QuestionBlocks.Count);

        foreach (var inputBlock in Input.QuestionBlocks.OrderBy(x => x.SortOrder))
        {
            QuestionContentBlock entity;

            if (inputBlock.Id.HasValue)
            {
                entity = AttachQuestionBlockForUpdate(
                    db,
                    question.Id,
                    questionBlockSnapshots[inputBlock.Id.Value]);
            }
            else
            {
                entity = new QuestionContentBlock
                {
                    QuizQuestionId = question.Id
                };
                db.QuestionContentBlocks.Add(entity);
            }

            if (inputBlock.RemoveFile &&
                inputBlock.BlockType is ContentBlockType.Image or ContentBlockType.Audio)
            {
                ClearStoredFile(entity);
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

                    if (IsAjaxRequest())
                    {
                        return AjaxValidationError();
                    }
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
            entity.Autoplay = inputBlock.Autoplay &&
                inputBlock.BlockType is ContentBlockType.Audio or ContentBlockType.Video or ContentBlockType.YouTube;
            persistedQuestionBlocks.Add(entity);
        }

        var submittedAnswerBlockIds = Input.AnswerBlocks
            .Where(x => x.Id.HasValue)
            .Select(x => x.Id!.Value)
            .ToHashSet();

        if (submittedAnswerBlockIds.Count !=
                Input.AnswerBlocks.Count(x => x.Id.HasValue) ||
            submittedAnswerBlockIds.Any(id =>
                !answerBlockSnapshots.ContainsKey(id)))
        {
            ModelState.AddModelError(string.Empty, localizer["Error_Unexpected"]);
            if (IsAjaxRequest())
            {
                return AjaxValidationError();
            }
            return Page();
        }

        foreach (var snapshot in answerBlockSnapshots.Values
                     .Where(x => !submittedAnswerBlockIds.Contains(x.Id)))
        {
            db.AnswerContentBlocks.Remove(new AnswerContentBlock
            {
                Id = snapshot.Id,
                QuizQuestionId = question.Id
            });
        }

        var persistedAnswerBlocks =
            new List<AnswerContentBlock>(Input.AnswerBlocks.Count);

        foreach (var inputBlock in Input.AnswerBlocks.OrderBy(x => x.SortOrder))
        {
            AnswerContentBlock entity;

            if (inputBlock.Id.HasValue)
            {
                entity = AttachAnswerBlockForUpdate(
                    db,
                    question.Id,
                    answerBlockSnapshots[inputBlock.Id.Value]);
            }
            else
            {
                entity = new AnswerContentBlock
                {
                    QuizQuestionId = question.Id
                };
                db.AnswerContentBlocks.Add(entity);
            }

            var isAnswerOptionsMarker =
                inputBlock.BlockType == ContentBlockType.AnswerOptions;
            var isHostAnswerOption =
                isHostMultipleChoice && answerOptionSet.Contains(inputBlock);

            if (!isAnswerOptionsMarker &&
                inputBlock.RemoveFile &&
                inputBlock.BlockType is ContentBlockType.Image or ContentBlockType.Audio)
            {
                ClearStoredFile(entity);
            }

            if (!isAnswerOptionsMarker &&
                inputBlock.UploadedFile is not null &&
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

                    if (IsAjaxRequest())
                    {
                        return AjaxValidationError();
                    }
                    return Page();
                }
            }

            entity.SortOrder = inputBlock.SortOrder;
            entity.BlockType = inputBlock.BlockType;
            entity.TextContent = isAnswerOptionsMarker
                ? Input.PresentationType == QuestionPresentationType.AllPlayerMultipleChoice
                    ? AnswerOptionsBlockContract.StoreOptionState(
                        answerLayout.Options.Count,
                        AnswerOptionsBlockContract.ParseCorrectOptionIndexes(
                            inputBlock.TextContent,
                            answerLayout.Options.Count))
                    : AnswerOptionsBlockContract.StoreOptionCount(
                        answerLayout.Options.Count)
                : inputBlock.TextContent?.Trim();
            entity.TopCaption = isAnswerOptionsMarker || isHostAnswerOption
                ? null
                : inputBlock.TopCaption?.Trim();
            entity.BottomCaption = isAnswerOptionsMarker || isHostAnswerOption
                ? null
                : inputBlock.BottomCaption?.Trim();
            entity.ExternalUrl = isAnswerOptionsMarker || isHostAnswerOption
                ? null
                : inputBlock.ExternalUrl?.Trim();
            entity.AudioOnly = !isAnswerOptionsMarker &&
                !isHostAnswerOption &&
                inputBlock.AudioOnly;
            entity.Autoplay = !isAnswerOptionsMarker &&
                !isHostAnswerOption &&
                inputBlock.Autoplay &&
                inputBlock.BlockType is ContentBlockType.Audio or ContentBlockType.Video or ContentBlockType.YouTube;
            if (isAnswerOptionsMarker || isHostAnswerOption)
            {
                ClearStoredFile(entity);
            }
            persistedAnswerBlocks.Add(entity);
        }

        await db.SaveChangesAsync(cancellationToken);

        if (IsAjaxRequest())
        {
            return new JsonResult(new
            {
                success = true,
                message = localizer["Message_QuestionSaved"].Value,
                questionBlocks = persistedQuestionBlocks
                    .OrderBy(x => x.SortOrder)
                    .Select(x => new { id = x.Id, sortOrder = x.SortOrder })
                    .ToArray(),
                answerBlocks = persistedAnswerBlocks
                    .OrderBy(x => x.SortOrder)
                    .Select(x => new { id = x.Id, sortOrder = x.SortOrder })
                    .ToArray()
            });
        }

        TempData["SuccessMessage"] = localizer["Message_QuestionSaved"].Value;
        return RedirectToPage(new { id = Input.Id });
    }

    internal sealed record EditableContentBlockSnapshot(
        int Id,
        int SortOrder,
        ContentBlockType BlockType,
        string? TextContent,
        string? TopCaption,
        string? BottomCaption,
        string? ExternalUrl,
        bool AudioOnly,
        bool Autoplay);

    internal static IQueryable<EditableContentBlockSnapshot>
        GetQuestionBlockEditMetadataQuery(
            QuizDbContext db,
            int questionId) =>
        db.QuestionContentBlocks
            .AsNoTracking()
            .Where(x => x.QuizQuestionId == questionId)
            .Select(x => new EditableContentBlockSnapshot(
                x.Id,
                x.SortOrder,
                x.BlockType,
                x.TextContent,
                x.TopCaption,
                x.BottomCaption,
                x.ExternalUrl,
                x.AudioOnly,
                x.Autoplay));

    internal static IQueryable<EditableContentBlockSnapshot>
        GetAnswerBlockEditMetadataQuery(
            QuizDbContext db,
            int questionId) =>
        db.AnswerContentBlocks
            .AsNoTracking()
            .Where(x => x.QuizQuestionId == questionId)
            .Select(x => new EditableContentBlockSnapshot(
                x.Id,
                x.SortOrder,
                x.BlockType,
                x.TextContent,
                x.TopCaption,
                x.BottomCaption,
                x.ExternalUrl,
                x.AudioOnly,
                x.Autoplay));

    internal static QuestionContentBlock AttachQuestionBlockForUpdate(
        QuizDbContext db,
        int questionId,
        EditableContentBlockSnapshot snapshot)
    {
        var entity = new QuestionContentBlock
        {
            Id = snapshot.Id,
            QuizQuestionId = questionId
        };
        ApplyEditableSnapshot(entity, snapshot);
        db.QuestionContentBlocks.Attach(entity);
        return entity;
    }

    internal static AnswerContentBlock AttachAnswerBlockForUpdate(
        QuizDbContext db,
        int questionId,
        EditableContentBlockSnapshot snapshot)
    {
        var entity = new AnswerContentBlock
        {
            Id = snapshot.Id,
            QuizQuestionId = questionId
        };
        ApplyEditableSnapshot(entity, snapshot);
        db.AnswerContentBlocks.Attach(entity);
        return entity;
    }

    private static void ApplyEditableSnapshot(
        ContentBlockBase entity,
        EditableContentBlockSnapshot snapshot)
    {
        entity.SortOrder = snapshot.SortOrder;
        entity.BlockType = snapshot.BlockType;
        entity.TextContent = snapshot.TextContent;
        entity.TopCaption = snapshot.TopCaption;
        entity.BottomCaption = snapshot.BottomCaption;
        entity.ExternalUrl = snapshot.ExternalUrl;
        entity.AudioOnly = snapshot.AudioOnly;
        entity.Autoplay = snapshot.Autoplay;
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

    private void ValidateAllPlayerMultipleChoiceAnswerOptions()
    {
        var layout = GetAnswerOptionsLayout();
        if (layout.Marker is null ||
            layout.StoredOptionCount != layout.Options.Count ||
            layout.Options.Count is < 2 or > 4)
        {
            ModelState.AddModelError(
                $"{nameof(Input)}.{nameof(Input.AnswerBlocks)}",
                "All-player multiple-choice questions require between 2 and 4 answer options.");
            return;
        }

        var correctOptionIndexes = AnswerOptionsBlockContract.ParseCorrectOptionIndexes(
            layout.Marker.TextContent,
            layout.Options.Count);
        if (correctOptionIndexes.Count == 0)
        {
            ModelState.AddModelError(
                $"{nameof(Input)}.{nameof(Input.AnswerBlocks)}",
                "All-player multiple-choice questions require at least one correct answer option.");
            return;
        }

        if (Input.QuestionBlocks.Any(block =>
                block.BlockType is not ContentBlockType.Text and
                    not ContentBlockType.Image))
        {
            ModelState.AddModelError(
                $"{nameof(Input)}.{nameof(Input.QuestionBlocks)}",
                "All-player multiple-choice questions support only text and image question blocks.");
            return;
        }

        if (layout.Options.Any(option => !IsValidAllPlayerAnswerOption(option)))
        {
            ModelState.AddModelError(
                $"{nameof(Input)}.{nameof(Input.AnswerBlocks)}",
                "Every answer option must be non-empty text or an image.");
            return;
        }

        var textOptions = layout.Options
            .Where(option => option.BlockType == ContentBlockType.Text)
            .Select(option => option.TextContent!.Trim())
            .ToArray();
        if (textOptions.Distinct(StringComparer.OrdinalIgnoreCase).Count() !=
            textOptions.Length)
        {
            ModelState.AddModelError(
                $"{nameof(Input)}.{nameof(Input.AnswerBlocks)}",
                "Text answer options must be unique.");
        }
    }

    private void ValidateHostMultipleChoiceAnswerOptions()
    {
        var layout = GetAnswerOptionsLayout();
        var options = layout.Options;
        if (layout.Marker is null ||
            layout.StoredOptionCount != options.Count ||
            options.Count is < 4 or > 10)
        {
            ModelState.AddModelError(
                $"{nameof(Input)}.{nameof(Input.AnswerBlocks)}",
                "Multiple-choice questions require between 4 and 10 answer options.");
            return;
        }

        if (options.Any(option =>
                option.BlockType != ContentBlockType.Text ||
                string.IsNullOrWhiteSpace(option.TextContent) ||
                option.TextContent.Trim().Length > 20))
        {
            ModelState.AddModelError(
                $"{nameof(Input)}.{nameof(Input.AnswerBlocks)}",
                "Every answer option must be non-empty text with at most 20 characters.");
            return;
        }

        var normalizedOptions = options
            .Select(option => option.TextContent!.Trim())
            .ToArray();
        if (normalizedOptions.Distinct(StringComparer.OrdinalIgnoreCase).Count() !=
            normalizedOptions.Length)
        {
            ModelState.AddModelError(
                $"{nameof(Input)}.{nameof(Input.AnswerBlocks)}",
                "Multiple-choice answer options must be unique.");
        }
    }

    private static bool IsValidAllPlayerAnswerOption(ContentBlockInputModel option)
    {
        if (option.BlockType == ContentBlockType.Text)
        {
            return !string.IsNullOrWhiteSpace(option.TextContent);
        }

        if (option.BlockType != ContentBlockType.Image || option.RemoveFile)
        {
            return false;
        }

        return option.UploadedFile is { Length: > 0 } ||
            (!string.IsNullOrWhiteSpace(option.FileContentType) &&
             !string.IsNullOrWhiteSpace(option.FileName));
    }

    private AnswerOptionsInputLayout GetAnswerOptionsLayout()
    {
        var ordered = Input.AnswerBlocks
            .OrderBy(block => block.SortOrder)
            .ToArray();
        var marker = ordered.FirstOrDefault(block =>
            block.BlockType == ContentBlockType.AnswerOptions);
        if (marker is null)
        {
            return new AnswerOptionsInputLayout(null, 0, [], ordered);
        }

        var markerIndex = Array.IndexOf(ordered, marker);
        var storedOptionCount = AnswerOptionsBlockContract.ParseOptionCount(
            marker.TextContent);
        var options = ordered
            .Skip(markerIndex + 1)
            .Take(storedOptionCount)
            .ToArray();
        var additionalBlocks = ordered
            .Skip(markerIndex + 1 + storedOptionCount)
            .ToArray();
        return new AnswerOptionsInputLayout(
            marker,
            storedOptionCount,
            options,
            additionalBlocks);
    }

    private static void NormalizeAnswerOptionsStructure(
        QuestionPresentationType presentationType,
        List<ContentBlockInputModel> blocks,
        bool wrapLegacyBlocks)
    {
        blocks ??= [];
        var ordered = blocks
            .OrderBy(block => block.SortOrder)
            .ToList();
        var marker = ordered.FirstOrDefault(block =>
            block.BlockType == ContentBlockType.AnswerOptions);

        if (!MultipleChoiceAnswerContract.IsMultipleChoice(presentationType))
        {
            if (marker is not null)
            {
                ordered.Remove(marker);
            }
        }
        else if (marker is null && wrapLegacyBlocks)
        {
            marker = new ContentBlockInputModel
            {
                BlockType = ContentBlockType.AnswerOptions,
                TextContent = AnswerOptionsBlockContract.StoreOptionCount(
                    ordered.Count),
                IsAnswerBlock = true
            };
            ordered.Insert(0, marker);
        }
        else if (marker is not null && ordered[0] != marker)
        {
            ordered.Remove(marker);
            ordered.Insert(0, marker);
        }

        for (var index = 0; index < ordered.Count; index++)
        {
            ordered[index].SortOrder = index;
            ordered[index].IsAnswerBlock = true;
        }

        blocks.Clear();
        blocks.AddRange(ordered);
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

            var normalized = NormalizeTagName(tag);
            if (seen.Add(normalized))
            {
                result.Add(tag);
            }
        }

        return result;
    }

    private static string NormalizeTagName(string tag) =>
        tag.Trim().ToUpperInvariant();

    private static void SynchronizeTags(QuizQuestion question, IReadOnlyCollection<string> tags)
    {
        var desired = tags.ToDictionary(NormalizeTagName, tag => tag, StringComparer.Ordinal);
        foreach (var existing in question.Tags.ToArray())
        {
            if (!desired.ContainsKey(existing.NormalizedName))
            {
                question.Tags.Remove(existing);
            }
        }

        var existingNames = question.Tags
            .Select(tag => tag.NormalizedName)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var pair in desired)
        {
            if (existingNames.Add(pair.Key))
            {
                question.Tags.Add(new QuizQuestionTag
                {
                    Name = pair.Value,
                    NormalizedName = pair.Key
                });
            }
        }
    }

    private bool IsAjaxRequest() => string.Equals(
        Request.Headers["X-Requested-With"],
        "XMLHttpRequest",
        StringComparison.OrdinalIgnoreCase);

    private IActionResult AjaxValidationError()
    {
        var error = ModelState.Values
            .SelectMany(value => value.Errors)
            .Select(item => item.ErrorMessage)
            .FirstOrDefault(message => !string.IsNullOrWhiteSpace(message))
            ?? localizer["Error_Unexpected"].Value;

        return BadRequest(new { success = false, error });
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

        [Display(Name = "Label_WagerMode")]
        public QuestionWagerMode WagerMode { get; set; }

        [Display(Name = "Label_QuestionType")]
        public QuestionPresentationType PresentationType { get; set; }

        public string? AllPlayerMode { get; set; }

        [Display(Name = "Label_ExcludeFromRandomWagerSelection")]
        public bool ExcludeFromRandomWagerSelection { get; set; }

        [Display(Name = "Label_AllowAnswerRewardModifiers")]
        public bool AllowAnswerRewardModifiers { get; set; }

        [Display(Name = "Label_BuzzMode")]
        public BuzzActivationMode BuzzModeOverride { get; set; }

        [Display(Name = "Label_BuzzDelay")]
        [Range(0, int.MaxValue)]
        public int BuzzDelaySeconds { get; set; }

        public List<string> Tags { get; set; } = [];

        public List<ContentBlockInputModel> QuestionBlocks { get; set; } = [];

        public List<ContentBlockInputModel> AnswerBlocks { get; set; } = [];
    }

    private sealed record AnswerOptionsInputLayout(
        ContentBlockInputModel? Marker,
        int StoredOptionCount,
        IReadOnlyList<ContentBlockInputModel> Options,
        IReadOnlyList<ContentBlockInputModel> AdditionalBlocks);
}