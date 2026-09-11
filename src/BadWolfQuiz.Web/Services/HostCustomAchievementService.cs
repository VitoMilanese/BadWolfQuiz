using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace BadWolfQuiz.Web.Services;

public sealed class HostCustomAchievementService(QuizDbContext db)
{
    public const int MaximumTags = 20;
    public const int MaximumArtworkBytes = 2 * 1024 * 1024;
    public const int ArtworkSize = 250;
    public const int MaximumTarget = 1_000_000;
    public const string CodePrefix = "Custom:";

    public async Task<IReadOnlyList<HostCustomAchievementProgress>> LoadForPlayerAsync(
        string? hostId,
        string playerName,
        string? currentGameCode,
        string? accountId,
        CancellationToken cancellationToken = default)
    {
        var applicableHostIds = await ResolveApplicableHostsAsync(hostId, accountId, cancellationToken);
        if (applicableHostIds.Count == 0)
        {
            return [];
        }

        int? currentGameSessionId = null;
        if (!string.IsNullOrWhiteSpace(currentGameCode))
        {
            currentGameSessionId = await db.GameSessions
                .IgnoreQueryFilters()
                .Where(session => session.PublicCode == currentGameCode)
                .Select(session => (int?)session.Id)
                .SingleOrDefaultAsync(cancellationToken);
        }

        var definitions = await db.HostCustomAchievements
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(item => item.Tags)
            .Where(item => applicableHostIds.Contains(item.HostId) && !item.IsDeleted)
            .OrderBy(item => item.CreatedAtUtc)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);
        if (definitions.Count == 0)
        {
            return [];
        }

        var identity = PlayerAchievementIdentity.Create(accountId, hostId, playerName);
        if (!identity.IsValid)
        {
            return [];
        }

        var existingUnlocks = await QueryUnlocks(identity)
            .AsNoTracking()
            .Where(item => item.AchievementCode.StartsWith(CodePrefix))
            .ToListAsync(cancellationToken);
        var unlockedByCode = existingUnlocks
            .GroupBy(item => item.AchievementCode, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.OrderBy(item => item.UnlockedAtUtc).First(), StringComparer.Ordinal);

        var progress = new List<HostCustomAchievementProgress>(definitions.Count);
        foreach (var hostGroup in definitions.GroupBy(item => item.HostId, StringComparer.Ordinal))
        {
            var contributions = await LoadContributionsAsync(
                hostGroup.Key,
                playerName,
                accountId,
                cancellationToken);

            foreach (var definition in hostGroup)
            {
                var normalizedTags = definition.Tags
                    .Select(tag => tag.NormalizedName)
                    .ToHashSet(StringComparer.Ordinal);
                var matches = contributions
                    .Where(item => item.Tags.Overlaps(normalizedTags))
                    .OrderBy(item => item.CreatedAtUtc)
                    .ThenBy(item => item.ResultId)
                    .ToArray();
                var code = BuildCode(definition.Id);
                var isUnlocked = unlockedByCode.TryGetValue(code, out var unlock);

                if (!isUnlocked && matches.Length >= definition.Target)
                {
                    var source = matches[definition.Target - 1];
                    unlock = new PlayerAchievement
                    {
                        AccountId = identity.AccountId,
                        HostId = identity.AccountId is null ? identity.HostId : null,
                        PlayerKey = identity.AccountId is null ? identity.PlayerKey : null,
                        AchievementCode = code,
                        UnlockedAtUtc = source.CreatedAtUtc,
                        SourceGameSessionId = source.GameSessionId
                    };
                    db.PlayerAchievements.Add(unlock);
                    unlockedByCode[code] = unlock;
                    isUnlocked = true;
                }

                progress.Add(new HostCustomAchievementProgress(
                    definition.Id,
                    definition.HostId,
                    code,
                    definition.Name,
                    definition.Description,
                    Math.Min(matches.Length, definition.Target),
                    definition.Target,
                    isUnlocked,
                    isUnlocked && currentGameSessionId.HasValue && unlock!.SourceGameSessionId == currentGameSessionId.Value,
                    $"/AchievementArtwork/{definition.Id:D}"));
            }
        }

