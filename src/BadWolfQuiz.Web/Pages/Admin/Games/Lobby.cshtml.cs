using BadWolfQuiz.Game.Runtime;
using BadWolfQuiz.Web.Hubs;
using BadWolfQuiz.Web.Localization;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Localization;

namespace BadWolfQuiz.Web.Pages.Admin.Games;

public sealed class LobbyModel(
    GameSessionRegistry sessionRegistry,
    IHubContext<GameHub> gameHub,
    IStringLocalizer<SharedResource> localizer) : PageModel
{
    public GameSessionRegistration Game { get; private set; } = null!;

    public IReadOnlyList<GamePlayer> Players { get; private set; } = [];

    public IReadOnlyList<GameBoardCategory> BoardCategories { get; private set; } = [];

    public RuntimeQuestion? CurrentQuestion { get; private set; }

    public IActionResult OnGet(Guid id)
    {
        return LoadPage(id);
    }

    public async Task<IActionResult> OnPostStartAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var game = sessionRegistry.Find(new GameSessionId(id));

        if (game is null)
        {
            return NotFound();
        }

        try
        {
            sessionRegistry.StartGame(game.PublicCode);
        }
        catch (GameRuleViolationException)
        {
            TempData["ErrorMessage"] = localizer["GameLobby_StartRequiresPlayer"].Value;
            return RedirectToPage(new { id });
        }

        await gameHub.Clients
            .Group(GameHub.GroupName(game.PublicCode))
            .SendAsync(
                "GameStatusChanged",
                GameHub.CreateStatusUpdate(game),
                cancellationToken);

        return RedirectToPage(new { id });
    }

    public IActionResult OnPostSelectQuestion(
        Guid id,
        int sourceQuestionId)
    {
        var game = sessionRegistry.Find(new GameSessionId(id));

        if (game is null)
        {
            return NotFound();
        }

        try
        {
            sessionRegistry.SelectQuestion(game.PublicCode, sourceQuestionId);
        }
        catch (GameRuleViolationException)
        {
            TempData["ErrorMessage"] = localizer["GameBoard_SelectionRejected"].Value;
        }

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostApproveRejoinAsync(
        Guid id,
        Guid playerId,
        CancellationToken cancellationToken)
    {
        var game = sessionRegistry.Find(new GameSessionId(id));

        if (game is null)
        {
            return NotFound();
        }

        var approval = sessionRegistry.ApprovePlayerRejoin(
            game.PublicCode,
            new GamePlayerId(playerId));

        if (approval is null)
        {
            return NotFound();
        }

        if (approval.ConnectionIds.Count > 0)
        {
            await gameHub.Clients
                .Clients(approval.ConnectionIds)
                .SendAsync("RejoinApproved", cancellationToken);
        }

        await gameHub.Clients
            .Group(GameHub.GroupName(game.PublicCode))
            .SendAsync(
                "PlayersChanged",
                GameHub.CreatePlayersUpdate(sessionRegistry, game),
                cancellationToken);

        return RedirectToPage(new { id });
    }

    private IActionResult LoadPage(Guid id)
    {
        var game = sessionRegistry.Find(new GameSessionId(id));

        if (game is null)
        {
            return NotFound();
        }

        Game = game;
        Players = sessionRegistry.GetPlayers(game);

        var currentRound = game.Session.Quiz.Rounds
            .OrderBy(round => round.SortOrder)
            .First();

        var roundQuestions = game.Session.Board.Questions
            .Where(question => question.SourceRoundId == currentRound.SourceRoundId)
            .ToArray();

        BoardCategories = roundQuestions
            .GroupBy(question => new
            {
                question.SourceCategoryId,
                question.CategoryTitle
            })
            .Select(group => new GameBoardCategory(
                group.Key.SourceCategoryId,
                group.Key.CategoryTitle,
                group.OrderBy(question => question.RowIndex).ToArray()))
            .ToArray();

        CurrentQuestion = roundQuestions.FirstOrDefault(question =>
            question.Status is not RuntimeQuestionStatus.Available and
                not RuntimeQuestionStatus.Resolved);

        return Page();
    }
}


public sealed record GameBoardCategory(
    int SourceCategoryId,
    string Title,
    IReadOnlyList<RuntimeQuestion> Questions);
