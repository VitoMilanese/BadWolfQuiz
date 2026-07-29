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

        return new QuizSnapshot(quiz.Id, quiz.Title, rounds);
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
            questions);
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
            question.Category.Title);
    }
}
