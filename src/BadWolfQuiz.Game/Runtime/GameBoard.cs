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

    internal void RestoreState(IReadOnlyList<RuntimeQuestionState> states)
    {
        foreach (var state in states)
        {
            _questions.Single(question =>
                question.SourceQuestionId == state.SourceQuestionId)
                .RestoreState(state);
        }
    }

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
            question.PresentationType,
            question.QuestionBlocks,
            question.AnswerBlocks));
    }

    private static HashSet<int> SelectRandomWagerQuestions(
        QuizRoundSnapshot round)
    {
        var candidates = round.Questions
            .Where(question => !question.ExcludeFromRandomWagerSelection &&
                question.PresentationType == QuestionPresentationType.Standard)
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
        QuestionPresentationType presentationType,
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
        PresentationType = presentationType;
        RevealedClueCount = presentationType == QuestionPresentationType.FourClues ? 2 : 0;
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

    public bool IsSpecial { get; private set; }

    public QuestionPresentationType PresentationType { get; }

    public int RevealedClueCount { get; private set; }

    public bool CanRevealClue => PresentationType == QuestionPresentationType.FourClues &&
        RevealedClueCount < 4 &&
        Status is RuntimeQuestionStatus.Selected or RuntimeQuestionStatus.Active;

    public int CorrectAnswerValue => IsSpecial
        ? Wager?.Amount ?? Points
        : PresentationType == QuestionPresentationType.FourClues
            ? RevealedClueCount switch { 3 => Points / 2, 4 => Points / 4, _ => Points }
            : Points;

    public IReadOnlyList<ContentBlockSnapshot> QuestionBlocks { get; }

    public IReadOnlyList<ContentBlockSnapshot> AnswerBlocks { get; }

    public RuntimeQuestionStatus Status { get; private set; } = RuntimeQuestionStatus.Available;

    public GamePlayerId? SelectedByPlayerId { get; private set; }

    public Wager? Wager { get; private set; }

    public QuestionBuzzerStatus BuzzerStatus { get; private set; } =
        QuestionBuzzerStatus.Inactive;

    public GamePlayerId? AnsweringPlayerId { get; private set; }

    public IReadOnlyList<QuestionAnswerAttempt> AnswerAttempts => _readOnlyAnswerAttempts;

    internal RuntimeQuestionState CaptureState() => new(
        SourceQuestionId,
        IsSpecial,
        Status,
        SelectedByPlayerId,
        Wager,
        BuzzerStatus,
        AnsweringPlayerId,
        _answerAttempts.ToArray(),
        RevealedClueCount);

    internal void RestoreState(RuntimeQuestionState state)
    {
        IsSpecial = state.IsSpecial;
        Status = state.Status;
        SelectedByPlayerId = state.SelectedByPlayerId;
        Wager = state.Wager;
        BuzzerStatus = state.BuzzerStatus;
        AnsweringPlayerId = state.AnsweringPlayerId;
        RevealedClueCount = PresentationType == QuestionPresentationType.FourClues
            ? Math.Clamp(state.RevealedClueCount == 0 ? 2 : state.RevealedClueCount, 2, 4)
            : 0;
        _answerAttempts.Clear();
        _answerAttempts.AddRange(state.AnswerAttempts);
    }

    internal void SuspendOpenBuzzerForRecovery()
    {
        if (BuzzerStatus != QuestionBuzzerStatus.Open)
        {
            return;
        }

        BuzzerStatus = QuestionBuzzerStatus.Inactive;
        AnsweringPlayerId = null;
    }

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
        AnsweringPlayerId = playerId;
        BuzzerStatus = QuestionBuzzerStatus.Closed;
        Status = RuntimeQuestionStatus.Active;
    }

    internal void ActivateBuzzer()
    {
        if (IsSpecial || Status is not RuntimeQuestionStatus.Selected and
            not RuntimeQuestionStatus.Active)
        {
            throw new GameRuleViolationException(
                "The buzzer can only be activated for an active regular question.");
        }

        if (BuzzerStatus == QuestionBuzzerStatus.Claimed)
        {
            throw new GameRuleViolationException(
                "The buzzer cannot be activated while a player is answering.");
        }

        BuzzerStatus = QuestionBuzzerStatus.Open;
        AnsweringPlayerId = null;
        Status = RuntimeQuestionStatus.Active;
    }

    internal void RevealNextClue()
    {
        if (!CanRevealClue)
        {
            throw new GameRuleViolationException("No additional clue can be revealed.");
        }

        RevealedClueCount++;
    }

    internal void ClaimBuzzer(GamePlayerId playerId)
    {
        if (IsSpecial || BuzzerStatus != QuestionBuzzerStatus.Open)
        {
            throw new GameRuleViolationException(
                "The buzzer is not open for the current question.");
        }

        if (_answerAttempts.Any(attempt => attempt.PlayerId == playerId))
        {
            throw new GameRuleViolationException(
                "This player has already answered the current question.");
        }

        AnsweringPlayerId = playerId;
        BuzzerStatus = QuestionBuzzerStatus.Claimed;
    }

    internal QuestionAnswerAttempt JudgeAnswer(
        GamePlayerId playerId,
        bool isCorrect,
        DateTimeOffset judgedAtUtc,
        int? correctAnswerValue = null)
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
            : correctAnswerValue ?? CorrectAnswerValue;

        var scoreDelta = isCorrect
            ? value
            : IsSpecial
                ? -value
                : -Points;

        var attempt = new QuestionAnswerAttempt(
            playerId,
            isCorrect,
            scoreDelta,
            judgedAtUtc);

        _answerAttempts.Add(attempt);

        if (isCorrect || IsSpecial)
        {
            BuzzerStatus = QuestionBuzzerStatus.Closed;
            AnsweringPlayerId = null;
            Status = RuntimeQuestionStatus.ShowingAnswer;
        }
        else if (!IsSpecial)
        {
            BuzzerStatus = QuestionBuzzerStatus.Open;
            AnsweringPlayerId = null;
        }

        return attempt;
    }

    internal QuestionAnswerAttempt AddHistoricalAttempt(
        GamePlayerId playerId,
        bool isCorrect,
        int value,
        DateTimeOffset judgedAtUtc)
    {
        EnsureHistoricalPlayerIsUnique(playerId);

        if (value < 0)
        {
            throw new GameRuleViolationException(
                "An answer history value cannot be negative.");
        }

        var attempt = new QuestionAnswerAttempt(
            playerId,
            isCorrect,
            isCorrect ? value : -value,
            judgedAtUtc);

        _answerAttempts.Add(attempt);
        return attempt;
    }

    internal void ResolveFromHistory()
    {
        if (Status != RuntimeQuestionStatus.Available)
        {
            return;
        }

        BuzzerStatus = QuestionBuzzerStatus.Closed;
        AnsweringPlayerId = null;
        Status = RuntimeQuestionStatus.Resolved;
    }

    internal (QuestionAnswerAttempt Previous, QuestionAnswerAttempt Updated)
        UpdateHistoricalAttempt(
            Guid attemptId,
            GamePlayerId playerId,
            bool isCorrect,
            int value)
    {
        if (value < 0)
        {
            throw new GameRuleViolationException(
                "An answer history value cannot be negative.");
        }

        var index = _answerAttempts.FindIndex(attempt => attempt.Id == attemptId);

        if (index < 0)
        {
            throw new GameRuleViolationException(
                "The selected answer history entry does not exist.");
        }

        if (_answerAttempts.Any(attempt =>
                attempt.Id != attemptId &&
                attempt.PlayerId == playerId))
        {
            throw new GameRuleViolationException(
                "This player already has an answer entry for the selected question.");
        }

        var previous = _answerAttempts[index];
        var updated = previous with
        {
            PlayerId = playerId,
            IsCorrect = isCorrect,
            ScoreDelta = isCorrect ? value : -value
        };

        _answerAttempts[index] = updated;
        return (previous, updated);
    }

    internal QuestionAnswerAttempt RemoveHistoricalAttempt(Guid attemptId)
    {
        var index = _answerAttempts.FindIndex(attempt => attempt.Id == attemptId);

        if (index < 0)
        {
            throw new GameRuleViolationException(
                "The selected answer history entry does not exist.");
        }

        var removed = _answerAttempts[index];
        _answerAttempts.RemoveAt(index);
        return removed;
    }

    private void EnsureHistoricalPlayerIsUnique(GamePlayerId playerId)
    {
        if (_answerAttempts.Any(attempt => attempt.PlayerId == playerId))
        {
            throw new GameRuleViolationException(
                "This player already has an answer entry for the selected question.");
        }
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

        BuzzerStatus = QuestionBuzzerStatus.Closed;
        AnsweringPlayerId = null;
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

    internal void ForceResolve()
    {
        BuzzerStatus = QuestionBuzzerStatus.Closed;
        AnsweringPlayerId = null;
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


public enum QuestionBuzzerStatus
{
    Inactive = 1,
    Open = 2,
    Claimed = 3,
    Closed = 4
}
