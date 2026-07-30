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
            : new HashSet<int>();

        return round.Questions.Select(question => new RuntimeQuestion(
            round.SourceRoundId,
            question.SourceQuestionId,
            question.SourceCategoryId,
            question.CategoryTitle,
            question.RowIndex,
            question.Points,
            round.UseRandomWagerQuestions
                ? randomWagerQuestionIds.Contains(question.SourceQuestionId)
                : question.IsSpecial,
            question.QuestionBlocks,
            question.AnswerBlocks));
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
    private readonly List<QuestionAnswerAttempt> _answerAttempts = [];
    private readonly IReadOnlyList<QuestionAnswerAttempt> _readOnlyAnswerAttempts;
    internal RuntimeQuestion(
        int sourceRoundId,
        int sourceQuestionId,
        int sourceCategoryId,
        string categoryTitle,
        int rowIndex,
        int points,
        bool isSpecial,
        IReadOnlyList<ContentBlockSnapshot> questionBlocks,
        IReadOnlyList<ContentBlockSnapshot> answerBlocks)
    {
        SourceRoundId = sourceRoundId;
        SourceQuestionId = sourceQuestionId;
        SourceCategoryId = sourceCategoryId;
        CategoryTitle = categoryTitle;
        RowIndex = rowIndex;
        Points = points;
        IsSpecial = isSpecial;
        QuestionBlocks = questionBlocks;
        AnswerBlocks = answerBlocks;
        _readOnlyAnswerAttempts = _answerAttempts.AsReadOnly();
    }

    public int SourceRoundId { get; }

    public int SourceQuestionId { get; }

    public int SourceCategoryId { get; }

    public string CategoryTitle { get; }

    public int RowIndex { get; }

    public int Points { get; }

    public bool IsSpecial { get; }

    public IReadOnlyList<ContentBlockSnapshot> QuestionBlocks { get; }

    public IReadOnlyList<ContentBlockSnapshot> AnswerBlocks { get; }

    public RuntimeQuestionStatus Status { get; private set; } = RuntimeQuestionStatus.Available;

    public GamePlayerId? SelectedByPlayerId { get; private set; }

    public Wager? Wager { get; private set; }

    public IReadOnlyList<QuestionAnswerAttempt> AnswerAttempts => _readOnlyAnswerAttempts;

    internal void Select(GamePlayerId selectedByPlayerId)
    {
        if (Status != RuntimeQuestionStatus.Available)
        {
            throw new GameRuleViolationException("Only an available question can be selected.");
        }

        SelectedByPlayerId = selectedByPlayerId;
        Status = IsSpecial
            ? RuntimeQuestionStatus.AwaitingWager
            : RuntimeQuestionStatus.Selected;
    }

    internal void SubmitWager(
        GamePlayerId playerId,
        int amount,
        DateTimeOffset submittedAtUtc)
    {
        if (!IsSpecial || Status != RuntimeQuestionStatus.AwaitingWager)
        {
            throw new GameRuleViolationException(
                "A wager can only be submitted for a wager question awaiting a wager.");
        }

        Wager = new Wager(playerId, amount, submittedAtUtc);
        Status = RuntimeQuestionStatus.Active;
    }
    internal QuestionAnswerAttempt JudgeAnswer(
        GamePlayerId playerId,
        bool isCorrect,
        DateTimeOffset judgedAtUtc)
    {
        if (Status is not RuntimeQuestionStatus.Selected and
            not RuntimeQuestionStatus.Active)
        {
            throw new GameRuleViolationException(
                "Only an active question can have an answer judged.");
        }

        if (_answerAttempts.Any(attempt => attempt.PlayerId == playerId))
        {
            throw new GameRuleViolationException(
                "This player has already answered the current question.");
        }

        if (IsSpecial && Wager?.PlayerId != playerId)
        {
            throw new GameRuleViolationException(
                "Only the wager player can answer a wager question.");
        }

        var value = IsSpecial
            ? Wager?.Amount ?? throw new GameRuleViolationException(
                "A wager question cannot be judged before its wager is accepted.")
            : Points;
        var scoreDelta = isCorrect ? value : -value;
        var attempt = new QuestionAnswerAttempt(
            playerId,
            isCorrect,
            scoreDelta,
            judgedAtUtc);

        _answerAttempts.Add(attempt);

        if (isCorrect || IsSpecial)
        {
            Status = RuntimeQuestionStatus.ShowingAnswer;
        }

        return attempt;
    }

    internal void ResolveWithoutCorrectAnswer()
    {
        if (IsSpecial ||
            Status is not RuntimeQuestionStatus.Selected and
                not RuntimeQuestionStatus.Active)
        {
            throw new GameRuleViolationException(
                "Only an active regular question can be closed without a correct answer.");
        }

        Status = RuntimeQuestionStatus.ShowingAnswer;
    }

    internal void CloseAnswer()
    {
        if (Status != RuntimeQuestionStatus.ShowingAnswer)
        {
            throw new GameRuleViolationException(
                "Only a displayed answer can be closed.");
        }

        Status = RuntimeQuestionStatus.Resolved;
    }
}

public enum RuntimeQuestionStatus
{
    Available = 1,
    Selected = 2,
    AwaitingWager = 3,
    Active = 4,
    AwaitingJudgment = 5,
    ShowingAnswer = 6,
    Resolved = 7
}
