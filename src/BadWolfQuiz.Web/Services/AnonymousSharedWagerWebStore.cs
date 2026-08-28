using System.Collections.Concurrent;
using BadWolfQuiz.Game.Definitions;
using BadWolfQuiz.Game.Runtime;

namespace BadWolfQuiz.Web.Services;

public static class AnonymousSharedWagerWebStore
{
    private static readonly ConcurrentDictionary<GameSessionId, AnonymousSharedWagerState>
        States = new();

    public static AnonymousSharedWagerState? GetOrStart(GameSessionRegistration game)
    {
        ArgumentNullException.ThrowIfNull(game);

        lock (game)
        {
            var question = FindCurrentQuestion(game);
            if (question is null)
            {
                States.TryRemove(game.Session.Id, out _);
                return null;
            }

            if (States.TryGetValue(game.Session.Id, out var existing) &&
                existing.SourceQuestionId == question.SourceQuestionId)
            {
                var resolved = AnonymousSharedWagerCoordinator.ResolveRemovedParticipants(
                    game.Session,
                    existing,
                    DateTimeOffset.UtcNow);
                States[game.Session.Id] = resolved;
                ActivateIfComplete(game, resolved, question);
                return resolved;
            }

            if (question.Status != RuntimeQuestionStatus.AwaitingWager)
            {
                return null;
            }

            var created = AnonymousSharedWagerCoordinator.Start(
                game.Session,
                question.SourceQuestionId,
                DateTimeOffset.UtcNow);
            States[game.Session.Id] = created;
            ActivateIfComplete(game, created, question);
            game.MarkPersistenceChanged();
            return created;
        }
    }

    public static AnonymousSharedWagerState Submit(
        GameSessionRegistration game,
        GamePlayerId playerId,
        int percentage)
    {
        lock (game)
        {
            var state = GetOrStart(game)
                ?? throw new GameRuleViolationException(
                    "No anonymous shared wager is currently accepting contributions.");
            state = AnonymousSharedWagerLifecycle.Submit(
                state,
                playerId,
                percentage,
                DateTimeOffset.UtcNow);
            States[game.Session.Id] = state;
            var question = game.Session.Board.Questions.Single(item =>
                item.SourceQuestionId == state.SourceQuestionId);
            ActivateIfComplete(game, state, question);
            game.MarkPersistenceChanged();
            return state;
        }
    }

    public static AnonymousSharedWagerState ForceFullStake(
        GameSessionRegistration game,
        GamePlayerId playerId)
    {
        lock (game)
        {
            var state = GetOrStart(game)
                ?? throw new GameRuleViolationException(
                    "No anonymous shared wager is currently accepting contributions.");
            state = AnonymousSharedWagerLifecycle.ForceMissingAsFullStake(
                state,
                playerId,
                DateTimeOffset.UtcNow);
            States[game.Session.Id] = state;
            var question = game.Session.Board.Questions.Single(item =>
                item.SourceQuestionId == state.SourceQuestionId);
            ActivateIfComplete(game, state, question);
            game.MarkPersistenceChanged();
            return state;
        }
    }

    public static QuestionAnswerAttempt Settle(
        GameSessionRegistration game,
        bool isCorrect)
    {
        lock (game)
        {
            var state = States.TryGetValue(game.Session.Id, out var current)
                ? current
                : throw new GameRuleViolationException(
                    "The anonymous shared wager state is unavailable.");
            var attempt = AnonymousSharedWagerCoordinator.SettleAnswer(
                game.Session,
                state,
                isCorrect);
            game.BuzzerRace = null;
            game.MarkPersistenceChanged();
            States.TryRemove(game.Session.Id, out _);
            return attempt;
        }
    }

    public static AnonymousSharedWagerCalculation? GetCalculation(
        GameSessionRegistration game,
        AnonymousSharedWagerState state)
    {
        var question = game.Session.Board.Questions.SingleOrDefault(item =>
            item.SourceQuestionId == state.SourceQuestionId);
        return question is not null && state.IsComplete
            ? AnonymousSharedWagerLifecycle.Complete(state, question.Points)
            : null;
    }

    public static void Restore(
        GameSessionRegistration game,
        AnonymousSharedWagerState? state)
    {
        if (state is null)
        {
            States.TryRemove(game.Session.Id, out _);
            return;
        }

        States[game.Session.Id] = state;
    }

    public static AnonymousSharedWagerState? Capture(GameSessionRegistration game) =>
        States.TryGetValue(game.Session.Id, out var state) ? state : null;

    private static RuntimeQuestion? FindCurrentQuestion(GameSessionRegistration game) =>
        game.Session.Board.Questions.FirstOrDefault(question =>
            QuestionWagerModes.IsAnonymousShared(question.PresentationType) &&
            question.Status is RuntimeQuestionStatus.AwaitingWager or
                RuntimeQuestionStatus.Active);

    private static void ActivateIfComplete(
        GameSessionRegistration game,
        AnonymousSharedWagerState state,
        RuntimeQuestion question)
    {
        if (!state.IsComplete || question.Status != RuntimeQuestionStatus.AwaitingWager)
        {
            return;
        }

        AnonymousSharedWagerCoordinator.ActivateQuestion(
            game.Session,
            state,
            DateTimeOffset.UtcNow);
    }
}
