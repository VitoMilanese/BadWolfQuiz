using System.Runtime.CompilerServices;
using BadWolfQuiz.Game.Runtime;

namespace BadWolfQuiz.Web.Services;

public static class PlayerAchievementRuntimeState
{
    private static readonly ConditionalWeakTable<GameSessionRegistration, RuntimeState> States = new();

    public static bool LinkPlayerAccount(
        GameSessionRegistration game,
        GamePlayerId playerId,
        string? accountId)
    {
        ArgumentNullException.ThrowIfNull(game);
        if (string.IsNullOrWhiteSpace(accountId) ||
            game.Session.AllPlayers.All(player => player.Id != playerId))
        {
            return false;
        }

        var state = States.GetOrCreateValue(game);
        lock (state)
        {
            if (state.PlayerAccountIds.TryGetValue(playerId, out var existing))
            {
                return string.Equals(existing, accountId, StringComparison.Ordinal);
            }

            state.PlayerAccountIds[playerId] = accountId.Trim();
        }

        game.MarkPersistenceChanged();
        return true;
    }

    public static string? GetPlayerAccountId(
        GameSessionRegistration game,
        GamePlayerId playerId)
    {
        ArgumentNullException.ThrowIfNull(game);
        if (!States.TryGetValue(game, out var state))
        {
            return null;
        }

        lock (state)
        {
            return state.PlayerAccountIds.TryGetValue(playerId, out var accountId)
                ? accountId
                : null;
        }
    }

    public static void MarkAllInWager(
        GameSessionRegistration game,
        int sourceQuestionId,
        GamePlayerId playerId)
    {
        ArgumentNullException.ThrowIfNull(game);
        var state = States.GetOrCreateValue(game);
        var added = false;
        lock (state)
        {
            added = state.AllInWagers.Add(new AllInWagerSnapshot(playerId, sourceQuestionId));
        }

        if (added)
        {
            game.MarkPersistenceChanged();
        }
    }

    public static bool WasAllInWager(
        GameSessionRegistration game,
        int sourceQuestionId,
        GamePlayerId playerId)
    {
        ArgumentNullException.ThrowIfNull(game);
        if (!States.TryGetValue(game, out var state))
        {
            return false;
        }

        lock (state)
        {
            return state.AllInWagers.Contains(new AllInWagerSnapshot(playerId, sourceQuestionId));
        }
    }

    internal static void RecordQuestionOpened(
        GameSessionRegistration game,
        RuntimeQuestion question)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(question);
        if (question.SelectedByPlayerId is not { } selectedByPlayerId)
        {
            return;
        }

