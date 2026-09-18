using BadWolfQuiz.Game.Definitions;
using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace BadWolfQuiz.Web.Services;

public sealed record QuizQuestionRepairResult(
    bool Changed,
    bool ResetToStandard);

public static class QuizQuestionRepairOperations
{
    public static async Task<QuizQuestionRepairResult> RepairAsync(
        QuizDbContext db,
        QuizQuestion question,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(question);

        var anomaly = QuizQuestionAnomalyDetector.Detect(
            question,
            question.Category.Round.Rows);
        if (anomaly is null)
        {
            return new(false, false);
        }

        var presentationType =
            AllPlayerQuestionCompatibility.ResolveStoredPresentationType(question);
        var resetToStandard = presentationType is
            QuestionPresentationType.FourClues or
            QuestionPresentationType.AllPlayerMultipleChoice or
            QuestionPresentationType.AllPlayerMultipleChoiceOnDemand or
            QuestionPresentationType.HostMultipleChoice;

        if (!resetToStandard)
        {
            return new(false, false);
        }

        await using var transaction =
            await db.Database.BeginTransactionAsync(cancellationToken);

        var answerMarkers = question.AnswerBlocks
            .Where(block => block.BlockType == ContentBlockType.AnswerOptions)
            .ToArray();
        if (answerMarkers.Length > 0)
        {
            db.AnswerContentBlocks.RemoveRange(answerMarkers);
            foreach (var block in answerMarkers)
            {
                question.AnswerBlocks.Remove(block);
            }
        }

        var emptyQuestionBlocks = question.QuestionBlocks
            .Where(IsEmpty)
            .ToArray();
        if (emptyQuestionBlocks.Length > 0)
        {
            db.QuestionContentBlocks.RemoveRange(emptyQuestionBlocks);
            foreach (var block in emptyQuestionBlocks)
            {
                question.QuestionBlocks.Remove(block);
            }
        }

        var emptyAnswerBlocks = question.AnswerBlocks
            .Where(IsEmpty)
            .ToArray();
        if (emptyAnswerBlocks.Length > 0)
        {
            db.AnswerContentBlocks.RemoveRange(emptyAnswerBlocks);
            foreach (var block in emptyAnswerBlocks)
            {
                question.AnswerBlocks.Remove(block);
            }
        }

        if (question.QuestionBlocks.Count == 0)
        {
            question.QuestionBlocks.Add(new QuestionContentBlock
            {
                QuizQuestionId = question.Id,
                BlockType = ContentBlockType.Text,
                SortOrder = 1
            });
        }

        if (question.AnswerBlocks.Count == 0)
        {
            question.AnswerBlocks.Add(new AnswerContentBlock
            {
                QuizQuestionId = question.Id,
                BlockType = ContentBlockType.Text,
                SortOrder = 1
            });
        }

        question.PresentationType = QuestionPresentationType.Standard;
        question.IsSpecial = false;
        question.ExcludeFromRandomWagerSelection = false;
        question.BuzzModeOverride = BuzzActivationMode.UseRoundDefault;
        question.BuzzDelaySeconds = 0;

        var now = DateTime.UtcNow;
        question.UpdatedAtUtc = now;
        question.Category.Round.Quiz.UpdatedAtUtc = now;

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new(true, true);
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
