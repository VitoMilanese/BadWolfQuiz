using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace BadWolfQuiz.Web.Services;

public sealed class DiscordMuteTimeoutService(
    DiscordMuteCoordinator coordinator,
    GameSessionRegistry sessions,
    IDbContextFactory<QuizDbContext> dbFactory,
    ILogger<DiscordMuteTimeoutService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await coordinator.ReleaseExpiredAutomaticMutesAsync(
                    ResolveAsync, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Failed to release expired Discord media mute.");
            }
        }
    }

    private async Task<(string HostId, HostDiscordConnection Connection)?> ResolveAsync(
        Guid gameId,
        CancellationToken cancellationToken)
    {
        var game = sessions.GetAll().SingleOrDefault(item => item.Session.Id.Value == gameId);
        if (game?.HostId is not { } hostId)
        {
            return null;
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var connection = await db.HostDiscordConnections
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(item => item.HostId == hostId, cancellationToken);
        return connection is null ? null : (hostId, connection);
    }
}
