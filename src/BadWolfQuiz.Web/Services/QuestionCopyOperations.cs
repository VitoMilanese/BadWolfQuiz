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

        var sourceQuizId = await db.QuizQuestions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(question =>
                question.Id == sourceQuestionId &&
                question.Category.Round.Quiz.HostId == hostId &&
                !question.Category.Round.Quiz.IsArchived &&
                question.Category.Round.Quiz.MediaState == QuizMediaState.Active)
            .Select(question => (int?)question.Category.Round.QuizId)
            .SingleOrDefaultAsync(cancellationToken);
        if (!sourceQuizId.HasValue)
        {
            return null;
        }

        var quizzes = await db.Quizzes
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AsSplitQuery()
            .Where(quiz =>
                quiz.HostId == hostId &&
                !quiz.IsArchived &&
                quiz.MediaState == QuizMediaState.Active)
            .Include(quiz => quiz.Rounds)
                .ThenInclude(round => round.Rows)
            .Include(quiz => quiz.Rounds)
                .ThenInclude(round => round.Categories)
                    .ThenInclude(category => category.Questions)
            .OrderBy(quiz => quiz.Title)
            .ThenBy(quiz => quiz.Id)
            .ToListAsync(cancellationToken);

        return quizzes
            .SelectMany(quiz => quiz.Rounds
                .OrderBy(round => round.SortOrder)
                .ThenBy(round => round.Id)
                .SelectMany(round => round.Categories
                    .OrderBy(category => category.SortOrder)
                    .ThenBy(category => category.Id)
                    .Select(category => new QuestionCopyDestination(
                        quiz.Id,
                        quiz.Title,
                        round.Id,
                        round.Title,
                        category.Id,
                        category.Title,
                        HasCapacity(round, category, maximumQuestionCount)))))
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

        var rowIndex = targetRound.Rows
            .Where(row =>
                row.RowIndex > 0 &&
                row.RowIndex <= maximumQuestionCount &&
                targetCategory.Questions.All(question =>
                    question.RowIndex != row.RowIndex))
            .OrderBy(row => row.RowIndex)
            .Select(row => row.RowIndex)
            .FirstOrDefault();

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
        var copy = CloneQuestion(source, rowIndex, now);
        targetCategory.Questions.Add(copy);

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

    private static bool HasCapacity(
        QuizRound round,
        QuizCategory category,
        int maximumQuestionCount)
    {
        var hasFreeExistingRow = round.Rows.Any(row =>
            row.RowIndex > 0 &&
            row.RowIndex <= maximumQuestionCount &&
            category.Questions.All(question => question.RowIndex != row.RowIndex));

        return hasFreeExistingRow || round.Rows.Count < maximumQuestionCount;
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
