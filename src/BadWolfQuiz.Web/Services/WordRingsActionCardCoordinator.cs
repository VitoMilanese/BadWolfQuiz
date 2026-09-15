using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;

namespace BadWolfQuiz.Web.Services;

public enum WordRingsActionCardKind
{
    Swap = 1,
    Replace = 2,
    Block = 3,
    Temporary = 4,
    Immunity = 5,
    Hint = 6,
    Rest = 7,
    Shuffle = 8,
    Timeout = 9,
    Shield = 10,
    Mask = 11,
    Anagram = 12,
    Cleanse = 13
}

public sealed record WordRingsActionCardItem(int Id, bool IsTemporary);

public sealed record WordRingsActionCardTarget(Guid Id, string Name, bool IsSelf);

public sealed record WordRingsActionCardSnapshot(
    bool Enabled,
    bool Active,
    int CorrectWordsPerCard,
    int MaximumCards,
    int CorrectProgress,
    bool IsOwnTurn,
    IReadOnlyList<WordRingsActionCardItem> Cards,
    IReadOnlyList<WordRingsActionCardTarget> Players,
    IReadOnlyList<string> Words,
    IReadOnlyList<string> BlockedWords,
    string? HintWord,
    bool FailureImmunity,
    bool Shield,
    bool Masked,
    bool Anagrammed,
    long Revision);

public sealed record WordRingsActionCardUseResult(
    WordRingsActionCardSnapshot State,
    string TargetName,
    bool BlockedByShield);

public sealed class WordRingsActionCardCoordinator
{
    public const int MinimumCards = 2;
    public const int MaximumCards = 4;
    public const int MinimumCorrectWordsPerCard = 1;
    public const int MaximumCorrectWordsPerCard = 20;

    private static readonly WordRingsActionCardKind[] MultiplayerPool =
        Enum.GetValues<WordRingsActionCardKind>();

    private static readonly HashSet<WordRingsActionCardKind> OutOfTurnCards =
        [WordRingsActionCardKind.Shuffle, WordRingsActionCardKind.Shield, WordRingsActionCardKind.Cleanse];

