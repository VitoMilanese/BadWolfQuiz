using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace BadWolfQuiz.Web.Services;

public enum WordRingsRoomError
{
    RoomNotFound,
    RoomExpired,
    InvalidPlayer,
    InvalidPlayerName,
    InvalidTargetScore,
    RoomAlreadyStarted,
    NeedMorePlayers,
    NotHost,
    InvalidPhase,
    NotYourTurn,
    InvalidWord,
    InvalidPlacement,
    ConfigurationUnavailable
}

public sealed class WordRingsRoomException(WordRingsRoomError error)
    : Exception(error.ToString())
{
    public WordRingsRoomError Error { get; } = error;
}

public sealed record WordRingsRoomPlayerSnapshot(
    Guid Id,
    string Name,
    double Score,
    bool IsHost,
    bool IsCurrentTurn,
    int RemainingWords);

public sealed record WordRingsRoomPlacementSnapshot(
    long Id,
    string Word,
    string Membership,
    double X,
    double Y,
    bool IsCorrect,
    bool IsPartial,
    double PointsAwarded,
    Guid PlayerId,
    string PlayerName);

public sealed record WordRingsRoomSnapshot(
    string RoomCode,
    long Version,
    string Phase,
    string Outcome,
    int TargetScore,
    bool PartialScoreEnabled,
    double TeamScore,
    Guid PlayerId,
    Guid? CurrentPlayerId,
    bool IsHost,
    bool CanStart,
    string BlueRuleText,
    string YellowRuleText,
    string RedRuleText,
    IReadOnlyList<WordRingsRoomPlayerSnapshot> Players,
    IReadOnlyList<string> BankWords,
    IReadOnlyList<string> QueuedWords,
    IReadOnlyList<WordRingsRoomPlacementSnapshot> Placements);

public sealed record WordRingsRoomConnection(
    string RoomCode,
    string PlayerToken,
    WordRingsRoomSnapshot State);

public sealed record WordRingsRoomPlacementResult(
    WordRingsRoomSnapshot State,
    string Word,
    string ActualMembership,
    string ExpectedMembership,
    bool IsCorrect,
    bool IsPartial,
    double PointsAwarded,
    bool TurnContinues);

public sealed class WordRingsRoomStore
{
    public const int MinimumTargetScore = 5;
    public const int MaximumTargetScore = 15;
    public const int MaximumBankWords = 10;
    public const int MaximumPlayerWords = 20;
    public static readonly TimeSpan InactivityTimeout = TimeSpan.FromHours(2);

    private const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private const int CodeLength = 6;
    private const int MaximumCodeAttempts = 100;
    private const int MaximumPlayerNameLength = 30;
    private const int MaximumSharedPlacements = 20;

    private static readonly ConcurrentDictionary<string, Lazy<WordRingsRoomStore>> Instances =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly object _sync = new();
    private readonly Dictionary<string, RoomState> _rooms = new(StringComparer.OrdinalIgnoreCase);
    private readonly IWebHostEnvironment _environment;
    private readonly TimeProvider _timeProvider;

