using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;

namespace BadWolfQuiz.Web.Services;

public sealed record WordRingsGameplayOptionsSnapshot(int TargetScore, int HandSize);

public sealed record WordRingsWordCountCapture(Guid Id);

public sealed class WordRingsGameplayOptionsCoordinator
{
    public const int MinimumTargetScore = 5;
    public const int MaximumTargetScore = 20;
    public const int MinimumHandSize = 5;
    public const int MaximumHandSize = 10;
    public const int DefaultTargetScore = 5;
    public const int DefaultHandSize = 5;

    private static readonly ConcurrentDictionary<string, Lazy<WordRingsGameplayOptionsCoordinator>> Instances =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly FieldInfo StoreSyncField = typeof(WordRingsRoomStore)
        .GetField("_sync", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static readonly FieldInfo StoreRoomsField = typeof(WordRingsRoomStore)
        .GetField("_rooms", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static readonly FieldInfo ActionMetaSyncField = typeof(WordRingsActionCardCoordinator)
        .GetField("_metaSync", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static readonly FieldInfo ActionRoomsField = typeof(WordRingsActionCardCoordinator)
        .GetField("_rooms", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private readonly WordRingsRoomStore _store;
    private readonly WordRingsRoomHostCoordinator _host;
    private readonly WordRingsActionCardCoordinator _cards;
    private readonly object _sync = new();
    private readonly Dictionary<string, RoomOptions> _roomOptions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<Guid, WordCountCaptureState> _wordCountCaptures = [];

    private WordRingsGameplayOptionsCoordinator(IWebHostEnvironment environment)
    {
        _store = WordRingsRoomStore.Get(environment);
        _host = WordRingsRoomHostCoordinator.Get(environment);
        _cards = WordRingsActionCardCoordinator.Get(environment);
    }

    public static WordRingsGameplayOptionsCoordinator Get(IWebHostEnvironment environment)
    {
        var root = Path.GetFullPath(environment.ContentRootPath);
        return Instances.GetOrAdd(
            root,
            _ => new Lazy<WordRingsGameplayOptionsCoordinator>(
                () => new WordRingsGameplayOptionsCoordinator(environment),
                LazyThreadSafetyMode.ExecutionAndPublication)).Value;
    }

    public WordRingsGameplayOptionsSnapshot ConfigureRoom(
        string? roomCode,
        string? playerToken,
        int targetScore,
        int handSize)
    {
        Validate(targetScore, handSize);
        var state = _host.GetRoomState(roomCode, playerToken);
        if (!state.IsHost)
        {
            throw new WordRingsRoomException(WordRingsRoomError.NotHost);
        }

        lock (_sync)
        {
            _roomOptions[state.RoomCode] = new RoomOptions(targetScore, handSize);
        }
        return new WordRingsGameplayOptionsSnapshot(targetScore, handSize);
    }

    public WordRingsGameplayOptionsSnapshot GetOptions(string? roomCode, string? playerToken)
    {
        var state = _host.GetRoomState(roomCode, playerToken);
        var options = GetOrCreateOptions(state.RoomCode, state.TargetScore);
        return new WordRingsGameplayOptionsSnapshot(state.TargetScore, options.HandSize);
    }

    public WordRingsRoomSnapshot GetDecoratedRoomState(string? roomCode, string? playerToken) =>
        DecorateRoomState(_host.GetRoomState(roomCode, playerToken));

    public WordRingsRoomSnapshot DecorateRoomState(WordRingsRoomSnapshot state)
    {
        var options = GetOrCreateOptions(state.RoomCode, state.TargetScore);
        var allWords = state.BankWords.Concat(state.QueuedWords).ToArray();
        var allowed = state.DedicatedHostMode && state.IsHost
            ? Math.Min(options.HandSize, allWords.Length)
            : Math.Min(GetEffectiveHandSize(options.HandSize, state.TargetScore, state.PlayerScore), allWords.Length);

        return state with
        {
            BankWords = allWords.Take(allowed).ToArray(),
            QueuedWords = allWords.Skip(allowed).ToArray()
        };
    }

    public WordRingsRoomHostSnapshot DecorateHostState(WordRingsRoomHostSnapshot state)
    {
        var options = GetOrCreateOptions(state.RoomCode, state.TargetScore);
        return state with { HandLimit = options.HandSize };
    }

    public int GetEffectiveHandSize(string? roomCode, Guid playerId)
    {
        var code = Normalize(roomCode);
        lock (StoreSyncField.GetValue(_store)!)
        {
            var room = GetRoomObject(code);
            var targetScore = Convert.ToDouble(Get(room, "TargetScore"));
            var player = FindPlayer((IList)Get(room, "Players")!, playerId)
                ?? throw new WordRingsRoomException(WordRingsRoomError.InvalidPlayer);
            var score = Convert.ToDouble(Get(player, "Score"));
            var options = GetOrCreateOptions(code, (int)Math.Round(targetScore));
            return GetEffectiveHandSize(options.HandSize, targetScore, score);
        }
    }

    public WordRingsRoomSnapshot PrepareRound(string? roomCode, string? playerToken)
    {
        var state = _host.GetRoomState(roomCode, playerToken);
        if (!state.IsHost)
        {
            throw new WordRingsRoomException(WordRingsRoomError.NotHost);
        }

        var options = GetOrCreateOptions(state.RoomCode, state.TargetScore);
        lock (_sync)
        {
            options.FirstPlayerSelected = false;
        }

        if (!state.SeedSetupPending)
        {
            SelectFirstPlayer(state.RoomCode, state.DedicatedHostMode);
        }
        return DecorateRoomState(_host.GetRoomState(state.RoomCode, playerToken));
    }

    public WordRingsRoomHostSnapshot SelectFirstPlayerAfterSeeds(string? roomCode, string? playerToken)
    {
        var state = _host.GetRoomState(roomCode, playerToken);
        if (!state.IsHost)
        {
            throw new WordRingsRoomException(WordRingsRoomError.NotHost);
        }
        SelectFirstPlayer(state.RoomCode, state.DedicatedHostMode);
        return DecorateHostState(_host.GetHostState(state.RoomCode, playerToken));
    }

    public WordRingsWordCountCapture CaptureWordCounts(string? roomCode, string? playerToken)
    {
        var state = _host.GetRoomState(roomCode, playerToken);
        Dictionary<Guid, int> counts;
        lock (StoreSyncField.GetValue(_store)!)
        {
            var room = GetRoomObject(state.RoomCode);
            counts = ((IList)Get(room, "Players")!)
                .Cast<object>()
                .ToDictionary(
                    player => (Guid)Get(player, "Id")!,
                    player => ((IList)Get(player, "RemainingWords")!).Count);
        }

        var id = Guid.NewGuid();
        lock (_sync)
        {
            _wordCountCaptures[id] = new WordCountCaptureState(state.RoomCode, state.PlayerId, counts);
        }
        return new WordRingsWordCountCapture(id);
    }

    public void RestoreWordCounts(
        string? roomCode,
        string? playerToken,
        Guid captureId,
        bool allowCallerReturnedWord)
    {
        var caller = _host.GetRoomState(roomCode, playerToken);
        WordCountCaptureState capture;
        lock (_sync)
        {
            if (!_wordCountCaptures.Remove(captureId, out capture!) ||
                !string.Equals(capture.RoomCode, caller.RoomCode, StringComparison.OrdinalIgnoreCase) ||
                capture.CallerPlayerId != caller.PlayerId)
            {
                throw new InvalidOperationException("WordCountCaptureUnavailable");
            }
        }

        var changed = false;
        lock (StoreSyncField.GetValue(_store)!)
        {
            var room = GetRoomObject(caller.RoomCode);
            foreach (var player in ((IList)Get(room, "Players")!).Cast<object>())
            {
                var playerId = (Guid)Get(player, "Id")!;
                if (!capture.Counts.TryGetValue(playerId, out var maximum)) continue;
                if (allowCallerReturnedWord && playerId == caller.PlayerId) maximum++;
                var words = (IList)Get(player, "RemainingWords")!;
                while (words.Count > maximum)
                {
                    words.RemoveAt(words.Count - 1);
                    changed = true;
                }
            }
            if (changed) IncrementVersion(room);
        }
    }

    public WordRingsRoomSnapshot FinalizePlayerExhaustion(
        string? roomCode,
        string? playerToken,
        Guid playerId)
    {
        var caller = _host.GetRoomState(roomCode, playerToken);
        if (!caller.IsHost && caller.PlayerId != playerId)
        {
            throw new WordRingsRoomException(WordRingsRoomError.InvalidPlayer);
        }
        ApplyExhaustionWin(caller.RoomCode, playerId);
        return DecorateRoomState(_host.GetRoomState(caller.RoomCode, playerToken));
    }

    public WordRingsRoomHostSnapshot FinalizePlacementExhaustion(
        string? roomCode,
        string? playerToken,
        long placementId)
    {
        var caller = _host.GetRoomState(roomCode, playerToken);
        if (!caller.IsHost)
        {
            throw new WordRingsRoomException(WordRingsRoomError.NotHost);
        }

        Guid? playerId = null;
        lock (StoreSyncField.GetValue(_store)!)
        {
            var room = GetRoomObject(caller.RoomCode);
            var placement = ((IList)Get(room, "Placements")!).Cast<object>()
                .FirstOrDefault(item => Convert.ToInt64(Get(item, "Id")) == placementId);
            if (placement is not null)
            {
                playerId = (Guid)Get(placement, "PlayerId")!;
            }
        }
        if (playerId is Guid id) ApplyExhaustionWin(caller.RoomCode, id);
        return DecorateHostState(_host.GetHostState(caller.RoomCode, playerToken));
    }

    public WordRingsActionCardSnapshot NormalizeActionCardEffects(
        string? roomCode,
        string? playerToken,
        int cardId,
        string? targetName)
    {
        var state = _host.GetRoomState(roomCode, playerToken);
        var target = state.Players.FirstOrDefault(player =>
            string.Equals(player.Name, targetName, StringComparison.Ordinal))
            ?? state.Players.First(player => player.Id == state.PlayerId);
        var visible = GetVisibleWords(state.RoomCode, target.Id);
        var changed = false;

        var metaSync = ActionMetaSyncField.GetValue(_cards)!;
        lock (metaSync)
        {
            var rooms = (IDictionary)ActionRoomsField.GetValue(_cards)!;
            if (!rooms.Contains(state.RoomCode)) return _cards.GetState(state.RoomCode, playerToken);
            var meta = rooms[state.RoomCode]!;
            var players = (IDictionary)Get(meta, "Players")!;
            if (!players.Contains(target.Id)) return _cards.GetState(state.RoomCode, playerToken);
            var targetMeta = players[target.Id]!;

            if (cardId == (int)WordRingsActionCardKind.Block)
            {
                var blocked = (HashSet<string>)Get(targetMeta, "BlockedWords")!;
                var replacement = visible
                    .OrderBy(_ => Random.Shared.Next())
                    .Take(Math.Min(2, visible.Count))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                if (!blocked.SetEquals(replacement))
                {
                    blocked.Clear();
                    blocked.UnionWith(replacement);
                    changed = true;
                }
            }
            else if (cardId == (int)WordRingsActionCardKind.Hint)
            {
                var hint = PickVisibleScoringWord(state.RoomCode, visible);
                if (!string.Equals((string?)Get(targetMeta, "HintWord"), hint, StringComparison.OrdinalIgnoreCase))
                {
                    Set(targetMeta, "HintWord", hint);
                    changed = true;
                }
            }
            else if (cardId == (int)WordRingsActionCardKind.Mask ||
                     cardId == (int)WordRingsActionCardKind.Anagram)
            {
                var property = cardId == (int)WordRingsActionCardKind.Mask ? "MaskedWords" : "AnagrammedWords";
                var affected = (HashSet<string>)Get(targetMeta, property)!;
                var visibleSet = visible.ToHashSet(StringComparer.OrdinalIgnoreCase);
                changed = affected.RemoveWhere(word => !visibleSet.Contains(word)) > 0;
            }

            if (changed)
            {
                Set(meta, "Revision", Convert.ToInt64(Get(meta, "Revision")) + 1);
            }
        }

        return _cards.GetState(state.RoomCode, playerToken);
    }

    private List<string> GetVisibleWords(string code, Guid playerId)
    {
        lock (StoreSyncField.GetValue(_store)!)
        {
            var room = GetRoomObject(code);
            var player = FindPlayer((IList)Get(room, "Players")!, playerId)
                ?? throw new WordRingsRoomException(WordRingsRoomError.InvalidPlayer);
            var targetScore = Convert.ToDouble(Get(room, "TargetScore"));
            var score = Convert.ToDouble(Get(player, "Score"));
            var options = GetOrCreateOptions(code, (int)Math.Round(targetScore));
            var limit = GetEffectiveHandSize(options.HandSize, targetScore, score);
            return ((IList)Get(player, "RemainingWords")!).Cast<string>().Take(limit).ToList();
        }
    }

    private string? PickVisibleScoringWord(string code, IReadOnlyCollection<string> visible)
    {
        lock (StoreSyncField.GetValue(_store)!)
        {
            var room = GetRoomObject(code);
            var puzzle = (WordRingsPuzzle)Get(room, "Puzzle")!;
            return visible
                .Where(word => puzzle.Expected.TryGetValue(word, out var membership) && !string.IsNullOrWhiteSpace(membership))
                .OrderBy(_ => Random.Shared.Next())
                .FirstOrDefault();
        }
    }

    private void SelectFirstPlayer(string code, bool dedicatedHostMode)
    {
        var options = GetOrCreateOptions(code, DefaultTargetScore);
        lock (_sync)
        {
            if (options.FirstPlayerSelected) return;
        }

        lock (StoreSyncField.GetValue(_store)!)
        {
            var room = GetRoomObject(code);
            if (!string.Equals(Get(room, "Phase")?.ToString(), "Playing", StringComparison.Ordinal)) return;
            var players = (IList)Get(room, "Players")!;
            var candidates = Enumerable.Range(0, players.Count)
                .Where(index =>
                {
                    var player = players[index]!;
                    if (dedicatedHostMode && (bool)Get(player, "IsHost")!) return false;
                    if (!(bool)Get(player, "IsPlayingParticipant")!) return false;
                    return ((IList)Get(player, "RemainingWords")!).Count > 0;
                })
                .ToArray();
            if (candidates.Length == 0) return;

            Set(room, "CurrentPlayerIndex", candidates[Random.Shared.Next(candidates.Length)]);
            Set(room, "OutsidePointAwardedThisTurn", false);
            IncrementVersion(room);
        }

        lock (_sync)
        {
            options.FirstPlayerSelected = true;
        }
    }

    private void ApplyExhaustionWin(string code, Guid playerId)
    {
        lock (StoreSyncField.GetValue(_store)!)
        {
            var room = GetRoomObject(code);
            if (!string.Equals(Get(room, "Phase")?.ToString(), "Playing", StringComparison.Ordinal)) return;
            var player = FindPlayer((IList)Get(room, "Players")!, playerId);
            if (player is null || ((IList)Get(player, "RemainingWords")!).Count != 0) return;
            if (!(bool)Get(player, "IsPlayingParticipant")!) return;

            SetEnum(room, "Phase", "Finished");
            SetEnum(room, "Outcome", "Won");
            Set(room, "WinnerPlayerId", playerId);
            Set(room, "TurnDeadlineUtc", null);
            Set(room, "PausedTurnSeconds", null);
            IncrementVersion(room);
        }
    }

    private RoomOptions GetOrCreateOptions(string code, int targetScore)
    {
        lock (_sync)
        {
            if (!_roomOptions.TryGetValue(code, out var options))
            {
                options = new RoomOptions(
                    Math.Clamp(targetScore, MinimumTargetScore, MaximumTargetScore),
                    Math.Min(DefaultHandSize, Math.Max(MinimumHandSize, targetScore)));
                _roomOptions[code] = options;
            }
            return options;
        }
    }

    private static int GetEffectiveHandSize(int configuredHandSize, double targetScore, double score)
    {
        var remaining = targetScore - score;
        if (remaining <= 0) return 0;
        return Math.Min(configuredHandSize, Math.Max(1, (int)Math.Ceiling(remaining)));
    }

    private static void Validate(int targetScore, int handSize)
    {
        if (targetScore is < MinimumTargetScore or > MaximumTargetScore)
        {
            throw new WordRingsRoomException(WordRingsRoomError.InvalidTargetScore);
        }
        if (handSize is < MinimumHandSize or > MaximumHandSize || handSize > targetScore)
        {
            throw new InvalidOperationException("InvalidHandSize");
        }
    }

    private object GetRoomObject(string code)
    {
        var rooms = (IDictionary)StoreRoomsField.GetValue(_store)!;
        if (!rooms.Contains(code)) throw new WordRingsRoomException(WordRingsRoomError.RoomNotFound);
        return rooms[code]!;
    }

    private static object? FindPlayer(IList players, Guid playerId) =>
        players.Cast<object>().FirstOrDefault(player => (Guid)Get(player, "Id")! == playerId);

    private static object? Get(object instance, string property) =>
        instance.GetType().GetProperty(property, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .GetValue(instance);

    private static void Set(object instance, string property, object? value) =>
        instance.GetType().GetProperty(property, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .SetValue(instance, value);

    private static void SetEnum(object instance, string property, string value)
    {
        var info = instance.GetType().GetProperty(property, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;
        info.SetValue(instance, Enum.Parse(info.PropertyType, value, ignoreCase: false));
    }

    private static void IncrementVersion(object room) =>
        Set(room, "Version", Convert.ToInt64(Get(room, "Version")) + 1);

    private static string Normalize(string? value) => (value ?? string.Empty).Trim().ToUpperInvariant();

    private sealed class RoomOptions(int targetScore, int handSize)
    {
        public int TargetScore { get; } = targetScore;
        public int HandSize { get; } = handSize;
        public bool FirstPlayerSelected { get; set; }
    }

    private sealed record WordCountCaptureState(
        string RoomCode,
        Guid CallerPlayerId,
        IReadOnlyDictionary<Guid, int> Counts);
}
