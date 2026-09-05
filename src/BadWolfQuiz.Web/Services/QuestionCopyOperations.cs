using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace BadWolfQuiz.Web.Services;

public enum QuestionCopyStatus
{
    Success,
    SourceNotFound,
    TargetNotFound,
    NoCapacity
}

public sealed record QuestionCopyResult(
    QuestionCopyStatus Status,
    int? QuestionId = null,
    int? QuizId = null,
    int? RoundId = null,
    int? CategoryId = null)
{
    public bool Succeeded => Status == QuestionCopyStatus.Success;
}

public sealed record QuestionCopyDestination(
    int QuizId,
    string QuizTitle,
    int RoundId,
    string RoundTitle,
    int CategoryId,
    string CategoryTitle,
    bool HasCapacity);

public static class QuestionCopyOperations
{
    public static async Task<IReadOnlyList<QuestionCopyDestination>?> GetDestinationsAsync(
        QuizDbContext db,
        string hostId,
        int sourceQuestionId,
        int maximumQuestionCount,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentException.ThrowIfNullOrWhiteSpace(hostId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sourceQuestionId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumQuestionCount);

        var sourceExists = await db.QuizQuestions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(question =>
                question.Id == sourceQuestionId &&
                question.Category.Round.Quiz.HostId == hostId &&
                !question.Category.Round.Quiz.IsArchived &&
                question.Category.Round.Quiz.MediaState == QuizMediaState.Active,
                cancellationToken);
        if (!sourceExists)
        {
            return null;
        }

        var categories = await db.QuizCategories
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(category =>
                category.Round.Quiz.HostId == hostId &&
                !category.Round.Quiz.IsArchived &&
                category.Round.Quiz.MediaState == QuizMediaState.Active)
            .OrderBy(category => category.Round.Quiz.Title)
            .ThenBy(category => category.Round.QuizId)
            .ThenBy(category => category.Round.SortOrder)
            .ThenBy(category => category.Round.Id)
            .ThenBy(category => category.SortOrder)
            .ThenBy(category => category.Id)
            .Select(category => new
            {
                QuizId = category.Round.QuizId,
                QuizTitle = category.Round.Quiz.Title,
                RoundId = category.QuizRoundId,
                RoundTitle = category.Round.Title,
                CategoryId = category.Id,
                CategoryTitle = category.Title,
                RoundRowCount = category.Round.Rows.Count
            })
            .ToListAsync(cancellationToken);

        var fullRoundIds = categories
            .Where(category => category.RoundRowCount >= maximumQuestionCount)
            .Select(category => category.RoundId)
            .Distinct()
            .ToArray();
        if (fullRoundIds.Length == 0)
        {
            return categories
                .Select(category => new QuestionCopyDestination(
                    category.QuizId,
                    category.QuizTitle,
                    category.RoundId,
                    category.RoundTitle,
                    category.CategoryId,
                    category.CategoryTitle,
                    HasCapacity: true))
                .ToList();
        }

        var fullCategoryIds = categories
            .Where(category => category.RoundRowCount >= maximumQuestionCount)
            .Select(category => category.CategoryId)
            .ToArray();

        var rows = await db.QuizRoundRows
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(row =>
                fullRoundIds.Contains(row.QuizRoundId) &&
                row.RowIndex > 0 &&
                row.RowIndex <= maximumQuestionCount)
            .Select(row => new
            {
                RoundId = row.QuizRoundId,
                row.RowIndex
            })
            .ToListAsync(cancellationToken);

        var questions = await db.QuizQuestions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(question =>
                fullCategoryIds.Contains(question.QuizCategoryId) &&
                question.RowIndex > 0 &&
                question.RowIndex <= maximumQuestionCount)
            .Select(question => new
            {
                CategoryId = question.QuizCategoryId,
                question.RowIndex,
                IsBlank =
                    question.TimeLimitSecondsOverride == null &&
                    question.BuzzModeOverride == BuzzActivationMode.UseRoundDefault &&
                    question.BuzzDelaySeconds == 0 &&
                    !question.IsSpecial &&
                    question.PresentationType == default &&
                    !question.ExcludeFromRandomWagerSelection &&
                    !question.QuestionBlocks.Any(block =>
                        block.BlockType != ContentBlockType.Text ||
                        (block.TextContent != null && block.TextContent.Trim() != string.Empty) ||
                        (block.TopCaption != null && block.TopCaption.Trim() != string.Empty) ||
                        (block.BottomCaption != null && block.BottomCaption.Trim() != string.Empty) ||
                        (block.MediaPath != null && block.MediaPath.Trim() != string.Empty) ||
                        (block.ExternalUrl != null && block.ExternalUrl.Trim() != string.Empty) ||
                        (block.FileData != null && block.FileData.Length > 0) ||
                        (block.FileContentType != null && block.FileContentType.Trim() != string.Empty) ||
                        (block.FileName != null && block.FileName.Trim() != string.Empty) ||
                        block.AudioOnly ||
                        block.Autoplay) &&
                    !question.AnswerBlocks.Any(block =>
                        block.BlockType != ContentBlockType.Text ||
                        (block.TextContent != null && block.TextContent.Trim() != string.Empty) ||
                        (block.TopCaption != null && block.TopCaption.Trim() != string.Empty) ||
                        (block.BottomCaption != null && block.BottomCaption.Trim() != string.Empty) ||
                        (block.MediaPath != null && block.MediaPath.Trim() != string.Empty) ||
                        (block.ExternalUrl != null && block.ExternalUrl.Trim() != string.Empty) ||
                        (block.FileData != null && block.FileData.Length > 0) ||
                        (block.FileContentType != null && block.FileContentType.Trim() != string.Empty) ||
                        (block.FileName != null && block.FileName.Trim() != string.Empty) ||
                        block.AudioOnly ||
                        block.Autoplay)
            })
            .ToListAsync(cancellationToken);

        var rowsByRound = rows
            .GroupBy(row => row.RoundId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(row => row.RowIndex).ToArray());
        var questionsByCategory = questions
            .GroupBy(question => question.CategoryId)
            .ToDictionary(
                group => group.Key,
                group => group.ToDictionary(
                    question => question.RowIndex,
                    question => question.IsBlank));

        return categories
            .Select(category =>
            {
                if (category.RoundRowCount < maximumQuestionCount)
                {
                    return new QuestionCopyDestination(
                        category.QuizId,
                        category.QuizTitle,
                        category.RoundId,
                        category.RoundTitle,
                        category.CategoryId,
                        category.CategoryTitle,
                        HasCapacity: true);
                }

                rowsByRound.TryGetValue(category.RoundId, out var roundRows);
                questionsByCategory.TryGetValue(
                    category.CategoryId,
                    out var categoryQuestions);
                var hasFreeExistingRow = roundRows?.Any(rowIndex =>
                    categoryQuestions is null ||
                    !categoryQuestions.TryGetValue(rowIndex, out var isBlank) ||
                    isBlank) == true;

                return new QuestionCopyDestination(
                    category.QuizId,
                    category.QuizTitle,
                    category.RoundId,
                    category.RoundTitle,
                    category.CategoryId,
                    category.CategoryTitle,
                    hasFreeExistingRow);
            })
            .ToList();
    }

