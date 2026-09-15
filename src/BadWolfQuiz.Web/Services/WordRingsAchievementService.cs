using System.Security.Cryptography;
using System.Text;
using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace BadWolfQuiz.Web.Services;

public sealed class WordRingsAchievementService(QuizDbContext db)
{
    private const string PlacementEventPrefix = "__WRP:";
    private const string CorrectEventPrefix = "__WRC:";
    private const string BlueEventPrefix = "__WRB:";
    private const string YellowEventPrefix = "__WRY:";
    private const string RedEventPrefix = "__WRR:";
    private const string HostedGameEventPrefix = "__WRH:";

    public Task<bool> UnlockSoloAiAsync(
        string? accountId,
        CancellationToken cancellationToken = default) =>
        new PlayerAchievementService(db).UnlockAccountAsync(
            accountId,
            "WordRingsSoloAi",
            cancellationToken: cancellationToken);

    public async Task RecordPlacementAsync(
        string? accountId,
        string? hostId,
        string playerName,
        string scopeKey,
        string eventKey,
        bool fullyCorrect,
        string? membership,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(scopeKey) || string.IsNullOrWhiteSpace(eventKey))
        {
            return;
        }

        var normalizedMembership = CanonicalMembership(membership);
        var scopeHash = ShortHash(scopeKey);
        var eventHash = ShortHash(eventKey);
        var resultKind = fullyCorrect ? normalizedMembership.Length == 0 ? "O" : "C" : "X";
        var placementEventCode = $"{PlacementEventPrefix}{scopeHash}:{resultKind}:{eventHash}";

