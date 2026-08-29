using BadWolfQuiz.Web.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace BadWolfQuiz.Web.Services;

public sealed class MinigameRoomCleanupService(
    MinigameRoomStore roomStore,
    IHubContext<MinigameHub> hubContext,
    ILogger<MinigameRoomCleanupService> logger) : BackgroundService
{
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var timer = new PeriodicTimer(CleanupInterval);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                var expiredCodes = roomStore.RemoveExpired();
                foreach (var roomCode in expiredCodes)
                {
                    logger.LogDebug(
                        "Expired minigame room {RoomCode} after one hour of inactivity.",
                        roomCode);
                    await hubContext.Clients
                        .Group(MinigameRoomStore.GetSignalRGroupName(roomCode))
                        .SendAsync(
                            "roomExpired",
                            roomCode,
                            cancellationToken: stoppingToken);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal application shutdown.
        }
    }
}