    public static async Task<QuestionCopyResult> CopyAsync(
        QuizDbContext db,
        string hostId,
        int sourceQuestionId,
        int targetCategoryId,
        int maximumQuestionCount,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentException.ThrowIfNullOrWhiteSpace(hostId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sourceQuestionId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetCategoryId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumQuestionCount);

        var source = await db.QuizQuestions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AsSplitQuery()
            .Include(question => question.Category)
                .ThenInclude(category => category.Round)
                    .ThenInclude(round => round.Quiz)
            .Include(question => question.QuestionBlocks)
            .Include(question => question.AnswerBlocks)
            .SingleOrDefaultAsync(question =>
                question.Id == sourceQuestionId &&
                question.Category.Round.Quiz.HostId == hostId &&
                !question.Category.Round.Quiz.IsArchived &&
                question.Category.Round.Quiz.MediaState == QuizMediaState.Active,
                cancellationToken);
        if (source is null)
        {
            return new QuestionCopyResult(QuestionCopyStatus.SourceNotFound);
        }

        var targetCategory = await db.QuizCategories
            .IgnoreQueryFilters()
            .AsSplitQuery()
            .Include(category => category.Round)
                .ThenInclude(round => round.Rows)
            .Include(category => category.Round)
                .ThenInclude(round => round.Categories)
                    .ThenInclude(category => category.Questions)
                        .ThenInclude(question => question.QuestionBlocks)
            .Include(category => category.Round)
                .ThenInclude(round => round.Categories)
                    .ThenInclude(category => category.Questions)
                        .ThenInclude(question => question.AnswerBlocks)
            .Include(category => category.Round)
                .ThenInclude(round => round.Quiz)
            .SingleOrDefaultAsync(category =>
                category.Id == targetCategoryId &&
                category.Round.Quiz.HostId == hostId &&
                !category.Round.Quiz.IsArchived &&
                category.Round.Quiz.MediaState == QuizMediaState.Active,
                cancellationToken);
        if (targetCategory is null)
        {
            return new QuestionCopyResult(QuestionCopyStatus.TargetNotFound);
        }

        var targetRound = targetCategory.Round;
        var targetQuiz = targetRound.Quiz;
        QuizQuestion? targetPlaceholder = null;
        var rowIndex = 0;

        foreach (var row in targetRound.Rows
            .Where(row => row.RowIndex > 0 && row.RowIndex <= maximumQuestionCount)
            .OrderBy(row => row.RowIndex))
        {
            var existingQuestion = targetCategory.Questions
                .SingleOrDefault(question => question.RowIndex == row.RowIndex);
            if (existingQuestion is not null && !IsBlankQuestion(existingQuestion))
            {
                continue;
            }

            rowIndex = row.RowIndex;
            targetPlaceholder = existingQuestion;
            break;
        }

        var addedRoundRow = false;
        if (rowIndex == 0)
        {
            if (targetRound.Rows.Count >= maximumQuestionCount)
            {
                return new QuestionCopyResult(QuestionCopyStatus.NoCapacity);
            }

            rowIndex = Enumerable.Range(1, maximumQuestionCount)
                .FirstOrDefault(candidate => targetRound.Rows.All(row =>
                    row.RowIndex != candidate));
            if (rowIndex == 0)
            {
                return new QuestionCopyResult(QuestionCopyStatus.NoCapacity);
            }

            var roundNumber = await db.QuizRounds
                .IgnoreQueryFilters()
                .CountAsync(round =>
                    round.QuizId == targetQuiz.Id &&
                    round.SortOrder <= targetRound.SortOrder,
                    cancellationToken);
            roundNumber = Math.Max(1, roundNumber);

            targetRound.Rows.Add(new QuizRoundRow
            {
                RowIndex = rowIndex,
                Points = checked(200 * roundNumber * rowIndex)
            });
            addedRoundRow = true;
        }

        var now = DateTime.UtcNow;
        QuizQuestion copy;
        if (targetPlaceholder is null)
        {
            copy = CloneQuestion(source, rowIndex, now);
            targetCategory.Questions.Add(copy);
        }
        else
        {
            copy = targetPlaceholder;
            CopyQuestionInto(db, source, copy, now);
        }

        if (addedRoundRow)
        {
            foreach (var category in targetRound.Categories)
            {
                if (category.Id == targetCategory.Id ||
                    category.Questions.Any(question =>
                        question.RowIndex == rowIndex))
                {
                    continue;
                }

                category.Questions.Add(CreateBlankQuestion(rowIndex, now));
            }
        }

        targetQuiz.UpdatedAtUtc = now;
        await db.SaveChangesAsync(cancellationToken);

        return new QuestionCopyResult(
            QuestionCopyStatus.Success,
            copy.Id,
            targetQuiz.Id,
            targetRound.Id,
            targetCategory.Id);
    }