        var state = States.GetOrCreateValue(game);
        lock (state)
        {
            if (state.RoundFirstPicks.ContainsKey(question.SourceRoundId))
            {
                return;
            }

            var players = game.Session.Players.ToArray();
            var selected = players.SingleOrDefault(player => player.Id == selectedByPlayerId);
            if (selected is null)
            {
                return;
            }

            var minimumScore = players.Length == 0
                ? selected.Score
                : players.Min(player => player.Score);
            state.RoundFirstPicks[question.SourceRoundId] = new RoundFirstPickSnapshot(
                question.SourceRoundId,
                selectedByPlayerId,
                selected.Score == minimumScore,
                players.Select(player => player.Id).ToArray());
        }
    }

    public static RoundFirstPickSnapshot? GetRoundFirstPick(
        GameSessionRegistration game,
        int sourceRoundId)
    {
        ArgumentNullException.ThrowIfNull(game);
        if (!States.TryGetValue(game, out var state))
        {
            return null;
        }

        lock (state)
        {
            return state.RoundFirstPicks.TryGetValue(sourceRoundId, out var firstPick)
                ? firstPick
                : null;
        }
    }

    public static void RecordBuzzerRace(
        GameSessionRegistration game,
        BuzzerRaceSnapshot race)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(race);

        var question = game.Session.Board.Questions.SingleOrDefault(item =>
            item.SourceQuestionId == race.SourceQuestionId);
        if (question is null)
        {
            return;
        }

        var state = States.GetOrCreateValue(game);
        var changed = false;
        lock (state)
        {
            if (!state.BuzzerPressedPlayerIdsByRound.TryGetValue(
                    question.SourceRoundId,
                    out var playerIds))
            {
                playerIds = [];
                state.BuzzerPressedPlayerIdsByRound[question.SourceRoundId] = playerIds;
            }

            changed |= playerIds.Add(race.WinnerPlayerId);
            foreach (var latePlayer in race.LatePlayers)
            {
                changed |= playerIds.Add(latePlayer.PlayerId);
            }
        }

        if (changed)
        {
            game.MarkPersistenceChanged();
        }
    }

    public static bool HasBuzzerPressInRound(
        GameSessionRegistration game,
        int sourceRoundId,
        GamePlayerId playerId)
    {
        ArgumentNullException.ThrowIfNull(game);
        if (!States.TryGetValue(game, out var state))
        {
            return false;
        }

        lock (state)
        {
            return state.BuzzerPressedPlayerIdsByRound.TryGetValue(
                    sourceRoundId,
                    out var playerIds) &&
                playerIds.Contains(playerId);
        }
    }

    public static void ResetForRestart(GameSessionRegistration game)
    {
        ArgumentNullException.ThrowIfNull(game);
        if (!States.TryGetValue(game, out var state))
        {
            return;
        }

        lock (state)
        {
            state.AllInWagers.Clear();
            state.RoundFirstPicks.Clear();
            state.BuzzerPressedPlayerIdsByRound.Clear();
        }
    }

    public static PlayerAchievementRuntimeSnapshot Capture(GameSessionRegistration game)
    {
        ArgumentNullException.ThrowIfNull(game);
        if (!States.TryGetValue(game, out var state))
        {
            return PlayerAchievementRuntimeSnapshot.Empty;
        }

        lock (state)
        {
            return new PlayerAchievementRuntimeSnapshot(
                state.PlayerAccountIds
                    .OrderBy(item => item.Key.Value)
                    .Select(item => new PlayerAccountLinkSnapshot(item.Key, item.Value))
                    .ToArray(),
                state.AllInWagers
                    .OrderBy(item => item.SourceQuestionId)
                    .ThenBy(item => item.PlayerId.Value)
                    .ToArray(),
                state.BuzzerPressedPlayerIdsByRound
                    .OrderBy(item => item.Key)
                    .Select(item => new RoundBuzzerPressSnapshot(
                        item.Key,
                        item.Value.OrderBy(playerId => playerId.Value).ToArray()))
                    .ToArray(),
                state.RoundFirstPicks.Values
                    .OrderBy(item => item.SourceRoundId)
                    .ToArray());
        }
    }

    public static void Restore(
        GameSessionRegistration game,
        PlayerAchievementRuntimeSnapshot? snapshot)
    {
        ArgumentNullException.ThrowIfNull(game);
        if (snapshot is null)
        {
            return;
        }

        var validPlayerIds = game.Session.AllPlayers
            .Select(player => player.Id)
            .ToHashSet();
        var validQuestions = game.Session.Board.Questions
            .ToDictionary(question => question.SourceQuestionId);
        var validRoundIds = validQuestions.Values
            .Select(question => question.SourceRoundId)
            .ToHashSet();
        var state = States.GetOrCreateValue(game);

        lock (state)
        {
            state.PlayerAccountIds.Clear();
            state.AllInWagers.Clear();
            state.RoundFirstPicks.Clear();
            state.BuzzerPressedPlayerIdsByRound.Clear();

            foreach (var link in snapshot.PlayerAccounts ?? [])
            {
                if (validPlayerIds.Contains(link.PlayerId) &&
                    !string.IsNullOrWhiteSpace(link.AccountId))
                {
                    state.PlayerAccountIds.TryAdd(link.PlayerId, link.AccountId.Trim());
                }
            }

            foreach (var wager in snapshot.AllInWagers ?? [])
            {
                if (validPlayerIds.Contains(wager.PlayerId) &&
                    validQuestions.ContainsKey(wager.SourceQuestionId))
                {
                    state.AllInWagers.Add(wager);
                }
            }

            foreach (var round in snapshot.BuzzerPresses ?? [])
            {
                if (!validRoundIds.Contains(round.SourceRoundId))
                {
                    continue;
                }

                var playerIds = round.PlayerIds
                    .Where(validPlayerIds.Contains)
                    .ToHashSet();
                if (playerIds.Count > 0)
                {
                    state.BuzzerPressedPlayerIdsByRound[round.SourceRoundId] = playerIds;
                }
            }

            foreach (var firstPick in snapshot.RoundFirstPicks ?? [])
            {
                if (!validRoundIds.Contains(firstPick.SourceRoundId) ||
                    !validPlayerIds.Contains(firstPick.PlayerId) ||
                    state.RoundFirstPicks.ContainsKey(firstPick.SourceRoundId))
                {
                    continue;
                }

                state.RoundFirstPicks[firstPick.SourceRoundId] = firstPick with
                {
                    PresentPlayerIds = firstPick.PresentPlayerIds
                        .Where(validPlayerIds.Contains)
                        .Distinct()
                        .ToArray()
                };
            }
        }
    }

    private sealed class RuntimeState
    {
        public Dictionary<GamePlayerId, string> PlayerAccountIds { get; } = [];
        public HashSet<AllInWagerSnapshot> AllInWagers { get; } = [];
        public Dictionary<int, RoundFirstPickSnapshot> RoundFirstPicks { get; } = [];
        public Dictionary<int, HashSet<GamePlayerId>> BuzzerPressedPlayerIdsByRound { get; } = [];
    }
}

public sealed record PlayerAchievementRuntimeSnapshot(
    IReadOnlyList<PlayerAccountLinkSnapshot>? PlayerAccounts,
    IReadOnlyList<AllInWagerSnapshot>? AllInWagers,
    IReadOnlyList<RoundBuzzerPressSnapshot>? BuzzerPresses = null,
    IReadOnlyList<RoundFirstPickSnapshot>? RoundFirstPicks = null)
{
    public static PlayerAchievementRuntimeSnapshot Empty { get; } = new([], [], [], []);
}

public sealed record PlayerAccountLinkSnapshot(
    GamePlayerId PlayerId,
    string AccountId);

public sealed record AllInWagerSnapshot(
    GamePlayerId PlayerId,
    int SourceQuestionId);

public sealed record RoundBuzzerPressSnapshot(
    int SourceRoundId,
    IReadOnlyList<GamePlayerId> PlayerIds);

public sealed record RoundFirstPickSnapshot(
    int SourceRoundId,
    GamePlayerId PlayerId,
    bool WasLowestScore,
    IReadOnlyList<GamePlayerId> PresentPlayerIds);
