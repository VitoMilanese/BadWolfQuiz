using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace BadWolfQuiz.Web.Services;

internal sealed record QuestionHintGameplayItem(
    int Id,
    int SortOrder,
    ContentBlockType BlockType,
    string? TextContent,
    string? TopCaption,
    string? BottomCaption,
    string? FileContentType,
    bool HasFileData);

internal static class QuestionHintGameplayData
{
    internal static async Task<IReadOnlyList<QuestionHintGameplayItem>> LoadAsync(
        QuizDbContext db,
        int quizId,
        int sourceQuestionId,
        CancellationToken cancellationToken = default)
    {
        var blocks = await db.QuestionHintContentBlocks
            .AsNoTracking()
            .Where(block =>
                block.QuizQuestionId == sourceQuestionId &&
                block.Question.Category.Round.QuizId == quizId)
            .OrderBy(block => block.SortOrder)
            .ThenBy(block => block.Id)
            .Select(block => new QuestionHintGameplayItem(
                block.Id,
                block.SortOrder,
                block.BlockType,
                block.TextContent,
                block.TopCaption,
                block.BottomCaption,
                block.FileContentType,
                block.FileData != null))
            .ToListAsync(cancellationToken);

        return blocks
            .Where(IsNonEmptyHint)
            .Take(4)
            .ToArray();
    }

    private static bool IsNonEmptyHint(QuestionHintGameplayItem block) =>
        block.BlockType switch
        {
            ContentBlockType.Text => !string.IsNullOrWhiteSpace(block.TextContent),
            ContentBlockType.Image =>
                block.HasFileData &&
                !string.IsNullOrWhiteSpace(block.FileContentType),
            _ => false
        };
}