    private static bool IsBlankQuestion(QuizQuestion question) =>
        question.TimeLimitSecondsOverride is null &&
        question.BuzzModeOverride == BuzzActivationMode.UseRoundDefault &&
        question.BuzzDelaySeconds == 0 &&
        !question.IsSpecial &&
        question.PresentationType == default &&
        !question.ExcludeFromRandomWagerSelection &&
        question.QuestionBlocks.All(IsBlankBlock) &&
        question.AnswerBlocks.All(IsBlankBlock);

    private static bool IsBlankBlock(ContentBlockBase block) =>
        block.BlockType == ContentBlockType.Text &&
        string.IsNullOrWhiteSpace(block.TextContent) &&
        string.IsNullOrWhiteSpace(block.TopCaption) &&
        string.IsNullOrWhiteSpace(block.BottomCaption) &&
        string.IsNullOrWhiteSpace(block.MediaPath) &&
        string.IsNullOrWhiteSpace(block.ExternalUrl) &&
        block.FileData is not { Length: > 0 } &&
        string.IsNullOrWhiteSpace(block.FileContentType) &&
        string.IsNullOrWhiteSpace(block.FileName) &&
        !block.AudioOnly &&
        !block.Autoplay;

    private static void CopyQuestionInto(
        QuizDbContext db,
        QuizQuestion source,
        QuizQuestion target,
        DateTime now)
    {
        target.TimeLimitSecondsOverride = source.TimeLimitSecondsOverride;
        target.BuzzModeOverride = source.BuzzModeOverride;
        target.BuzzDelaySeconds = source.BuzzDelaySeconds;
        target.IsSpecial = source.IsSpecial;
        target.PresentationType = source.PresentationType;
        target.ExcludeFromRandomWagerSelection = source.ExcludeFromRandomWagerSelection;
        target.UpdatedAtUtc = now;

        var existingQuestionBlocks = target.QuestionBlocks.ToArray();
        var existingAnswerBlocks = target.AnswerBlocks.ToArray();
        db.QuestionContentBlocks.RemoveRange(existingQuestionBlocks);
        db.AnswerContentBlocks.RemoveRange(existingAnswerBlocks);
        target.QuestionBlocks.Clear();
        target.AnswerBlocks.Clear();

        foreach (var block in source.QuestionBlocks.OrderBy(block => block.SortOrder))
        {
            target.QuestionBlocks.Add(CloneQuestionBlock(block));
        }

        foreach (var block in source.AnswerBlocks.OrderBy(block => block.SortOrder))
        {
            target.AnswerBlocks.Add(CloneAnswerBlock(block));
        }
    }

