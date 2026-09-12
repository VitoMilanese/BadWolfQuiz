using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace BadWolfQuiz.Web.Services;

public sealed class HostCustomAchievementVisibilityService(QuizDbContext db)
{
    public async Task<IReadOnlyList<HostCustomAchievementProgress>> LoadForGameAsync(
        string? hostId,
        string playerName,
        string? currentGameCode,
        string? accountId,
        CancellationToken cancellationToken = default)
    {
        var currentHostItems = (await new HostCustomAchievementService(db).LoadForPlayerAsync(
                hostId,
                playerName,
                currentGameCode,
                accountId,
                cancellationToken))
            .ToList();

        if (string.IsNullOrWhiteSpace(accountId))
        {
            return currentHostItems;
        }

        var normalizedAccountId = accountId.Trim();
        var persistedUnlocks = await db.PlayerAchievements
            .AsNoTracking()
            .Where(item =>
                item.AccountId == normalizedAccountId &&
                item.AchievementCode.StartsWith(HostCustomAchievementService.CodePrefix))
            .OrderBy(item => item.UnlockedAtUtc)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);
        if (persistedUnlocks.Count == 0)
        {
            return currentHostItems;
        }

        var visibleCodes = currentHostItems
            .Select(item => item.Code)
            .ToHashSet(StringComparer.Ordinal);
        var missingUnlocksByCode = persistedUnlocks
            .Where(item => !visibleCodes.Contains(item.AchievementCode))
            .GroupBy(item => item.AchievementCode, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.First(),
                StringComparer.Ordinal);
        if (missingUnlocksByCode.Count == 0)
        {
            return currentHostItems;
        }

        var definitionIds = missingUnlocksByCode.Keys
            .Select(code => HostCustomAchievementService.TryParseCode(code, out var id)
                ? (Guid?)id
                : null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToArray();
        if (definitionIds.Length == 0)
        {
            return currentHostItems;
        }

        var definitions = await db.HostCustomAchievements
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(item => definitionIds.Contains(item.Id))
            .ToListAsync(cancellationToken);

        int? currentGameSessionId = null;
        if (!string.IsNullOrWhiteSpace(currentGameCode))
        {
            currentGameSessionId = await db.GameSessions
                .IgnoreQueryFilters()
                .Where(session => session.PublicCode == currentGameCode)
                .Select(session => (int?)session.Id)
                .SingleOrDefaultAsync(cancellationToken);
        }

        foreach (var definition in definitions)
        {
            var code = HostCustomAchievementService.BuildCode(definition.Id);
            if (!missingUnlocksByCode.TryGetValue(code, out var unlock))
            {
                continue;
            }

            currentHostItems.Add(new HostCustomAchievementProgress(
                definition.Id,
                definition.HostId,
                code,
                definition.Name,
                definition.Description,
                definition.Target,
                definition.Target,
                IsUnlocked: true,
                IsNewInCurrentGame: currentGameSessionId.HasValue &&
                    unlock.SourceGameSessionId == currentGameSessionId.Value,
                ArtworkUrl: $"/AchievementArtwork/{definition.Id:D}"));
        }

        return currentHostItems
            .OrderByDescending(item => item.IsUnlocked)
            .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }
}
