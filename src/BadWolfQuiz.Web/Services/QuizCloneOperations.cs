using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace BadWolfQuiz.Web.Services;

public static class QuizCloneOperations
{
    public static async Task<Quiz?> CloneAsync(
        QuizDbContext db,
        int sourceQuizId,
        string title,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);

        var source = await db.Quizzes
            .AsNoTracking()
            .AsSplitQuery()
            .Include(quiz => quiz.FinalDescriptionBlocks)
            .Include(quiz => quiz.FinalQuestionBlocks)
            .Include(quiz => quiz.FinalAnswerBlocks)
            .Include(quiz => quiz.Rounds)
                .ThenInclude(round => round.Rows)
            .Include(quiz => quiz.Rounds)
                .ThenInclude(round => round.DescriptionBlocks)
            .Include(quiz => quiz.Rounds)
                .ThenInclude(round => round.Categories)
                    .ThenInclude(category => category.DescriptionBlocks)
            .Include(quiz => quiz.Rounds)
                .ThenInclude(round => round.Categories)
                    .ThenInclude(category => category.Questions)
                        .ThenInclude(question => question.QuestionBlocks)
            .Include(quiz => quiz.Rounds)
                .ThenInclude(round => round.Categories)
                    .ThenInclude(category => category.Questions)
                        .ThenInclude(question => question.AnswerBlocks)
            .SingleOrDefaultAsync(
                quiz => quiz.Id == sourceQuizId &&
                    !quiz.IsArchived &&
                    quiz.MediaState == QuizMediaState.Active,
                cancellationToken);

        if (source is null)
        {
            return null;
        }

        var now = DateTime.UtcNow;
        var clone = new Quiz
        {
            HostId = source.HostId,
            Title = title,
            Description = source.Description,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            IsArchived = false,
            MediaState = QuizMediaState.Active,
            PreventAutomaticArchiving = source.PreventAutomaticArchiving,
            LastPlayedAtUtc = null,
            IsPublic = false,
            PublishedAtUtc = null
        };

        foreach (var sourceRound in source.Rounds.OrderBy(round => round.SortOrder))
        {
            var round = new QuizRound
            {
                Title = sourceRound.Title,
                SortOrder = sourceRound.SortOrder,
                DefaultTimeLimitSeconds = sourceRound.DefaultTimeLimitSeconds,
                DefaultBuzzMode = sourceRound.DefaultBuzzMode,
                UseRandomWagerQuestions = sourceRound.UseRandomWagerQuestions,
                RandomWagerQuestionCount = sourceRound.RandomWagerQuestionCount,
                UseRandomAnonymousSharedWagerQuestions =
                    sourceRound.UseRandomAnonymousSharedWagerQuestions,
                RandomAnonymousSharedWagerQuestionCount =
                    sourceRound.RandomAnonymousSharedWagerQuestionCount
            };

            foreach (var sourceRow in sourceRound.Rows.OrderBy(row => row.RowIndex))
            {
                round.Rows.Add(new QuizRoundRow
                {
                    RowIndex = sourceRow.RowIndex,
                    Points = sourceRow.Points
                });
            }

            foreach (var sourceBlock in sourceRound.DescriptionBlocks.OrderBy(block => block.SortOrder))
            {
                round.DescriptionBlocks.Add(CloneBlock<RoundDescriptionContentBlock>(sourceBlock));
            }

            foreach (var sourceCategory in sourceRound.Categories.OrderBy(category => category.SortOrder))
            {
                var category = new QuizCategory
                {
                    Title = sourceCategory.Title,
                    SortOrder = sourceCategory.SortOrder
                };

                foreach (var sourceBlock in sourceCategory.DescriptionBlocks.OrderBy(block => block.SortOrder))
                {
                    category.DescriptionBlocks.Add(CloneBlock<CategoryDescriptionContentBlock>(sourceBlock));
                }

                foreach (var sourceQuestion in sourceCategory.Questions.OrderBy(question => question.RowIndex))
                {
                    var question = new QuizQuestion
                    {
                        RowIndex = sourceQuestion.RowIndex,
                        TimeLimitSecondsOverride = sourceQuestion.TimeLimitSecondsOverride,
                        BuzzModeOverride = sourceQuestion.BuzzModeOverride,
                        BuzzDelaySeconds = sourceQuestion.BuzzDelaySeconds,
                        IsSpecial = sourceQuestion.IsSpecial,
                        PresentationType = sourceQuestion.PresentationType,
                        ExcludeFromRandomWagerSelection = sourceQuestion.ExcludeFromRandomWagerSelection,
                        UpdatedAtUtc = now
                    };

                    foreach (var sourceBlock in sourceQuestion.QuestionBlocks.OrderBy(block => block.SortOrder))
                    {
                        question.QuestionBlocks.Add(CloneBlock<QuestionContentBlock>(sourceBlock));
                    }

                    foreach (var sourceBlock in sourceQuestion.AnswerBlocks.OrderBy(block => block.SortOrder))
                    {
                        question.AnswerBlocks.Add(CloneBlock<AnswerContentBlock>(sourceBlock));
                    }

                    category.Questions.Add(question);
                }

                round.Categories.Add(category);
            }

            clone.Rounds.Add(round);
        }

        foreach (var sourceBlock in source.FinalDescriptionBlocks.OrderBy(block => block.SortOrder))
        {
            clone.FinalDescriptionBlocks.Add(CloneBlock<FinalDescriptionContentBlock>(sourceBlock));
        }

        foreach (var sourceBlock in source.FinalQuestionBlocks.OrderBy(block => block.SortOrder))
        {
            clone.FinalQuestionBlocks.Add(CloneBlock<FinalQuestionContentBlock>(sourceBlock));
        }

        foreach (var sourceBlock in source.FinalAnswerBlocks.OrderBy(block => block.SortOrder))
        {
            clone.FinalAnswerBlocks.Add(CloneBlock<FinalAnswerContentBlock>(sourceBlock));
        }

        db.Quizzes.Add(clone);
        await db.SaveChangesAsync(cancellationToken);
        return clone;
    }

    private static TBlock CloneBlock<TBlock>(ContentBlockBase source)
        where TBlock : ContentBlockBase, new() => new()
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
