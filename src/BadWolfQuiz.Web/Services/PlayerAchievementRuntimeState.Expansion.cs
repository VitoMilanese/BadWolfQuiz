using BadWolfQuiz.Game.Definitions;
using BadWolfQuiz.Game.Runtime;

namespace BadWolfQuiz.Web.Services;

public static partial class PlayerAchievementRuntimeState
{
    public static void RecordNewPlayerJoined(
        GameSessionRegistration game,
        GamePlayerId playerId)
    {
        ArgumentNullException.ThrowIfNull(game);
        if (game.Session.Status != GameSessionStatus.Running ||
            game.Session.CurrentRoundIndex < 1)
        {
            return;
        }

        var state = States.GetOrCreateValue(game);
        var changed = false;
        lock (state)
        {
            changed = AddPendingLocked(state, playerId, "LateJoiner");
        }

        if (changed)
        {
            game.MarkPersistenceChanged();
        }
    }

    public static void RecordPlayerKicked(
        GameSessionRegistration game,
        GamePlayerId playerId)
    {
        ArgumentNullException.ThrowIfNull(game);
        var state = States.GetOrCreateValue(game);
        var changed = false;
        lock (state)
        {
            changed = state.KickedPlayerIds.Add(playerId);
        }

        if (changed)
        {
            game.MarkPersistenceChanged();
        }
    }

    public static void RecordKickedPlayerReturned(
        GameSessionRegistration game,
        GamePlayerId playerId)
    {
        ArgumentNullException.ThrowIfNull(game);
        var state = States.GetOrCreateValue(game);
        var changed = false;
        lock (state)
        {
            if (state.KickedPlayerIds.Remove(playerId))
            {
                AddPendingLocked(state, playerId, "KickedAndReturned");
                changed = true;
            }
        }

        if (changed)
        {
            game.MarkPersistenceChanged();
        }
    }

    public static void RecordPlayerDisconnected(
        GameSessionRegistration game,
        GamePlayerId playerId)
    {
        ArgumentNullException.ThrowIfNull(game);
        var state = States.GetOrCreateValue(game);
        var changed = false;
        lock (state)
        {
            if (game.Session.CurrentRoundIndex <= 1 &&
                state.FirstRoundParticipantIds.Contains(playerId))
            {
                changed |= state.DisconnectedBeforeThirdRoundPlayerIds.Add(playerId);
            }

            changed |= state.ThirdRoundReturnCandidateIds.Remove(playerId);
        }

        if (changed)
        {
            game.MarkPersistenceChanged();
        }
    }

    public static void RecordPlayerReconnected(
        GameSessionRegistration game,
        GamePlayerId playerId)
    {
        ArgumentNullException.ThrowIfNull(game);
        if (game.Session.Status != GameSessionStatus.Running ||
            game.Session.CurrentRoundIndex != 2)
        {
            return;
        }

        var state = States.GetOrCreateValue(game);
        var changed = false;
        lock (state)
        {
            if (state.DisconnectedBeforeThirdRoundPlayerIds.Contains(playerId))
            {
                changed = state.ThirdRoundReturnCandidateIds.Add(playerId);
            }
        }

        if (changed)
        {
            game.MarkPersistenceChanged();
        }
    }

    public static bool DidCompleteFirstToThirdReturn(
        GameSessionRegistration game,
        GamePlayerId playerId)
    {
        ArgumentNullException.ThrowIfNull(game);
        if (!States.TryGetValue(game, out var state))
        {
            return false;
        }

        lock (state)
        {
            return state.ThirdRoundReturnCandidateIds.Contains(playerId);
        }
    }

    public static void MarkPendingAchievement(
        GameSessionRegistration game,
        GamePlayerId playerId,
        string achievementCode)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentException.ThrowIfNullOrWhiteSpace(achievementCode);
        var state = States.GetOrCreateValue(game);
        var changed = false;
        lock (state)
        {
            changed = AddPendingLocked(state, playerId, achievementCode);
        }

