using System.Globalization;
using BadWolfQuiz.Game.Runtime;
using BadWolfQuiz.Web.Hubs;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.SignalR;

namespace BadWolfQuiz.Web.Pages.Admin.Games;

[Authorize]
public sealed class PlayerAdmissionModel(
    GameSessionRegistry sessionRegistry,
    CurrentHost currentHost,
    IHubContext<GameHub> gameHub) : PageModel
{
    public IActionResult OnGet(Guid id)
    {
        var game = FindOwnedGame(id);
        if (game is null)
        {
            return NotFound();
        }

        var waitingCount = sessionRegistry
            .GetPlayerLobbyEntries(game)
            .Count(player => player.Presence == PlayerPresenceStatus.RejoinPending);
        var labels = GetLabels();

        return new JsonResult(new
        {
            waitingCount,
            automaticallyAcceptNewPlayers = PlayerAdmissionAutomation.IsEnabled(game),
            game.AllowsNewPlayers,
            labels = new
            {
                acceptAllWaiting = labels.AcceptAllWaiting,
                automaticAcceptance = labels.AutomaticAcceptance,
                allowNewConnections = labels.AllowNewConnections,
                denyNewConnections = labels.DenyNewConnections,
                enabled = labels.Enabled,
                disabled = labels.Disabled
            }
        });
    }

    public async Task<IActionResult> OnPostAcceptAllWaitingAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var game = FindOwnedGame(id);
        if (game is null)
        {
            return NotFound();
        }

        var accepted = await PlayerAdmissionAutomation.AcceptWaitingPlayersAsync(
            game,
            sessionRegistry,
            gameHub,
            cancellationToken);

        return new JsonResult(new { success = true, accepted });
    }

    public IActionResult OnPostToggleAutomaticAcceptance(Guid id)
    {
        var game = FindOwnedGame(id);
        if (game is null)
        {
            return NotFound();
        }

        if (game.Session.Status != GameSessionStatus.Running)
        {
            return new JsonResult(new { success = false }) { StatusCode = 409 };
        }

        var enabled = PlayerAdmissionAutomation.Toggle(
            game,
            sessionRegistry,
            gameHub);

        return new JsonResult(new { success = true, enabled });
    }

    private GameSessionRegistration? FindOwnedGame(Guid id) =>
        sessionRegistry.FindOwned(new GameSessionId(id), currentHost.RequiredId);

    private static PlayerAdmissionLabels GetLabels() =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName switch
        {
            "uk" => new(
                "Прийняти всіх гравців, які очікують",
                "Автоматично приймати нових гравців",
                "Дозволити під’єднання нових гравців",
                "Заборонити під’єднання нових гравців",
                "увімкнено",
                "вимкнено"),
            "it" => new(
                "Accetta tutti i giocatori in attesa",
                "Accetta automaticamente i nuovi giocatori",
                "Consenti connessioni di nuovi giocatori",
                "Impedisci connessioni di nuovi giocatori",
                "attivo",
                "disattivo"),
            _ => new(
                "Accept all waiting players",
                "Automatically accept new players",
                "Allow new player connections",
                "Deny new player connections",
                "enabled",
                "disabled")
        };

    private sealed record PlayerAdmissionLabels(
        string AcceptAllWaiting,
        string AutomaticAcceptance,
        string AllowNewConnections,
        string DenyNewConnections,
        string Enabled,
        string Disabled);
}
