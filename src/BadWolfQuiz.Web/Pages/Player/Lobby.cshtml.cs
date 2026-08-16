using BadWolfQuiz.Game.Runtime;
using BadWolfQuiz.Web.Hubs;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace BadWolfQuiz.Web.Pages.Player;

public sealed class LobbyModel(
    GameSessionRegistry sessionRegistry,
    GameSettingsStore settingsStore,
    QuizRatingService quizRatingService,
    IOptions<FooterOptions> footerOptions,
    IHubContext<GameHub> gameHub,
    IWebHostEnvironment environment) : PageModel
{
    public GameSessionRegistration Game { get; private set; } = null!;

    public GamePlayer CurrentPlayer { get; private set; } = null!;

    public IReadOnlyList<GamePlayer> Players { get; private set; } = [];

    public string? AccessToken { get; private set; }
    public int? ExistingRating { get; private set; }
    public bool CanRateQuiz { get; private set; }
    public bool IsContributor { get; private set; }

    public async Task<IActionResult> OnGetAsync(
        string code,
        Guid playerId,
        string? accessToken,
        CancellationToken cancellationToken)
    {
        var game = sessionRegistry.Find(code);

        if (game is null)
        {
            return NotFound();
        }

        var players = sessionRegistry.GetPlayers(game);
        var currentPlayer = players.FirstOrDefault(
            player => player.Id == new GamePlayerId(playerId));

        if (currentPlayer is null)
        {
            return NotFound();
        }

        Game = game;
        CurrentPlayer = currentPlayer;
        Players = players;
        AccessToken = accessToken;
        IsContributor = ContributorRecognition.IsContributor(footerOptions.Value, currentPlayer.Name);
        var normalizedFrameId = ContributorAvatarFrameCatalog.Normalize(
            environment,
            currentPlayer.AvatarFrameId);
        ViewData["ContributorPlayer"] = IsContributor;
        ViewData["ContributorPlayerFrameEnabled"] =
            IsContributor &&
            currentPlayer.AvatarFrameEnabled &&
            normalizedFrameId is not null &&
            ContributorAvatarFrameCatalog.IsValid(environment, normalizedFrameId);
        ViewData["ContributorPlayerFrameId"] = IsContributor
            ? normalizedFrameId
            : null;
        CanRateQuiz = QuizRatingService.IsRatingAvailable(game.Session);
        if (CanRateQuiz)
        {
            ExistingRating = await quizRatingService.GetPlayerRatingAsync(
                code,
                currentPlayer.Id,
                cancellationToken);
        }
        var themeSettings = !string.IsNullOrWhiteSpace(game.HostId)
            ? await settingsStore.LoadAsync(game.HostId, cancellationToken)
            : game.Session.Settings;
        ViewData["GameThemeSettings"] = themeSettings;
        return Page();
    }

    public async Task<IActionResult> OnPostAvatarFrameAsync(
        string code,
        Guid playerId,
        string? accessToken,
        bool enabled,
        string? frameId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken) ||
            !ContributorAvatarFrameCatalog.IsValid(environment, frameId))
        {
            return new JsonResult(new { saved = false }) { StatusCode = 400 };
        }

        var validationConnectionId = $"contributor-frame:{Guid.NewGuid():N}";
        var connection = sessionRegistry.ConnectPlayer(
            code,
            accessToken,
            validationConnectionId,
            isVisible: false);

        if (connection is null)
        {
            return new JsonResult(new { saved = false }) { StatusCode = 403 };
        }

        try
        {
            if (connection.RequiresApproval ||
                connection.Player.Id != new GamePlayerId(playerId) ||
                !ContributorRecognition.IsContributor(footerOptions.Value, connection.Player.Name))
            {
                return new JsonResult(new { saved = false }) { StatusCode = 403 };
            }

            var normalizedFrameId = ContributorAvatarFrameCatalog.Normalize(
                environment,
                frameId)!;
            lock (connection.Game)
            {
                connection.Player.SetAvatarFrame(enabled, normalizedFrameId);
                connection.Game.MarkPersistenceChanged();
            }

            await gameHub.Clients
                .Group(GameHub.GroupName(connection.Game.PublicCode))
                .SendAsync(
                    "PlayersChanged",
                    GameHub.CreatePlayersUpdate(sessionRegistry, connection.Game),
                    cancellationToken);

            return new JsonResult(new
            {
                saved = true,
                enabled,
                frameId = normalizedFrameId
            });
        }
        finally
        {
            sessionRegistry.DisconnectPlayer(validationConnectionId);
        }
    }

    public async Task<IActionResult> OnPostRateQuizAsync(
        string code,
        Guid playerId,
        string? accessToken,
        int? score,
        CancellationToken cancellationToken)
    {
        var result = score.HasValue
            ? await quizRatingService.RateAsync(
                code,
                new GamePlayerId(playerId),
                score.Value,
                cancellationToken)
            : QuizRatingResult.InvalidScore;

        return new JsonResult(new
        {
            saved = result == QuizRatingResult.Saved
        })
        {
            StatusCode = result == QuizRatingResult.Saved ? 200 : 409
        };
    }

    public IActionResult OnGetFinalContentBlock(
        string code,
        Guid playerId,
        int sourceContentBlockId)
    {
        var game = sessionRegistry.Find(code);

        if (game is null ||
            game.Session.Status is not GameSessionStatus.FinalAnswering and
                not GameSessionStatus.FinalJudging and
                not GameSessionStatus.Completed)
        {
            return NotFound();
        }

        var block = game.Session.FinalQuestion?.Definition.QuestionBlocks
            .SingleOrDefault(item =>
                item.SourceContentBlockId == sourceContentBlockId);

        if (block?.FileData is null ||
            string.IsNullOrWhiteSpace(block.FileContentType))
        {
            return NotFound();
        }

        return string.IsNullOrWhiteSpace(block.FileName)
            ? File(block.FileData, block.FileContentType)
            : File(block.FileData, block.FileContentType, block.FileName);
    }
}
