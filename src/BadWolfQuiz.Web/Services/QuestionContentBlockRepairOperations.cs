using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace BadWolfQuiz.Web.Services;

public sealed record QuestionContentBlockRepairResult(
    int RemovedQuestionBlocks,
    int RemovedAnswerBlocks,
    int PreservedInvalidQuestionBlocks,
    int PreservedInvalidAnswerBlocks,
    bool AddedQuestionPlaceholder,
    bool AddedAnswerPlaceholder)
{
    public bool Changed =>
        RemovedQuestionBlocks > 0 ||
        RemovedAnswerBlocks > 0 ||
        AddedQuestionPlaceholder ||
        AddedAnswerPlaceholder;
}

public static class QuestionContentBlockRepairOperations
{
    public static async Task<QuestionContentBlockRepairResult> RepairEmptyInvalidBlocksAsync(
        QuizDbContext db,
        QuizQuestion question,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(question);

        var invalidQuestionBlocks = question.QuestionBlocks
            .Where(block => !Enum.IsDefined(block.BlockType))
            .ToArray();
        var invalidAnswerBlocks = question.AnswerBlocks
            .Where(block => !Enum.IsDefined(block.BlockType))
            .ToArray();

        var removableQuestionBlocks = invalidQuestionBlocks
            .Where(IsEmpty)
            .ToArray();
        var removableAnswerBlocks = invalidAnswerBlocks
            .Where(IsEmpty)
            .ToArray();

        var preservedInvalidQuestionBlocks =
            invalidQuestionBlocks.Length - removableQuestionBlocks.Length;
        var preservedInvalidAnswerBlocks =
            invalidAnswerBlocks.Length - removableAnswerBlocks.Length;

        if (removableQuestionBlocks.Length == 0 &&
            removableAnswerBlocks.Length == 0)
        {
            return new QuestionContentBlockRepairResult(
                0,
                0,
                preservedInvalidQuestionBlocks,
                preservedInvalidAnswerBlocks,
                false,
                false);
        }

        foreach (var block in removableQuestionBlocks)
        {
            question.QuestionBlocks.Remove(block);
        }

        foreach (var block in removableAnswerBlocks)
        {
            question.AnswerBlocks.Remove(block);
        }

        await using var transaction =
            await db.Database.BeginTransactionAsync(cancellationToken);

        var questionBlockIds = removableQuestionBlocks
            .Where(block => block.Id > 0)
            .Select(block => block.Id)
            .ToArray();
        if (questionBlockIds.Length > 0)
        {
            await db.QuestionContentBlocks
                .Where(block => questionBlockIds.Contains(block.Id))
                .ExecuteDeleteAsync(cancellationToken);
        }

        var answerBlockIds = removableAnswerBlocks
            .Where(block => block.Id > 0)
            .Select(block => block.Id)
            .ToArray();
        if (answerBlockIds.Length > 0)
        {
            await db.AnswerContentBlocks
                .Where(block => answerBlockIds.Contains(block.Id))
                .ExecuteDeleteAsync(cancellationToken);
        }

        var addedQuestionPlaceholder = false;
        if (question.QuestionBlocks.Count == 0)
        {
            var placeholder = new QuestionContentBlock
            {
                QuizQuestionId = question.Id,
                BlockType = ContentBlockType.Text,
                SortOrder = 1
            };
            question.QuestionBlocks.Add(placeholder);
            db.QuestionContentBlocks.Add(placeholder);
            addedQuestionPlaceholder = true;
        }

        var addedAnswerPlaceholder = false;
        if (question.AnswerBlocks.Count == 0)
        {
            var placeholder = new AnswerContentBlock
            {
                QuizQuestionId = question.Id,
                BlockType = ContentBlockType.Text,
                SortOrder = 1
            };
            question.AnswerBlocks.Add(placeholder);
            db.AnswerContentBlocks.Add(placeholder);
            addedAnswerPlaceholder = true;
        }

        if (addedQuestionPlaceholder || addedAnswerPlaceholder)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        return new QuestionContentBlockRepairResult(
            removableQuestionBlocks.Length,
            removableAnswerBlocks.Length,
            preservedInvalidQuestionBlocks,
            preservedInvalidAnswerBlocks,
            addedQuestionPlaceholder,
            addedAnswerPlaceholder);
    }

    private static bool IsEmpty(ContentBlockBase block) =>
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
}