        foreach (var identity in BuildIdentities(accountId, hostId, playerName))
        {
            if (!await AddProgressEventIfMissingAsync(identity, placementEventCode, cancellationToken))
            {
                continue;
            }

            if (fullyCorrect && normalizedMembership.Length > 0)
            {
                await AddProgressEventIfMissingAsync(
                    identity,
                    $"{CorrectEventPrefix}{scopeHash}:{eventHash}",
                    cancellationToken);
                if (normalizedMembership.Contains('A'))
                {
                    await AddProgressEventIfMissingAsync(identity, $"{BlueEventPrefix}{scopeHash}:{eventHash}", cancellationToken);
                }
                if (normalizedMembership.Contains('B'))
                {
                    await AddProgressEventIfMissingAsync(identity, $"{YellowEventPrefix}{scopeHash}:{eventHash}", cancellationToken);
                }
                if (normalizedMembership.Contains('C'))
                {
                    await AddProgressEventIfMissingAsync(identity, $"{RedEventPrefix}{scopeHash}:{eventHash}", cancellationToken);
                }
            }

            await db.SaveChangesAsync(cancellationToken);

            if (fullyCorrect && string.Equals(normalizedMembership, "ABC", StringComparison.Ordinal))
            {
                await UnlockIdentityAsync(identity, playerName, "WordRingsTripleCorrect", cancellationToken);
            }

            if (fullyCorrect && normalizedMembership.Length == 0 &&
                await HasThreeConsecutiveOutsideCorrectAsync(identity, scopeHash, cancellationToken))
            {
                await UnlockIdentityAsync(identity, playerName, "WordRingsOutsideThree", cancellationToken);
            }

            if (fullyCorrect && normalizedMembership.Length > 0)
            {
                var counters = await LoadCountersAsync(db, identity, cancellationToken);
                if (counters.CorrectPlacements >= 50)
                {
                    await UnlockIdentityAsync(identity, playerName, "WordRingsCorrect50", cancellationToken);
                }
                if (counters.BlueCorrectPlacements >= 50)
                {
                    await UnlockIdentityAsync(identity, playerName, "WordRingsBlue50", cancellationToken);
                }
                if (counters.YellowCorrectPlacements >= 50)
                {
                    await UnlockIdentityAsync(identity, playerName, "WordRingsYellow50", cancellationToken);
                }
                if (counters.RedCorrectPlacements >= 50)
                {
                    await UnlockIdentityAsync(identity, playerName, "WordRingsRed50", cancellationToken);
                }
            }
        }
    }

    public async Task RecordHostedGameAsync(
        string? accountId,
        string? hostId,
        string playerName,
        string gameKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(gameKey))
        {
            return;
        }

        var eventCode = $"{HostedGameEventPrefix}{ShortHash(gameKey)}";
        foreach (var identity in BuildIdentities(accountId, hostId, playerName))
        {
            if (!await AddProgressEventIfMissingAsync(identity, eventCode, cancellationToken))
            {
                continue;
            }

            await db.SaveChangesAsync(cancellationToken);
            var counters = await LoadCountersAsync(db, identity, cancellationToken);
            if (counters.HostedGames >= 1)
            {
                await UnlockIdentityAsync(identity, playerName, "WordRingsReferee", cancellationToken);
            }
            if (counters.HostedGames >= 10)
            {
                await UnlockIdentityAsync(identity, playerName, "WordRingsHost10", cancellationToken);
            }
        }
    }

    internal static async Task<PlayerAchievementHistory> ApplyProgressAsync(
        QuizDbContext db,
        PlayerAchievementIdentity identity,
        PlayerAchievementHistory history,
        CancellationToken cancellationToken)
    {
        var counters = await LoadCountersAsync(db, identity, cancellationToken);
        return history with
        {
            WordRingsCorrectPlacements = counters.CorrectPlacements,
            WordRingsBlueCorrectPlacements = counters.BlueCorrectPlacements,
            WordRingsYellowCorrectPlacements = counters.YellowCorrectPlacements,
            WordRingsRedCorrectPlacements = counters.RedCorrectPlacements,
            WordRingsHostedGames = counters.HostedGames
        };
    }

    internal static async Task<WordRingsAchievementCounters> LoadCountersAsync(
        QuizDbContext db,
        PlayerAchievementIdentity identity,
        CancellationToken cancellationToken)
    {
        if (!identity.IsValid)
        {
            return new WordRingsAchievementCounters();
        }

        var codes = await QueryForIdentity(db, identity)
            .AsNoTracking()
            .Where(item => item.AchievementCode.StartsWith("__WR"))
            .Select(item => item.AchievementCode)
            .ToListAsync(cancellationToken);
        return new WordRingsAchievementCounters(
            codes.Count(code => code.StartsWith(CorrectEventPrefix, StringComparison.Ordinal)),
            codes.Count(code => code.StartsWith(BlueEventPrefix, StringComparison.Ordinal)),
            codes.Count(code => code.StartsWith(YellowEventPrefix, StringComparison.Ordinal)),
            codes.Count(code => code.StartsWith(RedEventPrefix, StringComparison.Ordinal)),
            codes.Count(code => code.StartsWith(HostedGameEventPrefix, StringComparison.Ordinal)));
    }

    private async Task<bool> HasThreeConsecutiveOutsideCorrectAsync(
        PlayerAchievementIdentity identity,
        string scopeHash,
        CancellationToken cancellationToken)
    {
        var prefix = $"{PlacementEventPrefix}{scopeHash}:";
        var recent = await QueryForIdentity(db, identity)
            .AsNoTracking()
            .Where(item => item.AchievementCode.StartsWith(prefix))
            .OrderByDescending(item => item.UnlockedAtUtc)
            .ThenByDescending(item => item.Id)
            .Take(3)
            .Select(item => item.AchievementCode)
            .ToListAsync(cancellationToken);
        return recent.Count == 3 && recent.All(code => code.Contains(":O:", StringComparison.Ordinal));
    }

    private async Task<bool> AddProgressEventIfMissingAsync(
        PlayerAchievementIdentity identity,
        string eventCode,
        CancellationToken cancellationToken)
    {
        if (!identity.IsValid || eventCode.Length > 64)
        {
            return false;
        }

        if (db.ChangeTracker.Entries<PlayerAchievement>()
                .Select(entry => entry.Entity)
                .Any(item => item.AchievementCode == eventCode && MatchesIdentity(item, identity)) ||
            await QueryForIdentity(db, identity)
                .AsNoTracking()
                .AnyAsync(item => item.AchievementCode == eventCode, cancellationToken))
        {
            return false;
        }

        db.PlayerAchievements.Add(new PlayerAchievement
        {
            AccountId = identity.AccountId,
            HostId = identity.AccountId is null ? identity.HostId : null,
            PlayerKey = identity.AccountId is null ? identity.PlayerKey : null,
            AchievementCode = eventCode,
            UnlockedAtUtc = DateTime.UtcNow
        });
        return true;
    }

    private async Task UnlockIdentityAsync(
        PlayerAchievementIdentity identity,
        string playerName,
        string achievementCode,
        CancellationToken cancellationToken)
    {
        var service = new PlayerAchievementService(db);
        if (identity.AccountId is not null)
        {
            await service.UnlockAccountAsync(identity.AccountId, achievementCode, cancellationToken: cancellationToken);
        }
        else
        {
            await service.UnlockPlayerAsync(
                null,
                identity.HostId,
                playerName,
                achievementCode,
                cancellationToken: cancellationToken);
        }
    }

    private static IEnumerable<PlayerAchievementIdentity> BuildIdentities(
        string? accountId,
        string? hostId,
        string playerName)
    {
        var fallback = PlayerAchievementIdentity.Create(null, hostId, playerName);
        if (fallback.IsValid)
        {
            yield return fallback;
        }

        var account = PlayerAchievementIdentity.Create(accountId, null, null);
        if (account.IsValid)
        {
            yield return account;
        }
    }

    private static IQueryable<PlayerAchievement> QueryForIdentity(
        QuizDbContext db,
        PlayerAchievementIdentity identity) =>
        identity.AccountId is not null
            ? db.PlayerAchievements.Where(item => item.AccountId == identity.AccountId)
            : db.PlayerAchievements.Where(item =>
                item.AccountId == null && item.HostId == identity.HostId && item.PlayerKey == identity.PlayerKey);

    private static bool MatchesIdentity(PlayerAchievement achievement, PlayerAchievementIdentity identity) =>
        identity.AccountId is not null
            ? achievement.AccountId == identity.AccountId
            : achievement.AccountId is null &&
              achievement.HostId == identity.HostId &&
              achievement.PlayerKey == identity.PlayerKey;

    private static string CanonicalMembership(string? membership)
    {
        var value = (membership ?? string.Empty).Trim().ToUpperInvariant();
        return new string(value
            .Where(character => character is 'A' or 'B' or 'C')
            .Distinct()
            .OrderBy(character => character)
            .ToArray());
    }

    private static string ShortHash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes.AsSpan(0, 6));
    }
}

internal sealed record WordRingsAchievementCounters(
    int CorrectPlacements = 0,
    int BlueCorrectPlacements = 0,
    int YellowCorrectPlacements = 0,
    int RedCorrectPlacements = 0,
    int HostedGames = 0);
