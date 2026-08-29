using BadWolfQuiz.Game.Definitions;

namespace BadWolfQuiz.Game.Runtime;

public static class AnonymousSharedWagerCoordinator
{
    public static AnonymousSharedWagerState Start(
        GameSession session,
        int sourceQuestionId,
        DateTimeOffset startedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(session);

        var question = FindQuestion(session, sourceQuestionId);
        if (!question.IsSpecial ||
            !QuestionWagerModes.IsAnonymousShared(question.PresentationType) ||
            question.Status != RuntimeQuestionStatus.AwaitingWager ||
            question.SelectedByPlayerId is not { } answeringPlayerId)
        {
            throw new GameRuleViolationException(
                "The selected question is not awaiting an anonymous shared wager.");
        }

        return AnonymousSharedWagerLifecycle.Start(
            sourceQuestionId,
            answeringPlayerId,
            session.Players.Select(player => player.Id),
            startedAtUtc);
    }

    public static AnonymousSharedWagerState ResolveRemovedParticipants(
        GameSession session,
        AnonymousSharedWagerState state,
        DateTimeOffset resolvedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(state);

        var currentPlayerIds = session.Players.Select(player => player.Id).ToHashSet();
        foreach (var participantId in state.ParticipantIds)
        {
            if (!currentPlayerIds.Contains(participantId) &&
                !state.HasSubmitted(participantId))
            {
                state = AnonymousSharedWagerLifecycle.ForceMissingAsFullStake(
                    state,
                    participantId,
                    resolvedAtUtc);
            }
        }

        return state;
    }

    public static AnonymousSharedWagerCalculation ActivateQuestion(
        GameSession session,
        AnonymousSharedWagerState state,
        DateTimeOffset completedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(state);

        var question = FindQuestion(session, state.SourceQuestionId);
        if (question.Status != RuntimeQuestionStatus.AwaitingWager ||
            question.SelectedByPlayerId != state.AnsweringPlayerId ||
            !QuestionWagerModes.IsAnonymousShared(question.PresentationType))
        {
            throw new GameRuleViolationException(
                "The anonymous shared wager no longer matches the active question.");
        }

        var calculation = AnonymousSharedWagerLifecycle.Complete(
            state,
            question.Points);

        question.SubmitWager(
            state.AnsweringPlayerId,
            calculation.CombinedWager,
            completedAtUtc);
        session.Timer.Stop();

        if (session.Settings.WagerQuestionAnswerTimerStartMode ==
            GamePhaseStartMode.Automatic)
        {
            session.AnswerTimer.Restart();
        }
        else
        {
            session.AnswerTimer.Stop();
        }

        return calculation;
    }

    public static QuestionAnswerAttempt SettleAnswer(
        GameSession session,
        AnonymousSharedWagerState state,
        bool isCorrect)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(state);

        var question = FindQuestion(session, state.SourceQuestionId);
        var calculation = AnonymousSharedWagerLifecycle.Complete(
            state,
            question.Points);

        if (!QuestionWagerModes.IsAnonymousShared(question.PresentationType) ||
            question.Wager?.PlayerId != state.AnsweringPlayerId ||
            question.Wager.Amount != calculation.CombinedWager)
        {
            throw new GameRuleViolationException(
                "The anonymous shared wager is not ready for settlement.");
        }

        var answeringPlayer = session.AllPlayers.SingleOrDefault(
            player => player.Id == state.AnsweringPlayerId)
            ?? throw new GameRuleViolationException(
                "The answering player is no longer available for settlement.");

        var fundingPlayers = calculation.Contributions
            .Select(contribution => new
            {
                Contribution = contribution,
                Player = session.AllPlayers.SingleOrDefault(
                    player => player.Id == contribution.PlayerId)
                    ?? throw new GameRuleViolationException(
                        "A funding player is no longer available for settlement.")
            })
            .ToArray();

        // Validate every resulting score before changing any score. ApplyScore is
        // then non-throwing for these values, keeping the transfer atomic in memory.
        _ = checked(answeringPlayer.Score +
            (isCorrect ? calculation.CombinedWager : -calculation.CombinedWager));
        foreach (var funding in fundingPlayers)
        {
            _ = checked(funding.Player.Score +
                (isCorrect
                    ? -funding.Contribution.Amount
                    : funding.Contribution.Amount));
        }

        var attempt = session.JudgeQuestionAnswer(
            state.SourceQuestionId,
            state.AnsweringPlayerId,
            isCorrect);

        foreach (var funding in fundingPlayers)
        {
            funding.Player.ApplyScore(
                isCorrect
                    ? -funding.Contribution.Amount
                    : funding.Contribution.Amount);
        }

        var fundingDelta = fundingPlayers.Sum(funding =>
            isCorrect
                ? -funding.Contribution.Amount
                : funding.Contribution.Amount);
        if (fundingDelta != -attempt.ScoreDelta)
        {
            throw new InvalidOperationException(
                "Anonymous shared wager settlement must remain zero-sum.");
        }

        return attempt;
    }

    private static RuntimeQuestion FindQuestion(
        GameSession session,
        int sourceQuestionId) =>
        session.Board.Questions.SingleOrDefault(
            question => question.SourceQuestionId == sourceQuestionId)
        ?? throw new GameRuleViolationException(
            $"Question {sourceQuestionId} does not belong to this game.");
}