    private WordRingsRoomStore(IWebHostEnvironment environment, TimeProvider? timeProvider = null)
    {
        _environment = environment;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public static WordRingsRoomStore Get(IWebHostEnvironment environment)
    {
        var root = Path.GetFullPath(environment.ContentRootPath);
        return Instances.GetOrAdd(
            root,
            _ => new Lazy<WordRingsRoomStore>(
                () => new WordRingsRoomStore(environment),
                LazyThreadSafetyMode.ExecutionAndPublication)).Value;
    }

    public WordRingsRoomConnection CreateRoom(
        string? playerName,
        int targetScore,
        bool partialScoreEnabled)
    {
        var normalizedName = NormalizePlayerName(playerName);
        if (normalizedName is null)
        {
            throw new WordRingsRoomException(WordRingsRoomError.InvalidPlayerName);
        }
        if (targetScore is < MinimumTargetScore or > MaximumTargetScore)
        {
            throw new WordRingsRoomException(WordRingsRoomError.InvalidTargetScore);
        }

        lock (_sync)
        {
            var now = _timeProvider.GetUtcNow();
            RemoveExpiredCore(now);

            var puzzle = WordRingsRuleStore.Get(_environment).CreatePuzzle();
            if (!IsPlayable(puzzle))
            {
                throw new WordRingsRoomException(WordRingsRoomError.ConfigurationUnavailable);
            }

            var code = AllocateRoomCode();
            var host = new PlayerState(Guid.NewGuid(), CreatePlayerToken(), normalizedName, isHost: true);
            var room = new RoomState(
                code,
                targetScore,
                partialScoreEnabled,
                puzzle,
                host,
                now);
            _rooms.Add(code, room);
            return CreateConnection(room, host);
        }
    }

    public WordRingsRoomConnection JoinRoom(string? roomCode, string? playerName)
    {
        var normalizedName = NormalizePlayerName(playerName);
        if (normalizedName is null)
        {
            throw new WordRingsRoomException(WordRingsRoomError.InvalidPlayerName);
        }

        lock (_sync)
        {
            var now = _timeProvider.GetUtcNow();
            var room = GetActiveRoom(roomCode, now);
            if (room.Phase != RoomPhase.Waiting)
            {
                throw new WordRingsRoomException(WordRingsRoomError.RoomAlreadyStarted);
            }

            var player = new PlayerState(Guid.NewGuid(), CreatePlayerToken(), normalizedName, isHost: false);
            room.Players.Add(player);
            Touch(room, now);
            return CreateConnection(room, player);
        }
    }

    public WordRingsRoomSnapshot GetState(string? roomCode, string? playerToken)
    {
        lock (_sync)
        {
            var now = _timeProvider.GetUtcNow();
            var room = GetActiveRoom(roomCode, now);
            var player = GetPlayer(room, playerToken);
            room.LastActivityUtc = now;
            return CreateSnapshot(room, player);
        }
    }

    public WordRingsRoomSnapshot StartGame(string? roomCode, string? playerToken)
    {
        lock (_sync)
        {
            var now = _timeProvider.GetUtcNow();
            var room = GetActiveRoom(roomCode, now);
            var player = GetPlayer(room, playerToken);
            if (!player.IsHost)
            {
                throw new WordRingsRoomException(WordRingsRoomError.NotHost);
            }
            if (room.Phase != RoomPhase.Waiting)
            {
                throw new WordRingsRoomException(WordRingsRoomError.InvalidPhase);
            }
            if (room.Players.Count < 2)
            {
                throw new WordRingsRoomException(WordRingsRoomError.NeedMorePlayers);
            }

            var puzzle = WordRingsRuleStore.Get(_environment).CreatePuzzle();
            if (!IsPlayable(puzzle))
            {
                throw new WordRingsRoomException(WordRingsRoomError.ConfigurationUnavailable);
            }

            room.Puzzle = puzzle;
            room.TeamScore = 0;
            room.CurrentPlayerIndex = 0;
            room.OutsidePointAwardedThisTurn = false;
            room.Outcome = RoomOutcome.None;
            room.Placements.Clear();
            room.NextPlacementId = 1;

            var globallyUsed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var roomPlayer in room.Players)
            {
                roomPlayer.Score = 0;
                roomPlayer.RemainingWords.Clear();
                roomPlayer.RemainingWords.AddRange(BuildPlayerWords(puzzle, globallyUsed));
                globallyUsed.UnionWith(roomPlayer.RemainingWords);
            }

            room.Phase = RoomPhase.Playing;
            AdvancePastEmptyPlayers(room);
            if (room.CurrentPlayerIndex < 0)
            {
                room.Phase = RoomPhase.Finished;
                room.Outcome = RoomOutcome.Lost;
            }

            Touch(room, now);
            return CreateSnapshot(room, player);
        }
    }

