using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;

namespace BadWolfQuiz.Web.Services;

public sealed record WordRingsSwapBlockTransferSnapshot(
    Guid ActorId,
    Guid? CurrentPlayerId,
    IReadOnlyDictionary<Guid, string> PlayerNames,
    IReadOnlyDictionary<Guid, string[]> WordsByPlayer,
    IReadOnlyDictionary<Guid, string[]> BlockedWordsByPlayer);

public sealed class WordRingsActionCardRuntimePatch
{
    private static readonly ConcurrentDictionary<string, Lazy<WordRingsActionCardRuntimePatch>> Instances =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly FieldInfo StoreSyncField = typeof(WordRingsRoomStore)
        .GetField("_sync", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static readonly FieldInfo StoreRoomsField = typeof(WordRingsRoomStore)
        .GetField("_rooms", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static readonly FieldInfo StoreTimeProviderField = typeof(WordRingsRoomStore)
        .GetField("_timeProvider", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static readonly MethodInfo StoreAdvanceTurnMethod = typeof(WordRingsRoomStore)
        .GetMethod("AdvanceTurn", BindingFlags.Static | BindingFlags.NonPublic)!;
    private static readonly MethodInfo StoreEnsureTurnDeadlineMethod = typeof(WordRingsRoomStore)
        .GetMethod("EnsureTurnDeadline", BindingFlags.Static | BindingFlags.NonPublic)!;
    private static readonly MethodInfo StoreTouchMethod = typeof(WordRingsRoomStore)
        .GetMethod("Touch", BindingFlags.Static | BindingFlags.NonPublic)!;
    private static readonly MethodInfo StoreAddRoomEventMethod = typeof(WordRingsRoomStore)
        .GetMethod("AddRoomEvent", BindingFlags.Static | BindingFlags.NonPublic)!;
    private static readonly MethodInfo StoreCreateSnapshotMethod = typeof(WordRingsRoomStore)
        .GetMethod("CreateSnapshot", BindingFlags.Static | BindingFlags.NonPublic)!;

    private static readonly FieldInfo ActionMetaSyncField = typeof(WordRingsActionCardCoordinator)
        .GetField("_metaSync", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static readonly FieldInfo ActionRoomsField = typeof(WordRingsActionCardCoordinator)
        .GetField("_rooms", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static readonly FieldInfo PatchSyncField = typeof(WordRingsActionCardPatchCoordinator)
        .GetField("_sync", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static readonly FieldInfo PatchRoomsField = typeof(WordRingsActionCardPatchCoordinator)
        .GetField("_rooms", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private readonly WordRingsRoomStore _store;
    private readonly WordRingsRoomHostCoordinator _host;
    private readonly WordRingsActionCardCoordinator _cards;

    private WordRingsActionCardRuntimePatch(IWebHostEnvironment environment)
    {
        _store = WordRingsRoomStore.Get(environment);
        _host = WordRingsRoomHostCoordinator.Get(environment);
        _cards = WordRingsActionCardCoordinator.Get(environment);
    }

    public static WordRingsActionCardRuntimePatch Get(IWebHostEnvironment environment)
    {
        var root = Path.GetFullPath(environment.ContentRootPath);
        return Instances.GetOrAdd(
            root,
            _ => new Lazy<WordRingsActionCardRuntimePatch>(
                () => new WordRingsActionCardRuntimePatch(environment),
                LazyThreadSafetyMode.ExecutionAndPublication)).Value;
    }

    public WordRingsSwapBlockTransferSnapshot? CaptureSwapBlockTransfer(
        WordRingsRoomSnapshot before,
        int cardId)
    {
        if (cardId != (int)WordRingsActionCardKind.Swap)
        {
            return null;
        }

        var code = Normalize(before.RoomCode);
        var words = SnapshotRoomWords(code);
        var blocked = SnapshotBlockedWords(code);
        var names = before.Players.ToDictionary(player => player.Id, player => player.Name);
        return new WordRingsSwapBlockTransferSnapshot(
            before.PlayerId,
            before.CurrentPlayerId,
            names,
            words,
            blocked);
    }

    public bool CompleteSwapBlockTransfer(
        string? roomCode,
        Guid? requestedTargetPlayerId,
        string? resultTargetName,
        WordRingsSwapBlockTransferSnapshot? snapshot)
    {
        if (snapshot is null)
        {
            return false;
        }

        var code = Normalize(roomCode);
        var afterWords = SnapshotRoomWords(code);
        if (!snapshot.WordsByPlayer.TryGetValue(snapshot.ActorId, out var actorBeforeWords) ||
            !afterWords.TryGetValue(snapshot.ActorId, out var actorAfterWords))
        {
            return false;
        }

        var actorReplacement = FindReplacement(actorBeforeWords, actorAfterWords);
        if (actorReplacement is null)
        {
            return false;
        }
        var actorOutgoing = actorReplacement.Value.Before;
        var actorIncoming = actorReplacement.Value.After;

        var targetId = ResolveSwapTarget(
            snapshot,
            afterWords,
            requestedTargetPlayerId,
            resultTargetName,
            actorIncoming,
            actorOutgoing);
        if (targetId is null || !afterWords.TryGetValue(targetId.Value, out var targetAfterWords))
        {
            return false;
        }

        var actorShouldRemainBlocked = snapshot.BlockedWordsByPlayer.TryGetValue(targetId.Value, out var targetBlockedBefore) &&
                                       targetBlockedBefore.Contains(actorIncoming, StringComparer.OrdinalIgnoreCase);
        var targetShouldRemainBlocked = snapshot.BlockedWordsByPlayer.TryGetValue(snapshot.ActorId, out var actorBlockedBefore) &&
                                        actorBlockedBefore.Contains(actorOutgoing, StringComparer.OrdinalIgnoreCase);

        var metaSync = ActionMetaSyncField.GetValue(_cards)!;
        lock (metaSync)
        {
            var actionRooms = (IDictionary)ActionRoomsField.GetValue(_cards)!;
            if (!actionRooms.Contains(code))
            {
                return false;
            }

            var roomMeta = actionRooms[code]!;
            var players = (IDictionary)Get(roomMeta, "Players")!;
            if (!players.Contains(snapshot.ActorId) || !players.Contains(targetId.Value))
            {
                return false;
            }

            var changed = false;
            changed |= ApplyTransferredBlock(
                players[snapshot.ActorId]!,
                actorIncoming,
                actorAfterWords,
                actorShouldRemainBlocked,
                snapshot.CurrentPlayerId == snapshot.ActorId);
            changed |= ApplyTransferredBlock(
                players[targetId.Value]!,
                actorOutgoing,
                targetAfterWords,
                targetShouldRemainBlocked,
                snapshot.CurrentPlayerId == targetId.Value);

            if (changed)
            {
                Set(roomMeta, "Revision", Convert.ToInt64(Get(roomMeta, "Revision")) + 1);
            }
            return changed;
        }
    }

    public WordRingsActionCardImmunityResolution ResolveImmunityDecisionWithoutScore(
        WordRingsActionCardPatchCoordinator patch,
        string? roomCode,
        string? playerToken,
        long decisionId)
    {
        var caller = _host.GetRoomState(roomCode, playerToken);
        var code = Normalize(caller.RoomCode);
        var pending = GetPendingDecision(patch, code, caller.PlayerId, decisionId);

        var hostState = _host.GetHostState(code, pending.HostToken);
        if (!hostState.IsHost)
        {
            throw new WordRingsRoomException(WordRingsRoomError.NotHost);
        }

        var result = ResolveHostedFailureWithoutScore(code, pending.HostToken, pending.PlacementId);
        RemovePendingDecision(patch, code, caller.PlayerId, decisionId);
        _cards.RecordPlacementAttempt(code, pending.PlayerId, pending.Word, fullyCorrect: false);
        _ = patch.EnsureTargetReachable(code, playerToken);

        return new WordRingsActionCardImmunityResolution(
            false,
            pending.PlayerId,
            pending.PlacementId,
            result);
    }

    private Dictionary<Guid, string[]> SnapshotRoomWords(string code)
    {
        var sync = StoreSyncField.GetValue(_store)!;
        lock (sync)
        {
            var rooms = (IDictionary)StoreRoomsField.GetValue(_store)!;
            if (!rooms.Contains(code))
            {
                throw new WordRingsRoomException(WordRingsRoomError.RoomNotFound);
            }

            var room = rooms[code]!;
            var players = (IList)Get(room, "Players")!;
            return players.Cast<object>().ToDictionary(
                player => (Guid)Get(player, "Id")!,
                player => ((IList)Get(player, "RemainingWords")!).Cast<string>().ToArray());
        }
    }

    private Dictionary<Guid, string[]> SnapshotBlockedWords(string code)
    {
        var result = new Dictionary<Guid, string[]>();
        var metaSync = ActionMetaSyncField.GetValue(_cards)!;
        lock (metaSync)
        {
            var rooms = (IDictionary)ActionRoomsField.GetValue(_cards)!;
            if (!rooms.Contains(code))
            {
                return result;
            }

            var meta = rooms[code]!;
            var players = (IDictionary)Get(meta, "Players")!;
            foreach (DictionaryEntry entry in players)
            {
                var blocked = (HashSet<string>)Get(entry.Value!, "BlockedWords")!;
                result[(Guid)entry.Key] = blocked.ToArray();
            }
        }
        return result;
    }

    private static (string Before, string After)? FindReplacement(
        IReadOnlyList<string> before,
        IReadOnlyList<string> after)
    {
        if (before.Count != after.Count) return null;
        for (var index = 0; index < before.Count; index++)
        {
            if (string.Equals(before[index], after[index], StringComparison.OrdinalIgnoreCase)) continue;
            return (before[index], after[index]);
        }
        return null;
    }

    private static Guid? ResolveSwapTarget(
        WordRingsSwapBlockTransferSnapshot snapshot,
        IReadOnlyDictionary<Guid, string[]> afterWords,
        Guid? requestedTargetPlayerId,
        string? resultTargetName,
        string actorIncoming,
        string actorOutgoing)
    {
        if (requestedTargetPlayerId is Guid requested && requested != snapshot.ActorId)
        {
            return requested;
        }

        var nameMatches = snapshot.PlayerNames
            .Where(pair => pair.Key != snapshot.ActorId &&
                           string.Equals(pair.Value, resultTargetName, StringComparison.Ordinal))
            .Select(pair => pair.Key)
            .ToArray();
        if (nameMatches.Length == 1)
        {
            return nameMatches[0];
        }

        foreach (var pair in afterWords)
        {
            if (pair.Key == snapshot.ActorId || !snapshot.WordsByPlayer.TryGetValue(pair.Key, out var before))
            {
                continue;
            }

            var replacement = FindReplacement(before, pair.Value);
            if (replacement is not null &&
                string.Equals(replacement.Value.Before, actorIncoming, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(replacement.Value.After, actorOutgoing, StringComparison.OrdinalIgnoreCase))
            {
                return pair.Key;
            }
        }

        return null;
    }

    private static bool ApplyTransferredBlock(
        object playerMeta,
        string incomingWord,
        IReadOnlyCollection<string> currentWords,
        bool shouldRemainBlocked,
        bool isCurrentPlayer)
    {
        var blocked = (HashSet<string>)Get(playerMeta, "BlockedWords")!;
        var liveWords = currentWords.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var changed = blocked.RemoveWhere(word => !liveWords.Contains(word)) > 0;

        if (currentWords.Count <= 1)
        {
            changed |= blocked.Count > 0;
            blocked.Clear();
            changed |= SetBoolIfDifferent(playerMeta, "BlockAwaitingTurn", false);
            changed |= SetBoolIfDifferent(playerMeta, "BlockExpiresAtTurnEnd", false);
            return changed;
        }

        if (shouldRemainBlocked)
        {
            changed |= blocked.Add(incomingWord);
            if (isCurrentPlayer)
            {
                changed |= SetBoolIfDifferent(playerMeta, "BlockAwaitingTurn", false);
                changed |= SetBoolIfDifferent(playerMeta, "BlockExpiresAtTurnEnd", true);
            }
            else
            {
                changed |= SetBoolIfDifferent(playerMeta, "BlockAwaitingTurn", true);
                changed |= SetBoolIfDifferent(playerMeta, "BlockExpiresAtTurnEnd", false);
            }
        }
        else
        {
            changed |= blocked.Remove(incomingWord);
        }

        if (blocked.Count == 0)
        {
            changed |= SetBoolIfDifferent(playerMeta, "BlockAwaitingTurn", false);
            changed |= SetBoolIfDifferent(playerMeta, "BlockExpiresAtTurnEnd", false);
        }
        return changed;
    }

    private PendingDecisionData GetPendingDecision(
        WordRingsActionCardPatchCoordinator patch,
        string code,
        Guid playerId,
        long decisionId)
    {
        var sync = PatchSyncField.GetValue(patch)!;
        lock (sync)
        {
            var rooms = (IDictionary)PatchRoomsField.GetValue(patch)!;
            if (!rooms.Contains(code))
            {
                throw new InvalidOperationException("ActionCardImmunityDecisionUnavailable");
            }

            var meta = rooms[code]!;
            var decisions = (IDictionary)Get(meta, "PendingImmunityDecisions")!;
            if (!decisions.Contains(playerId))
            {
                throw new InvalidOperationException("ActionCardImmunityDecisionUnavailable");
            }

            var pending = decisions[playerId]!;
            if (Convert.ToInt64(Get(pending, "Id")) != decisionId)
            {
                throw new InvalidOperationException("ActionCardImmunityDecisionUnavailable");
            }

            return new PendingDecisionData(
                decisionId,
                Convert.ToInt64(Get(pending, "PlacementId")),
                (Guid)Get(pending, "PlayerId")!,
                (string)Get(pending, "Word")!,
                (string)Get(pending, "HostToken")!);
        }
    }

    private static void RemovePendingDecision(
        WordRingsActionCardPatchCoordinator patch,
        string code,
        Guid playerId,
        long decisionId)
    {
        var sync = PatchSyncField.GetValue(patch)!;
        lock (sync)
        {
            var rooms = (IDictionary)PatchRoomsField.GetValue(patch)!;
            if (!rooms.Contains(code)) return;
            var meta = rooms[code]!;
            var decisions = (IDictionary)Get(meta, "PendingImmunityDecisions")!;
            if (!decisions.Contains(playerId)) return;
            var current = decisions[playerId]!;
            if (Convert.ToInt64(Get(current, "Id")) == decisionId)
            {
                decisions.Remove(playerId);
            }
        }
    }

    private WordRingsRoomPlacementResult ResolveHostedFailureWithoutScore(
        string code,
        string hostToken,
        long placementId)
    {
        var sync = StoreSyncField.GetValue(_store)!;
        lock (sync)
        {
            var rooms = (IDictionary)StoreRoomsField.GetValue(_store)!;
            if (!rooms.Contains(code))
            {
                throw new WordRingsRoomException(WordRingsRoomError.RoomNotFound);
            }

            var room = rooms[code]!;
            if (!string.Equals(Get(room, "Phase")?.ToString(), "Playing", StringComparison.Ordinal))
            {
                throw new WordRingsRoomException(WordRingsRoomError.InvalidPhase);
            }

            var players = (IList)Get(room, "Players")!;
            var host = players.Cast<object>().FirstOrDefault(player =>
                (bool)Get(player, "IsHost")! &&
                string.Equals((string)Get(player, "Token")!, hostToken, StringComparison.Ordinal));
            if (host is null)
            {
                throw new WordRingsRoomException(WordRingsRoomError.NotHost);
            }

            var placements = (IList)Get(room, "Placements")!;
            var placementIndex = -1;
            object? placement = null;
            for (var index = 0; index < placements.Count; index++)
            {
                var candidate = placements[index]!;
                if ((long)Get(candidate, "Id")! != placementId || !(bool)Get(candidate, "IsPending")!) continue;
                placementIndex = index;
                placement = candidate;
                break;
            }
            if (placementIndex < 0 || placement is null)
            {
                throw new WordRingsRoomException(WordRingsRoomError.InvalidPlacement);
            }

            var playerId = (Guid)Get(placement, "PlayerId")!;
            var player = players.Cast<object>().FirstOrDefault(item => (Guid)Get(item, "Id")! == playerId)
                ?? throw new WordRingsRoomException(WordRingsRoomError.InvalidPlayer);
            var submittedMembership = CanonicalMembership((string)Get(placement, "SubmittedMembership")!);
            var finalMembership = CanonicalMembership((string)Get(placement, "Membership")!);
            var isPartial = !string.Equals(submittedMembership, finalMembership, StringComparison.Ordinal) &&
                            IsPartialPlacement(submittedMembership, finalMembership);

            var replacement = Activator.CreateInstance(
                placement.GetType(),
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                args:
                [
                    (long)Get(placement, "Id")!,
                    (string)Get(placement, "Word")!,
                    (string)Get(placement, "Membership")!,
                    Convert.ToDouble(Get(placement, "X")),
                    Convert.ToDouble(Get(placement, "Y")),
                    false,
                    isPartial,
                    0d,
                    playerId,
                    (string)Get(placement, "PlayerName")!,
                    false,
                    (string)Get(placement, "SubmittedMembership")!,
                    (bool)Get(placement, "IsSeed")!
                ],
                culture: null)
                ?? throw new InvalidOperationException("ActionCardImmunityDecisionUnavailable");
            placements[placementIndex] = replacement;

            StoreAddRoomEventMethod.Invoke(
                null,
                [room, WordRingsRoomEventType.HostMoved, playerId, (string)Get(player, "Name")!]);

            var allWordsUsed = players.Cast<object>()
                .All(item => ((IList)Get(item, "RemainingWords")!).Count == 0);
            if (allWordsUsed)
            {
                SetEnum(room, "Phase", "Finished");
                SetEnum(room, "Outcome", "Lost");
                Set(room, "WinnerPlayerId", null);
            }
            else
            {
                StoreAdvanceTurnMethod.Invoke(null, [room]);
            }

            var now = ((TimeProvider)StoreTimeProviderField.GetValue(_store)!).GetUtcNow();
            StoreEnsureTurnDeadlineMethod.Invoke(null, [room, now]);
            StoreTouchMethod.Invoke(null, [room, now]);
            var state = (WordRingsRoomSnapshot)StoreCreateSnapshotMethod.Invoke(null, [room, host])!;

            return new WordRingsRoomPlacementResult(
                state,
                (string)Get(replacement, "Word")!,
                submittedMembership,
                finalMembership,
                false,
                isPartial,
                0,
                false,
                false);
        }
    }

    private static bool IsPartialPlacement(string expected, string actual)
    {
        if (expected.Length == 0 || actual.Length == 0)
        {
            return false;
        }
        return expected.Any(actual.Contains);
    }

    private static string CanonicalMembership(string value) =>
        string.Concat((value ?? string.Empty)
            .Trim()
            .ToUpperInvariant()
            .Where(character => character is 'A' or 'B' or 'C')
            .Distinct()
            .OrderBy(character => character));

    private static bool SetBoolIfDifferent(object instance, string property, bool value)
    {
        var current = (bool)Get(instance, property)!;
        if (current == value) return false;
        Set(instance, property, value);
        return true;
    }

    private static object? Get(object instance, string property) =>
        instance.GetType().GetProperty(property, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .GetValue(instance);

    private static void Set(object instance, string property, object? value) =>
        instance.GetType().GetProperty(property, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .SetValue(instance, value);

    private static void SetEnum(object instance, string property, string value)
    {
        var propertyInfo = instance.GetType().GetProperty(
            property,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;
        propertyInfo.SetValue(instance, Enum.Parse(propertyInfo.PropertyType, value));
    }

    private static string Normalize(string? value) => (value ?? string.Empty).Trim().ToUpperInvariant();

    private sealed record PendingDecisionData(
        long Id,
        long PlacementId,
        Guid PlayerId,
        string Word,
        string HostToken);
}
