using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace BadWolfQuiz.Web.Services;

/// <summary>
/// Finds host-scoped anonymous achievement history that can be safely linked to a
/// current signed-in player and performs explicit host-confirmed adoption without
/// treating the current running game as finished achievement history.
/// </summary>
public sealed class PlayerAchievementHistoryConfirmationService(QuizDbContext db)
{
    public async Task<PlayerAchievementHistoryConfirmationPreview?> LoadPendingAsync(
        string? accountId,
        string? hostId,
        string playerName,
        CancellationToken cancellationToken = default)
    {
        var state = await LoadStateAsync(accountId, hostId, playerName, cancellationToken);
        if (state is null || state.HasConflictingClaim || !state.HasPendingChanges)
        {
            return null;
        }

        return new PlayerAchievementHistoryConfirmationPreview(
            state.PendingGameCount,
            state.PendingFallbackUnlocks
                .Where(item => !item.AchievementCode.StartsWith("__", StringComparison.Ordinal))
                .OrderByDescending(item => item.UnlockedAtUtc)
                .ThenByDescending(item => item.Id)
                .ToArray());
    }

    public async Task<PlayerAchievementHistoryConfirmationStatus> ConfirmAsync(
        string? accountId,
        string? hostId,
        string playerName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(accountId) ||
            string.IsNullOrWhiteSpace(hostId) ||
            PlayerAchievementService.NormalizePlayerKey(playerName).Length == 0)
        {
            return PlayerAchievementHistoryConfirmationStatus.InvalidIdentity;
        }

        var state = await LoadStateAsync(accountId, hostId, playerName, cancellationToken);
        if (state is null)
        {
            return PlayerAchievementHistoryConfirmationStatus.NothingToConfirm;
        }
        if (state.HasConflictingClaim)
        {
            return PlayerAchievementHistoryConfirmationStatus.ConflictingAccountClaim;
        }
        if (!state.HasPendingChanges)
        {
            return PlayerAchievementHistoryConfirmationStatus.NothingToConfirm;
        }

        // Reuse the existing finished-game adoption path instead of weakening it.
        // The host confirmation supplies an explicit proof that lets us use one
        // already-finished, eligible appearance as the confirmation anchor now.
        var achievementService = new PlayerAchievementService(db);
        await achievementService.AdoptHostNicknameHistoryAsync(
            state.AccountId,
            state.HostId,
            playerName,
            state.ConfirmationGameSessionId,
            cancellationToken);

        // The adoption above links old finished appearances and copies fallback
        // unlocks. Re-evaluate the account immediately so aggregate metrics based
        // on the newly linked finished history are synchronized as well.
        await achievementService.LoadForPlayerAsync(
            state.HostId,
            playerName,
            currentGameCode: null,
            accountId: state.AccountId,
            cancellationToken: cancellationToken);

