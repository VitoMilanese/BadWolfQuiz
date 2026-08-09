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
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(
            id,
            game => sessionRegistry.AddQuestionAnswerHistoryEntry(
                game.PublicCode,
                sourceQuestionId,
                new GamePlayerId(playerId),
                isCorrect,
                value),
            cancellationToken);
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
        return await ExecuteAsync(
            id,
            game => sessionRegistry.UpdateQuestionAnswerHistoryEntry(
                game.PublicCode,
                sourceQuestionId,
                attemptId,
                new GamePlayerId(playerId),
                isCorrect,
                value),
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
            cancellationToken);
    }

    private async Task<IActionResult> ExecuteAsync(
        Guid id,
        Action<GameSessionRegistration> command,
        CancellationToken cancellationToken)
    {
        var game = sessionRegistry.FindOwned(new GameSessionId(id), currentHost.RequiredId);

        if (game is null)
        {
            return NotFound();
        }

        try
        {
            command(game);
            await gameHistoryStore.SaveCompletedGameAsync(
                game,
                cancellationToken);
        }
        catch (GameRuleViolationException exception)
        {
            TempData["ErrorMessage"] = exception.Message switch
            {
                "This player already has an answer entry for the selected question." =>
                    localizer["AnswerHistory_PlayerAlreadyRecorded"].Value,
                "Answer history cannot be added to a question that has not been played." =>
                    localizer["AnswerHistory_QuestionNotPlayed"].Value,
                "An answer history value cannot be negative." =>
                    localizer["AnswerHistory_InvalidValue"].Value,
                _ => localizer["AnswerHistory_Rejected"].Value
            };
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

        return RedirectToPage(new { id });
    }

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
            .Where(question =>
                question.Status != RuntimeQuestionStatus.Available ||
                question.AnswerAttempts.Count > 0)
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
        question.AnswerAttempts
            .OrderByDescending(attempt => attempt.JudgedAtUtc)
            .ToArray());
}

public sealed record AnswerHistoryQuestion(
    int SourceQuestionId,
    string RoundTitle,
    string CategoryTitle,
    int Points,
    IReadOnlyList<QuestionAnswerAttempt> AnswerAttempts);