        if (changed)
        {
            game.MarkPersistenceChanged();
        }
    }

    public static IReadOnlyList<string> GetPendingAchievementCodes(
        GameSessionRegistration game,
        GamePlayerId playerId)
    {
        ArgumentNullException.ThrowIfNull(game);
        if (!States.TryGetValue(game, out var state))
        {
            return [];
        }

        lock (state)
        {
            return state.PendingUnlocks
                .Where(item => item.PlayerId == playerId)
                .Select(item => item.AchievementCode)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(code => code, StringComparer.Ordinal)
                .ToArray();
        }
    }

    private static bool RecordCloseBuzzerAchievementsLocked(
        RuntimeState state,
        BuzzerRaceSnapshot race)
    {
        var second = race.LatePlayers
            .OrderBy(item => item.DelayMilliseconds)
            .FirstOrDefault();
        if (second is null || second.DelayMilliseconds >= 50)
        {
            return false;
        }

        var changed = AddPendingLocked(
            state,
            race.WinnerPlayerId,
            "BuzzerPhotoFinishFirst");
        changed |= AddPendingLocked(
            state,
            second.PlayerId,
            "BuzzerPhotoFinishSecond");
        return changed;
    }

    private static void RecordFirstRoundParticipantsLocked(
        GameSessionRegistration game,
        RuntimeQuestion question,
        RuntimeState state)
    {
        var firstRoundId = game.Session.Quiz.Rounds
            .OrderBy(round => round.SortOrder)
            .Select(round => (int?)round.SourceRoundId)
            .FirstOrDefault();
        if (firstRoundId != question.SourceRoundId)
        {
            return;
        }

        foreach (var player in game.Session.Players)
        {
            state.FirstRoundParticipantIds.Add(player.Id);
        }
    }

    private static bool AddPendingLocked(
        RuntimeState state,
        GamePlayerId playerId,
        string achievementCode) =>
        state.PendingUnlocks.Add(new PendingAchievementSnapshot(
            playerId,
            achievementCode));

    private static void ResetExpansionLocked(RuntimeState state)
    {
        state.PendingUnlocks.Clear();
        state.KickedPlayerIds.Clear();
        state.FirstRoundParticipantIds.Clear();
        state.DisconnectedBeforeThirdRoundPlayerIds.Clear();
        state.ThirdRoundReturnCandidateIds.Clear();
    }

    private static PlayerAchievementRuntimeSnapshot CaptureExpansionLocked(
        RuntimeState state,
        PlayerAchievementRuntimeSnapshot snapshot) => snapshot with
    {
        PendingUnlocks = state.PendingUnlocks
            .OrderBy(item => item.PlayerId.Value)
            .ThenBy(item => item.AchievementCode, StringComparer.Ordinal)
            .ToArray(),
        KickedPlayerIds = state.KickedPlayerIds.OrderBy(id => id.Value).ToArray(),
        FirstRoundParticipantIds = state.FirstRoundParticipantIds.OrderBy(id => id.Value).ToArray(),
        DisconnectedBeforeThirdRoundPlayerIds = state.DisconnectedBeforeThirdRoundPlayerIds
            .OrderBy(id => id.Value)
            .ToArray(),
        ThirdRoundReturnCandidateIds = state.ThirdRoundReturnCandidateIds
            .OrderBy(id => id.Value)
            .ToArray()
    };

    private static void RestoreExpansionLocked(
        RuntimeState state,
        PlayerAchievementRuntimeSnapshot snapshot,
        HashSet<GamePlayerId> validPlayerIds)
    {
        foreach (var pending in snapshot.PendingUnlocks ?? [])
        {
            if (validPlayerIds.Contains(pending.PlayerId) &&
                !string.IsNullOrWhiteSpace(pending.AchievementCode))
            {
                state.PendingUnlocks.Add(pending);
            }
        }

        RestorePlayerIds(snapshot.KickedPlayerIds, state.KickedPlayerIds, validPlayerIds);
        RestorePlayerIds(snapshot.FirstRoundParticipantIds, state.FirstRoundParticipantIds, validPlayerIds);
        RestorePlayerIds(
            snapshot.DisconnectedBeforeThirdRoundPlayerIds,
            state.DisconnectedBeforeThirdRoundPlayerIds,
            validPlayerIds);
        RestorePlayerIds(
            snapshot.ThirdRoundReturnCandidateIds,
            state.ThirdRoundReturnCandidateIds,
            validPlayerIds);
    }

    private static void RestorePlayerIds(
        IReadOnlyList<GamePlayerId>? source,
        HashSet<GamePlayerId> target,
        HashSet<GamePlayerId> validPlayerIds)
    {
        foreach (var playerId in source ?? [])
        {
            if (validPlayerIds.Contains(playerId))
            {
                target.Add(playerId);
            }
        }
    }

    private sealed partial class RuntimeState
    {
        public HashSet<PendingAchievementSnapshot> PendingUnlocks { get; } = [];
        public HashSet<GamePlayerId> KickedPlayerIds { get; } = [];
        public HashSet<GamePlayerId> FirstRoundParticipantIds { get; } = [];
        public HashSet<GamePlayerId> DisconnectedBeforeThirdRoundPlayerIds { get; } = [];
        public HashSet<GamePlayerId> ThirdRoundReturnCandidateIds { get; } = [];
    }
}

public sealed partial record PlayerAchievementRuntimeSnapshot
{
    public IReadOnlyList<PendingAchievementSnapshot>? PendingUnlocks { get; init; }
    public IReadOnlyList<GamePlayerId>? KickedPlayerIds { get; init; }
    public IReadOnlyList<GamePlayerId>? FirstRoundParticipantIds { get; init; }
    public IReadOnlyList<GamePlayerId>? DisconnectedBeforeThirdRoundPlayerIds { get; init; }
    public IReadOnlyList<GamePlayerId>? ThirdRoundReturnCandidateIds { get; init; }
}

public sealed record PendingAchievementSnapshot(
    GamePlayerId PlayerId,
    string AchievementCode);
