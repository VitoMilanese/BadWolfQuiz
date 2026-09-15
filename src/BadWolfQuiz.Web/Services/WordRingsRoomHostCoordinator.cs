using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;

namespace BadWolfQuiz.Web.Services;

public sealed record WordRingsRoomHostRuleOption(Guid Id, string Text, bool IsSelected);
public sealed record WordRingsRoomHostRuleGroup(string Ring, IReadOnlyList<WordRingsRoomHostRuleOption> Options, Guid? SelectedRuleId, bool CanRefresh);
public sealed record WordRingsRoomHostPlayer(Guid Id, string Name, double Score, bool IsHost, bool IsPlayingParticipant, bool IsCurrentTurn, int RemainingWords);
public sealed record WordRingsRoomHostSnapshot(
    string RoomCode,
    long Version,
    string Phase,
    int TargetScore,
    Guid PlayerId,
    Guid? CurrentPlayerId,
    bool IsHost,
    bool CanStart,
    bool HostChoosesRules,
    bool JoinLocked,
    int HandLimit,
    IReadOnlyList<WordRingsRoomHostPlayer> Players,
    IReadOnlyList<WordRingsRoomHostRuleGroup> RuleSelections);

public sealed class WordRingsRoomHostCoordinator
{
    private const int RuleOptionCount = 6;
    private static readonly ConcurrentDictionary<string, Lazy<WordRingsRoomHostCoordinator>> Instances = new(StringComparer.OrdinalIgnoreCase);
    private static readonly FieldInfo SyncField = typeof(WordRingsRoomStore).GetField("_sync", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static readonly FieldInfo RoomsField = typeof(WordRingsRoomStore).GetField("_rooms", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private readonly WordRingsRoomStore _store;
    private readonly WordRingsRuleStore _rules;
    private readonly object _metaSync = new();
    private readonly Dictionary<string, RoomMeta> _rooms = new(StringComparer.OrdinalIgnoreCase);

    private WordRingsRoomHostCoordinator(IWebHostEnvironment environment)
    {
        _store = WordRingsRoomStore.Get(environment);
        _rules = WordRingsRuleStore.Get(environment);
    }

    public static WordRingsRoomHostCoordinator Get(IWebHostEnvironment environment)
    {
        var root = Path.GetFullPath(environment.ContentRootPath);
        return Instances.GetOrAdd(root, _ => new Lazy<WordRingsRoomHostCoordinator>(() => new(environment))).Value;
    }

    public WordRingsRoomConnection CreateRoom(
        string? playerName,
        int targetScore,
        bool partialScoreEnabled,
        bool hostChoosesRules,
        string? previousRoomCode = null,
        string? previousPlayerToken = null)
    {
        var connection = _store.CreateRoom(playerName, targetScore, partialScoreEnabled, previousRoomCode, previousPlayerToken);
        lock (_metaSync)
        {
            _rooms.Remove(Normalize(previousRoomCode));
            var meta = new RoomMeta(connection.PlayerToken, hostChoosesRules);
            if (hostChoosesRules) PrepareChoices(meta);
            _rooms[connection.RoomCode] = meta;
        }
        return connection;
    }

    public WordRingsRoomConnection JoinRoom(string? roomCode, string? playerName)
    {
        lock (_metaSync)
        {
            if (_rooms.TryGetValue(Normalize(roomCode), out var meta) && meta.JoinLocked)
            {
                throw new WordRingsRoomException(WordRingsRoomError.RoomAlreadyStarted);
            }
        }
        return _store.JoinRoom(roomCode, playerName);
    }

    public WordRingsRoomSnapshot StartGame(string? roomCode, string? playerToken)
    {
        var code = Normalize(roomCode);
        RoomMeta? meta;
        var dedicatedHost = false;
        lock (_metaSync)
        {
            _rooms.TryGetValue(code, out meta);
            dedicatedHost = meta?.HostChoosesRules == true;
            if (dedicatedHost)
            {
                EnsureHost(meta!, playerToken);
                if (!AllRulesSelected(meta!)) throw new InvalidOperationException("RulesNotSelected");
            }
        }

        if (dedicatedHost)
        {
            var waitingState = _store.GetState(code, playerToken);
            if (waitingState.Players.Count(player => !player.IsHost) < 2)
            {
                throw new WordRingsRoomException(WordRingsRoomError.NeedMorePlayers);
            }
            lock (_metaSync)
            {
                meta!.RoundFinishedPrepared = false;
            }
        }

        var started = _store.StartGame(code, playerToken);
        if (!dedicatedHost) return started;

        var puzzle = WordRingsRoomRuleSelection.CreatePuzzle(
            GetEnvironmentFromStore(),
            meta!.Selected[WordRingColor.Blue]!.Value,
            meta.Selected[WordRingColor.Yellow]!.Value,
            meta.Selected[WordRingColor.Red]!.Value);
        InjectHostedRound(code, puzzle);
        return _store.GetState(code, playerToken);
    }

    public WordRingsRoomHostSnapshot GetHostState(string? roomCode, string? playerToken)
    {
        var baseState = _store.GetState(roomCode, playerToken);
        var code = baseState.RoomCode;
        RoomMeta meta;
        lock (_metaSync)
        {
            if (!_rooms.TryGetValue(code, out meta!))
            {
                meta = new RoomMeta(string.Empty, false);
                _rooms[code] = meta;
            }
            if (meta.HostChoosesRules &&
                string.Equals(baseState.Phase, "finished", StringComparison.Ordinal) &&
                !meta.RoundFinishedPrepared)
            {
                PrepareChoices(meta);
                meta.RoundFinishedPrepared = true;
                meta.Revision++;
            }
        }
        return BuildHostSnapshot(baseState, meta);
    }

    public WordRingsRoomHostSnapshot SelectRule(string? roomCode, string? playerToken, string? ring, Guid ruleId)
    {
        var code = Normalize(roomCode);
        var color = ParseRing(ring);
        lock (_metaSync)
        {
            var meta = RequireMeta(code);
            EnsureHost(meta, playerToken);
            if (!meta.HostChoosesRules) throw new InvalidOperationException("HostRulesDisabled");
            if (!meta.Options[color].Any(option => option.Id == ruleId)) throw new InvalidOperationException("InvalidRuleSelection");
            meta.Selected[color] = ruleId;
            meta.Revision++;
        }
        return GetHostState(code, playerToken);
    }

    public WordRingsRoomHostSnapshot RefreshRules(string? roomCode, string? playerToken, string? ring)
    {
        var code = Normalize(roomCode);
        var color = ParseRing(ring);
        lock (_metaSync)
        {
            var meta = RequireMeta(code);
            EnsureHost(meta, playerToken);
            if (!meta.HostChoosesRules || meta.Refreshed[color]) throw new InvalidOperationException("RuleRefreshUnavailable");
            var old = meta.Offered[color];
            meta.Options[color] = PickOptions(color, old);
            foreach (var item in meta.Options[color]) old.Add(item.Id);
            meta.Selected[color] = null;
            meta.Refreshed[color] = true;
            meta.Revision++;
        }
        return GetHostState(code, playerToken);
    }

    public WordRingsRoomHostSnapshot SetJoinLocked(string? roomCode, string? playerToken, bool locked)
    {
        var code = Normalize(roomCode);
        lock (_metaSync)
        {
            var meta = RequireMeta(code);
            EnsureHost(meta, playerToken);
            meta.JoinLocked = locked;
            meta.Revision++;
        }
        return GetHostState(code, playerToken);
    }

    public WordRingsRoomHostSnapshot SetCurrentPlayer(string? roomCode, string? playerToken, Guid playerId)
    {
        var code = Normalize(roomCode);
        var dedicatedHost = EnsureCreator(code, playerToken);
        MutateRoom(code, room =>
        {
            var players = Players(room);
            var index = IndexOfPlayer(players, playerId);
            if (index < 0 ||
                (dedicatedHost && IsHost(players[index]!)) ||
                RemainingWords(players[index]!).Count == 0)
            {
                throw new InvalidOperationException("InvalidPlayer");
            }
            Set(room, "CurrentPlayerIndex", index);
            Set(room, "OutsidePointAwardedThisTurn", false);
            IncrementVersion(room);
        });
        return GetHostState(code, playerToken);
    }

    public WordRingsRoomHostSnapshot KickPlayer(string? roomCode, string? playerToken, Guid playerId)
    {
        var code = Normalize(roomCode);
        var dedicatedHost = EnsureCreator(code, playerToken);
        MutateRoom(code, room =>
        {
            var players = Players(room);
            var index = IndexOfPlayer(players, playerId);
            if (index < 0) throw new InvalidOperationException("InvalidPlayer");
            if (IsHost(players[index]!)) throw new InvalidOperationException("CannotKickHost");
            var current = (int)Get(room, "CurrentPlayerIndex")!;
            var removedCurrent = current == index;
            players.RemoveAt(index);
            if (players.Count == 0)
            {
                Set(room, "CurrentPlayerIndex", -1);
            }
            else if (removedCurrent)
            {
                Set(room, "CurrentPlayerIndex", FirstPlayableIndex(players, dedicatedHost));
                Set(room, "OutsidePointAwardedThisTurn", false);
            }
            else if (current > index)
            {
                Set(room, "CurrentPlayerIndex", current - 1);
            }
            IncrementVersion(room);
        });
        return GetHostState(code, playerToken);
    }

    private WordRingsRoomHostSnapshot BuildHostSnapshot(WordRingsRoomSnapshot state, RoomMeta meta)
    {
        var dedicatedHost = meta.HostChoosesRules;
        var players = state.Players.Select(player => new WordRingsRoomHostPlayer(
            player.Id, player.Name, player.Score, player.IsHost, !dedicatedHost || !player.IsHost,
            player.IsCurrentTurn, player.RemainingWords)).ToArray();
        var groups = state.IsHost && dedicatedHost
            ? Enum.GetValues<WordRingColor>().Select(color => new WordRingsRoomHostRuleGroup(
                RingCode(color),
                meta.Options[color].Select(option => new WordRingsRoomHostRuleOption(option.Id, option.Text, meta.Selected[color] == option.Id)).ToArray(),
                meta.Selected[color],
                !meta.Refreshed[color] && state.Phase != "playing")).ToArray()
            : Array.Empty<WordRingsRoomHostRuleGroup>();
        var canStart = state.IsHost && state.Phase != "playing" &&
                       players.Count(player => player.IsPlayingParticipant) >= 2 &&
                       (!dedicatedHost || AllRulesSelected(meta));
        return new WordRingsRoomHostSnapshot(
            state.RoomCode, state.Version + meta.Revision, state.Phase, state.TargetScore, state.PlayerId,
            state.CurrentPlayerId, state.IsHost, canStart, dedicatedHost, meta.JoinLocked,
            Math.Clamp(state.TargetScore, 5, 10), players, groups);
    }

    private void InjectHostedRound(string code, WordRingsPuzzle puzzle)
    {
        MutateRoom(code, room =>
        {
            Set(room, "Puzzle", puzzle);
            var players = Players(room);
            foreach (var player in players)
            {
                Set(player!, "Score", 0d);
                var words = RemainingWords(player!);
                words.Clear();
                if (IsHost(player!)) continue;
                foreach (var word in puzzle.Words.OrderBy(_ => Random.Shared.Next()).Take(WordRingsRoomStore.MaximumPlayerWords)) words.Add(word);
            }
            Set(room, "CurrentPlayerIndex", FirstPlayableIndex(players, dedicatedHost: true));
            Set(room, "OutsidePointAwardedThisTurn", false);
            IncrementVersion(room);
        });
    }

    private void PrepareChoices(RoomMeta meta)
    {
        foreach (var color in Enum.GetValues<WordRingColor>())
        {
            meta.Options[color] = PickOptions(color, new HashSet<Guid>());
            meta.Offered[color] = meta.Options[color].Select(option => option.Id).ToHashSet();
            meta.Selected[color] = null;
            meta.Refreshed[color] = false;
        }
    }

    private List<RuleOption> PickOptions(WordRingColor color, IReadOnlySet<Guid> excluded)
    {
        var all = _rules.GetRules(color).Where(rule => rule.IsEnabled && rule.Words.Count > 0).ToArray();
        var picked = all.Where(rule => !excluded.Contains(rule.Id)).OrderBy(_ => Random.Shared.Next()).Take(RuleOptionCount).ToList();
        if (picked.Count < Math.Min(RuleOptionCount, all.Length))
        {
            picked.AddRange(all.Where(rule => picked.All(item => item.Id != rule.Id)).OrderBy(_ => Random.Shared.Next()).Take(RuleOptionCount - picked.Count));
        }
        return picked.Select(rule => new RuleOption(rule.Id, rule.Text)).ToList();
    }

    private static bool AllRulesSelected(RoomMeta meta) => Enum.GetValues<WordRingColor>().All(color => meta.Selected[color].HasValue);
    private static string RingCode(WordRingColor color) => color switch { WordRingColor.Blue => "A", WordRingColor.Yellow => "B", _ => "C" };
    private static WordRingColor ParseRing(string? ring) => ring?.Trim().ToUpperInvariant() switch { "A" or "BLUE" => WordRingColor.Blue, "B" or "YELLOW" => WordRingColor.Yellow, "C" or "RED" => WordRingColor.Red, _ => throw new InvalidOperationException("InvalidRing") };
    private static string Normalize(string? code) => (code ?? string.Empty).Trim().ToUpperInvariant();
    private RoomMeta RequireMeta(string code) => _rooms.TryGetValue(code, out var meta) ? meta : throw new InvalidOperationException("RoomMetadataNotFound");
    private static void EnsureHost(RoomMeta meta, string? token) { if (!string.Equals(meta.HostToken, token, StringComparison.Ordinal)) throw new WordRingsRoomException(WordRingsRoomError.NotHost); }
    private bool EnsureCreator(string code, string? token)
    {
        lock (_metaSync)
        {
            var meta = RequireMeta(code);
            EnsureHost(meta, token);
            return meta.HostChoosesRules;
        }
    }

    private IWebHostEnvironment GetEnvironmentFromStore() =>
        (IWebHostEnvironment)typeof(WordRingsRoomStore).GetField("_environment", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_store)!;

    private void MutateRoom(string code, Action<object> mutation)
    {
        var sync = SyncField.GetValue(_store)!;
        lock (sync) mutation(GetRoomObject(code));
    }

    private object GetRoomObject(string code)
    {
        var rooms = (IDictionary)RoomsField.GetValue(_store)!;
        return rooms[Normalize(code)] ?? throw new WordRingsRoomException(WordRingsRoomError.RoomNotFound);
    }

    private static IList Players(object room) => (IList)Get(room, "Players")!;
    private static IList RemainingWords(object player) => (IList)Get(player, "RemainingWords")!;
    private static bool IsHost(object player) => (bool)Get(player, "IsHost")!;
    private static int IndexOfPlayer(IList players, Guid id) { for (var i = 0; i < players.Count; i++) if ((Guid)Get(players[i]!, "Id")! == id) return i; return -1; }
    private static int FirstPlayableIndex(IList players, bool dedicatedHost)
    {
        for (var i = 0; i < players.Count; i++)
        {
            if (dedicatedHost && IsHost(players[i]!)) continue;
            if (RemainingWords(players[i]!).Count > 0) return i;
        }
        return -1;
    }
    private static object? Get(object instance, string property) => instance.GetType().GetProperty(property, BindingFlags.Instance | BindingFlags.Public)!.GetValue(instance);
    private static void Set(object instance, string property, object? value) => instance.GetType().GetProperty(property, BindingFlags.Instance | BindingFlags.Public)!.SetValue(instance, value);
    private static void IncrementVersion(object room) => Set(room, "Version", (long)Get(room, "Version")! + 1);

    private sealed class RoomMeta(string hostToken, bool hostChoosesRules)
    {
        public string HostToken { get; } = hostToken;
        public bool HostChoosesRules { get; } = hostChoosesRules;
        public bool JoinLocked { get; set; }
        public long Revision { get; set; }
        public bool RoundFinishedPrepared { get; set; }
        public Dictionary<WordRingColor, List<RuleOption>> Options { get; } = new();
        public Dictionary<WordRingColor, HashSet<Guid>> Offered { get; } = new();
        public Dictionary<WordRingColor, Guid?> Selected { get; } = new();
        public Dictionary<WordRingColor, bool> Refreshed { get; } = new();
    }

    private sealed record RuleOption(Guid Id, string Text);
}
