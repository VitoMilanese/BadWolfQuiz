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
            .Where(question => question.IsEligibleForRandomWagerSelection)
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
    private readonly List<Wager> _allPlayerWagers = [];
    private readonly List<int> _remainingHostMultipleChoiceOptionIds = [];
    private readonly IReadOnlyList<QuestionAnswerAttempt> _readOnlyAnswerAttempts;
    private readonly IReadOnlyList<Wager> _readOnlyAllPlayerWagers;
    private readonly IReadOnlyList<int> _readOnlyRemainingHostMultipleChoiceOptionIds;

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
        _readOnlyAllPlayerWagers = _allPlayerWagers.AsReadOnly();
        _readOnlyRemainingHostMultipleChoiceOptionIds =
            _remainingHostMultipleChoiceOptionIds.AsReadOnly();

        if (IsHostMultipleChoice)
        {
            _remainingHostMultipleChoiceOptionIds.AddRange(
                answerBlocks
                    .OrderBy(block => block.SortOrder)
                    .Select(block => block.SourceContentBlockId));
        }
    }

    public int SourceRoundId { get; }

    public int SourceQuestionId { get; }

    public int SourceCategoryId { get; }

    public string CategoryTitle { get; }

    public int RowIndex { get; }

    public int Points { get; }

    public bool IsSpecial { get; private set; }

    public QuestionPresentationType PresentationType { get; }

    public bool IsAllPlayerQuestion => PresentationType is
        QuestionPresentationType.AllPlayerText or
        QuestionPresentationType.AllPlayerMultipleChoice;

    public bool IsHostMultipleChoice =>
        PresentationType == QuestionPresentationType.HostMultipleChoice;

    public int RevealedClueCount { get; private set; }

    public bool CanRevealClue => PresentationType == QuestionPresentationType.FourClues &&
        RevealedClueCount < 4 &&
        Status is RuntimeQuestionStatus.Selected or RuntimeQuestionStatus.Active;

    public int HostMultipleChoiceOriginalOptionCount =>
        IsHostMultipleChoice ? AnswerBlocks.Count : 0;

    public int HostMultipleChoiceCorrectOptionId => IsHostMultipleChoice
        ? AnswerBlocks
            .OrderBy(block => block.SortOrder)
            .First()
            .SourceContentBlockId
        : throw new GameRuleViolationException(
            "Only a host multiple-choice question has a correct option identifier.");

    public IReadOnlyList<int> RemainingHostMultipleChoiceOptionIds =>
        _readOnlyRemainingHostMultipleChoiceOptionIds;

    public IReadOnlyList<ContentBlockSnapshot> RemainingHostMultipleChoiceOptions =>
        IsHostMultipleChoice
            ? AnswerBlocks
                .Where(block => _remainingHostMultipleChoiceOptionIds.Contains(
                    block.SourceContentBlockId))
                .OrderBy(block => block.SortOrder)
                .ToArray()
            : [];

    public int HostMultipleChoiceRewardPercentage => IsHostMultipleChoice
        ? CalculateHostMultipleChoiceRewardPercentage(
            HostMultipleChoiceOriginalOptionCount,
            _remainingHostMultipleChoiceOptionIds.Count)
        : 100;

    public int HostMultipleChoiceRewardValue => IsHostMultipleChoice
        ? Math.Max(
            1,
            (int)Math.Round(
                Points * HostMultipleChoiceRewardPercentage / 100.0,
                MidpointRounding.AwayFromZero))
        : Points;

    public int CorrectAnswerValue => IsSpecial && !IsAllPlayerQuestion
        ? Wager?.Amount ?? Points
        : PresentationType == QuestionPresentationType.FourClues
            ? RevealedClueCount switch { 3 => Points / 2, 4 => Points / 4, _ => Points }
            : IsHostMultipleChoice
                ? HostMultipleChoiceRewardValue
                : Points;

    public IReadOnlyList<ContentBlockSnapshot> QuestionBlocks { get; }

    public IReadOnlyList<ContentBlockSnapshot> AnswerBlocks { get; }

    public RuntimeQuestionStatus Status { get; private set; } = RuntimeQuestionStatus.Available;

    public GamePlayerId? SelectedByPlayerId { get; private set; }

    public Wager? Wager { get; private set; }

    public IReadOnlyList<Wager> AllPlayerWagers => _readOnlyAllPlayerWagers;

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
        RevealedClueCount,
        _allPlayerWagers.ToArray(),
        IsHostMultipleChoice
            ? _remainingHostMultipleChoiceOptionIds.ToArray()
            : null);

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
        _allPlayerWagers.Clear();
        _allPlayerWagers.AddRange(state.AllPlayerWagers ?? []);

        if (IsHostMultipleChoice &&
            state.RemainingHostMultipleChoiceOptionIds is { Count: > 0 })
        {
            var validOptionIds = AnswerBlocks
                .Select(block => block.SourceContentBlockId)
                .ToHashSet();
            var restoredOptionIds = state.RemainingHostMultipleChoiceOptionIds
                .Where(validOptionIds.Contains)
                .Distinct()
                .ToArray();

            if (restoredOptionIds.Length >= 2 &&
                restoredOptionIds.Contains(HostMultipleChoiceCorrectOptionId))
            {
                _remainingHostMultipleChoiceOptionIds.Clear();
                _remainingHostMultipleChoiceOptionIds.AddRange(restoredOptionIds);
            }
        }
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

        if (IsSpecial)
        {
            if (IsAllPlayerQuestion)
            {
                BuzzerStatus = QuestionBuzzerStatus.Closed;
                AnsweringPlayerId = null;
            }

            Status = RuntimeQuestionStatus.AwaitingWager;
            return;
        }

        if (IsAllPlayerQuestion)
        {
            BuzzerStatus = QuestionBuzzerStatus.Closed;
            AnsweringPlayerId = null;
            Status = RuntimeQuestionStatus.Active;
            return;
        }

        Status = RuntimeQuestionStatus.Selected;
    }

    internal void SubmitWager(
        GamePlayerId playerId,
        int amount,
        DateTimeOffset submittedAtUtc)
    {
        if (!IsSpecial ||
            IsAllPlayerQuestion ||
            Status != RuntimeQuestionStatus.AwaitingWager)
        {
            throw new GameRuleViolationException(
                "A wager can only be submitted for a single-player wager question awaiting a wager.");
        }

        Wager = new Wager(playerId, amount, submittedAtUtc);
        AnsweringPlayerId = playerId;
        BuzzerStatus = QuestionBuzzerStatus.Closed;
        Status = RuntimeQuestionStatus.Active;
    }

    internal void SubmitAllPlayerWager(
        GamePlayerId playerId,
        int amount,
        DateTimeOffset submittedAtUtc)
    {
        if (!IsSpecial ||
            !IsAllPlayerQuestion ||
            Status != RuntimeQuestionStatus.AwaitingWager)
        {
            throw new GameRuleViolationException(
                "Player wagers can only be submitted while an all-player wager question is awaiting wagers.");
        }

        if (_allPlayerWagers.Any(wager => wager.PlayerId == playerId))
        {
            throw new GameRuleViolationException(
                "This player has already submitted a wager for the current question.");
        }

        _allPlayerWagers.Add(new Wager(playerId, amount, submittedAtUtc));
    }

    internal void BeginAllPlayerAnswering()
    {
        if (!IsSpecial ||
            !IsAllPlayerQuestion ||
            Status != RuntimeQuestionStatus.AwaitingWager)
        {
            throw new GameRuleViolationException(
                "Only an all-player wager question with locked wagers can begin answering.");
        }

        BuzzerStatus = QuestionBuzzerStatus.Closed;
        AnsweringPlayerId = null;
        Status = RuntimeQuestionStatus.Active;
    }

    internal void ActivateBuzzer()
    {
        if (IsAllPlayerQuestion)
        {
            if (Status is not RuntimeQuestionStatus.Selected and
                not RuntimeQuestionStatus.Active)
            {
                throw new GameRuleViolationException(
                    "Answers can only be activated for an active all-player question.");
            }

            BuzzerStatus = QuestionBuzzerStatus.Closed;
            AnsweringPlayerId = null;
            Status = RuntimeQuestionStatus.Active;
            return;
        }

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
        if (IsHostMultipleChoice)
        {
            throw new GameRuleViolationException(
                "A host multiple-choice question must be judged by selecting an answer option.");
        }

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

        if (IsSpecial &&
            !IsAllPlayerQuestion &&
            Wager?.PlayerId != playerId)
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

    internal HostMultipleChoiceSelectionResult SelectHostMultipleChoiceOption(
        GamePlayerId playerId,
        int sourceContentBlockId,
        DateTimeOffset judgedAtUtc)
    {
        if (!IsHostMultipleChoice ||
            Status is not RuntimeQuestionStatus.Selected and
                not RuntimeQuestionStatus.Active ||
            BuzzerStatus != QuestionBuzzerStatus.Claimed ||
            AnsweringPlayerId != playerId)
        {
            throw new GameRuleViolationException(
                "An answer option can only be selected for the player who currently owns the buzzer.");
        }

        if (!_remainingHostMultipleChoiceOptionIds.Contains(sourceContentBlockId))
        {
            throw new GameRuleViolationException(
                "The selected answer option is no longer available.");
        }

        if (_answerAttempts.Any(attempt => attempt.PlayerId == playerId))
        {
            throw new GameRuleViolationException(
                "This player has already answered the current question.");
        }

        var value = HostMultipleChoiceRewardValue;
        var percentage = HostMultipleChoiceRewardPercentage;
        var isCorrect = sourceContentBlockId == HostMultipleChoiceCorrectOptionId;
        var attempt = new QuestionAnswerAttempt(
            playerId,
            isCorrect,
            isCorrect ? value : -value,
            judgedAtUtc);
        _answerAttempts.Add(attempt);

        if (isCorrect)
        {
            BuzzerStatus = QuestionBuzzerStatus.Closed;
            AnsweringPlayerId = null;
            Status = RuntimeQuestionStatus.ShowingAnswer;
        }
        else
        {
            _remainingHostMultipleChoiceOptionIds.Remove(sourceContentBlockId);
            AnsweringPlayerId = null;

            if (_remainingHostMultipleChoiceOptionIds.Count <= 2)
            {
                BuzzerStatus = QuestionBuzzerStatus.Closed;
                Status = RuntimeQuestionStatus.ShowingAnswer;
            }
            else
            {
                BuzzerStatus = QuestionBuzzerStatus.Open;
                Status = RuntimeQuestionStatus.Active;
            }
        }

        return new HostMultipleChoiceSelectionResult(
            attempt,
            sourceContentBlockId,
            isCorrect,
            Status == RuntimeQuestionStatus.ShowingAnswer,
            _remainingHostMultipleChoiceOptionIds.Count,
            percentage,
            value);
    }

    internal HostMultipleChoiceEliminationResult EliminateRandomHostMultipleChoiceOption(
        Random random)
    {
        ArgumentNullException.ThrowIfNull(random);

        if (!IsHostMultipleChoice ||
            Status is not RuntimeQuestionStatus.Selected and
                not RuntimeQuestionStatus.Active ||
            AnsweringPlayerId is not null)
        {
            throw new GameRuleViolationException(
                "An answer option can only be eliminated while a host multiple-choice question is waiting for a buzzer.");
        }

        var removable = _remainingHostMultipleChoiceOptionIds
            .Where(id => id != HostMultipleChoiceCorrectOptionId)
            .ToArray();
        if (removable.Length == 0)
        {
            throw new GameRuleViolationException(
                "The question does not have an incorrect option to eliminate.");
        }

        var removedOptionId = removable[random.Next(removable.Length)];
        _remainingHostMultipleChoiceOptionIds.Remove(removedOptionId);

        if (_remainingHostMultipleChoiceOptionIds.Count <= 2)
        {
            BuzzerStatus = QuestionBuzzerStatus.Closed;
            AnsweringPlayerId = null;
            Status = RuntimeQuestionStatus.ShowingAnswer;
        }
        else
        {
            BuzzerStatus = QuestionBuzzerStatus.Open;
            AnsweringPlayerId = null;
            Status = RuntimeQuestionStatus.Active;
        }

        return new HostMultipleChoiceEliminationResult(
            removedOptionId,
            Status == RuntimeQuestionStatus.ShowingAnswer,
            _remainingHostMultipleChoiceOptionIds.Count,
            HostMultipleChoiceRewardPercentage,
            HostMultipleChoiceRewardValue);
    }

    public static int CalculateHostMultipleChoiceRewardPercentage(
        int originalOptionCount,
        int remainingOptionCount)
    {
        if (originalOptionCount is < 4 or > 10)
        {
            throw new ArgumentOutOfRangeException(nameof(originalOptionCount));
        }

        if (remainingOptionCount >= originalOptionCount)
        {
            return 100;
        }

        if (remainingOptionCount <= 3)
        {
            return 50;
        }

        var progress = 50.0 * (remainingOptionCount - 3) /
            (originalOptionCount - 3);
        return 50 + (int)Math.Ceiling(progress);
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
        if ((IsSpecial && !IsAllPlayerQuestion) ||
            Status is not RuntimeQuestionStatus.Selected and
                not RuntimeQuestionStatus.Active)
        {
            throw new GameRuleViolationException(
                "Only an active regular or all-player question can be closed without a correct answer.");
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

public sealed record HostMultipleChoiceSelectionResult(
    QuestionAnswerAttempt Attempt,
    int SelectedOptionId,
    bool IsCorrect,
    bool QuestionClosed,
    int RemainingOptionCount,
    int RewardPercentage,
    int RewardValue);

public sealed record HostMultipleChoiceEliminationResult(
    int RemovedOptionId,
    bool QuestionClosed,
    int RemainingOptionCount,
    int RewardPercentage,
    int RewardValue);

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
