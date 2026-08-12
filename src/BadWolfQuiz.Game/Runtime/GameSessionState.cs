namespace BadWolfQuiz.Game.Runtime;

public sealed record GameSessionState(
    GameSessionId Id,
    GameSessionStatus Status,
    GamePlayerId? ActivePlayerId,
    int CurrentRoundIndex,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? StartedAtUtc,
    IReadOnlyList<PlayerRoundStartScoreState> CurrentRoundStartScores,
    IReadOnlyList<GamePlayerState> Players,
    IReadOnlyList<GamePlayerState> RemovedPlayers,
    IReadOnlyList<RuntimeQuestionState> Questions,
    FinalQuestionState? FinalQuestion,
    bool IsForcedRoundAdvancePending = false);

public sealed record PlayerRoundStartScoreState(GamePlayerId PlayerId, int Score);

public sealed record GamePlayerState(
    GamePlayerId Id,
    string Name,
    int Score,
    string? AvatarId,
    string? UploadedImageDataUrl,
    bool UsesUploadedImage,
    DateTimeOffset JoinedAtUtc,
    string? WebcamUrl = null);

public sealed record RuntimeQuestionState(
    int SourceQuestionId,
    bool IsSpecial,
    RuntimeQuestionStatus Status,
    GamePlayerId? SelectedByPlayerId,
    Wager? Wager,
    QuestionBuzzerStatus BuzzerStatus,
    GamePlayerId? AnsweringPlayerId,
    IReadOnlyList<QuestionAnswerAttempt> AnswerAttempts,
    int RevealedClueCount = 0);

public sealed record FinalQuestionState(
    FinalQuestionStatus Status,
    IReadOnlyList<FinalPlayerSubmissionState> Submissions);

public sealed record FinalPlayerSubmissionState(
    GamePlayerId PlayerId,
    string PlayerName,
    int MaximumWager,
    FinalWager? Wager,
    FinalAnswer? Answer,
    bool? IsCorrect,
    DateTimeOffset? JudgedAtUtc);
