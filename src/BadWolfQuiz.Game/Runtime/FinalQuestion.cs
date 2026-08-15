using System.Collections.ObjectModel;
using BadWolfQuiz.Game.Definitions;

namespace BadWolfQuiz.Game.Runtime;

public sealed class FinalQuestion
{
    public const int MinimumWager = 5;
    public const int DefaultMaximumWager = 1000;

    private readonly List<FinalPlayerSubmission> _submissions;
    private readonly ReadOnlyCollection<FinalPlayerSubmission> _readOnlySubmissions;

    internal FinalQuestion(
        FinalQuestionSnapshot definition,
        IEnumerable<GamePlayer> players)
    {
        Definition = definition;
        _submissions = players
            .Select(player => new FinalPlayerSubmission(
                player.Id,
                player.Name,
                Math.Max(DefaultMaximumWager, player.Score)))
            .ToList();
        _readOnlySubmissions = _submissions.AsReadOnly();
    }

    public FinalQuestionSnapshot Definition { get; }

    public FinalQuestionStatus Status { get; private set; } =
        FinalQuestionStatus.Wagering;

    public IReadOnlyList<FinalPlayerSubmission> Submissions =>
        _readOnlySubmissions;

    internal FinalQuestionState CaptureState() => new(
        Status,
        _submissions.Select(submission => submission.CaptureState()).ToArray());

    internal static FinalQuestion Restore(
        FinalQuestionSnapshot definition,
        FinalQuestionState state)
    {
        var finalQuestion = new FinalQuestion(definition, []);
        finalQuestion.Status = state.Status;
        finalQuestion._submissions.AddRange(
            state.Submissions.Select(FinalPlayerSubmission.Restore));
        return finalQuestion;
    }

    internal FinalPlayerSubmission SubmitWager(
        GamePlayerId playerId,
        int amount,
        DateTimeOffset submittedAtUtc)
    {
        EnsureStatus(FinalQuestionStatus.Wagering);
        var submission = FindSubmission(playerId);
        submission.SubmitWager(amount, submittedAtUtc);
        return submission;
    }

    internal void LockWagers()
    {
        EnsureStatus(FinalQuestionStatus.Wagering);

        if (_submissions.Any(item => item.Wager is null))
        {
            throw new GameRuleViolationException(
                "Every final player must submit a wager before wagering can be locked.");
        }

        Status = FinalQuestionStatus.Answering;
    }

    internal FinalPlayerSubmission SubmitAnswer(
        GamePlayerId playerId,
        string answer,
        DateTimeOffset submittedAtUtc)
    {
        EnsureStatus(FinalQuestionStatus.Answering);
        var submission = FindSubmission(playerId);
        submission.SubmitAnswer(answer, submittedAtUtc);
        return submission;
    }

    internal void LockAnswers()
    {
        EnsureStatus(FinalQuestionStatus.Answering);

        if (_submissions.Any(item => item.Answer is null))
        {
            throw new GameRuleViolationException(
                "Every final player must submit an answer before answering can be locked.");
        }

        Status = FinalQuestionStatus.Judging;
    }

    internal FinalPlayerSubmission Judge(
        GamePlayerId playerId,
        bool isCorrect,
        DateTimeOffset judgedAtUtc)
    {
        EnsureStatus(FinalQuestionStatus.Judging);
        var submission = FindSubmission(playerId);
        submission.Judge(isCorrect, judgedAtUtc);

        if (_submissions.All(item => item.IsCorrect.HasValue))
        {
            Status = FinalQuestionStatus.Completed;
        }

        return submission;
    }

    internal bool RemovePlayer(GamePlayerId playerId)
    {
        var submission = _submissions.SingleOrDefault(item => item.PlayerId == playerId);
        if (submission is null)
        {
            return false;
        }

        _submissions.Remove(submission);

        if (_submissions.Count == 0 ||
            (Status == FinalQuestionStatus.Judging &&
             _submissions.All(item => item.IsCorrect.HasValue)))
        {
            Status = FinalQuestionStatus.Completed;
        }

        return true;
    }

    private FinalPlayerSubmission FindSubmission(GamePlayerId playerId)
    {
        return _submissions.SingleOrDefault(item => item.PlayerId == playerId)
            ?? throw new GameRuleViolationException(
                "The player does not participate in the final question.");
    }

    private void EnsureStatus(FinalQuestionStatus expected)
    {
        if (Status != expected)
        {
            throw new GameRuleViolationException(
                $"The final question must be in the {expected} phase.");
        }
    }
}

public sealed class FinalPlayerSubmission
{
    internal FinalPlayerSubmission(
        GamePlayerId playerId,
        string playerName,
        int maximumWager)
    {
        PlayerId = playerId;
        PlayerName = playerName;
        MaximumWager = maximumWager;
    }

    public GamePlayerId PlayerId { get; }

    public string PlayerName { get; }

    public int MaximumWager { get; }

    public FinalWager? Wager { get; private set; }

    public FinalAnswer? Answer { get; private set; }

    public bool? IsCorrect { get; private set; }

    public DateTimeOffset? JudgedAtUtc { get; private set; }

    internal FinalPlayerSubmissionState CaptureState() => new(
        PlayerId,
        PlayerName,
        MaximumWager,
        Wager,
        Answer,
        IsCorrect,
        JudgedAtUtc);

    internal static FinalPlayerSubmission Restore(FinalPlayerSubmissionState state)
    {
        var submission = new FinalPlayerSubmission(
            state.PlayerId,
            state.PlayerName,
            state.MaximumWager)
        {
            Wager = state.Wager,
            Answer = state.Answer,
            IsCorrect = state.IsCorrect,
            JudgedAtUtc = state.JudgedAtUtc
        };
        return submission;
    }

    internal void SubmitWager(int amount, DateTimeOffset submittedAtUtc)
    {
        if (Wager is not null)
        {
            throw new GameRuleViolationException(
                "The final wager has already been confirmed.");
        }

        if (amount < FinalQuestion.MinimumWager || amount > MaximumWager)
        {
            throw new GameRuleViolationException(
                $"The final wager must be between {FinalQuestion.MinimumWager} and {MaximumWager}.");
        }

        Wager = new FinalWager(amount, submittedAtUtc);
    }

    internal void SubmitAnswer(string answer, DateTimeOffset submittedAtUtc)
    {
        if (Wager is null)
        {
            throw new GameRuleViolationException(
                "A final answer requires a confirmed wager.");
        }

        if (Answer is not null)
        {
            throw new GameRuleViolationException(
                "The final answer has already been confirmed.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(answer);
        Answer = new FinalAnswer(answer.Trim(), submittedAtUtc);
    }

    internal void Judge(bool isCorrect, DateTimeOffset judgedAtUtc)
    {
        if (Answer is null || Wager is null)
        {
            throw new GameRuleViolationException(
                "A final submission must be complete before judging.");
        }

        if (IsCorrect.HasValue)
        {
            throw new GameRuleViolationException(
                "The final answer has already been judged.");
        }

        IsCorrect = isCorrect;
        JudgedAtUtc = judgedAtUtc;
    }
}

public sealed record FinalWager(int Amount, DateTimeOffset SubmittedAtUtc);

public sealed record FinalAnswer(string Text, DateTimeOffset SubmittedAtUtc);

public enum FinalQuestionStatus
{
    Wagering = 1,
    Answering = 2,
    Judging = 3,
    Completed = 4
}
