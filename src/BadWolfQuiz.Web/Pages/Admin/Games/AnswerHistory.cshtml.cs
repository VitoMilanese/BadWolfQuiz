using BadWolfQuiz.Game.Runtime;
using BadWolfQuiz.Web.Hubs;
using BadWolfQuiz.Web.Localization;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Localization;

namespace BadWolfQuiz.Web.Pages.Admin.Games;

public sealed class AnswerHistoryModel(
    GameSessionRegistry sessionRegistry,
    GameHistoryStore gameHistoryStore,
    CurrentHost currentHost,
    IHubContext<GameHub> gameHub,
    IStringLocalizer<SharedResource> localizer) : PageModel
{
    public GameSessionRegistration Game { get; private set; } = null!;

    public IReadOnlyList<AnswerHistoryQuestion> Questions { get; private set; } = [];

    public IReadOnlyList<AnswerHistoryQuestion> AddableQuestions { get; private set; } = [];

    public IActionResult OnGet(Guid id)
    {
        return LoadPage(id);
    }

    public async Task<IActionResult> OnPostAddAsync(
        Guid id,
        int sourceQuestionId,
        Guid playerId,
        bool isCorrect,
        int value,
        bool resolveQuestionIfAvailable = true,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidHistoryValue(value))
        {
            return ErrorResult(
                id,
                localizer["GameBoard_QuickScoreInvalidValue"].Value);
        }

        return await ExecuteAsync(
            id,
            game => sessionRegistry.AddQuestionAnswerHistoryEntry(
                game.PublicCode,
                sourceQuestionId,
                new GamePlayerId(playerId),
                isCorrect,
                value,
                resolveQuestionIfAvailable),
            createAjaxData: null,
            cancellationToken: cancellationToken);
    }

    public async Task<IActionResult> OnPostUpdateAsync(
        Guid id,
        int sourceQuestionId,
        Guid attemptId,
        Guid playerId,
        bool isCorrect,
        int value,
        CancellationToken cancellationToken)
    {
        if (!IsValidHistoryValue(value))
        {
            return ErrorResult(
                id,
                localizer["GameBoard_QuickScoreInvalidValue"].Value);
        }

        return await ExecuteAsync(
            id,
            game => sessionRegistry.UpdateQuestionAnswerHistoryEntry(
                game.PublicCode,
                sourceQuestionId,
                attemptId,
                new GamePlayerId(playerId),
                isCorrect,
                value),
            game => CreateUpdateAjaxData(game, sourceQuestionId, attemptId),
            cancellationToken);
    }

    public async Task<IActionResult> OnPostDeleteAsync(
        Guid id,
        int sourceQuestionId,
        Guid attemptId,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            id,
            game => sessionRegistry.RemoveQuestionAnswerHistoryEntry(
                game.PublicCode,
                sourceQuestionId,
                attemptId),
            game => CreateDeleteAjaxData(game, sourceQuestionId),
            cancellationToken);
    }

    private async Task<IActionResult> ExecuteAsync(
        Guid id,
        Action<GameSessionRegistration> command,
        Func<GameSessionRegistration, object?>? createAjaxData,
        CancellationToken cancellationToken)
    {
        var game = sessionRegistry.FindOwned(new GameSessionId(id), currentHost.RequiredId);

        if (game is null)
        {
            return NotFound();
        }

        string? errorMessage = null;
        try
        {
            command(game);
            await gameHistoryStore.SaveCompletedGameAsync(
                game,
                cancellationToken);
        }
        catch (GameRuleViolationException exception)
        {
            errorMessage = exception.Message switch
            {
                "This player already has an answer entry for the selected question." =>
                    localizer["AnswerHistory_PlayerAlreadyRecorded"].Value,
                "Answer history cannot be added to a question that has not been played." =>
                    localizer["AnswerHistory_QuestionNotPlayed"].Value,
                "An answer history value cannot be negative." =>
                    localizer["GameBoard_QuickScoreInvalidValue"].Value,
                _ => localizer["AnswerHistory_Rejected"].Value
            };

            if (!IsAjaxRequest())
            {
                TempData["ErrorMessage"] = errorMessage;
            }
        }

        await gameHub.Clients
            .Group(GameHub.GroupName(game.PublicCode))
            .SendAsync(
                "PlayersChanged",
                GameHub.CreatePlayersUpdate(sessionRegistry, game),
                cancellationToken);
        await gameHub.Clients
            .Group(GameHub.GroupName(game.PublicCode))
            .SendAsync(
                "BuzzerStateChanged",
                GameHub.CreateBuzzerUpdate(game),
                cancellationToken);

        if (IsAjaxRequest())
        {
            if (errorMessage is not null)
            {
                return AjaxError(errorMessage);
            }

            return new JsonResult(new
            {
                ok = true,
                data = createAjaxData?.Invoke(game)
            });
        }

        return RedirectToPage(new { id });
    }

    private object CreateUpdateAjaxData(
        GameSessionRegistration game,
        int sourceQuestionId,
        Guid attemptId)
    {
        var question = game.Session.Board.Questions
            .Single(item => item.SourceQuestionId == sourceQuestionId);
        var attempt = question.AnswerAttempts
            .Single(item => item.Id == attemptId);
        var player = game.Session.AllPlayers
            .Single(item => item.Id == attempt.PlayerId);

        return new
        {
            sourceQuestionId,
            attemptId,
            playerId = attempt.PlayerId.Value,
            playerName = player.Name,
            attempt.IsCorrect,
            attempt.ScoreDelta
        };
    }

    private object CreateDeleteAjaxData(
        GameSessionRegistration game,
        int sourceQuestionId)
    {
        var question = game.Session.Board.Questions
            .Single(item => item.SourceQuestionId == sourceQuestionId);
        var visibleQuestions = game.Session.Board.Questions
            .Where(IsVisibleInHistory)
            .ToArray();

        return new
        {
            sourceQuestionId,
            questionHasAttempts = question.AnswerAttempts.Count > 0,
            questionIsVisible = IsVisibleInHistory(question),
            questionCount = visibleQuestions.Length,
            answerCount = visibleQuestions.Sum(item => item.AnswerAttempts.Count),
            noEntriesLabel = localizer["AnswerHistory_NoEntries"].Value
        };
    }

    private IActionResult ErrorResult(Guid id, string message)
    {
        if (IsAjaxRequest())
        {
            return AjaxError(message);
        }

        TempData["ErrorMessage"] = message;
        return RedirectToPage(new { id });
    }

    private static JsonResult AjaxError(string message) => new(new
    {
        ok = false,
        error = message
    })
    {
        StatusCode = 400
    };

    private bool IsAjaxRequest() => string.Equals(
        Request.Headers["X-Requested-With"],
        "XMLHttpRequest",
        StringComparison.OrdinalIgnoreCase);

    private static bool IsVisibleInHistory(RuntimeQuestion question) =>
        question.Status != RuntimeQuestionStatus.Available ||
        question.AnswerAttempts.Count > 0;

    private static bool IsValidHistoryValue(int value) =>
        value > 0;

    private IActionResult LoadPage(Guid id)
    {
        var game = sessionRegistry.FindOwned(new GameSessionId(id), currentHost.RequiredId);

        if (game is null)
        {
            return NotFound();
        }

        Game = game;
        var availableRoundIds = game.Session.Quiz.Rounds
            .OrderBy(round => round.SortOrder)
            .Take(game.Session.CurrentRoundIndex + 1)
            .Select(round => round.SourceRoundId)
            .ToHashSet();

        Questions = game.Session.Board.Questions
            .Where(IsVisibleInHistory)
            .OrderByDescending(question => question.AnswerAttempts.Count > 0
                ? question.AnswerAttempts.Max(attempt => attempt.JudgedAtUtc)
                : DateTimeOffset.MinValue)
            .ThenBy(question => question.RowIndex)
            .ThenBy(question => question.SourceCategoryId)
            .Select(question => CreateQuestion(game, question))
            .ToArray();

        AddableQuestions = game.Session.Board.Questions
            .Where(question => availableRoundIds.Contains(question.SourceRoundId))
            .OrderBy(question => game.Session.Quiz.Rounds
                .Single(round => round.SourceRoundId == question.SourceRoundId)
                .SortOrder)
            .ThenBy(question => question.RowIndex)
            .ThenBy(question => question.SourceCategoryId)
            .Select(question => CreateQuestion(game, question))
            .ToArray();

        return Page();
    }

    private static AnswerHistoryQuestion CreateQuestion(
        GameSessionRegistration game,
        RuntimeQuestion question) => new(
        question.SourceQuestionId,
        game.Session.Quiz.Rounds
            .Single(round => round.SourceRoundId == question.SourceRoundId)
            .Title,
        question.CategoryTitle,
        question.Points,
        question.Status == RuntimeQuestionStatus.Available,
        question.AnswerAttempts
            .OrderByDescending(attempt => attempt.JudgedAtUtc)
            .ToArray());
}

public sealed record AnswerHistoryQuestion(
    int SourceQuestionId,
    string RoundTitle,
    string CategoryTitle,
    int Points,
    bool IsAvailable,
    IReadOnlyList<QuestionAnswerAttempt> AnswerAttempts);
