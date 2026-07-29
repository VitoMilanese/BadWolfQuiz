using System.Collections.ObjectModel;
using BadWolfQuiz.Game.Definitions;

namespace BadWolfQuiz.Game.Runtime;

public sealed class GameBoard
{
    private readonly ReadOnlyCollection<RuntimeQuestion> _questions;

    internal GameBoard(QuizSnapshot quiz)
    {
        _questions = quiz.Rounds
            .OrderBy(round => round.SortOrder)
            .SelectMany(CreateRoundQuestions)
            .ToList()
            .AsReadOnly();
    }

    public IReadOnlyList<RuntimeQuestion> Questions => _questions;

    private static IEnumerable<RuntimeQuestion> CreateRoundQuestions(
        QuizRoundSnapshot round)
    {
        var randomWagerQuestionIds = round.UseRandomWagerQuestions
            ? SelectRandomWagerQuestions(round)
            : [];

        return round.Questions.Select(question => new RuntimeQuestion(
            round.SourceRoundId,
            question.SourceQuestionId,
            question.SourceCategoryId,
            question.CategoryTitle,
            question.RowIndex,
            question.Points,
            round.UseRandomWagerQuestions
                ? randomWagerQuestionIds.Contains(question.SourceQuestionId)
                : question.IsSpecial));
    }

    private static HashSet<int> SelectRandomWagerQuestions(
        QuizRoundSnapshot round)
    {
        var candidates = round.Questions
            .Where(question => !question.ExcludeFromRandomWagerSelection)
            .ToList();

        for (var index = candidates.Count - 1; index > 0; index--)
        {
            var swapIndex = Random.Shared.Next(index + 1);
            (candidates[index], candidates[swapIndex]) =
                (candidates[swapIndex], candidates[index]);
        }

        return candidates
            .Take(round.RandomWagerQuestionCount)
            .Select(question => question.SourceQuestionId)
            .ToHashSet();
    }
}

public sealed class RuntimeQuestion
{
    internal RuntimeQuestion(
        int sourceRoundId,
        int sourceQuestionId,
        int sourceCategoryId,
        string categoryTitle,
        int rowIndex,
        int points,
        bool isSpecial)
    {
        SourceRoundId = sourceRoundId;
        SourceQuestionId = sourceQuestionId;
        SourceCategoryId = sourceCategoryId;
        CategoryTitle = categoryTitle;
        RowIndex = rowIndex;
        Points = points;
        IsSpecial = isSpecial;
    }

    public int SourceRoundId { get; }

    public int SourceQuestionId { get; }

    public int SourceCategoryId { get; }

    public string CategoryTitle { get; }

    public int RowIndex { get; }

    public int Points { get; }

    public bool IsSpecial { get; }

    public RuntimeQuestionStatus Status { get; private set; } = RuntimeQuestionStatus.Available;

    internal void Select()
    {
        if (Status != RuntimeQuestionStatus.Available)
        {
            throw new GameRuleViolationException("Only an available question can be selected.");
        }

        Status = IsSpecial
            ? RuntimeQuestionStatus.AwaitingWager
            : RuntimeQuestionStatus.Selected;
    }
}

public enum RuntimeQuestionStatus
{
    Available = 1,
    Selected = 2,
    AwaitingWager = 3,
    Active = 4,
    AwaitingJudgment = 5,
    Resolved = 6
}
