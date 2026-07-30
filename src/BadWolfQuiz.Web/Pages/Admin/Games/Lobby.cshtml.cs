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

    public WagerLimits? QuestionWagerLimits { get; private set; }

    public bool IsRoundSummaryVisible { get; private set; }

    public IReadOnlyList<RoundLeaderboardEntry> RoundLeaders { get; private set; } = [];

    public IActionResult OnGet(Guid id)
    {
        return LoadPage(id);
    }

    public IActionResult OnGetContentBlock(
        Guid id,
        int sourceQuestionId,
        int sourceContentBlockId,
        bool answer)
    {
        var game = sessionRegistry.Find(new GameSessionId(id));

        if (game is null)
        {
            return NotFound();
        }

        var question = game.Session.Board.Questions.SingleOrDefault(
            item => item.SourceQuestionId == sourceQuestionId);
        var blocks = answer
            ? question?.AnswerBlocks
            : question?.QuestionBlocks;
        var block = blocks?.SingleOrDefault(
            item => item.SourceContentBlockId == sourceContentBlockId);

        if (block?.FileData is null ||
            string.IsNullOrWhiteSpace(block.FileContentType))
        {
            return NotFound();
        }

        return string.IsNullOrWhiteSpace(block.FileName)
            ? File(block.FileData, block.FileContentType)
            : File(block.FileData, block.FileContentType, block.FileName);
    }

    public static string? GetYouTubeEmbedUrl(string? value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return value;
        }

        if (uri.AbsolutePath.StartsWith("/embed/", StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        string? videoId = null;

        if (uri.Host.EndsWith("youtu.be", StringComparison.OrdinalIgnoreCase))
        {
            videoId = uri.AbsolutePath.Trim('/');
        }
        else if (uri.Host.Contains("youtube.com", StringComparison.OrdinalIgnoreCase))
        {
            videoId = uri.Query
                .TrimStart('?')
                .Split('&', StringSplitOptions.RemoveEmptyEntries)
                .Select(item => item.Split('=', 2))
                .FirstOrDefault(item =>
                    item.Length == 2 &&
                    string.Equals(item[0], "v", StringComparison.OrdinalIgnoreCase))?
                .ElementAtOrDefault(1);
        }

        return string.IsNullOrWhiteSpace(videoId)
            ? value
            : $"https://www.youtube.com/embed/{Uri.EscapeDataString(videoId)}";
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

    public async Task<IActionResult> OnPostPauseQuestionTimerAsync(
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
            sessionRegistry.PauseQuestionTimer(game.PublicCode);
            await BroadcastTimerAsync(game, cancellationToken);
        }
        catch (GameRuleViolationException)
        {
            TempData["ErrorMessage"] =
                localizer["GameTimer_PauseRejected"].Value;
        }

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostResumeQuestionTimerAsync(
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
            sessionRegistry.ResumeQuestionTimer(game.PublicCode);
            await BroadcastTimerAsync(game, cancellationToken);
        }
        catch (GameRuleViolationException)
        {
            TempData["ErrorMessage"] =
                localizer["GameTimer_ResumeRejected"].Value;
        }

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostAdvanceRoundAsync(
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
            sessionRegistry.AdvanceToNextRound(game.PublicCode);
        }
        catch (GameRuleViolationException)
        {
            TempData["ErrorMessage"] =
                localizer["GameBoard_AdvanceRoundRejected"].Value;
        }

        await BroadcastPlayersAsync(game, cancellationToken);
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

    public IActionResult OnPostSubmitQuestionWager(
        Guid id,
        int sourceQuestionId,
        int amount)
    {
        var game = sessionRegistry.Find(new GameSessionId(id));

        if (game is null)
        {
            return NotFound();
        }

        try
        {
            sessionRegistry.SubmitQuestionWager(
                game.PublicCode,
                sourceQuestionId,
                amount);
        }
        catch (GameRuleViolationException)
        {
            TempData["ErrorMessage"] =
                localizer["GameBoard_WagerRejected"].Value;
        }

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostActivateBuzzerAsync(
        Guid id,
        int sourceQuestionId,
        CancellationToken cancellationToken)
    {
        var game = sessionRegistry.Find(new GameSessionId(id));

        if (game is null)
        {
            return NotFound();
        }

        try
        {
            sessionRegistry.ActivateQuestionBuzzer(
                game.PublicCode,
                sourceQuestionId);

            await BroadcastBuzzerAsync(game, cancellationToken);
        }
        catch (GameRuleViolationException)
        {
            TempData["ErrorMessage"] =
                localizer["GameBoard_BuzzerActivationRejected"].Value;
        }

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostJudgeQuestionAnswerAsync(
        Guid id,
        int sourceQuestionId,
        Guid playerId,
        bool isCorrect,
        CancellationToken cancellationToken)
    {
        var game = sessionRegistry.Find(new GameSessionId(id));

        if (game is null)
        {
            return NotFound();
        }

        try
        {
            sessionRegistry.JudgeQuestionAnswer(
                game.PublicCode,
                sourceQuestionId,
                new GamePlayerId(playerId),
                isCorrect);
        }
        catch (GameRuleViolationException)
        {
            TempData["ErrorMessage"] =
                localizer["GameBoard_JudgmentRejected"].Value;
        }

        await BroadcastPlayersAsync(game, cancellationToken);
        await BroadcastBuzzerAsync(game, cancellationToken);
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostResolveQuestionAsync(
        Guid id,
        int sourceQuestionId,
        CancellationToken cancellationToken)
    {
        var game = sessionRegistry.Find(new GameSessionId(id));

        if (game is null)
        {
            return NotFound();
        }

        try
        {
            sessionRegistry.ResolveQuestionWithoutCorrectAnswer(
                game.PublicCode,
                sourceQuestionId);
        }
        catch (GameRuleViolationException)
        {
            TempData["ErrorMessage"] =
                localizer["GameBoard_JudgmentRejected"].Value;
        }

        await BroadcastPlayersAsync(game, cancellationToken);
        await BroadcastBuzzerAsync(game, cancellationToken);
        return RedirectToPage(new { id });
    }

    public IActionResult OnPostCloseAnswer(
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
            sessionRegistry.CloseQuestionAnswer(
                game.PublicCode,
                sourceQuestionId);
        }
        catch (GameRuleViolationException)
        {
            TempData["ErrorMessage"] =
                localizer["GameBoard_CloseAnswerRejected"].Value;
        }

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostSetActivePlayerAsync(
        Guid id,
        Guid playerId,
        CancellationToken cancellationToken)
    {
        var game = sessionRegistry.Find(new GameSessionId(id));

        if (game is null)
        {
            return NotFound();
        }

        try
        {
            sessionRegistry.SetActivePlayer(
                game.PublicCode,
                new GamePlayerId(playerId));
        }
        catch (GameRuleViolationException)
        {
            TempData["ErrorMessage"] =
                localizer["GameBoard_ActivePlayerRejected"].Value;
        }

        await BroadcastPlayersAsync(game, cancellationToken);
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostRandomActivePlayerAsync(
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
            sessionRegistry.SelectRandomActivePlayer(game.PublicCode);
        }
        catch (GameRuleViolationException)
        {
            TempData["ErrorMessage"] =
                localizer["GameBoard_ActivePlayerRejected"].Value;
        }

        await BroadcastPlayersAsync(game, cancellationToken);
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

    private Task BroadcastTimerAsync(
        GameSessionRegistration game,
        CancellationToken cancellationToken)
    {
        return gameHub.Clients
            .Group(GameHub.GroupName(game.PublicCode))
            .SendAsync(
                "TimerStateChanged",
                GameHub.CreateTimerUpdate(game),
                cancellationToken);
    }

    private Task BroadcastBuzzerAsync(
        GameSessionRegistration game,
        CancellationToken cancellationToken)
    {
        var update = GameHub.CreateBuzzerUpdate(game);

        return update is null
            ? Task.CompletedTask
            : gameHub.Clients
                .Group(GameHub.GroupName(game.PublicCode))
                .SendAsync("BuzzerStateChanged", update, cancellationToken);
    }

    private Task BroadcastPlayersAsync(
        GameSessionRegistration game,
        CancellationToken cancellationToken)
    {
        return gameHub.Clients
            .Group(GameHub.GroupName(game.PublicCode))
            .SendAsync(
                "PlayersChanged",
                GameHub.CreatePlayersUpdate(sessionRegistry, game),
                cancellationToken);
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

        var currentRound = game.Session.CurrentRound;

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

        IsRoundSummaryVisible =
            CurrentQuestion is null &&
            game.Session.IsCurrentRoundComplete;

        if (IsRoundSummaryVisible)
        {
            RoundLeaders = Players
                .OrderByDescending(player => player.Score)
                .ThenBy(player => player.JoinedAtUtc)
                .Take(3)
                .Select((player, index) => new RoundLeaderboardEntry(
                    index + 1,
                    player.Id,
                    player.Name,
                    player.Score))
                .ToArray();
        }

        if (CurrentQuestion?.Status == RuntimeQuestionStatus.AwaitingWager)
        {
            QuestionWagerLimits = game.Session.GetQuestionWagerLimits(
                CurrentQuestion.SourceQuestionId);
        }

        return Page();
    }
}


public sealed record GameBoardCategory(
    int SourceCategoryId,
    string Title,
    IReadOnlyList<RuntimeQuestion> Questions);


public sealed record RoundLeaderboardEntry(
    int Position,
    GamePlayerId PlayerId,
    string PlayerName,
    int Score);
