using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace BadWolfQuiz.Web.Services;

/// <summary>
/// Stores an explicit reset/tombstone next to achievement unlock rows so a reset
/// achievement stays locked instead of being immediately restored from lifetime history.
/// </summary>
public sealed class PlayerAchievementResetService(QuizDbContext db)
{
    public const string MarkerPrefix = "__Reset:";

    public async Task<PlayerAchievementResetResult?> ResetAccountAsync(
        string accountId,
        string achievementCode,
        CancellationToken cancellationToken = default)
    {
        var metadata = await ResolveMetadataAsync(achievementCode, cancellationToken);
        if (metadata is null)
        {
            return null;
        }

        var identity = PlayerAchievementIdentity.Create(accountId, null, string.Empty);
        if (!identity.IsValid)
        {
            return metadata with { Reset = false };
        }

        var changed = await HasUnlockOrResetAsync(identity, achievementCode, cancellationToken);
        if (!changed)
        {
            return metadata with { Reset = false };
        }

        await ApplyResetAsync(
            identity,
            achievementCode,
            sourceGameSessionId: null,
            DateTime.UtcNow,
            cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return metadata with { Reset = true };
    }

    public async Task<PlayerAchievementResetResult?> ResetPlayerAsync(
        string? accountId,
        string? hostId,
        string playerName,
        string achievementCode,
        int? sourceGameSessionId,
        CancellationToken cancellationToken = default)
    {
        var metadata = await ResolveMetadataAsync(achievementCode, cancellationToken);
        if (metadata is null)
        {
            return null;
        }

        var identities = new List<PlayerAchievementIdentity>();
        var fallbackIdentity = PlayerAchievementIdentity.Create(null, hostId, playerName);
        if (fallbackIdentity.IsValid)
        {
            identities.Add(fallbackIdentity);
        }

        var accountIdentity = PlayerAchievementIdentity.Create(accountId, null, string.Empty);
        if (accountIdentity.IsValid && !identities.Contains(accountIdentity))
        {
            identities.Add(accountIdentity);
        }

        if (identities.Count == 0)
        {
            return metadata with { Reset = false };
        }

        var changed = false;
        foreach (var identity in identities)
        {
            changed |= await HasUnlockOrResetAsync(identity, achievementCode, cancellationToken);
        }

        if (!changed)
        {
            return metadata with { Reset = false };
        }

        var resetAtUtc = DateTime.UtcNow;
        foreach (var identity in identities)
        {
            await ApplyResetAsync(
                identity,
                achievementCode,
                sourceGameSessionId,
                resetAtUtc,
                cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        return metadata with { Reset = true };
    }

    public async Task<IReadOnlyDictionary<string, PlayerAchievementResetMarker>> LoadMarkersAsync(
        PlayerAchievementIdentity identity,
        CancellationToken cancellationToken = default)
    {
        if (!identity.IsValid)
        {
            return new Dictionary<string, PlayerAchievementResetMarker>(StringComparer.Ordinal);
        }

        var rows = await QueryForIdentity(identity)
            .AsNoTracking()
            .Where(item => item.AchievementCode.StartsWith(MarkerPrefix))
            .Select(item => new
            {
                item.AchievementCode,
                item.UnlockedAtUtc,
                item.SourceGameSessionId
            })
            .ToListAsync(cancellationToken);

        var result = new Dictionary<string, PlayerAchievementResetMarker>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            if (!TryParseMarkerCode(row.AchievementCode, out var code))
            {
                continue;
            }

            result[code] = new PlayerAchievementResetMarker(
                code,
                EnsureUtc(row.UnlockedAtUtc),
                row.SourceGameSessionId);
        }

        return result;
    }

    public Task<IReadOnlyDictionary<string, PlayerAchievementResetMarker>> LoadMarkersAsync(
        string? accountId,
        string? hostId,
        string playerName,
        CancellationToken cancellationToken = default) =>
        LoadMarkersAsync(
            PlayerAchievementIdentity.Create(accountId, hostId, playerName),
            cancellationToken);

    public async Task<PlayerAchievement?> FindMarkerAsync(
        PlayerAchievementIdentity identity,
        string achievementCode,
        CancellationToken cancellationToken = default)
    {
        if (!identity.IsValid)
        {
            return null;
        }

        var markerCode = BuildMarkerCode(achievementCode);
        return await QueryForIdentity(identity)
            .SingleOrDefaultAsync(
                item => item.AchievementCode == markerCode,
                cancellationToken);
    }

    public async Task<bool> RemoveMarkerAsync(
        PlayerAchievementIdentity identity,
        string achievementCode,
        CancellationToken cancellationToken = default)
    {
        var marker = await FindMarkerAsync(identity, achievementCode, cancellationToken);
        if (marker is null)
        {
            return false;
        }

        db.PlayerAchievements.Remove(marker);
        return true;
    }

    public static string BuildMarkerCode(string achievementCode) =>
        $"{MarkerPrefix}{achievementCode}";

    public static bool TryParseMarkerCode(string? markerCode, out string achievementCode)
    {
        achievementCode = string.Empty;
        if (string.IsNullOrWhiteSpace(markerCode) ||
            !markerCode.StartsWith(MarkerPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        achievementCode = markerCode[MarkerPrefix.Length..];
        return achievementCode.Length > 0;
    }

    private async Task ApplyResetAsync(
        PlayerAchievementIdentity identity,
        string achievementCode,
        int? sourceGameSessionId,
        DateTime resetAtUtc,
        CancellationToken cancellationToken)
    {
        var unlocks = await QueryForIdentity(identity)
            .Where(item => item.AchievementCode == achievementCode)
            .ToListAsync(cancellationToken);
        if (unlocks.Count > 0)
        {
            db.PlayerAchievements.RemoveRange(unlocks);
        }

        var marker = await FindMarkerAsync(identity, achievementCode, cancellationToken);
        if (marker is null)
        {
            marker = new PlayerAchievement
            {
                AccountId = identity.AccountId,
                HostId = identity.AccountId is null ? identity.HostId : null,
                PlayerKey = identity.AccountId is null ? identity.PlayerKey : null,
                AchievementCode = BuildMarkerCode(achievementCode)
            };
            db.PlayerAchievements.Add(marker);
        }

        marker.UnlockedAtUtc = resetAtUtc;
        marker.SourceGameSessionId = sourceGameSessionId;
    }

    private async Task<bool> HasUnlockOrResetAsync(
        PlayerAchievementIdentity identity,
        string achievementCode,
        CancellationToken cancellationToken)
    {
        var markerCode = BuildMarkerCode(achievementCode);
        return await QueryForIdentity(identity)
            .AsNoTracking()
            .AnyAsync(
                item => item.AchievementCode == achievementCode ||
                        item.AchievementCode == markerCode,
                cancellationToken);
    }

    private async Task<PlayerAchievementResetResult?> ResolveMetadataAsync(
        string achievementCode,
        CancellationToken cancellationToken)
    {
        var normalizedCode = achievementCode?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedCode))
        {
            return null;
        }

        var builtIn = PlayerAchievementService.Catalog.FirstOrDefault(item =>
            string.Equals(item.Code, normalizedCode, StringComparison.Ordinal));
        if (builtIn is not null)
        {
            return new PlayerAchievementResetResult(
                Reset: false,
                builtIn.Code,
                builtIn.Target,
                builtIn.IsSecret,
                IsHostDefined: false);
        }

        if (!HostCustomAchievementService.TryParseCode(normalizedCode, out var customId))
        {
            return null;
        }

        var custom = await db.HostCustomAchievements
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(item => item.Id == customId)
            .Select(item => new { item.Id, item.Target })
            .SingleOrDefaultAsync(cancellationToken);
        if (custom is null)
        {
            return null;
        }

        return new PlayerAchievementResetResult(
            Reset: false,
            HostCustomAchievementService.BuildCode(custom.Id),
            custom.Target,
            IsSecret: false,
            IsHostDefined: true);
    }

    private IQueryable<PlayerAchievement> QueryForIdentity(PlayerAchievementIdentity identity) =>
        identity.AccountId is not null
            ? db.PlayerAchievements.Where(item => item.AccountId == identity.AccountId)
            : db.PlayerAchievements.Where(item =>
                item.AccountId == null &&
                item.HostId == identity.HostId &&
                item.PlayerKey == identity.PlayerKey);

    private static DateTime EnsureUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}

public sealed record PlayerAchievementResetMarker(
    string AchievementCode,
    DateTime ResetAtUtc,
    int? SourceGameSessionId);

public sealed record PlayerAchievementResetResult(
    bool Reset,
    string AchievementCode,
    int Target,
    bool IsSecret,
    bool IsHostDefined);