    public WordRingsRoomPlacementResult SubmitPlacement(
        string? roomCode,
        string? playerToken,
        string? word,
        string? membership,
        double x,
        double y)
    {
        lock (_sync)
        {
            var now = _timeProvider.GetUtcNow();
            var room = GetActiveRoom(roomCode, now);
            var player = GetPlayer(room, playerToken);
            if (room.Phase != RoomPhase.Playing)
            {
                throw new WordRingsRoomException(WordRingsRoomError.InvalidPhase);
            }
            if (room.CurrentPlayerIndex < 0 ||
                room.Players[room.CurrentPlayerIndex].Id != player.Id)
            {
                throw new WordRingsRoomException(WordRingsRoomError.NotYourTurn);
            }

            var actualWord = player.RemainingWords.FirstOrDefault(item =>
                string.Equals(item, word?.Trim(), StringComparison.OrdinalIgnoreCase));
            if (actualWord is null)
            {
                throw new WordRingsRoomException(WordRingsRoomError.InvalidWord);
            }

            var actualMembership = NormalizeMembership(membership);
            if (actualMembership is null)
            {
                throw new WordRingsRoomException(WordRingsRoomError.InvalidPlacement);
            }
            var expectedMembership = CanonicalMembership(
                room.Puzzle.Expected.TryGetValue(actualWord, out var expected)
                    ? expected
                    : string.Empty);
            var isCorrect = string.Equals(
                actualMembership,
                expectedMembership,
                StringComparison.Ordinal);
            var isPartial = !isCorrect &&
                IsPartialPlacement(expectedMembership, actualMembership);

            double points = 0;
            if (isCorrect)
            {
                if (expectedMembership.Length == 0)
                {
                    if (!room.OutsidePointAwardedThisTurn)
                    {
                        points = 1;
                        room.OutsidePointAwardedThisTurn = true;
                    }
                }
                else
                {
                    points = 1;
                }
            }
            else if (room.PartialScoreEnabled && isPartial)
            {
                points = 0.5;
            }

            player.RemainingWords.Remove(actualWord);
            player.Score += points;
            room.TeamScore += points;
            room.Placements.Add(new PlacementState(
                room.NextPlacementId++,
                actualWord,
                actualMembership,
                Math.Clamp(x, 3, 97),
                Math.Clamp(y, 3, 97),
                isCorrect,
                isPartial,
                points,
                player.Id,
                player.Name));
            if (room.Placements.Count > MaximumSharedPlacements)
            {
                room.Placements.RemoveRange(0, room.Placements.Count - MaximumSharedPlacements);
            }

            var turnContinues = isCorrect;
            if (room.TeamScore >= room.TargetScore)
            {
                room.Phase = RoomPhase.Finished;
                room.Outcome = RoomOutcome.Won;
                turnContinues = false;
            }
            else if (room.Players.All(item => item.RemainingWords.Count == 0))
            {
                room.Phase = RoomPhase.Finished;
                room.Outcome = RoomOutcome.Lost;
                turnContinues = false;
            }
            else if (!isCorrect || player.RemainingWords.Count == 0)
            {
                AdvanceTurn(room);
                turnContinues = false;
            }

            Touch(room, now);
            return new WordRingsRoomPlacementResult(
                CreateSnapshot(room, player),
                actualWord,
                actualMembership,
                expectedMembership,
                isCorrect,
                isPartial,
                points,
                turnContinues);
        }
    }

    private static bool IsPlayable(WordRingsPuzzle puzzle) =>
        !string.IsNullOrWhiteSpace(puzzle.BlueRuleText) &&
        !string.IsNullOrWhiteSpace(puzzle.YellowRuleText) &&
        !string.IsNullOrWhiteSpace(puzzle.RedRuleText) &&
        puzzle.Words.Count > 0;