    private static QuizQuestion CloneQuestion(
        QuizQuestion source,
        int rowIndex,
        DateTime now)
    {
        var copy = new QuizQuestion
        {
            RowIndex = rowIndex,
            TimeLimitSecondsOverride = source.TimeLimitSecondsOverride,
            BuzzModeOverride = source.BuzzModeOverride,
            BuzzDelaySeconds = source.BuzzDelaySeconds,
            IsSpecial = source.IsSpecial,
            PresentationType = source.PresentationType,
            ExcludeFromRandomWagerSelection =
                source.ExcludeFromRandomWagerSelection,
            UpdatedAtUtc = now
        };

        foreach (var block in source.QuestionBlocks.OrderBy(block => block.SortOrder))
        {
            copy.QuestionBlocks.Add(CloneQuestionBlock(block));
        }

        foreach (var block in source.AnswerBlocks.OrderBy(block => block.SortOrder))
        {
            copy.AnswerBlocks.Add(CloneAnswerBlock(block));
        }

        return copy;
    }

    private static QuizQuestion CreateBlankQuestion(int rowIndex, DateTime now)
    {
        var question = new QuizQuestion
        {
            RowIndex = rowIndex,
            UpdatedAtUtc = now
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
        return question;
    }

    private static QuestionContentBlock CloneQuestionBlock(
        QuestionContentBlock source) => new()
    {
        BlockType = source.BlockType,
        TextContent = source.TextContent,
        TopCaption = source.TopCaption,
        BottomCaption = source.BottomCaption,
        MediaPath = source.MediaPath,
        ExternalUrl = source.ExternalUrl,
        SortOrder = source.SortOrder,
        AudioOnly = source.AudioOnly,
        Autoplay = source.Autoplay,
        FileData = source.FileData?.ToArray(),
        FileContentType = source.FileContentType,
        FileName = source.FileName
    };

    private static AnswerContentBlock CloneAnswerBlock(
        AnswerContentBlock source) => new()
    {
        BlockType = source.BlockType,
        TextContent = source.TextContent,
        TopCaption = source.TopCaption,
        BottomCaption = source.BottomCaption,
        MediaPath = source.MediaPath,
        ExternalUrl = source.ExternalUrl,
        SortOrder = source.SortOrder,
        AudioOnly = source.AudioOnly,
        Autoplay = source.Autoplay,
        FileData = source.FileData?.ToArray(),
        FileContentType = source.FileContentType,
        FileName = source.FileName
    };
}
