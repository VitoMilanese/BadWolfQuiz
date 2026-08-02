using BadWolfQuiz.Game.Definitions;
using BadWolfQuiz.Web.Models;

namespace BadWolfQuiz.Web.Services;

public sealed class QuizSnapshotFactory
{
    public QuizSnapshot Create(Quiz quiz)
    {
        ArgumentNullException.ThrowIfNull(quiz);

        var rounds = quiz.Rounds
            .OrderBy(round => round.SortOrder)
            .Select(CreateRound)
            .ToList();

        var finalQuestion = quiz.FinalQuestionBlocks.Count == 0 ||
            quiz.FinalAnswerBlocks.Count == 0
                ? null
                : new FinalQuestionSnapshot(
                    quiz.FinalQuestionBlocks.Select(CreateContentBlock),
                    quiz.FinalAnswerBlocks.Select(CreateContentBlock));

        return new QuizSnapshot(quiz.Id, quiz.Title, rounds, finalQuestion);
    }

    private static QuizRoundSnapshot CreateRound(QuizRound round)
    {
        var pointsByRow = round.Rows.ToDictionary(row => row.RowIndex, row => row.Points);

        var questions = round.Categories
            .OrderBy(category => category.SortOrder)
            .SelectMany(category => category.Questions
                .OrderBy(question => question.RowIndex)
                .Select(question => CreateQuestion(question, pointsByRow)))
            .ToList();

        return new QuizRoundSnapshot(
            round.Id,
            round.Title,
            round.SortOrder,
            questions,
            round.UseRandomWagerQuestions,
            round.RandomWagerQuestionCount);
    }

    private static QuizQuestionSnapshot CreateQuestion(
        QuizQuestion question,
        IReadOnlyDictionary<int, int> pointsByRow)
    {
        if (!pointsByRow.TryGetValue(question.RowIndex, out var points))
        {
            throw new InvalidOperationException(
                $"Question {question.Id} references missing row {question.RowIndex}.");
        }

        return new QuizQuestionSnapshot(
            question.Id,
            question.QuizCategoryId,
            question.RowIndex,
            points,
            question.IsSpecial,
            question.Category.Title,
            question.ExcludeFromRandomWagerSelection,
            question.QuestionBlocks.Select(CreateContentBlock),
            question.AnswerBlocks.Select(CreateContentBlock),
            question.PresentationType);
    }

    private static ContentBlockSnapshot CreateContentBlock(ContentBlockBase block)
    {
        return new ContentBlockSnapshot(
            block.Id,
            (ContentBlockKind)(int)block.BlockType,
            block.TextContent,
            block.TopCaption,
            block.BottomCaption,
            block.MediaPath,
            block.ExternalUrl,
            block.FileData?.ToArray(),
            block.FileContentType,
            block.FileName,
            block.SortOrder,
            block.AudioOnly);
    }
}