    private static readonly ConcurrentDictionary<string, Lazy<WordRingsActionCardCoordinator>> Instances =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly FieldInfo SyncField = typeof(WordRingsRoomStore)
        .GetField("_sync", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static readonly FieldInfo RoomsField = typeof(WordRingsRoomStore)
        .GetField("_rooms", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private readonly WordRingsRoomStore _store;
    private readonly WordRingsRoomHostCoordinator _host;
    private readonly object _metaSync = new();
    private readonly Dictionary<string, RoomActionMeta> _rooms = new(StringComparer.OrdinalIgnoreCase);

    private WordRingsActionCardCoordinator(IWebHostEnvironment environment)
    {
        _store = WordRingsRoomStore.Get(environment);
        _host = WordRingsRoomHostCoordinator.Get(environment);
    }

    public static WordRingsActionCardCoordinator Get(IWebHostEnvironment environment)
    {
        var root = Path.GetFullPath(environment.ContentRootPath);
        return Instances.GetOrAdd(
            root,
            _ => new Lazy<WordRingsActionCardCoordinator>(
                () => new WordRingsActionCardCoordinator(environment),
                LazyThreadSafetyMode.ExecutionAndPublication)).Value;
    }

    public void RegisterRoom(
        WordRingsRoomConnection connection,
        bool enabled,
        int correctWordsPerCard,
        int maximumCards,
        string? previousRoomCode = null)
    {
        ValidateConfiguration(correctWordsPerCard, maximumCards);
        lock (_metaSync)
        {
            var previous = Normalize(previousRoomCode);
            if (previous.Length > 0 && !string.Equals(previous, connection.RoomCode, StringComparison.OrdinalIgnoreCase))
            {
                _rooms.Remove(previous);
            }

            var meta = new RoomActionMeta(enabled, correctWordsPerCard, maximumCards);
            SynchronizePlayers(meta, connection.State);
            meta.LastCurrentPlayerId = connection.State.CurrentPlayerId;
            _rooms[connection.RoomCode] = meta;
        }
    }

    public void BeginRound(string? roomCode, string? playerToken)
    {
        var state = _host.GetRoomState(roomCode, playerToken);
        lock (_metaSync)
        {
            var meta = RequireMeta(state.RoomCode);
            meta.Players.Clear();
            SynchronizePlayers(meta, state);
            meta.LastCurrentPlayerId = state.CurrentPlayerId;
            meta.Revision++;
        }
    }

    public WordRingsActionCardSnapshot GetState(string? roomCode, string? playerToken)
    {
        var state = SynchronizeTurnState(roomCode, playerToken);
        lock (_metaSync)
        {
            var meta = RequireMeta(state.RoomCode);
            SynchronizePlayers(meta, state);
            return BuildSnapshot(meta, state);
        }
    }

    public void EnsureWordUsable(string? roomCode, string? playerToken, string? word)
    {
        var state = SynchronizeTurnState(roomCode, playerToken);
        var normalizedWord = word?.Trim() ?? string.Empty;
        lock (_metaSync)
        {
            var meta = RequireMeta(state.RoomCode);
            if (!meta.Players.TryGetValue(state.PlayerId, out var player)) return;
            if (player.BlockedWords.Contains(normalizedWord))
            {
                throw new InvalidOperationException("ActionCardWordBlocked");
            }
        }
    }

    public WordRingsRoomPlacementResult? TryInterceptFailedPlacement(
        string? roomCode,
        string? playerToken,
        string? word,
        string? membership)
    {
        var state = SynchronizeTurnState(roomCode, playerToken);
        var normalizedWord = word?.Trim() ?? string.Empty;
        string expectedMembership;
        lock (SyncField.GetValue(_store)!)
        {
            var room = GetRoomObject(state.RoomCode);
            var puzzle = (WordRingsPuzzle)Get(room, "Puzzle")!;
            expectedMembership = CanonicalMembership(
                puzzle.Expected.TryGetValue(normalizedWord, out var expected)
                    ? expected
                    : string.Empty);
        }

        var actualMembership = CanonicalMembership(membership);
        if (string.Equals(actualMembership, expectedMembership, StringComparison.Ordinal))
        {
            return null;
        }

        lock (_metaSync)
        {
            var meta = RequireMeta(state.RoomCode);
            SynchronizePlayers(meta, state);
            if (!meta.Players.TryGetValue(state.PlayerId, out var player)) return null;
            if (player.BlockedWords.Contains(normalizedWord))
            {
                throw new InvalidOperationException("ActionCardWordBlocked");
            }
            if (!TryConsumeFailureImmunity(player)) return null;

            ConsumeAttemptEffects(player, normalizedWord);
            meta.Revision++;
        }

        var refreshed = _host.GetRoomState(state.RoomCode, playerToken);
        return new WordRingsRoomPlacementResult(
            refreshed,
            normalizedWord,
            actualMembership,
            expectedMembership,
            false,
            false,
            0,
            true,
            false);
    }

    public bool TryCancelHostedFailureWithImmunity(
        string? roomCode,
        string? hostToken,
        long placementId,
        WordRingsRoomHostPendingPlacement? pending)
    {
        if (pending is null || !pending.WasMoved) return false;
        var code = Normalize(roomCode);

        lock (_metaSync)
        {
            var meta = RequireMeta(code);
            if (!meta.Players.TryGetValue(pending.PlayerId, out var player) ||
                !TryConsumeFailureImmunity(player))
            {
                return false;
            }
            ConsumeAttemptEffects(player, pending.Word);
            meta.Revision++;
        }

        lock (SyncField.GetValue(_store)!)
        {
            var room = GetRoomObject(code);
            var placements = (IList)Get(room, "Placements")!;
            var placementIndex = FindIndex(placements, item => (long)Get(item, "Id")! == placementId);
            if (placementIndex < 0) return false;

            var players = Players(room);
            var target = FindPlayer(players, pending.PlayerId)
                ?? throw new WordRingsRoomException(WordRingsRoomError.InvalidPlayer);
            var words = RemainingWords(target);
            if (!ContainsWord(words, pending.Word)) words.Insert(0, pending.Word);
            placements.RemoveAt(placementIndex);
            Set(room, "TurnDeadlineUtc", null);
            Set(room, "PausedTurnSeconds", null);
            IncrementVersion(room);
        }

        return true;
    }

    public void RecordHostedSubmission(string? roomCode, Guid playerId, string? word)
    {
        var code = Normalize(roomCode);
        lock (_metaSync)
        {
            var meta = RequireMeta(code);
            if (!meta.Players.TryGetValue(playerId, out var player)) return;
            ConsumeAttemptEffects(player, word);
            meta.Revision++;
        }
    }

    public void RecordPlacementAttempt(
        string? roomCode,
        Guid playerId,
        string? word,
        bool fullyCorrect)
    {
        var code = Normalize(roomCode);
        WordRingsRoomSnapshot? roomState = null;
        try
        {
            roomState = GetAnyRoomState(code);
        }
        catch
        {
            // The room may already have expired; there is nothing to award.
        }

        lock (_metaSync)
        {
            var meta = RequireMeta(code);
            if (!meta.Players.TryGetValue(playerId, out var player)) return;
            ConsumeAttemptEffects(player, word);

            if (fullyCorrect && roomState is not null && IsActive(meta, roomState))
            {
                player.CorrectProgress++;
                if (player.CorrectProgress >= meta.CorrectWordsPerCard)
                {
                    player.CorrectProgress = 0;
                    TryAwardNormalCard(meta, player);
                }
            }
            meta.Revision++;
        }
    }

    public WordRingsActionCardUseResult UseCard(
        string? roomCode,
        string? playerToken,
        int cardId,
        Guid? targetPlayerId,
        string? word)
    {
        if (!Enum.IsDefined(typeof(WordRingsActionCardKind), cardId))
        {
            throw new InvalidOperationException("InvalidActionCard");
        }

        var kind = (WordRingsActionCardKind)cardId;
        var state = SynchronizeTurnState(roomCode, playerToken);
        var code = state.RoomCode;
        Guid actorId = state.PlayerId;
        Guid targetId;
        string targetName;
        bool blockedByShield = false;
        PlayerActionState actor;
        PlayerActionState target;
        int shuffleCount = 0;
        bool shuffleHadTemporary = false;
        TemporaryLifetime shuffleTemporaryLifetime = TemporaryLifetime.None;
        HashSet<WordRingsActionCardKind>? shuffleOldKinds = null;

        lock (_metaSync)
        {
            var meta = RequireMeta(code);
            SynchronizePlayers(meta, state);
            if (!IsActive(meta, state) || !IsParticipant(state, actorId))
            {
                throw new InvalidOperationException("ActionCardsUnavailable");
            }

            actor = RequirePlayer(meta, actorId);
            if (!HasCard(actor, kind)) throw new InvalidOperationException("ActionCardNotOwned");
            if (state.CurrentPlayerId != actorId && !OutOfTurnCards.Contains(kind))
            {
                throw new WordRingsRoomException(WordRingsRoomError.NotYourTurn);
            }

            targetId = ResolveTargetPlayerId(kind, state, actorId, targetPlayerId);
            target = RequirePlayer(meta, targetId);
            targetName = state.Players.First(player => player.Id == targetId).Name;

            if (kind == WordRingsActionCardKind.Shuffle && targetId == actorId)
            {
                shuffleCount = TotalCardCount(actor);
                shuffleHadTemporary = actor.TemporaryCard.HasValue;
                shuffleTemporaryLifetime = actor.TemporaryLifetime;
                shuffleOldKinds = CurrentKinds(actor);
            }
            else
            {
                ConsumeOwnedCard(actor, kind);
            }

            if (IsNegativeExternalEffect(kind, actorId, targetId) && target.Shield)
            {
                target.Shield = false;
                blockedByShield = true;
                meta.Revision++;
                return new WordRingsActionCardUseResult(
                    BuildSnapshot(meta, state),
                    targetName,
                    true);
            }
        }

        switch (kind)
        {
            case WordRingsActionCardKind.Swap:
                SwapWords(code, actorId, targetId, word);
                break;
            case WordRingsActionCardKind.Replace:
                ReplaceWord(code, actorId, targetId, word);
                break;
            case WordRingsActionCardKind.Block:
                ApplyBlock(code, targetId);
                break;
            case WordRingsActionCardKind.Temporary:
                GrantTemporaryCard(code, state, targetId);
                break;
            case WordRingsActionCardKind.Immunity:
                lock (_metaSync)
                {
                    RequirePlayer(RequireMeta(code), targetId).FailureImmunity = true;
                }
                break;
            case WordRingsActionCardKind.Hint:
                ApplyHint(code, targetId);
                break;
            case WordRingsActionCardKind.Rest:
                lock (_metaSync)
                {
                    RequirePlayer(RequireMeta(code), actorId).PendingTurnFailureImmunity = true;
                }
                AdvanceTurnUnderlying(code);
                break;
            case WordRingsActionCardKind.Shuffle:
                lock (_metaSync)
                {
                    var meta = RequireMeta(code);
                    var shuffleTarget = RequirePlayer(meta, targetId);
                    var count = targetId == actorId ? shuffleCount : TotalCardCount(shuffleTarget);
                    var hadTemporary = targetId == actorId ? shuffleHadTemporary : shuffleTarget.TemporaryCard.HasValue;
                    var lifetime = targetId == actorId ? shuffleTemporaryLifetime : shuffleTarget.TemporaryLifetime;
                    var oldKinds = targetId == actorId ? shuffleOldKinds! : CurrentKinds(shuffleTarget);
                    ShuffleCards(shuffleTarget, count, hadTemporary, lifetime, oldKinds);
                }
                break;
            case WordRingsActionCardKind.Timeout:
                lock (_metaSync)
                {
                    RequirePlayer(RequireMeta(code), targetId).SkipTurns++;
                }
                break;
            case WordRingsActionCardKind.Shield:
                lock (_metaSync)
                {
                    RequirePlayer(RequireMeta(code), targetId).Shield = true;
                }
                break;
            case WordRingsActionCardKind.Mask:
                lock (_metaSync)
                {
                    RequirePlayer(RequireMeta(code), targetId).Masked = true;
                }
                break;
            case WordRingsActionCardKind.Anagram:
                lock (_metaSync)
                {
                    RequirePlayer(RequireMeta(code), targetId).Anagrammed = true;
                }
                break;
            case WordRingsActionCardKind.Cleanse:
                ApplyCleanse(code, state, targetId);
                break;
            default:
                throw new InvalidOperationException("InvalidActionCard");
        }

        lock (_metaSync)
        {
            RequireMeta(code).Revision++;
        }
        var refreshed = SynchronizeTurnState(code, playerToken);
        lock (_metaSync)
        {
            var meta = RequireMeta(code);
            return new WordRingsActionCardUseResult(
                BuildSnapshot(meta, refreshed),
                targetName,
                blockedByShield);
        }
    }

    private WordRingsRoomSnapshot SynchronizeTurnState(string? roomCode, string? playerToken)
    {
        var state = _host.GetRoomState(roomCode, playerToken);
        for (var guard = 0; guard < 16; guard++)
        {
            var skipCurrent = false;
            lock (_metaSync)
            {
                var meta = RequireMeta(state.RoomCode);
                SynchronizePlayers(meta, state);
                ApplyTurnBoundary(meta, state);
                if (IsActive(meta, state) && state.CurrentPlayerId is Guid currentId &&
                    meta.Players.TryGetValue(currentId, out var current) && current.SkipTurns > 0)
                {
                    current.SkipTurns--;
                    meta.Revision++;
                    skipCurrent = true;
                }
            }

            if (!skipCurrent) return state;
            AdvanceTurnUnderlying(state.RoomCode);
            state = _host.GetRoomState(state.RoomCode, playerToken);
        }
        return state;
    }

    private WordRingsRoomSnapshot GetAnyRoomState(string roomCode)
    {
        lock (SyncField.GetValue(_store)!)
        {
            var room = GetRoomObject(roomCode);
            var players = Players(room);
            var player = players.Cast<object>().FirstOrDefault();
            if (player is null) throw new WordRingsRoomException(WordRingsRoomError.InvalidPlayer);
            var token = (string)Get(player, "Token")!;
            return _host.GetRoomState(roomCode, token);
        }
    }

    private static void ValidateConfiguration(int correctWordsPerCard, int maximumCards)
    {
        if (correctWordsPerCard is < MinimumCorrectWordsPerCard or > MaximumCorrectWordsPerCard ||
            maximumCards is < MinimumCards or > MaximumCards)
        {
            throw new InvalidOperationException("InvalidActionCardConfiguration");
        }
    }

    private static void SynchronizePlayers(RoomActionMeta meta, WordRingsRoomSnapshot state)
    {
        var live = state.Players.Select(player => player.Id).ToHashSet();
        foreach (var stale in meta.Players.Keys.Where(id => !live.Contains(id)).ToArray())
        {
            meta.Players.Remove(stale);
        }
        foreach (var player in state.Players)
        {
            if (!meta.Players.ContainsKey(player.Id)) meta.Players[player.Id] = new PlayerActionState();
        }
    }

    private static void ApplyTurnBoundary(RoomActionMeta meta, WordRingsRoomSnapshot state)
    {
        var previousId = meta.LastCurrentPlayerId;
        var nextId = state.CurrentPlayerId;
        if (previousId == nextId) return;

        if (previousId is Guid oldId && meta.Players.TryGetValue(oldId, out var oldPlayer))
        {
            if (oldPlayer.BlockExpiresAtTurnEnd)
            {
                oldPlayer.BlockedWords.Clear();
                oldPlayer.BlockExpiresAtTurnEnd = false;
            }
            oldPlayer.TurnFailureImmunity = false;
            if (oldPlayer.TemporaryLifetime == TemporaryLifetime.ExpireAtTurnEnd)
            {
                ClearTemporaryCard(oldPlayer);
            }
        }

        if (nextId is Guid newId && meta.Players.TryGetValue(newId, out var newPlayer))
        {
            if (newPlayer.PendingTurnFailureImmunity)
            {
                newPlayer.PendingTurnFailureImmunity = false;
                newPlayer.TurnFailureImmunity = true;
            }
            if (newPlayer.BlockAwaitingTurn)
            {
                newPlayer.BlockAwaitingTurn = false;
                newPlayer.BlockExpiresAtTurnEnd = true;
            }
            if (newPlayer.TemporaryLifetime == TemporaryLifetime.ExpireOnNextTurnStart)
            {
                ClearTemporaryCard(newPlayer);
            }
            else if (newPlayer.TemporaryLifetime == TemporaryLifetime.AwaitNextTurnThenExpireAtEnd)
            {
                newPlayer.TemporaryLifetime = TemporaryLifetime.ExpireAtTurnEnd;
            }
        }

        meta.LastCurrentPlayerId = nextId;
        meta.Revision++;
    }

    private static WordRingsActionCardSnapshot BuildSnapshot(RoomActionMeta meta, WordRingsRoomSnapshot state)
    {
        var own = RequirePlayer(meta, state.PlayerId);
        var cards = own.Cards
            .Select(kind => new WordRingsActionCardItem((int)kind, false))
            .Concat(own.TemporaryCard is WordRingsActionCardKind temporary
                ? [new WordRingsActionCardItem((int)temporary, true)]
                : Array.Empty<WordRingsActionCardItem>())
            .ToArray();
        var targets = state.Players
            .Where(player => IsParticipant(state, player.Id))
            .Select(player => new WordRingsActionCardTarget(player.Id, player.Name, player.Id == state.PlayerId))
            .ToArray();

        return new WordRingsActionCardSnapshot(
            meta.Enabled,
            IsActive(meta, state) && IsParticipant(state, state.PlayerId),
            meta.CorrectWordsPerCard,
            meta.MaximumCards,
            own.CorrectProgress,
            state.Phase == "playing" && state.CurrentPlayerId == state.PlayerId,
            cards,
            targets,
            state.BankWords.ToArray(),
            own.BlockedWords.ToArray(),
            own.HintWord,
            own.FailureImmunity || own.TurnFailureImmunity || own.PendingTurnFailureImmunity,
            own.Shield,
            own.Masked,
            own.Anagrammed,
            meta.Revision);
    }

    private static bool IsActive(RoomActionMeta meta, WordRingsRoomSnapshot state) =>
        meta.Enabled &&
        string.Equals(state.Phase, "playing", StringComparison.Ordinal) &&
        state.Players.Count(player => IsParticipant(state, player.Id)) >= 2;

    private static bool IsParticipant(WordRingsRoomSnapshot state, Guid playerId)
    {
        var player = state.Players.FirstOrDefault(item => item.Id == playerId);
        return player is not null && !(state.DedicatedHostMode && player.IsHost);
    }

    private static Guid ResolveTargetPlayerId(
        WordRingsActionCardKind kind,
        WordRingsRoomSnapshot state,
        Guid actorId,
        Guid? requestedTargetId)
    {
        var participantIds = state.Players
            .Where(player => IsParticipant(state, player.Id))
            .Select(player => player.Id)
            .ToArray();
        var others = participantIds.Where(id => id != actorId).ToArray();

        if (kind is WordRingsActionCardKind.Swap or WordRingsActionCardKind.Block)
        {
            if (others.Length == 0) throw new InvalidOperationException("ActionCardTargetUnavailable");
            return others[Random.Shared.Next(others.Length)];
        }
        if (kind == WordRingsActionCardKind.Rest) return actorId;
        if (kind == WordRingsActionCardKind.Timeout)
        {
            if (requestedTargetId is not Guid timeoutTarget || timeoutTarget == actorId || !others.Contains(timeoutTarget))
            {
                throw new InvalidOperationException("ActionCardTargetUnavailable");
            }
            return timeoutTarget;
        }

        var target = requestedTargetId ?? actorId;
        if (!participantIds.Contains(target)) throw new InvalidOperationException("ActionCardTargetUnavailable");
        return target;
    }

    private static bool IsNegativeExternalEffect(
        WordRingsActionCardKind kind,
        Guid actorId,
        Guid targetId) =>
        actorId != targetId && kind is
            WordRingsActionCardKind.Swap or
            WordRingsActionCardKind.Replace or
            WordRingsActionCardKind.Block or
            WordRingsActionCardKind.Timeout or
            WordRingsActionCardKind.Mask or
            WordRingsActionCardKind.Anagram;

    private void SwapWords(string code, Guid actorId, Guid targetId, string? requestedWord)
    {
        lock (SyncField.GetValue(_store)!)
        {
            var room = GetRoomObject(code);
            var players = Players(room);
            var actor = FindPlayer(players, actorId) ?? throw new WordRingsRoomException(WordRingsRoomError.InvalidPlayer);
            var target = FindPlayer(players, targetId) ?? throw new WordRingsRoomException(WordRingsRoomError.InvalidPlayer);
            var actorWords = RemainingWords(actor);
            var targetWords = RemainingWords(target);
            if (actorWords.Count == 0 || targetWords.Count == 0) throw new InvalidOperationException("ActionCardWordUnavailable");

            var actorIndex = FindWordIndex(actorWords, requestedWord);
            if (actorIndex < 0) actorIndex = Random.Shared.Next(Math.Min(actorWords.Count, WordRingsRoomStore.MaximumBankWords));
            var targetIndex = Random.Shared.Next(Math.Min(targetWords.Count, WordRingsRoomStore.MaximumBankWords));
            var actorWord = (string)actorWords[actorIndex]!;
            var targetWord = (string)targetWords[targetIndex]!;
            actorWords[actorIndex] = targetWord;
            targetWords[targetIndex] = actorWord;
            IncrementVersion(room);

            lock (_metaSync)
            {
                var meta = RequireMeta(code);
                var actorMeta = RequirePlayer(meta, actorId);
                var targetMeta = RequirePlayer(meta, targetId);
                if (string.Equals(actorMeta.HintWord, actorWord, StringComparison.OrdinalIgnoreCase)) actorMeta.HintWord = null;
                if (string.Equals(targetMeta.HintWord, targetWord, StringComparison.OrdinalIgnoreCase)) targetMeta.HintWord = null;
            }
        }
    }

    private void ReplaceWord(string code, Guid actorId, Guid targetId, string? requestedWord)
    {
        lock (SyncField.GetValue(_store)!)
        {
            var room = GetRoomObject(code);
            var players = Players(room);
            var target = FindPlayer(players, targetId) ?? throw new WordRingsRoomException(WordRingsRoomError.InvalidPlayer);
            var words = RemainingWords(target);
            if (words.Count == 0) throw new InvalidOperationException("ActionCardWordUnavailable");
            var index = targetId == actorId ? FindWordIndex(words, requestedWord) : -1;
            if (index < 0) index = Random.Shared.Next(Math.Min(words.Count, WordRingsRoomStore.MaximumBankWords));
            var discarded = (string)words[index]!;
            var replacement = PickUnusedPuzzleWord(room, discarded);
            if (replacement is null) throw new InvalidOperationException("ActionCardWordUnavailable");
            words[index] = replacement;
            IncrementVersion(room);

            lock (_metaSync)
            {
                var targetMeta = RequirePlayer(RequireMeta(code), targetId);
                if (string.Equals(targetMeta.HintWord, discarded, StringComparison.OrdinalIgnoreCase)) targetMeta.HintWord = null;
            }
        }
    }

    private void ApplyBlock(string code, Guid targetId)
    {
        string[] blocked;
        lock (SyncField.GetValue(_store)!)
        {
            var room = GetRoomObject(code);
            var target = FindPlayer(Players(room), targetId) ?? throw new WordRingsRoomException(WordRingsRoomError.InvalidPlayer);
            var words = RemainingWords(target).Cast<string>()
                .Take(WordRingsRoomStore.MaximumBankWords)
                .OrderBy(_ => Random.Shared.Next())
                .Take(2)
                .ToArray();
            if (words.Length == 0) throw new InvalidOperationException("ActionCardWordUnavailable");
            blocked = words;
        }

        lock (_metaSync)
        {
            var meta = RequireMeta(code);
            var target = RequirePlayer(meta, targetId);
            target.BlockedWords.Clear();
            foreach (var item in blocked) target.BlockedWords.Add(item);
            target.BlockAwaitingTurn = true;
            target.BlockExpiresAtTurnEnd = false;
        }
    }

    private void GrantTemporaryCard(string code, WordRingsRoomSnapshot state, Guid targetId)
    {
        lock (_metaSync)
        {
            var meta = RequireMeta(code);
            var target = RequirePlayer(meta, targetId);
            var excluded = CurrentKinds(target);
            var candidates = MultiplayerPool
                .Where(kind => kind != WordRingsActionCardKind.Temporary && !excluded.Contains(kind))
                .OrderBy(_ => Random.Shared.Next())
                .ToArray();
            if (candidates.Length == 0) return;
            target.TemporaryCard = candidates[0];
            target.TemporaryLifetime = state.CurrentPlayerId == targetId
                ? TemporaryLifetime.ExpireOnNextTurnStart
                : TemporaryLifetime.AwaitNextTurnThenExpireAtEnd;
        }
    }

    private void ApplyHint(string code, Guid targetId)
    {
        string? hint;
        lock (SyncField.GetValue(_store)!)
        {
            var room = GetRoomObject(code);
            var target = FindPlayer(Players(room), targetId) ?? throw new WordRingsRoomException(WordRingsRoomError.InvalidPlayer);
            var puzzle = (WordRingsPuzzle)Get(room, "Puzzle")!;
            hint = RemainingWords(target).Cast<string>()
                .Take(WordRingsRoomStore.MaximumBankWords)
                .Where(word => puzzle.Expected.TryGetValue(word, out var membership) && !string.IsNullOrEmpty(membership))
                .OrderBy(_ => Random.Shared.Next())
                .FirstOrDefault();
        }
        if (hint is null) throw new InvalidOperationException("ActionCardWordUnavailable");
        lock (_metaSync)
        {
            RequirePlayer(RequireMeta(code), targetId).HintWord = hint;
        }
    }

    private void ApplyCleanse(string code, WordRingsRoomSnapshot state, Guid targetId)
    {
        lock (_metaSync)
        {
            var target = RequirePlayer(RequireMeta(code), targetId);
            var hadNegative = target.BlockedWords.Count > 0 || target.BlockAwaitingTurn || target.BlockExpiresAtTurnEnd ||
                              target.Masked || target.Anagrammed || target.SkipTurns > 0;
            target.BlockedWords.Clear();
            target.BlockAwaitingTurn = false;
            target.BlockExpiresAtTurnEnd = false;
            target.Masked = false;
            target.Anagrammed = false;
            target.SkipTurns = 0;
            if (!hadNegative)
            {
                if (state.CurrentPlayerId == targetId) target.TurnFailureImmunity = true;
                else target.PendingTurnFailureImmunity = true;
            }
        }
    }

    private static void ConsumeAttemptEffects(PlayerActionState player, string? word)
    {
        player.Masked = false;
        player.Anagrammed = false;
        if (!string.IsNullOrWhiteSpace(word) &&
            string.Equals(player.HintWord, word.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            player.HintWord = null;
        }
    }

    private static bool TryConsumeFailureImmunity(PlayerActionState player)
    {
        if (player.TurnFailureImmunity)
        {
            player.TurnFailureImmunity = false;
            return true;
        }
        if (player.FailureImmunity)
        {
            player.FailureImmunity = false;
            return true;
        }
        return false;
    }

    private static void TryAwardNormalCard(RoomActionMeta meta, PlayerActionState player)
    {
        if (player.Cards.Count >= meta.MaximumCards) return;
        var excluded = CurrentKinds(player);
        var candidates = MultiplayerPool.Where(kind => !excluded.Contains(kind)).ToArray();
        if (candidates.Length == 0) return;
        player.Cards.Add(candidates[Random.Shared.Next(candidates.Length)]);
    }

    private static void ShuffleCards(
        PlayerActionState player,
        int desiredCount,
        bool hadTemporary,
        TemporaryLifetime temporaryLifetime,
        IReadOnlySet<WordRingsActionCardKind> oldKinds)
    {
        player.Cards.Clear();
        ClearTemporaryCard(player);
        if (desiredCount <= 0) return;

        var candidates = MultiplayerPool
            .Where(kind => !oldKinds.Contains(kind))
            .OrderBy(_ => Random.Shared.Next())
            .ToList();
        if (candidates.Count < desiredCount)
        {
            candidates.AddRange(MultiplayerPool
                .Where(kind => !candidates.Contains(kind))
                .OrderBy(_ => Random.Shared.Next()));
        }
        var picked = candidates.Take(desiredCount).ToArray();
        var normalCount = desiredCount - (hadTemporary ? 1 : 0);
        player.Cards.AddRange(picked.Take(normalCount));
        if (hadTemporary && picked.Length > normalCount)
        {
            player.TemporaryCard = picked[normalCount];
            player.TemporaryLifetime = temporaryLifetime;
        }
    }

    private static HashSet<WordRingsActionCardKind> CurrentKinds(PlayerActionState player)
    {
        var result = player.Cards.ToHashSet();
        if (player.TemporaryCard is WordRingsActionCardKind temporary) result.Add(temporary);
        return result;
    }

    private static int TotalCardCount(PlayerActionState player) =>
        player.Cards.Count + (player.TemporaryCard.HasValue ? 1 : 0);

    private static bool HasCard(PlayerActionState player, WordRingsActionCardKind kind) =>
        player.Cards.Contains(kind) || player.TemporaryCard == kind;

    private static void ConsumeOwnedCard(PlayerActionState player, WordRingsActionCardKind kind)
    {
        if (player.TemporaryCard == kind)
        {
            ClearTemporaryCard(player);
            return;
        }
        if (!player.Cards.Remove(kind)) throw new InvalidOperationException("ActionCardNotOwned");
    }

    private static void ClearTemporaryCard(PlayerActionState player)
    {
        player.TemporaryCard = null;
        player.TemporaryLifetime = TemporaryLifetime.None;
    }

    private void AdvanceTurnUnderlying(string code)
    {
        lock (SyncField.GetValue(_store)!)
        {
            var room = GetRoomObject(code);
            var players = Players(room);
            var current = (int)Get(room, "CurrentPlayerIndex")!;
            Set(room, "OutsidePointAwardedThisTurn", false);
            Set(room, "TurnDeadlineUtc", null);
            Set(room, "PausedTurnSeconds", null);
            if (players.Count == 0)
            {
                Set(room, "CurrentPlayerIndex", -1);
                IncrementVersion(room);
                return;
            }

            for (var offset = 1; offset <= players.Count; offset++)
            {
                var index = (current + offset + players.Count) % players.Count;
                var player = players[index]!;
                if ((bool)Get(player, "IsPlayingParticipant")! && RemainingWords(player).Count > 0)
                {
                    Set(room, "CurrentPlayerIndex", index);
                    IncrementVersion(room);
                    return;
                }
            }
            Set(room, "CurrentPlayerIndex", -1);
            IncrementVersion(room);
        }
    }

    private string? PickUnusedPuzzleWord(object room, string discarded)
    {
        var puzzle = (WordRingsPuzzle)Get(room, "Puzzle")!;
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var player in Players(room).Cast<object>())
        {
            foreach (var word in RemainingWords(player).Cast<string>()) used.Add(word);
        }
        foreach (var placement in ((IList)Get(room, "Placements")!).Cast<object>())
        {
            if (Get(placement, "Word") is string word) used.Add(word);
        }
        used.Remove(discarded);

        var candidates = puzzle.Words
            .Where(word => !string.Equals(word, discarded, StringComparison.OrdinalIgnoreCase) && !used.Contains(word))
            .OrderBy(_ => Random.Shared.Next())
            .ToArray();
        if (candidates.Length > 0) return candidates[0];

        return puzzle.Words
            .Where(word => !string.Equals(word, discarded, StringComparison.OrdinalIgnoreCase))
            .OrderBy(_ => Random.Shared.Next())
            .FirstOrDefault();
    }

    private RoomActionMeta RequireMeta(string code) =>
        _rooms.TryGetValue(Normalize(code), out var meta)
            ? meta
            : throw new InvalidOperationException("ActionCardRoomNotConfigured");

    private static PlayerActionState RequirePlayer(RoomActionMeta meta, Guid playerId) =>
        meta.Players.TryGetValue(playerId, out var player)
            ? player
            : throw new WordRingsRoomException(WordRingsRoomError.InvalidPlayer);

    private object GetRoomObject(string code)
    {
        var rooms = (IDictionary)RoomsField.GetValue(_store)!;
        return rooms[Normalize(code)] ?? throw new WordRingsRoomException(WordRingsRoomError.RoomNotFound);
    }

    private static IList Players(object room) => (IList)Get(room, "Players")!;
    private static IList RemainingWords(object player) => (IList)Get(player, "RemainingWords")!;

    private static object? FindPlayer(IList players, Guid id)
    {
        foreach (var player in players.Cast<object>())
        {
            if ((Guid)Get(player, "Id")! == id) return player;
        }
        return null;
    }

    private static int FindWordIndex(IList words, string? requestedWord)
    {
        if (string.IsNullOrWhiteSpace(requestedWord)) return -1;
        for (var index = 0; index < words.Count; index++)
        {
            if (string.Equals((string?)words[index], requestedWord.Trim(), StringComparison.OrdinalIgnoreCase)) return index;
        }
        return -1;
    }

    private static bool ContainsWord(IList words, string word) =>
        words.Cast<string>().Any(item => string.Equals(item, word, StringComparison.OrdinalIgnoreCase));

    private static int FindIndex(IList items, Func<object, bool> predicate)
    {
        for (var index = 0; index < items.Count; index++)
        {
            if (items[index] is object item && predicate(item)) return index;
        }
        return -1;
    }

    private static object? Get(object instance, string property) =>
        instance.GetType().GetProperty(property, BindingFlags.Instance | BindingFlags.Public)!.GetValue(instance);

    private static void Set(object instance, string property, object? value) =>
        instance.GetType().GetProperty(property, BindingFlags.Instance | BindingFlags.Public)!.SetValue(instance, value);

    private static void IncrementVersion(object room) =>
        Set(room, "Version", (long)Get(room, "Version")! + 1);

    private static string Normalize(string? value) => (value ?? string.Empty).Trim().ToUpperInvariant();

    private static string CanonicalMembership(string? value) =>
        new((value ?? string.Empty)
            .Trim()
            .ToUpperInvariant()
            .Where(character => character is 'A' or 'B' or 'C')
            .Distinct()
            .OrderBy(character => character)
            .ToArray());

    private sealed class RoomActionMeta(bool enabled, int correctWordsPerCard, int maximumCards)
    {
        public bool Enabled { get; } = enabled;
        public int CorrectWordsPerCard { get; } = correctWordsPerCard;
        public int MaximumCards { get; } = maximumCards;
        public Dictionary<Guid, PlayerActionState> Players { get; } = [];
        public Guid? LastCurrentPlayerId { get; set; }
        public long Revision { get; set; }
    }

    private sealed class PlayerActionState
    {
        public List<WordRingsActionCardKind> Cards { get; } = [];
        public WordRingsActionCardKind? TemporaryCard { get; set; }
        public TemporaryLifetime TemporaryLifetime { get; set; }
        public int CorrectProgress { get; set; }
        public bool FailureImmunity { get; set; }
        public bool TurnFailureImmunity { get; set; }
        public bool PendingTurnFailureImmunity { get; set; }
        public bool Shield { get; set; }
        public HashSet<string> BlockedWords { get; } = new(StringComparer.OrdinalIgnoreCase);
        public bool BlockAwaitingTurn { get; set; }
        public bool BlockExpiresAtTurnEnd { get; set; }
        public string? HintWord { get; set; }
        public bool Masked { get; set; }
        public bool Anagrammed { get; set; }
        public int SkipTurns { get; set; }
    }

    private enum TemporaryLifetime
    {
        None,
        ExpireOnNextTurnStart,
        AwaitNextTurnThenExpireAtEnd,
        ExpireAtTurnEnd
    }
}