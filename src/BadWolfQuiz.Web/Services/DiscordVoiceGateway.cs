using System.Collections.Concurrent;
using Discord;
using Discord.Net;
using Discord.WebSocket;
using Microsoft.Extensions.Options;

namespace BadWolfQuiz.Web.Services;

public sealed class DiscordVoiceGateway(
    DiscordSocketClient client,
    IOptions<DiscordIntegrationOptions> options,
    ILogger<DiscordVoiceGateway> logger) : IHostedService, IDiscordVoiceGateway
{
    private readonly DiscordIntegrationOptions settings = options.Value;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> channelLocks = new();

    public bool IsReady => settings.Enabled && client.ConnectionState == ConnectionState.Connected;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!settings.Enabled)
        {
            return;
        }

        client.Log += message =>
        {
            logger.Log(message.Severity >= LogSeverity.Error ? LogLevel.Error : LogLevel.Information,
                message.Exception, "Discord gateway: {Message}", message.Message);
            return Task.CompletedTask;
        };
        await client.LoginAsync(TokenType.Bot, settings.BotToken);
        await client.StartAsync();
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (client.LoginState != LoginState.LoggedOut)
        {
            await client.StopAsync();
            await client.LogoutAsync();
        }
    }

    public IReadOnlyList<DiscordGuildOption> GetGuilds(IReadOnlySet<string> allowedGuildIds) =>
        client.Guilds
            .Where(guild => allowedGuildIds.Contains(guild.Id.ToString()))
            .OrderBy(guild => guild.Name)
            .Select(guild => new DiscordGuildOption(guild.Id.ToString(), guild.Name))
            .ToArray();

    public IReadOnlyList<DiscordVoiceChannelOption> GetVoiceChannels(string guildId)
    {
        if (!ulong.TryParse(guildId, out var id) || client.GetGuild(id) is not { } guild)
        {
            return [];
        }

        return guild.VoiceChannels
            .Where(channel => HasRequiredPermissions(guild.CurrentUser, channel).CanView)
            .OrderBy(channel => channel.Position)
            .Select(channel => new DiscordVoiceChannelOption(channel.Id.ToString(), channel.Name))
            .ToArray();
    }

    public DiscordConnectionHealth GetHealth(string? guildId, string? channelId)
    {
        if (!IsReady || !ulong.TryParse(guildId, out var parsedGuildId) ||
            client.GetGuild(parsedGuildId) is not { } guild)
        {
            return new(IsReady, false, false, false, false, false);
        }

        if (!ulong.TryParse(channelId, out var parsedChannelId) ||
            guild.GetVoiceChannel(parsedChannelId) is not { } channel)
        {
            return new(true, true, false, false, false, false);
        }

        var permissions = HasRequiredPermissions(guild.CurrentUser, channel);
        return new(true, true, true, permissions.CanView, permissions.CanConnect, permissions.CanMute);
    }

    public async Task<DiscordMuteResult> SetMutedAsync(
        string guildId,
        string channelId,
        string hostDiscordUserId,
        bool muted,
        int maximumParallelOperations,
        CancellationToken cancellationToken)
    {
        if (!ulong.TryParse(guildId, out var parsedGuildId) ||
            !ulong.TryParse(channelId, out var parsedChannelId) ||
            client.GetGuild(parsedGuildId) is not { } guild ||
            guild.GetVoiceChannel(parsedChannelId) is not { } channel)
        {
            return new(0, 0, 1, 0, [new("", "Discord voice channel is unavailable.")]);
        }

        var channelLock = channelLocks.GetOrAdd($"{guildId}:{channelId}", _ => new(1, 1));
        await channelLock.WaitAsync(cancellationToken);
        try
        {
            var targets = channel.ConnectedUsers
                .Where(user => user.Id.ToString() != hostDiscordUserId && !user.IsBot)
                .ToArray();
            var failures = new ConcurrentBag<DiscordMuteFailure>();
            var succeeded = 0;
            var skipped = 0;

            await Parallel.ForEachAsync(
                targets,
                new ParallelOptions
                {
                    CancellationToken = cancellationToken,
                    MaxDegreeOfParallelism = maximumParallelOperations
                },
                async (user, token) =>
                {
                    if (user.IsMuted == muted)
                    {
                        Interlocked.Increment(ref skipped);
                        return;
                    }

                    try
                    {
                        await user.ModifyAsync(properties => properties.Mute = muted,
                            new RequestOptions { CancelToken = token });
                        Interlocked.Increment(ref succeeded);
                    }
                    catch (HttpException exception) when ((int)exception.HttpCode == 400)
                    {
                        Interlocked.Increment(ref skipped);
                    }
                    catch (Exception exception)
                    {
                        failures.Add(new(user.Id.ToString(), exception.Message));
                    }
                });

            return new(targets.Length, succeeded, failures.Count, skipped, failures.ToArray());
        }
        finally
        {
            channelLock.Release();
        }
    }

    private static (bool CanView, bool CanConnect, bool CanMute) HasRequiredPermissions(
        SocketGuildUser bot,
        SocketVoiceChannel channel)
    {
        var permissions = bot.GetPermissions(channel);
        return (permissions.ViewChannel, permissions.Connect, permissions.MuteMembers);
    }
}
