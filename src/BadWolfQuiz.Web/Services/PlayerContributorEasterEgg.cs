using System.Globalization;
using BadWolfQuiz.Game.Runtime;

namespace BadWolfQuiz.Web.Services;

public static class PlayerContributorEasterEgg
{
    public static PlayerContributorJoinIdentity ResolveForJoin(
        IEnumerable<GamePlayer> existingPlayers,
        string requestedName,
        int dayOfMonth)
    {
        ArgumentNullException.ThrowIfNull(existingPlayers);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestedName);

        var originalName = requestedName.Trim();
        var existingPlayer = existingPlayers.FirstOrDefault(player =>
            string.Equals(
                player.OriginalName,
                originalName,
                StringComparison.OrdinalIgnoreCase));

        if (existingPlayer is not null)
        {
            return new PlayerContributorJoinIdentity(
                existingPlayer.Name,
                existingPlayer.OriginalName,
                existingPlayer.Name,
                ActivateTemporaryPrivileges: false);
        }

        var alias = ResolveAlias(originalName, dayOfMonth);
        return new PlayerContributorJoinIdentity(
            alias.DisplayName,
            originalName,
            alias.DisplayName,
            alias.IsActive);
    }

    public static PlayerContributorAlias ResolveAlias(
        string requestedName,
        int dayOfMonth)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestedName);

        var originalName = requestedName.Trim();
        if (dayOfMonth is < 1 or > 31)
        {
            return new PlayerContributorAlias(originalName, false);
        }

        var prefixes = dayOfMonth < 10
            ? new[]
            {
                dayOfMonth.ToString("00", CultureInfo.InvariantCulture),
                dayOfMonth.ToString(CultureInfo.InvariantCulture)
            }
            : new[] { dayOfMonth.ToString(CultureInfo.InvariantCulture) };

        foreach (var prefix in prefixes)
        {
            if (!originalName.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            if (originalName.Length > prefix.Length &&
                char.IsDigit(originalName[prefix.Length]))
            {
                continue;
            }

            var displayName = originalName[prefix.Length..].TrimStart();
            if (!string.IsNullOrWhiteSpace(displayName))
            {
                return new PlayerContributorAlias(displayName, true);
            }
        }

        return new PlayerContributorAlias(originalName, false);
    }
}

public static class PlayerContributorAccess
{
    public static bool IsContributor(FooterOptions options, GamePlayer player)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(player);

        return player.HasTemporaryContributorPrivileges ||
               ContributorRecognition.IsContributor(options, player.OriginalName);
    }
}

public sealed record PlayerContributorAlias(string DisplayName, bool IsActive);

public sealed record PlayerContributorJoinIdentity(
    string JoinName,
    string OriginalName,
    string DisplayName,
    bool ActivateTemporaryPrivileges);
