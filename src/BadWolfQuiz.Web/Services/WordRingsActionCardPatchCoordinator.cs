using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;

namespace BadWolfQuiz.Web.Services;

public sealed record WordRingsActionCardNoticeSnapshot(
    long Id,
    string Kind,
    string ActorName,
    string TargetName,
    int CardId,
    long CreatedAtUnixMilliseconds);

public sealed record WordRingsActionCardImmunityDecisionSnapshot(
    long Id,
    long PlacementId,
    string Word);

public sealed record WordRingsActionCardPatchSnapshot(
    WordRingsActionCardNoticeSnapshot? Notice,
    WordRingsActionCardImmunityDecisionSnapshot? PendingImmunityDecision,
    bool AnyPendingImmunityDecision);

public sealed record WordRingsActionCardImmunityQueueResult(
    bool Queued,
    WordRingsRoomHostSnapshot State);

public sealed record WordRingsActionCardImmunityResolution(
    bool ReturnedWord,
    Guid PlayerId,
    long PlacementId,
    WordRingsRoomPlacementResult? PlacementResult);

public sealed class WordRingsActionCardPatchCoordinator
{
    private static readonly ConcurrentDictionary<string, Lazy<WordRingsActionCardPatchCoordinator>> Instances =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly FieldInfo ActionMetaSyncField = typeof(WordRingsActionCardCoordinator)
        .GetField("_metaSync", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static readonly FieldInfo ActionRoomsField = typeof(WordRingsActionCardCoordinator)
        .GetField("_rooms", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static readonly FieldInfo StoreSyncField = typeof(WordRingsRoomStore)
        .GetField("_sync", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static readonly FieldInfo StoreRoomsField = typeof(WordRingsRoomStore)
        .GetField("_rooms", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private readonly WordRingsActionCardCoordinator _cards;
    private readonly WordRingsRoomHostCoordinator _host;
    private readonly WordRingsRoomStore _store;
    private readonly object _sync = new();
    private readonly Dictionary<string, RoomPatchMeta> _rooms = new(StringComparer.OrdinalIgnoreCase);

    private WordRingsActionCardPatchCoordinator(IWebHostEnvironment environment)
    {
        _cards = WordRingsActionCardCoordinator.Get(environment);
        _host = WordRingsRoomHostCoordinator.Get(environment);
        _store = WordRingsRoomStore.Get(environment);
    }

    public static WordRingsActionCardPatchCoordinator Get(IWebHostEnvironment environment)
    {
        var root = Path.GetFullPath(environment.ContentRootPath);
        return Instances.GetOrAdd(
            root,
            _ => new Lazy<WordRingsActionCardPatchCoordinator>(
                () => new WordRingsActionCardPatchCoordinator(environment),
                LazyThreadSafetyMode.ExecutionAndPublication)).Value;
    }

    public WordRingsActionCardPatchSnapshot GetState(string? roomCode, Guid playerId)
    {
        var code = Normalize(roomCode);
        lock (_sync)
        {
            var meta = GetOrCreateMeta(code);
            meta.PendingImmunityDecisions.TryGetValue(playerId, out var pending);
            return new WordRingsActionCardPatchSnapshot(
                meta.LastNotice,
                pending is null
                    ? null
                    : new WordRingsActionCardImmunityDecisionSnapshot(
                        pending.Id,
                        pending.PlacementId,
                        pending.Word),
                meta.PendingImmunityDecisions.Count > 0);
        }
    }

    public void RecordCardUse(
        WordRingsRoomSnapshot before,
        int cardId,
        WordRingsActionCardUseResult result)
    {
        if (cardId <= 0) return;
        var actorName = before.Players.FirstOrDefault(player => player.Id == before.PlayerId)?.Name ?? string.Empty;
        var targetName = result.TargetName;
        var kind = result.BlockedByShield
            ? "shield-blocked"
            : IsPositive(cardId, actorName, targetName) ? "positive" : "negative";
        PublishNotice(before.RoomCode, kind, actorName, targetName, cardId);
    }

    public void ApplySubmissionLifecycle(string? roomCode, Guid playerId, bool fullyCorrect)
    {
        ClearObfuscation(roomCode, playerId, clearMask: true, clearAnagram: fullyCorrect);
        EnsureTargetReachable(roomCode);
    }

    public void ApplyHostedResolutionLifecycle(string? roomCode, string? hostToken, long placementId)
    {
        var state = _host.GetRoomState(roomCode, hostToken);
        if (!state.IsHost)
        {
            throw new WordRingsRoomException(WordRingsRoomError.NotHost);
        }
        var placement = state.Placements.FirstOrDefault(item => item.Id == placementId && !item.IsPending);
        if (placement?.IsCorrect == true)
        {
            ClearObfuscation(state.RoomCode, placement.PlayerId, clearMask: true, clearAnagram: true);
        }
        EnsureTargetReachable(state.RoomCode);
    }

    public WordRingsActionCardImmunityQueueResult TryQueueHostedImmunity(
        string? roomCode,
        string? hostToken,
        long placementId)
    {
        var hostState = _host.GetHostState(roomCode, hostToken);
        if (!hostState.IsHost)
        {
            throw new WordRingsRoomException(WordRingsRoomError.NotHost);
        }
        var pending = hostState.PendingPlacement;
        if (pending is null || pending.Id != placementId || !pending.WasMoved)
        {
            return new WordRingsActionCardImmunityQueueResult(false, hostState);
        }

        var code = Normalize(hostState.RoomCode);
        lock (_sync)
        {
            var patchMeta = GetOrCreateMeta(code);
            if (patchMeta.PendingImmunityDecisions.Values.Any(item => item.PlacementId == placementId))
            {
                return new WordRingsActionCardImmunityQueueResult(true, hostState);
            }
        }

        if (!TryConsumeFailureImmunity(code, pending.PlayerId))
        {
            return new WordRingsActionCardImmunityQueueResult(false, hostState);
        }

        long decisionId;
        lock (_sync)
        {
            var patchMeta = GetOrCreateMeta(code);
            decisionId = patchMeta.NextImmunityDecisionId++;
            patchMeta.PendingImmunityDecisions[pending.PlayerId] = new PendingImmunityDecision(
                decisionId,
                pending.Id,
                pending.PlayerId,
                pending.PlayerName,
                pending.Word,
                hostToken ?? string.Empty);
        }
        PublishNotice(code, "immunity", pending.PlayerName, pending.PlayerName, (int)WordRingsActionCardKind.Immunity);
        return new WordRingsActionCardImmunityQueueResult(true, _host.GetHostState(code, hostToken));
    }

    public WordRingsActionCardImmunityResolution ResolveImmunityDecision(
        string? roomCode,
        string? playerToken,
        long decisionId,
        bool returnWord)
    {
        var caller = _host.GetRoomState(roomCode, playerToken);
        var code = Normalize(caller.RoomCode);
        PendingImmunityDecision pending;
        lock (_sync)
        {
            var meta = GetOrCreateMeta(code);
            if (!meta.PendingImmunityDecisions.TryGetValue(caller.PlayerId, out pending!) || pending.Id != decisionId)
            {
                throw new InvalidOperationException("ActionCardImmunityDecisionUnavailable");
            }
        }

        WordRingsRoomPlacementResult? placementResult = null;
        if (returnWord)
        {
            RestorePendingHostedWord(code, pending);
        }
        else
        {
            placementResult = _host.ResolvePlacementWithResult(code, pending.HostToken, pending.PlacementId);
            _cards.RecordPlacementAttempt(code, pending.PlayerId, pending.Word, placementResult.IsCorrect);
            if (placementResult.IsCorrect)
            {
                ClearObfuscation(code, pending.PlayerId, clearMask: true, clearAnagram: true);
            }
        }

        lock (_sync)
        {
            if (_rooms.TryGetValue(code, out var meta) &&
                meta.PendingImmunityDecisions.TryGetValue(caller.PlayerId, out var current) &&
                current.Id == decisionId)
            {
                meta.PendingImmunityDecisions.Remove(caller.PlayerId);
            }
        }
        EnsureTargetReachable(code);
        return new WordRingsActionCardImmunityResolution(
            returnWord,
            pending.PlayerId,
            pending.PlacementId,
            placementResult);
    }

    public void GrantDebugCard(string? roomCode, string? hostToken, Guid playerId, int cardId)
    {
        if (!Enum.IsDefined(typeof(WordRingsActionCardKind), cardId))
        {
            throw new InvalidOperationException("InvalidActionCard");
        }
        var roomState = _host.GetRoomState(roomCode, hostToken);
        if (!roomState.IsHost)
        {
            throw new WordRingsRoomException(WordRingsRoomError.NotHost);
        }

        _ = _cards.GetState(roomState.RoomCode, hostToken);
        var metaSync = ActionMetaSyncField.GetValue(_cards)!;
        lock (metaSync)
        {
            var meta = RequireActionMeta(roomState.RoomCode);
            if (!(bool)Get(meta, "Enabled")!)
            {
                throw new InvalidOperationException("ActionCardsUnavailable");
            }
            var players = (IDictionary)Get(meta, "Players")!;
            if (!players.Contains(playerId))
            {
                throw new WordRingsRoomException(WordRingsRoomError.InvalidPlayer);
            }
            var player = players[playerId]!;
            var cards = (IList)Get(player, "Cards")!;
            var kind = (WordRingsActionCardKind)cardId;
            if (cards.Contains(kind))
            {
                throw new InvalidOperationException("ActionCardDuplicate");
            }
            var maximumCards = (int)Get(meta, "MaximumCards")!;
            if (cards.Count >= maximumCards)
            {
                throw new InvalidOperationException("ActionCardHandFull");
            }
            cards.Add(kind);
            IncrementActionRevision(meta);
        }
    }

    public WordRingsRoomSnapshot EnsureTargetReachable(string? roomCode, string? playerToken)
    {
        var state = _host.GetRoomState(roomCode, playerToken);
        EnsureTargetReachable(state.RoomCode);
        return _host.GetRoomState(state.RoomCode, playerToken);
    }

    private void EnsureTargetReachable(string? roomCode)
    {
        var code = Normalize(roomCode);
        var storeSync = StoreSyncField.GetValue(_store)!;
        lock (storeSync)
        {
            var rooms = (IDictionary)StoreRoomsField.GetValue(_store)!;
            if (!rooms.Contains(code)) return;
            var room = rooms[code]!;
            if (!string.Equals(Get(room, "Phase")?.ToString(), "Playing", StringComparison.Ordinal)) return;

            var targetScore = (int)Get(room, "TargetScore")!;
            var puzzle = (WordRingsPuzzle)Get(room, "Puzzle")!;
            var placements = (IList)Get(room, "Placements")!;
            var placedWords = placements.Cast<object>()
                .Select(item => (string?)Get(item, "Word"))
                .Where(word => !string.IsNullOrWhiteSpace(word))
                .Cast<string>()
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var players = (IList)Get(room, "Players")!;
            var globallyHeld = players.Cast<object>()
                .SelectMany(player => ((IList)Get(player, "RemainingWords")!).Cast<string>())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var changed = false;

            foreach (var player in players.Cast<object>())
            {
                if (!(bool)Get(player, "IsPlayingParticipant")!) continue;
                var score = Convert.ToDouble(Get(player, "Score"));
                var neededPoints = Math.Max(0, (int)Math.Ceiling(targetScore - score));
                if (neededPoints == 0) continue;

                var words = (IList)Get(player, "RemainingWords")!;
                var scoringRemaining = words.Cast<string>().Count(word => IsScoringWord(puzzle, word));
                var requiredScoringWords = neededPoints + 1;
                var missing = requiredScoringWords - scoringRemaining;
                if (missing <= 0) continue;

                var candidates = puzzle.Words
                    .Where(word => IsScoringWord(puzzle, word))
                    .Where(word => !placedWords.Contains(word) && !globallyHeld.Contains(word))
                    .OrderBy(_ => Random.Shared.Next())
                    .ToList();
                if (candidates.Count < missing)
                {
                    var ownWords = words.Cast<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
                    candidates.AddRange(puzzle.Words
                        .Where(word => IsScoringWord(puzzle, word))
                        .Where(word => !placedWords.Contains(word) && !ownWords.Contains(word))
                        .Where(word => !candidates.Contains(word, StringComparer.OrdinalIgnoreCase))
                        .OrderBy(_ => Random.Shared.Next()));
                }

                foreach (var word in candidates.Take(missing))
                {
                    words.Add(word);
                    globallyHeld.Add(word);
                    changed = true;
                }
            }

            if (changed)
            {
                Set(room, "Version", Convert.ToInt64(Get(room, "Version")) + 1);
            }
        }
    }

    private void ClearObfuscation(
        string? roomCode,
        Guid playerId,
        bool clearMask,
        bool clearAnagram)
    {
        var code = Normalize(roomCode);
        var metaSync = ActionMetaSyncField.GetValue(_cards)!;
        lock (metaSync)
        {
            if (!TryGetActionMeta(code, out var meta)) return;
            var players = (IDictionary)Get(meta, "Players")!;
            if (!players.Contains(playerId)) return;
            var player = players[playerId]!;
            var changed = false;
            if (clearMask)
            {
                var masked = (HashSet<string>)Get(player, "MaskedWords")!;
                changed |= masked.Count > 0;
                masked.Clear();
            }
            if (clearAnagram)
            {
                var anagrammed = (HashSet<string>)Get(player, "AnagrammedWords")!;
                changed |= anagrammed.Count > 0;
                anagrammed.Clear();
            }
            if (changed) IncrementActionRevision(meta);
        }
    }

    private bool TryConsumeFailureImmunity(string code, Guid playerId)
    {
        var metaSync = ActionMetaSyncField.GetValue(_cards)!;
        lock (metaSync)
        {
            if (!TryGetActionMeta(code, out var meta)) return false;
            var players = (IDictionary)Get(meta, "Players")!;
            if (!players.Contains(playerId)) return false;
            var player = players[playerId]!;
            if ((bool)Get(player, "TurnFailureImmunity")!)
            {
                Set(player, "TurnFailureImmunity", false);
                IncrementActionRevision(meta);
                return true;
            }
            if ((bool)Get(player, "ActivatedFailureImmunity")!)
            {
                Set(player, "ActivatedFailureImmunity", false);
                IncrementActionRevision(meta);
                return true;
            }
            return false;
        }
    }

    private void RestorePendingHostedWord(string code, PendingImmunityDecision pending)
    {
        var storeSync = StoreSyncField.GetValue(_store)!;
        lock (storeSync)
        {
            var rooms = (IDictionary)StoreRoomsField.GetValue(_store)!;
            if (!rooms.Contains(code))
            {
                throw new WordRingsRoomException(WordRingsRoomError.RoomNotFound);
            }
            var room = rooms[code]!;
            var placements = (IList)Get(room, "Placements")!;
            var placementIndex = -1;
            for (var index = 0; index < placements.Count; index++)
            {
                var placement = placements[index]!;
                if ((long)Get(placement, "Id")! == pending.PlacementId &&
                    (bool)Get(placement, "IsPending")!)
                {
                    placementIndex = index;
                    break;
                }
            }
            if (placementIndex < 0)
            {
                throw new InvalidOperationException("ActionCardImmunityDecisionUnavailable");
            }

            var players = (IList)Get(room, "Players")!;
            var playerIndex = -1;
            object? player = null;
            for (var index = 0; index < players.Count; index++)
            {
                if ((Guid)Get(players[index]!, "Id")! != pending.PlayerId) continue;
                playerIndex = index;
                player = players[index];
                break;
            }
            if (player is null)
            {
                throw new WordRingsRoomException(WordRingsRoomError.InvalidPlayer);
            }

            var words = (IList)Get(player, "RemainingWords")!;
            if (!words.Cast<string>().Any(word => string.Equals(word, pending.Word, StringComparison.OrdinalIgnoreCase)))
            {
                words.Insert(0, pending.Word);
            }
            placements.RemoveAt(placementIndex);
            Set(room, "CurrentPlayerIndex", playerIndex);
            Set(room, "OutsidePointAwardedThisTurn", false);
            Set(room, "TurnDeadlineUtc", null);
            Set(room, "PausedTurnSeconds", null);
            Set(room, "Version", Convert.ToInt64(Get(room, "Version")) + 1);
        }
    }

    private void PublishNotice(string? roomCode, string kind, string actorName, string targetName, int cardId)
    {
        var code = Normalize(roomCode);
        lock (_sync)
        {
            var meta = GetOrCreateMeta(code);
            meta.LastNotice = new WordRingsActionCardNoticeSnapshot(
                meta.NextNoticeId++,
                kind,
                actorName,
                targetName,
                cardId,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        }
    }

    private static bool IsPositive(int cardId, string actorName, string targetName) =>
        cardId switch
        {
            2 or 8 => string.Equals(actorName, targetName, StringComparison.Ordinal),
            4 or 5 or 6 or 7 or 13 => true,
            _ => false
        };

    private static bool IsScoringWord(WordRingsPuzzle puzzle, string word) =>
        puzzle.Expected.TryGetValue(word, out var membership) && !string.IsNullOrWhiteSpace(membership);

    private RoomPatchMeta GetOrCreateMeta(string code)
    {
        if (!_rooms.TryGetValue(code, out var meta))
        {
            meta = new RoomPatchMeta();
            _rooms[code] = meta;
        }
        return meta;
    }

    private bool TryGetActionMeta(string code, out object meta)
    {
        var rooms = (IDictionary)ActionRoomsField.GetValue(_cards)!;
        if (!rooms.Contains(code))
        {
            meta = null!;
            return false;
        }
        meta = rooms[code]!;
        return true;
    }

    private object RequireActionMeta(string code) =>
        TryGetActionMeta(Normalize(code), out var meta)
            ? meta
            : throw new InvalidOperationException("ActionCardRoomNotConfigured");

    private static void IncrementActionRevision(object meta) =>
        Set(meta, "Revision", Convert.ToInt64(Get(meta, "Revision")) + 1);

    private static object? Get(object instance, string property) =>
        instance.GetType().GetProperty(property, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .GetValue(instance);

    private static void Set(object instance, string property, object? value) =>
        instance.GetType().GetProperty(property, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .SetValue(instance, value);

    private static string Normalize(string? value) => (value ?? string.Empty).Trim().ToUpperInvariant();

    private sealed class RoomPatchMeta
    {
        public long NextNoticeId { get; set; } = 1;
        public long NextImmunityDecisionId { get; set; } = 1;
        public WordRingsActionCardNoticeSnapshot? LastNotice { get; set; }
        public Dictionary<Guid, PendingImmunityDecision> PendingImmunityDecisions { get; } = [];
    }

    private sealed record PendingImmunityDecision(
        long Id,
        long PlacementId,
        Guid PlayerId,
        string PlayerName,
        string Word,
        string HostToken);
}