        return PlayerAchievementHistoryConfirmationStatus.Confirmed;
    }

    private async Task<PlayerAchievementHistoryConfirmationState?> LoadStateAsync(
        string? accountId,
        string? hostId,
        string playerName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(accountId) || string.IsNullOrWhiteSpace(hostId))
        {
            return null;
        }

        var normalizedAccountId = accountId.Trim();
        var normalizedHostId = hostId.Trim();
        var playerKey = PlayerAchievementService.NormalizePlayerKey(playerName);
        if (playerKey.Length == 0)
        {
            return null;
        }

        var hostCandidates = await db.GamePlayers
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(player =>
                player.Session.HostId == normalizedHostId &&
                player.Session.Status == GameSessionStatus.Finished &&
                player.CountsForAchievementHistory)
            .Select(player => new PlayerAchievementHistoryConfirmationCandidate(
                player.Id,
                player.GameSessionId,
                player.Name))
            .ToListAsync(cancellationToken);
        var candidates = hostCandidates
            .Where(player => string.Equals(
                PlayerAchievementService.NormalizePlayerKey(player.Name),
                playerKey,
                StringComparison.Ordinal))
            .ToArray();
        if (candidates.Length == 0)
        {
            return null;
        }

        var candidateIds = candidates.Select(item => item.GamePlayerId).ToArray();
        var existingLinks = await db.PlayerGameAccountLinks
            .AsNoTracking()
            .Where(link => candidateIds.Contains(link.GamePlayerId))
            .Select(link => new { link.GamePlayerId, link.AccountId })
            .ToListAsync(cancellationToken);
        var hasConflictingClaim = existingLinks.Any(link =>
            !string.Equals(link.AccountId, normalizedAccountId, StringComparison.Ordinal));
        if (hasConflictingClaim)
        {
            return new PlayerAchievementHistoryConfirmationState(
                normalizedAccountId,
                normalizedHostId,
                candidates[0].GameSessionId,
                true,
                0,
                []);
        }

        var linkedPlayerIds = existingLinks
            .Select(link => link.GamePlayerId)
            .ToHashSet();
        var pendingGameCount = candidates.Count(candidate =>
            !linkedPlayerIds.Contains(candidate.GamePlayerId));
        var claimableGameIds = candidates
            .Select(candidate => candidate.GameSessionId)
            .ToHashSet();

        var fallbackUnlocks = await db.PlayerAchievements
            .AsNoTracking()
            .Where(item =>
                item.AccountId == null &&
                item.HostId == normalizedHostId &&
                item.PlayerKey == playerKey)
            .OrderBy(item => item.UnlockedAtUtc)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);
        var accountUnlocks = await db.PlayerAchievements
            .AsNoTracking()
            .Where(item => item.AccountId == normalizedAccountId)
            .ToListAsync(cancellationToken);
        var accountByCode = accountUnlocks
            .ToDictionary(item => item.AchievementCode, StringComparer.Ordinal);

        var pendingFallbackUnlocks = fallbackUnlocks
            .Where(item =>
                item.SourceGameSessionId is null ||
                claimableGameIds.Contains(item.SourceGameSessionId.Value))
            .Where(item => NeedsMerge(item, accountByCode))
            .ToArray();

        var confirmationGameSessionId = candidates
            .OrderByDescending(candidate => candidate.GameSessionId)
            .First()
            .GameSessionId;
        return new PlayerAchievementHistoryConfirmationState(
            normalizedAccountId,
            normalizedHostId,
            confirmationGameSessionId,
            false,
            pendingGameCount,
            pendingFallbackUnlocks);
    }

    private static bool NeedsMerge(
        PlayerAchievement fallback,
        IReadOnlyDictionary<string, PlayerAchievement> accountByCode)
    {
        if (!accountByCode.TryGetValue(fallback.AchievementCode, out var existing))
        {
            return true;
        }

        return fallback.UnlockedAtUtc < existing.UnlockedAtUtc ||
            fallback.UnlockedAtUtc == existing.UnlockedAtUtc &&
            existing.SourceGameSessionId is null &&
            fallback.SourceGameSessionId is not null;
    }

    private sealed record PlayerAchievementHistoryConfirmationCandidate(
        int GamePlayerId,
        int GameSessionId,
        string Name);

    private sealed record PlayerAchievementHistoryConfirmationState(
        string AccountId,
        string HostId,
        int ConfirmationGameSessionId,
        bool HasConflictingClaim,
        int PendingGameCount,
        IReadOnlyList<PlayerAchievement> PendingFallbackUnlocks)
    {
        public bool HasPendingChanges =>
            !HasConflictingClaim &&
            (PendingGameCount > 0 || PendingFallbackUnlocks.Count > 0);
    }
}

public sealed record PlayerAchievementHistoryConfirmationPreview(
    int PendingGameCount,
    IReadOnlyList<PlayerAchievement> PendingAchievements);

public enum PlayerAchievementHistoryConfirmationStatus
{
    Confirmed,
    NothingToConfirm,
    ConflictingAccountClaim,
    InvalidIdentity
}