    private string AllocateRoomCode()
    {
        for (var attempt = 0; attempt < MaximumCodeAttempts; attempt++)
        {
            Span<char> chars = stackalloc char[CodeLength];
            for (var index = 0; index < chars.Length; index++)
            {
                chars[index] = CodeAlphabet[RandomNumberGenerator.GetInt32(CodeAlphabet.Length)];
            }
            var candidate = new string(chars);
            if (!_rooms.ContainsKey(candidate))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("Could not allocate a unique Word Rings room code.");
    }

    private RoomState GetActiveRoom(string? roomCode, DateTimeOffset now)
    {
        var normalized = NormalizeRoomCode(roomCode);
        if (!_rooms.TryGetValue(normalized, out var room))
        {
            throw new WordRingsRoomException(WordRingsRoomError.RoomNotFound);
        }
        if (now - room.LastActivityUtc > InactivityTimeout)
        {
            _rooms.Remove(normalized);
            throw new WordRingsRoomException(WordRingsRoomError.RoomExpired);
        }
        return room;
    }

    private static PlayerState GetPlayer(RoomState room, string? playerToken)
    {
        if (string.IsNullOrWhiteSpace(playerToken))
        {
            throw new WordRingsRoomException(WordRingsRoomError.InvalidPlayer);
        }
        var player = room.Players.FirstOrDefault(item =>
            string.Equals(item.Token, playerToken, StringComparison.Ordinal));
        return player ?? throw new WordRingsRoomException(WordRingsRoomError.InvalidPlayer);
    }

    private static void Touch(RoomState room, DateTimeOffset now)
    {
        room.LastActivityUtc = now;
        room.Version++;
    }

    private void RemoveExpiredCore(DateTimeOffset now)
    {
        var expired = _rooms
            .Where(pair => now - pair.Value.LastActivityUtc > InactivityTimeout)
            .Select(pair => pair.Key)
            .ToArray();
        foreach (var code in expired)
        {
            _rooms.Remove(code);
        }
    }

    private static void AdvanceTurn(RoomState room)
    {
        room.OutsidePointAwardedThisTurn = false;
        if (room.Players.Count == 0)
        {
            room.CurrentPlayerIndex = -1;
            return;
        }

        var start = room.CurrentPlayerIndex;
        for (var offset = 1; offset <= room.Players.Count; offset++)
        {
            var index = (start + offset + room.Players.Count) % room.Players.Count;
            if (room.Players[index].RemainingWords.Count > 0)
            {
                room.CurrentPlayerIndex = index;
                return;
            }
        }
        room.CurrentPlayerIndex = -1;
    }

    private static void AdvancePastEmptyPlayers(RoomState room)
    {
        if (room.Players.Count == 0)
        {
            room.CurrentPlayerIndex = -1;
            return;
        }
        if (room.Players[room.CurrentPlayerIndex].RemainingWords.Count > 0)
        {
            return;
        }
        AdvanceTurn(room);
    }

    private static WordRingsRoomConnection CreateConnection(RoomState room, PlayerState player) =>
        new(room.Code, player.Token, CreateSnapshot(room, player));

    private static WordRingsRoomSnapshot CreateSnapshot(RoomState room, PlayerState player)
    {
        var currentPlayerId = room.Phase == RoomPhase.Playing &&
                              room.CurrentPlayerIndex >= 0 &&
                              room.CurrentPlayerIndex < room.Players.Count
            ? room.Players[room.CurrentPlayerIndex].Id
            : (Guid?)null;
        var playerSnapshots = room.Players
            .Select(item => new WordRingsRoomPlayerSnapshot(
                item.Id,
                item.Name,
                item.Score,
                item.IsHost,
                currentPlayerId == item.Id,
                item.RemainingWords.Count))
            .ToArray();
        var bank = player.RemainingWords.Take(MaximumBankWords).ToArray();
        var queued = player.RemainingWords.Skip(MaximumBankWords).ToArray();
        var placements = room.Placements
            .Select(item => new WordRingsRoomPlacementSnapshot(
                item.Id,
                item.Word,
                item.Membership,
                item.X,
                item.Y,
                item.IsCorrect,
                item.IsPartial,
                item.PointsAwarded,
                item.PlayerId,
                item.PlayerName))
            .ToArray();

        return new WordRingsRoomSnapshot(
            room.Code,
            room.Version,
            room.Phase.ToString().ToLowerInvariant(),
            room.Outcome.ToString().ToLowerInvariant(),
            room.TargetScore,
            room.PartialScoreEnabled,
            room.TeamScore,
            player.Id,
            currentPlayerId,
            player.IsHost,
            player.IsHost && room.Phase == RoomPhase.Waiting && room.Players.Count >= 2,
            room.Puzzle.BlueRuleText,
            room.Puzzle.YellowRuleText,
            room.Puzzle.RedRuleText,
            playerSnapshots,
            bank,
            queued,
            placements);
    }

    private static IReadOnlyList<string> BuildPlayerWords(
        WordRingsPuzzle puzzle,
        IReadOnlySet<string> globallyUsed)
    {
        var targetCount = Math.Min(MaximumPlayerWords, puzzle.Words.Count);
        if (targetCount == 0)
        {
            return [];
        }

        var preferred = puzzle.Words
            .Where(word => !globallyUsed.Contains(word))
            .OrderBy(_ => Random.Shared.Next())
            .ToList();
        if (preferred.Count < targetCount)
        {
            preferred.AddRange(puzzle.Words
                .Where(word => !preferred.Contains(word, StringComparer.OrdinalIgnoreCase))
                .OrderBy(_ => Random.Shared.Next()));
        }

        var pool = preferred.Take(Math.Max(targetCount, preferred.Count)).ToArray();
        var matching = pool
            .Select(word => new WordCandidate(
                word,
                puzzle.Expected.TryGetValue(word, out var membership)
                    ? CanonicalMembership(membership)
                    : string.Empty))
            .Where(item => item.Membership.Length > 0)
            .ToList();
        var outside = pool
            .Where(word => !puzzle.Expected.TryGetValue(word, out var membership) ||
                           string.IsNullOrEmpty(membership))
            .OrderBy(_ => Random.Shared.Next())
            .ToList();

        var minimumMatching = Math.Min(
            matching.Count,
            (int)Math.Ceiling(targetCount * 0.8));
        var outsideCount = Math.Min(outside.Count, Math.Max(0, targetCount - minimumMatching));
        var matchingCount = Math.Min(matching.Count, targetCount - outsideCount);
        var selected = SelectBalanced(matching, matchingCount).ToList();
        selected.AddRange(outside.Take(targetCount - selected.Count));
        if (selected.Count < targetCount)
        {
            selected.AddRange(pool
                .Where(word => !selected.Contains(word, StringComparer.OrdinalIgnoreCase))
                .Take(targetCount - selected.Count));
        }

        return selected.OrderBy(_ => Random.Shared.Next()).ToArray();
    }

    private static IReadOnlyList<string> SelectBalanced(IReadOnlyList<WordCandidate> source, int count)
    {
        var remaining = source.OrderBy(_ => Random.Shared.Next()).ToList();
        var selected = new List<string>();
        var ringUse = new Dictionary<char, int> { ['A'] = 0, ['B'] = 0, ['C'] = 0 };
        var regionUse = new Dictionary<string, int>(StringComparer.Ordinal);

        while (selected.Count < count && remaining.Count > 0)
        {
            var minimumUse = ringUse.Values.Min();
            var best = remaining
                .Select(candidate => new
                {
                    Candidate = candidate,
                    Score = candidate.Membership
                        .Where(ringUse.ContainsKey)
                        .Distinct()
                        .Count(ring => ringUse[ring] == minimumUse) * 1000.0 +
                        (candidate.Membership.Length switch
                        {
                            >= 3 => 800,
                            2 => 500,
                            _ => 0
                        }) -
                        candidate.Membership
                            .Where(ringUse.ContainsKey)
                            .Distinct()
                            .Sum(ring => ringUse[ring]) * 90.0 -
                        regionUse.GetValueOrDefault(candidate.Membership) * 65.0 +
                        Random.Shared.NextDouble() * 10.0
                })
                .OrderByDescending(item => item.Score)
                .First();

            selected.Add(best.Candidate.Word);
            foreach (var ring in best.Candidate.Membership.Where(ringUse.ContainsKey).Distinct())
            {
                ringUse[ring]++;
            }
            regionUse[best.Candidate.Membership] =
                regionUse.GetValueOrDefault(best.Candidate.Membership) + 1;
            remaining.Remove(best.Candidate);
        }

        return selected;
    }

    private static bool IsPartialPlacement(string expected, string actual)
    {
        if (expected.Length == 0 || actual.Length == 0)
        {
            return false;
        }
        return expected.Any(actual.Contains);
    }

    private static string? NormalizeMembership(string? value)
    {
        var raw = value?.Trim().ToUpperInvariant() ?? string.Empty;
        if (raw.Any(ch => ch is not ('A' or 'B' or 'C')))
        {
            return null;
        }
        return CanonicalMembership(raw);
    }

    private static string CanonicalMembership(string value) =>
        string.Concat(value
            .Where(ch => ch is 'A' or 'B' or 'C')
            .Distinct()
            .OrderBy(ch => ch));

    private static string NormalizeRoomCode(string? value)
    {
        var code = value?.Trim().ToUpperInvariant() ?? string.Empty;
        if (code.Length != CodeLength || code.Any(ch => !char.IsLetterOrDigit(ch)))
        {
            throw new WordRingsRoomException(WordRingsRoomError.RoomNotFound);
        }
        return code;
    }

    private static string? NormalizePlayerName(string? value)
    {
        var name = value?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > MaximumPlayerNameLength)
        {
            return null;
        }
        return name;
    }

