using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using BadWolfQuiz.Web.Data;

namespace BadWolfQuiz.Web.Services;

public sealed record WordRingsActionAchievementActionCapture(
    Guid ActorId,
    int CardId,
    Guid? RequestedTargetId,
    IReadOnlyDictionary<Guid, string> PlayerNames,
    IReadOnlyDictionary<Guid, string[]> WordsByPlayer,
    IReadOnlyDictionary<Guid, string[]> BlockedWordsByPlayer,
    IReadOnlyDictionary<Guid, string[]> MaskedWordsByPlayer,
    IReadOnlyDictionary<Guid, string[]> AnagrammedWordsByPlayer);

public sealed class WordRingsActionAchievementTracker
{
    private static readonly ConcurrentDictionary<string, Lazy<WordRingsActionAchievementTracker>> Instances =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly FieldInfo StoreSyncField = typeof(WordRingsRoomStore)
        .GetField("_sync", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static readonly FieldInfo StoreRoomsField = typeof(WordRingsRoomStore)
        .GetField("_rooms", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static readonly FieldInfo ActionMetaSyncField = typeof(WordRingsActionCardCoordinator)
        .GetField("_metaSync", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static readonly FieldInfo ActionRoomsField = typeof(WordRingsActionCardCoordinator)
        .GetField("_rooms", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static readonly HashSet<int> LastingNegativeCards =
    [
        (int)WordRingsActionCardKind.Block,
        (int)WordRingsActionCardKind.Timeout,
        (int)WordRingsActionCardKind.Mask,
        (int)WordRingsActionCardKind.Anagram
    ];

    private readonly WordRingsRoomStore _store;
    private readonly WordRingsRoomHostCoordinator _host;
    private readonly WordRingsActionCardCoordinator _cards;
    private readonly object _sync = new();
    private readonly Dictionary<string, RoomAchievementState> _rooms = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<Guid, PlacementCapture> _placementCaptures = [];

    private WordRingsActionAchievementTracker(IWebHostEnvironment environment)
    {
        _store = WordRingsRoomStore.Get(environment);
        _host = WordRingsRoomHostCoordinator.Get(environment);
        _cards = WordRingsActionCardCoordinator.Get(environment);
    }

    public static WordRingsActionAchievementTracker Get(IWebHostEnvironment environment)
    {
        var root = Path.GetFullPath(environment.ContentRootPath);
        return Instances.GetOrAdd(
            root,
            _ => new Lazy<WordRingsActionAchievementTracker>(
                () => new WordRingsActionAchievementTracker(environment),
                LazyThreadSafetyMode.ExecutionAndPublication)).Value;
    }

    public WordRingsActionAchievementActionCapture CaptureAction(
        WordRingsRoomSnapshot before,
        int cardId,
        Guid? requestedTargetId)
    {
        var code = Normalize(before.RoomCode);
        return new WordRingsActionAchievementActionCapture(
            before.PlayerId,
            Math.Abs(cardId),
            requestedTargetId,
            before.Players.ToDictionary(player => player.Id, player => player.Name),
            SnapshotRoomWords(code),
            SnapshotEffectWords(code, "BlockedWords"),
            SnapshotEffectWords(code, "MaskedWords"),
            SnapshotEffectWords(code, "AnagrammedWords"));
    }

    public async Task RecordActionAsync(
        QuizDbContext db,
        WordRingsRoomSnapshot before,
        int originalCardId,
        Guid? requestedTargetId,
        WordRingsActionCardUseResult result,
        WordRingsActionAchievementActionCapture capture,
        CancellationToken cancellationToken = default)
    {
        var cardId = Math.Abs(originalCardId);
        var discarded = originalCardId < 0;
        var actorId = before.PlayerId;
        var targetId = ResolveTargetId(capture, requestedTargetId, result.TargetName);
        var code = Normalize(before.RoomCode);

        if (result.BlockedByShield && targetId is Guid shieldedPlayerId)
        {
            await UnlockAsync(db, code, shieldedPlayerId, "WordRingsShieldSave", cancellationToken);
        }

        if (!discarded && !result.BlockedByShield && cardId == (int)WordRingsActionCardKind.Swap)
        {
            await RecordSwapAchievementsAsync(db, code, actorId, targetId, capture, cancellationToken);
        }

        if (!discarded && !result.BlockedByShield && cardId == (int)WordRingsActionCardKind.Cleanse && targetId is Guid cleanseTarget)
        {
            var unlock = false;
            lock (_sync)
            {
                var room = GetOrCreateRoom(code);
                unlock = room.LastNegativeByActor.TryGetValue(actorId, out var previous) && previous.TargetId == cleanseTarget;
                room.LastNegativeByActor.Remove(actorId);
            }
            if (unlock)
            {
                await UnlockAsync(db, code, actorId, "WordRingsCleanseImmediate", cancellationToken);
            }
        }
        else
        {
            lock (_sync)
            {
                var room = GetOrCreateRoom(code);
                if (!discarded && !result.BlockedByShield && LastingNegativeCards.Contains(cardId) && targetId is Guid negativeTarget)
                {
                    room.LastNegativeByActor[actorId] = new NegativeAction(negativeTarget, cardId);
                }
                else if (!discarded)
                {
                    room.LastNegativeByActor.Remove(actorId);
                }
            }
        }

        Guid? firstEmptyPlayer = null;
        lock (_sync)
        {
            var room = GetOrCreateRoom(code);
            if (!room.FirstEmptyActionHandAwarded)
            {
                var counts = SnapshotCardCounts(code);
                if (counts.TryGetValue(actorId, out var actorCards) && actorCards == 0)
                {
                    room.FirstEmptyActionHandAwarded = true;
                    firstEmptyPlayer = actorId;
                }
            }
        }
        if (firstEmptyPlayer is Guid emptyPlayer)
        {
            await UnlockAsync(db, code, emptyPlayer, "WordRingsFirstEmptyActionHand", cancellationToken);
        }
    }

    public Guid CapturePlacement(
        string? roomCode,
        string? playerToken,
        long? placementId,
        string? word)
    {
        var caller = _host.GetRoomState(roomCode, playerToken);
        var playerId = caller.PlayerId;
        var normalizedWord = (word ?? string.Empty).Trim();

        if (placementId is long id)
        {
            var placement = caller.Placements.FirstOrDefault(item => item.Id == id);
            if (placement is null && caller.IsHost)
            {
                var pending = _host.GetHostState(caller.RoomCode, playerToken).PendingPlacement;
                if (pending?.Id == id)
                {
                    playerId = pending.PlayerId;
                    normalizedWord = pending.Word;
                }
            }
            else if (placement is not null)
            {
                playerId = placement.PlayerId;
                normalizedWord = placement.Word;
            }
        }

        if (normalizedWord.Length == 0)
        {
            throw new InvalidOperationException("AchievementPlacementUnavailable");
        }

        var effects = SnapshotPlayerEffectState(caller.RoomCode, playerId);
        var capture = new PlacementCapture(
            caller.RoomCode,
            playerId,
            normalizedWord,
            effects.Masked.Contains(normalizedWord, StringComparer.OrdinalIgnoreCase),
            effects.Anagrammed.Contains(normalizedWord, StringComparer.OrdinalIgnoreCase),
            string.Equals(effects.HintWord, normalizedWord, StringComparison.OrdinalIgnoreCase));
        var captureId = Guid.NewGuid();
        lock (_sync)
        {
            _placementCaptures[captureId] = capture;
        }
        return captureId;
    }

    public async Task FinalizePlacementAsync(
        QuizDbContext db,
        string? roomCode,
        string? playerToken,
        Guid captureId,
        long? placementId,
        CancellationToken cancellationToken = default)
    {
        var caller = _host.GetRoomState(roomCode, playerToken);
        PlacementCapture capture;
        lock (_sync)
        {
            if (!_placementCaptures.Remove(captureId, out capture!) ||
                !string.Equals(capture.RoomCode, caller.RoomCode, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        var placement = placementId is long id
            ? caller.Placements.FirstOrDefault(item => item.Id == id)
            : caller.Placements.LastOrDefault(item =>
                item.PlayerId == capture.PlayerId &&
                string.Equals(item.Word, capture.Word, StringComparison.OrdinalIgnoreCase));
        if (placement is null || placement.IsPending || placement.IsCorrect != true)
        {
            return;
        }

        if (capture.WasMasked)
        {
            await UnlockAsync(db, caller.RoomCode, capture.PlayerId, "WordRingsMaskedCorrect", cancellationToken);
        }
        if (capture.WasAnagrammed)
        {
            await UnlockAsync(db, caller.RoomCode, capture.PlayerId, "WordRingsAnagramCorrect", cancellationToken);
        }
        if (capture.WasHinted && string.Equals(CanonicalMembership(placement.Membership), "ABC", StringComparison.Ordinal))
        {
            await UnlockAsync(db, caller.RoomCode, capture.PlayerId, "WordRingsHintTripleCorrect", cancellationToken);
        }
    }

    private async Task RecordSwapAchievementsAsync(
        QuizDbContext db,
        string code,
        Guid actorId,
        Guid? targetId,
        WordRingsActionAchievementActionCapture capture,
        CancellationToken cancellationToken)
    {
        var after = SnapshotRoomWords(code);
        if (!capture.WordsByPlayer.TryGetValue(actorId, out var actorBefore) ||
            !after.TryGetValue(actorId, out var actorAfter))
        {
            return;
        }
        var incoming = FindIncomingWord(actorBefore, actorAfter);
        if (incoming is null) return;

        var resolvedTarget = targetId ?? ResolveSwapTargetByWord(capture, after, incoming);
        if (resolvedTarget is not Guid target) return;

        if (capture.MaskedWordsByPlayer.TryGetValue(target, out var masked) && masked.Contains(incoming, StringComparer.OrdinalIgnoreCase))
        {
            await UnlockAsync(db, code, actorId, "WordRingsStealMaskedWord", cancellationToken);
        }
        if (capture.AnagrammedWordsByPlayer.TryGetValue(target, out var anagrammed) && anagrammed.Contains(incoming, StringComparer.OrdinalIgnoreCase))
        {
            await UnlockAsync(db, code, actorId, "WordRingsStealAnagramWord", cancellationToken);
        }
        if (capture.BlockedWordsByPlayer.TryGetValue(target, out var blocked) && blocked.Contains(incoming, StringComparer.OrdinalIgnoreCase))
        {
            await UnlockAsync(db, code, actorId, "WordRingsStealBlockedWord", cancellationToken);
        }
    }

    private async Task UnlockAsync(
        QuizDbContext db,
        string roomCode,
        Guid playerId,
        string achievementCode,
        CancellationToken cancellationToken)
    {
        var identity = _host.GetAchievementParticipant(roomCode, playerId);
        if (identity is null || identity.RoundNumber <= 0) return;
        await new PlayerAchievementService(db).UnlockPlayerAsync(
            identity.AccountId,
            identity.HostId,
            identity.PlayerName,
            achievementCode,
            cancellationToken: cancellationToken);
    }

    private Dictionary<Guid, string[]> SnapshotRoomWords(string code)
    {
        lock (StoreSyncField.GetValue(_store)!)
        {
            var room = GetRoom(code);
            return ((IList)Get(room, "Players")!).Cast<object>().ToDictionary(
                player => (Guid)Get(player, "Id")!,
                player => ((IList)Get(player, "RemainingWords")!).Cast<string>().ToArray());
        }
    }

    private Dictionary<Guid, string[]> SnapshotEffectWords(string code, string propertyName)
    {
        var result = new Dictionary<Guid, string[]>();
        lock (ActionMetaSyncField.GetValue(_cards)!)
        {
            var rooms = (IDictionary)ActionRoomsField.GetValue(_cards)!;
            if (!rooms.Contains(code)) return result;
            var players = (IDictionary)Get(rooms[code]!, "Players")!;
            foreach (DictionaryEntry entry in players)
            {
                result[(Guid)entry.Key] = ((HashSet<string>)Get(entry.Value!, propertyName)!).ToArray();
            }
        }
        return result;
    }

    private Dictionary<Guid, int> SnapshotCardCounts(string code)
    {
        var result = new Dictionary<Guid, int>();
        lock (ActionMetaSyncField.GetValue(_cards)!)
        {
            var rooms = (IDictionary)ActionRoomsField.GetValue(_cards)!;
            if (!rooms.Contains(code)) return result;
            var players = (IDictionary)Get(rooms[code]!, "Players")!;
            foreach (DictionaryEntry entry in players)
            {
                result[(Guid)entry.Key] = ((IList)Get(entry.Value!, "Cards")!).Count;
            }
        }
        return result;
    }

    private PlayerEffectState SnapshotPlayerEffectState(string code, Guid playerId)
    {
        lock (ActionMetaSyncField.GetValue(_cards)!)
        {
            var rooms = (IDictionary)ActionRoomsField.GetValue(_cards)!;
            if (!rooms.Contains(code)) return new PlayerEffectState([], [], null);
            var players = (IDictionary)Get(rooms[code]!, "Players")!;
            if (!players.Contains(playerId)) return new PlayerEffectState([], [], null);
            var player = players[playerId]!;
            return new PlayerEffectState(
                ((HashSet<string>)Get(player, "MaskedWords")!).ToArray(),
                ((HashSet<string>)Get(player, "AnagrammedWords")!).ToArray(),
                (string?)Get(player, "HintWord"));
        }
    }

    private static Guid? ResolveTargetId(
        WordRingsActionAchievementActionCapture capture,
        Guid? requestedTargetId,
        string? targetName)
    {
        if (requestedTargetId is Guid requested) return requested;
        var matches = capture.PlayerNames
            .Where(pair => string.Equals(pair.Value, targetName, StringComparison.Ordinal))
            .Select(pair => pair.Key)
            .ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }

    private static string? FindIncomingWord(IReadOnlyList<string> before, IReadOnlyList<string> after)
    {
        if (before.Count != after.Count) return null;
        for (var index = 0; index < before.Count; index++)
        {
            if (!string.Equals(before[index], after[index], StringComparison.OrdinalIgnoreCase)) return after[index];
        }
        return null;
    }

    private static Guid? ResolveSwapTargetByWord(
        WordRingsActionAchievementActionCapture capture,
        IReadOnlyDictionary<Guid, string[]> afterWords,
        string incoming)
    {
        foreach (var pair in capture.WordsByPlayer)
        {
            if (pair.Key == capture.ActorId || !afterWords.TryGetValue(pair.Key, out var after)) continue;
            if (FindIncomingWord(pair.Value, after) is { } replacement &&
                pair.Value.Contains(incoming, StringComparer.OrdinalIgnoreCase))
            {
                return pair.Key;
            }
        }
        return null;
    }

    private object GetRoom(string code)
    {
        var rooms = (IDictionary)StoreRoomsField.GetValue(_store)!;
        if (!rooms.Contains(code)) throw new WordRingsRoomException(WordRingsRoomError.RoomNotFound);
        return rooms[code]!;
    }

    private RoomAchievementState GetOrCreateRoom(string code)
    {
        if (!_rooms.TryGetValue(code, out var state))
        {
            state = new RoomAchievementState();
            _rooms[code] = state;
        }
        return state;
    }

    private static string CanonicalMembership(string? membership)
    {
        var value = (membership ?? string.Empty).Trim().ToUpperInvariant();
        return new string(value
            .Where(character => character is 'A' or 'B' or 'C')
            .Distinct()
            .OrderBy(character => character)
            .ToArray());
    }

    private static object? Get(object instance, string property) =>
        instance.GetType().GetProperty(property, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(instance);

    private static string Normalize(string? value) => (value ?? string.Empty).Trim().ToUpperInvariant();

    private sealed class RoomAchievementState
    {
        public bool FirstEmptyActionHandAwarded { get; set; }
        public Dictionary<Guid, NegativeAction> LastNegativeByActor { get; } = [];
    }

    private sealed record NegativeAction(Guid TargetId, int CardId);
    private sealed record PlacementCapture(
        string RoomCode,
        Guid PlayerId,
        string Word,
        bool WasMasked,
        bool WasAnagrammed,
        bool WasHinted);
    private sealed record PlayerEffectState(string[] Masked, string[] Anagrammed, string? HintWord);
}