        if (db.ChangeTracker.HasChanges())
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return progress
            .OrderByDescending(item => item.IsUnlocked)
            .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    public async Task<IReadOnlyList<HostCustomAchievement>> LoadDefinitionsAsync(
        string hostId,
        CancellationToken cancellationToken = default) =>
        await db.HostCustomAchievements
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(item => item.Tags)
            .Where(item => item.HostId == hostId && !item.IsDeleted)
            .OrderBy(item => item.Name)
            .ToListAsync(cancellationToken);

    public async Task<HostCustomAchievement?> LoadDefinitionAsync(
        Guid id,
        string hostId,
        bool includeDeleted = false,
        CancellationToken cancellationToken = default) =>
        await db.HostCustomAchievements
            .IgnoreQueryFilters()
            .Include(item => item.Tags)
            .SingleOrDefaultAsync(
                item => item.Id == id && item.HostId == hostId && (includeDeleted || !item.IsDeleted),
                cancellationToken);

    public async Task<HostCustomAchievement?> LoadDefinitionByCodeAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        if (!TryParseCode(code, out var id))
        {
            return null;
        }

        return await db.HostCustomAchievements
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(item => item.Tags)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, string hostId, CancellationToken cancellationToken = default)
    {
        var entity = await db.HostCustomAchievements
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(item => item.Id == id && item.HostId == hostId && !item.IsDeleted, cancellationToken);
        if (entity is null)
        {
            return;
        }

        entity.IsDeleted = true;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public static string BuildCode(Guid id) => $"{CodePrefix}{id:N}";

    public static bool TryParseCode(string? code, out Guid id)
    {
        id = default;
        return !string.IsNullOrWhiteSpace(code) &&
            code.StartsWith(CodePrefix, StringComparison.Ordinal) &&
            Guid.TryParseExact(code[CodePrefix.Length..], "N", out id);
    }

    public static IReadOnlyList<HostCustomAchievementTagInput> NormalizeTags(IEnumerable<string>? values)
    {
        var result = new List<HostCustomAchievementTagInput>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var raw in values ?? [])
        {
            var name = raw.Trim();
            if (name.Length == 0)
            {
                continue;
            }

            if (name.Length > 100)
            {
                throw new ArgumentException("Tags cannot exceed 100 characters.", nameof(values));
            }

            var normalized = NormalizeTag(name);
            if (seen.Add(normalized))
            {
                result.Add(new HostCustomAchievementTagInput(name, normalized));
            }
        }

        if (result.Count is < 1 or > MaximumTags)
        {
            throw new ArgumentException($"Choose between 1 and {MaximumTags} tags.", nameof(values));
        }

        return result;
    }

    public static string NormalizeTag(string value) => value.Trim().ToUpperInvariant();

    public static bool HasPngSignature(ReadOnlySpan<byte> bytes) =>
        bytes.Length >= 24 &&
        bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47 &&
        bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A;

    public static bool HasRequiredPngDimensions(ReadOnlySpan<byte> bytes)
    {
        if (!HasPngSignature(bytes) || bytes.Length < 24)
        {
            return false;
        }

        var width = ReadBigEndianInt32(bytes.Slice(16, 4));
        var height = ReadBigEndianInt32(bytes.Slice(20, 4));
        return width == ArtworkSize && height == ArtworkSize;
    }

    private static int ReadBigEndianInt32(ReadOnlySpan<byte> bytes) =>
        (bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3];

    private async Task<HashSet<string>> ResolveApplicableHostsAsync(
        string? hostId,
        string? accountId,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(hostId))
        {
            return new HashSet<string>([hostId.Trim()], StringComparer.Ordinal);
        }

        if (string.IsNullOrWhiteSpace(accountId))
        {
            return [];
        }

        return (await db.PlayerGameAccountLinks
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(link =>
                    link.AccountId == accountId &&
                    link.Player.CountsForAchievementHistory &&
                    link.Player.Session.Status == GameSessionStatus.Finished &&
                    link.Player.Session.HostId != null)
                .Select(link => link.Player.Session.HostId!)
                .Distinct()
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.Ordinal);
    }

    private async Task<IReadOnlyList<Contribution>> LoadContributionsAsync(
        string hostId,
        string playerName,
        string? accountId,
        CancellationToken cancellationToken)
    {
        int[] playerIds;
        if (!string.IsNullOrWhiteSpace(accountId))
        {
            playerIds = await db.PlayerGameAccountLinks
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(link =>
                    link.AccountId == accountId &&
                    link.Player.CountsForAchievementHistory &&
                    link.Player.Session.Status == GameSessionStatus.Finished &&
                    link.Player.Session.HostId == hostId)
                .Select(link => link.GamePlayerId)
                .Distinct()
                .ToArrayAsync(cancellationToken);
        }
        else
        {
            var key = PlayerAchievementService.NormalizePlayerKey(playerName);
            var candidates = await db.GamePlayers
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(player =>
                    player.Session.HostId == hostId &&
                    player.Session.Status == GameSessionStatus.Finished &&
                    player.CountsForAchievementHistory)
                .Select(player => new { player.Id, player.Name })
                .ToListAsync(cancellationToken);
            playerIds = candidates
                .Where(player => string.Equals(PlayerAchievementService.NormalizePlayerKey(player.Name), key, StringComparison.Ordinal))
                .Select(player => player.Id)
                .ToArray();
        }

        if (playerIds.Length == 0)
        {
            return [];
        }

        var answers = await db.PlayerQuestionResults
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(result => playerIds.Contains(result.GamePlayerId) && result.IsCorrect == true)
            .Select(result => new
            {
                result.Id,
                result.CreatedAtUtc,
                result.GameQuestion.GameSessionId,
                result.GameQuestion.QuizQuestionId
            })
            .ToListAsync(cancellationToken);
        if (answers.Count == 0)
        {
            return [];
        }

        var questionIds = answers.Select(item => item.QuizQuestionId).Distinct().ToArray();
        var tags = await db.QuizQuestionTags
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(tag => questionIds.Contains(tag.QuizQuestionId))
            .Select(tag => new { tag.QuizQuestionId, tag.NormalizedName })
            .ToListAsync(cancellationToken);
        var tagsByQuestion = tags
            .GroupBy(tag => tag.QuizQuestionId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(tag => tag.NormalizedName).ToHashSet(StringComparer.Ordinal));

        return answers
            .Select(answer => new Contribution(
                answer.Id,
                answer.GameSessionId,
                answer.CreatedAtUtc,
                tagsByQuestion.TryGetValue(answer.QuizQuestionId, out var questionTags)
                    ? questionTags
                    : new HashSet<string>(StringComparer.Ordinal)))
            .ToArray();
    }

    private IQueryable<PlayerAchievement> QueryUnlocks(PlayerAchievementIdentity identity) =>
        identity.AccountId is not null
            ? db.PlayerAchievements.Where(item => item.AccountId == identity.AccountId)
            : db.PlayerAchievements.Where(item =>
                item.AccountId == null && item.HostId == identity.HostId && item.PlayerKey == identity.PlayerKey);

    private sealed record Contribution(
        int ResultId,
        int GameSessionId,
        DateTime CreatedAtUtc,
        HashSet<string> Tags);
}

public sealed record HostCustomAchievementProgress(
    Guid Id,
    string HostId,
    string Code,
    string Name,
    string Description,
    int Progress,
    int Target,
    bool IsUnlocked,
    bool IsNewInCurrentGame,
    string ArtworkUrl);

public sealed record HostCustomAchievementTagInput(string Name, string NormalizedName);
