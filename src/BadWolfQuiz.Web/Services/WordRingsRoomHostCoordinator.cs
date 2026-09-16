using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;

namespace BadWolfQuiz.Web.Services;

public sealed record WordRingsRoomHostRuleOption(Guid Id, string Text, bool IsSelected);
public sealed record WordRingsRoomHostRuleGroup(string Ring, IReadOnlyList<WordRingsRoomHostRuleOption> Options, Guid? SelectedRuleId, bool CanRefresh);
public sealed record WordRingsRoomHostPlayer(Guid Id, string Name, double Score, bool IsHost, bool IsPlayingParticipant, bool IsCurrentTurn, int RemainingWords);
public sealed record WordRingsRoomHostPendingPlacement(
    long Id,
    string Word,
    string SubmittedMembership,
    string Membership,
    Guid PlayerId,
    string PlayerName,
    bool WasMoved);
public sealed record WordRingsRoomBrandLogo(byte[] Data, string ContentType);
public sealed record WordRingsAchievementParticipant(
    Guid PlayerId,
    string PlayerName,
    string? AccountId,
    string? HostId,
    bool IsHost,
    bool DedicatedHostMode,
    int RoundNumber);
public sealed record WordRingsCompletedRoundAchievement(
    string RoomCode,
    int RoundNumber,
    bool DedicatedHostMode,
    WordRingsAchievementParticipant Host,
    int PlayingParticipantCount,
    Guid? WinnerPlayerId);
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
    IReadOnlyList<WordRingsRoomHostRuleGroup> RuleSelections,
    WordRingsRoomHostPendingPlacement? PendingPlacement)
{
    public bool SeedSetupPending { get; init; }
    public int SeedWordsRemaining { get; init; }
}

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
        string? previousPlayerToken = null,
        byte[]? brandLogoData = null,
        string? brandLogoContentType = null,
        int turnDurationSeconds = 0,
        string? hostAccountId = null)
    {
        var connection = _store.CreateRoom(
            playerName,
            targetScore,
            partialScoreEnabled,
            previousRoomCode,
            previousPlayerToken,
            turnDurationSeconds);
        lock (_metaSync)
        {
            _rooms.Remove(Normalize(previousRoomCode));
            var hostPlayer = connection.State.Players.Single(player => player.Id == connection.State.PlayerId);
            var meta = new RoomMeta(
                connection.PlayerToken,
                hostChoosesRules,
                brandLogoData,
                brandLogoContentType,
                hostPlayer.Id,
                hostPlayer.Name,
                hostAccountId);
            if (hostChoosesRules) PrepareChoices(meta);
            _rooms[connection.RoomCode] = meta;
        }
        return connection with { State = DecorateRoomState(connection.State, hostChoosesRules) };
    }

    public WordRingsRoomBrandLogo? GetBrandLogo(string? roomCode)
    {
        lock (_metaSync)
        {
            if (!_rooms.TryGetValue(Normalize(roomCode), out var meta) ||
                meta.BrandLogoData is null ||
                string.IsNullOrWhiteSpace(meta.BrandLogoContentType))
            {
                return null;
            }

            return new WordRingsRoomBrandLogo(
                meta.BrandLogoData.ToArray(),
                meta.BrandLogoContentType);
        }
    }

    public WordRingsRoomConnection JoinRoom(string? roomCode, string? playerName, string? accountId = null)
    {
        var code = Normalize(roomCode);
        lock (_metaSync)
        {
            if (_rooms.TryGetValue(code, out var meta) && meta.JoinLocked)
            {
                throw new WordRingsRoomException(WordRingsRoomError.RoomAlreadyStarted);
            }
        }
        var connection = _store.JoinRoom(code, playerName);
        lock (_metaSync)
        {
            if (_rooms.TryGetValue(code, out var meta))
            {
                var player = connection.State.Players.Single(item => item.Id == connection.State.PlayerId);
                meta.PlayerIdentities[player.Id] = new RoomPlayerIdentity(player.Name, NormalizeAccountId(accountId));
            }
        }
        return connection with { State = DecorateRoomState(connection.State, IsDedicatedHostRoom(code)) };
    }

    public WordRingsRoomSnapshot GetRoomState(string? roomCode, string? playerToken)
    {
        var state = _store.GetState(roomCode, playerToken);
        return DecorateRoomState(state, IsDedicatedHostRoom(state.RoomCode));
    }

    public void LeaveRoom(string? roomCode, string? playerToken) =>
        _store.LeaveRoom(roomCode, playerToken);

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
            if (waitingState.Players.Count(player => !player.IsHost) < 1)
            {
                throw new WordRingsRoomException(WordRingsRoomError.NeedMorePlayers);
            }
            lock (_metaSync)
            {
                meta!.RoundFinishedPrepared = false;
            }
        }

        var started = _store.StartGame(code, playerToken);
        lock (_metaSync)
        {
            if (meta is not null)
            {
                meta.RoundNumber++;
                meta.RoundPlayingParticipantCount = started.Players.Count(player => !dedicatedHost || !player.IsHost);
            }
        }
        if (!dedicatedHost) return started;

        var puzzle = WordRingsRoomRuleSelection.CreatePuzzle(
            GetEnvironmentFromStore(),
            meta!.Selected[WordRingColor.Blue]!.Value,
            meta.Selected[WordRingColor.Yellow]!.Value,
            meta.Selected[WordRingColor.Red]!.Value);
        var seedCount = InjectHostedRound(code, puzzle);
        lock (_metaSync)
        {
            meta.SeedWordCount = seedCount;
            meta.SeedSetupPending = seedCount > 0;
            meta.Revision++;
        }
        return GetRoomState(code, playerToken);
    }

    public WordRingsRoomPlacementResult SubmitPlacement(
        string? roomCode,
        string? playerToken,
        string? word,
        string? membership,
        double x,
        double y)
    {
        var code = Normalize(roomCode);
        var dedicatedHost = IsDedicatedHostRoom(code);
        var result = dedicatedHost
            ? _store.SubmitHostedPlacement(code, playerToken, word, membership, x, y)
            : _store.SubmitPlacement(code, playerToken, word, membership, x, y);
        return result with { State = DecorateRoomState(result.State, dedicatedHost) };
    }

    public WordRingsRoomSnapshot MovePlacement(
        string? roomCode,
        string? playerToken,
        long placementId,
        string? membership,
        double x,
        double y)
    {
        var code = Normalize(roomCode);
        var dedicatedHost = IsDedicatedHostRoom(code);
        if (dedicatedHost)
        {
            var current = GetRoomState(code, playerToken);
            var placement = current.Placements.FirstOrDefault(item => item.Id == placementId);
            if (placement?.IsSeed == true)
            {
                if (IsSeedSetupPending(code))
                {
                    if (!current.IsHost) throw new WordRingsRoomException(WordRingsRoomError.NotHost);
                }
                else
                {
                    membership = placement.Membership;
                }
            }
        }
        var state = dedicatedHost
            ? _store.MoveHostedPlacement(code, playerToken, placementId, membership, x, y)
            : _store.MovePlacement(code, playerToken, placementId, membership, x, y);
        return DecorateRoomState(state, dedicatedHost);
    }

    public WordRingsRoomSnapshot PlaceSeedWord(
        string? roomCode,
        string? playerToken,
        string? word,
        string? membership,
        double x,
        double y)
    {
        var code = Normalize(roomCode);
        lock (_metaSync)
        {
            var meta = RequireMeta(code);
            EnsureHost(meta, playerToken);
            if (!meta.HostChoosesRules || !meta.SeedSetupPending)
            {
                throw new InvalidOperationException("SeedSetupUnavailable");
            }
        }
        var state = _store.PlaceHostSeedWord(code, playerToken, word, membership, x, y);
        return DecorateRoomState(state, dedicatedHost: true);
    }

    public WordRingsRoomHostSnapshot ConfirmSeedSetup(string? roomCode, string? playerToken)
    {
        var code = Normalize(roomCode);
        int requiredCount;
        lock (_metaSync)
        {
            var meta = RequireMeta(code);
            EnsureHost(meta, playerToken);
            if (!meta.HostChoosesRules || !meta.SeedSetupPending)
            {
                throw new InvalidOperationException("SeedSetupUnavailable");
            }
            requiredCount = meta.SeedWordCount;
        }

        _ = _store.ConfirmHostSeedSetup(code, playerToken, requiredCount);
        lock (_metaSync)
        {
            var meta = RequireMeta(code);
            meta.SeedSetupPending = false;
            meta.Revision++;
        }
        return GetHostState(code, playerToken);
    }

    public WordRingsRoomPlacementResult ResolvePlacementWithResult(
        string? roomCode,
        string? playerToken,
        long placementId)
    {
        var code = Normalize(roomCode);
        lock (_metaSync)
        {
            var meta = RequireMeta(code);
            EnsureHost(meta, playerToken);
            if (!meta.HostChoosesRules) throw new InvalidOperationException("HostJudgingDisabled");
        }
        var result = _store.ResolveHostedPlacement(code, playerToken, placementId);
        return result with { State = DecorateRoomState(result.State, dedicatedHost: true) };
    }

    public WordRingsRoomHostSnapshot ResolvePlacement(
        string? roomCode,
        string? playerToken,
        long placementId)
    {
        _ = ResolvePlacementWithResult(roomCode, playerToken, placementId);
        return GetHostState(roomCode, playerToken);
    }

    public WordRingsAchievementParticipant? GetAchievementParticipant(string? roomCode, Guid playerId)
    {
        var code = Normalize(roomCode);
        lock (_metaSync)
        {
            if (!_rooms.TryGetValue(code, out var meta) ||
                !meta.PlayerIdentities.TryGetValue(playerId, out var identity))
            {
                return null;
            }
            return new WordRingsAchievementParticipant(
                playerId,
                identity.PlayerName,
                identity.AccountId,
                meta.HostAccountId,
                playerId == meta.HostPlayerId,
                meta.HostChoosesRules,
                meta.RoundNumber);
        }
    }

    public WordRingsCompletedRoundAchievement? TryClaimCompletedRoundAchievement(WordRingsRoomSnapshot state)
    {
        if (!string.Equals(state.Phase, "finished", StringComparison.Ordinal))
        {
            return null;
        }

        lock (_metaSync)
        {
            if (!_rooms.TryGetValue(state.RoomCode, out var meta) ||
                meta.RoundNumber <= 0 ||
                meta.LastAchievementRound >= meta.RoundNumber ||
                !meta.PlayerIdentities.TryGetValue(meta.HostPlayerId, out var hostIdentity))
            {
                return null;
            }
            meta.LastAchievementRound = meta.RoundNumber;
            var host = new WordRingsAchievementParticipant(
                meta.HostPlayerId,
                hostIdentity.PlayerName,
                hostIdentity.AccountId,
                meta.HostAccountId,
                true,
                meta.HostChoosesRules,
                meta.RoundNumber);
            return new WordRingsCompletedRoundAchievement(
                state.RoomCode,
                meta.RoundNumber,
                meta.HostChoosesRules,
                host,
                meta.RoundPlayingParticipantCount,
                state.WinnerPlayerId);
        }
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
        if (dedicatedHost && IsSeedSetupPending(code))
        {
            throw new InvalidOperationException("SeedSetupPending");
        }
        if (dedicatedHost && GetRoomState(code, playerToken).Placements.Any(item => item.IsPending))
        {
            throw new InvalidOperationException("PlacementPending");
        }
        _ = _store.SetCurrentPlayer(code, playerToken, playerId, dedicatedHost);
        return GetHostState(code, playerToken);
    }

    public WordRingsRoomHostSnapshot KickPlayer(string? roomCode, string? playerToken, Guid playerId)
    {
        var code = Normalize(roomCode);
        var dedicatedHost = EnsureCreator(code, playerToken);
        if (dedicatedHost && GetRoomState(code, playerToken).Placements.Any(item => item.IsPending))
        {
            throw new InvalidOperationException("PlacementPending");
        }
        _ = _store.KickPlayer(code, playerToken, playerId);
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
        var pending = state.IsHost && dedicatedHost
            ? state.Placements.LastOrDefault(item => item.IsPending)
            : null;
        var pendingPlacement = pending is null
            ? null
            : new WordRingsRoomHostPendingPlacement(
                pending.Id,
                pending.Word,
                pending.SubmittedMembership,
                pending.Membership,
                pending.PlayerId,
                pending.PlayerName,
                !string.Equals(pending.SubmittedMembership, pending.Membership, StringComparison.Ordinal));
        var canStart = state.IsHost && state.Phase != "playing" &&
                       players.Count(player => player.IsPlayingParticipant) >= 1 &&
                       (!dedicatedHost || AllRulesSelected(meta));
        return new WordRingsRoomHostSnapshot(
            state.RoomCode, state.Version + meta.Revision, state.Phase, state.TargetScore, state.PlayerId,
            state.CurrentPlayerId, state.IsHost, canStart, dedicatedHost, meta.JoinLocked,
            Math.Clamp(state.TargetScore, 5, 10), players, groups, pendingPlacement)
        {
            SeedSetupPending = state.IsHost && dedicatedHost && meta.SeedSetupPending,
            SeedWordsRemaining = state.IsHost && dedicatedHost && meta.SeedSetupPending
                ? state.BankWords.Count + state.QueuedWords.Count
                : 0
        };
    }

    private int InjectHostedRound(string code, WordRingsPuzzle puzzle)
    {
        var seedWords = WordRingsSeedWordSelector.SelectHostSetup(puzzle, 4);
        var excluded = seedWords.Select(item => item.Word).ToHashSet(StringComparer.OrdinalIgnoreCase);
        MutateRoom(code, room =>
        {
            Set(room, "Puzzle", puzzle);
            ((IList)Get(room, "Placements")!).Clear();
            Set(room, "NextPlacementId", 1L);
            var players = Players(room);
            foreach (var player in players)
            {
                Set(player!, "Score", 0d);
                Set(player!, "IsPlayingParticipant", !IsHost(player!));
                var words = RemainingWords(player!);
                words.Clear();
                if (IsHost(player!))
                {
                    foreach (var seed in seedWords) words.Add(seed.Word);
                    continue;
                }
                foreach (var word in puzzle.Words
                             .Where(word => !excluded.Contains(word))
                             .OrderBy(_ => Random.Shared.Next())
                             .Take(WordRingsRoomStore.MaximumPlayerWords))
                {
                    words.Add(word);
                }
            }
            Set(room, "CurrentPlayerIndex", seedWords.Count > 0 ? -1 : FirstPlayableIndex(players, dedicatedHost: true));
            Set(room, "OutsidePointAwardedThisTurn", false);
            Set(room, "TurnDeadlineUtc", null);
            Set(room, "PausedTurnSeconds", null);
            IncrementVersion(room);
        });
        return seedWords.Count;
    }

    private void PrepareChoices(RoomMeta meta)
    {
        meta.SeedSetupPending = false;
        meta.SeedWordCount = 0;
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
    private static string? NormalizeAccountId(string? accountId) => string.IsNullOrWhiteSpace(accountId) ? null : accountId.Trim();
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

    private bool IsDedicatedHostRoom(string? roomCode)
    {
        lock (_metaSync)
        {
            return _rooms.TryGetValue(Normalize(roomCode), out var meta) && meta.HostChoosesRules;
        }
    }

    private bool IsSeedSetupPending(string? roomCode)
    {
        lock (_metaSync)
        {
            return _rooms.TryGetValue(Normalize(roomCode), out var meta) && meta.SeedSetupPending;
        }
    }

    private WordRingsRoomSnapshot DecorateRoomState(
        WordRingsRoomSnapshot state,
        bool dedicatedHost)
    {
        if (!dedicatedHost)
        {
            return state with { DedicatedHostMode = false };
        }

        var seedSetupPending = IsSeedSetupPending(state.RoomCode);
        var finished = string.Equals(state.Phase, "finished", StringComparison.Ordinal);
        if (!state.IsHost && !finished)
        {
            return state with
            {
                DedicatedHostMode = true,
                SeedSetupPending = seedSetupPending,
                BlueRuleText = string.Empty,
                YellowRuleText = string.Empty,
                RedRuleText = string.Empty
            };
        }

        if (state.IsHost && string.Equals(state.Phase, "waiting", StringComparison.Ordinal))
        {
            lock (_metaSync)
            {
                if (_rooms.TryGetValue(state.RoomCode, out var meta) && meta.HostChoosesRules)
                {
                    return state with
                    {
                        DedicatedHostMode = true,
                        SeedSetupPending = seedSetupPending,
                        BlueRuleText = SelectedRuleText(meta, WordRingColor.Blue),
                        YellowRuleText = SelectedRuleText(meta, WordRingColor.Yellow),
                        RedRuleText = SelectedRuleText(meta, WordRingColor.Red)
                    };
                }
            }
        }

        return state with { DedicatedHostMode = true, SeedSetupPending = seedSetupPending };
    }

    private static string SelectedRuleText(RoomMeta meta, WordRingColor color)
    {
        var selectedId = meta.Selected[color];
        if (!selectedId.HasValue) return string.Empty;
        return meta.Options[color].FirstOrDefault(option => option.Id == selectedId.Value)?.Text ?? string.Empty;
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

    private sealed class RoomMeta(
        string hostToken,
        bool hostChoosesRules,
        byte[]? brandLogoData = null,
        string? brandLogoContentType = null,
        Guid hostPlayerId = default,
        string? hostPlayerName = null,
        string? hostAccountId = null)
    {
        public string HostToken { get; } = hostToken;
        public bool HostChoosesRules { get; } = hostChoosesRules;
        public Guid HostPlayerId { get; } = hostPlayerId;
        public string? HostAccountId { get; } = NormalizeAccountId(hostAccountId);
        public Dictionary<Guid, RoomPlayerIdentity> PlayerIdentities { get; } = hostPlayerId == Guid.Empty
            ? new Dictionary<Guid, RoomPlayerIdentity>()
            : new Dictionary<Guid, RoomPlayerIdentity>
            {
                [hostPlayerId] = new(hostPlayerName ?? string.Empty, NormalizeAccountId(hostAccountId))
            };
        public int RoundNumber { get; set; }
        public int LastAchievementRound { get; set; }
        public int RoundPlayingParticipantCount { get; set; }
        public byte[]? BrandLogoData { get; } = brandLogoData?.ToArray();
        public string? BrandLogoContentType { get; } = string.IsNullOrWhiteSpace(brandLogoContentType)
            ? null
            : brandLogoContentType.Trim();
        public bool JoinLocked { get; set; }
        public long Revision { get; set; }
        public bool RoundFinishedPrepared { get; set; }
        public bool SeedSetupPending { get; set; }
        public int SeedWordCount { get; set; }
        public Dictionary<WordRingColor, List<RuleOption>> Options { get; } = new();
        public Dictionary<WordRingColor, HashSet<Guid>> Offered { get; } = new();
        public Dictionary<WordRingColor, Guid?> Selected { get; } = new();
        public Dictionary<WordRingColor, bool> Refreshed { get; } = new();
    }

    private sealed record RoomPlayerIdentity(string PlayerName, string? AccountId);
    private sealed record RuleOption(Guid Id, string Text);
}
