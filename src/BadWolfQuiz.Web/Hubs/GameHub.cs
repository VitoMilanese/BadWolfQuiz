using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.SignalR;

namespace BadWolfQuiz.Web.Hubs;

public sealed class GameHub(BuzzCoordinator buzzCoordinator) : Hub
{
    public Task JoinSession(string publicCode)
        => Groups.AddToGroupAsync(Context.ConnectionId, GroupName(publicCode));

    public async Task Buzz(string publicCode, int gameQuestionId, int playerId, string playerName)
    {
        var accepted = buzzCoordinator.TryBuzz(gameQuestionId, playerId);

        if (accepted)
        {
            await Clients.Group(GroupName(publicCode)).SendAsync(
                "BuzzAccepted",
                new { gameQuestionId, playerId, playerName });
        }
        else
        {
            await Clients.Caller.SendAsync(
                "BuzzRejected",
                new { gameQuestionId, winnerPlayerId = buzzCoordinator.GetWinner(gameQuestionId) });
        }
    }

    public async Task ResetBuzz(string publicCode, int gameQuestionId)
    {
        buzzCoordinator.Reset(gameQuestionId);
        await Clients.Group(GroupName(publicCode)).SendAsync("BuzzReset", new { gameQuestionId });
    }

    private static string GroupName(string publicCode)
        => $"game:{publicCode.Trim().ToUpperInvariant()}";
}
