using System.ComponentModel.DataAnnotations;
using BadWolfQuiz.Web.Hubs;
using BadWolfQuiz.Web.Localization;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Localization;

namespace BadWolfQuiz.Web.Pages.Join;

public sealed class IndexModel(
    GameSessionRegistry sessionRegistry,
    AvatarCatalog avatarCatalog,
    IHubContext<GameHub> gameHub,
    IStringLocalizer<SharedResource> localizer,
    ILogger<IndexModel> logger) : PageModel
{
    [BindProperty]
    public JoinGameInput Input { get; set; } = new();

    public string? GameInstanceId { get; private set; }

    public void OnGet(string? code)
    {
        if (!string.IsNullOrWhiteSpace(code))
        {
            Input.GameCode = GameSessionRegistry.NormalizeCode(code);
            GameInstanceId = sessionRegistry.Find(Input.GameCode)?.ClientInstanceId;
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Input.GameCode = GameSessionRegistry.NormalizeCode(Input.GameCode ?? string.Empty);
        Input.PlayerName = Input.PlayerName?.Trim() ?? string.Empty;
        GameInstanceId = sessionRegistry.Find(Input.GameCode)?.ClientInstanceId;
        ModelState.Clear();
        TryValidateModel(Input, nameof(Input));

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var avatarId = avatarCatalog.IsValid(Input.AvatarId)
            ? Input.AvatarId
            : null;
        var result = sessionRegistry.JoinPlayer(
            Input.GameCode,
            Input.PlayerName,
            avatarId);

        switch (result.Status)
        {
            case PlayerJoinStatus.Success:
            {
                var game = result.Game!;
                var player = result.Player!;
                var redirect = RedirectToPage(
                    "/Player/Lobby",
                    new
                    {
                        code = game.PublicCode,
                        playerId = player.Id.Value,
                        accessToken = result.AccessToken
                    });

                try
                {
                    await gameHub.Clients
                        .Group(GameHub.GroupName(game.PublicCode))
                        .SendAsync(
                            "PlayersChanged",
                            GameHub.CreatePlayersUpdate(sessionRegistry, game),
                            CancellationToken.None);
                }
                catch (Exception exception)
                {
                    logger.LogWarning(
                        exception,
                        "Failed to broadcast player join for game {GameCode} and player {PlayerId}; continuing with player redirect.",
                        game.PublicCode,
                        player.Id.Value);
                }

                return redirect;
            }

            case PlayerJoinStatus.GameNotFound:
                ModelState.AddModelError(
                    $"{nameof(Input)}.{nameof(Input.GameCode)}",
                    localizer["Error_GameNotFound"]);
                break;

            case PlayerJoinStatus.NameAlreadyUsed:
                ModelState.AddModelError(
                    $"{nameof(Input)}.{nameof(Input.PlayerName)}",
                    localizer["Error_PlayerExists"]);
                break;

            case PlayerJoinStatus.GameAlreadyStarted:
                ModelState.AddModelError(
                    string.Empty,
                    localizer["Message_GameStarted"]);
                break;

            case PlayerJoinStatus.GameClosed:
                ModelState.AddModelError(
                    string.Empty,
                    localizer["Error_GameNoLongerAcceptsPlayers"]);
                break;

            case PlayerJoinStatus.PlayerBlocked:
                ModelState.AddModelError(
                    string.Empty,
                    localizer["Error_PlayerBlocked"]);
                break;
        }

        return Page();
    }

    public sealed class JoinGameInput
    {
        [Required(ErrorMessage = "Error_Required")]
        [StringLength(GameCodeGenerator.CodeLength, MinimumLength = GameCodeGenerator.CodeLength)]
        public string GameCode { get; set; } = string.Empty;

        [Required(ErrorMessage = "Error_Required")]
        [StringLength(60, ErrorMessage = "Error_MaxLength")]
        public string PlayerName { get; set; } = string.Empty;

        public string? AvatarId { get; set; }
    }
}