    private static string CreatePlayerToken() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(24));

    private enum RoomPhase
    {
        Waiting,
        Playing,
        Finished
    }

    private enum RoomOutcome
    {
        None,
        Won,
        Lost
    }

    private sealed class RoomState
    {
        public RoomState(
            string code,
            int targetScore,
            bool partialScoreEnabled,
            WordRingsPuzzle puzzle,
            PlayerState host,
            DateTimeOffset now)
        {
            Code = code;
            TargetScore = targetScore;
            PartialScoreEnabled = partialScoreEnabled;
            Puzzle = puzzle;
            Players.Add(host);
            LastActivityUtc = now;
        }

        public string Code { get; }
        public int TargetScore { get; }
        public bool PartialScoreEnabled { get; }
        public WordRingsPuzzle Puzzle { get; set; }
        public List<PlayerState> Players { get; } = [];
        public List<PlacementState> Placements { get; } = [];
        public RoomPhase Phase { get; set; } = RoomPhase.Waiting;
        public RoomOutcome Outcome { get; set; } = RoomOutcome.None;
        public double TeamScore { get; set; }
        public int CurrentPlayerIndex { get; set; } = -1;
        public bool OutsidePointAwardedThisTurn { get; set; }
        public long NextPlacementId { get; set; } = 1;
        public long Version { get; set; } = 1;
        public DateTimeOffset LastActivityUtc { get; set; }
    }

    private sealed class PlayerState(
        Guid id,
        string token,
        string name,
        bool isHost)
    {
        public Guid Id { get; } = id;
        public string Token { get; } = token;
        public string Name { get; } = name;
        public bool IsHost { get; } = isHost;
        public double Score { get; set; }
        public List<string> RemainingWords { get; } = [];
    }

    private sealed record PlacementState(
        long Id,
        string Word,
        string Membership,
        double X,
        double Y,
        bool IsCorrect,
        bool IsPartial,
        double PointsAwarded,
        Guid PlayerId,
        string PlayerName);

    private sealed record WordCandidate(string Word, string Membership);
}
