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

public static class WordRingsRoomEventType
{
    public const string PlayerJoined = "player-joined";
    public const string PlayerLeft = "player-left";
    public const string PlayerKicked = "player-kicked";
    public const string TurnTransferred = "turn-transferred";
    public const string CheckSubmitted = "check-submitted";
    public const string HostCorrect = "host-correct";
    public const string HostMoved = "host-moved";
}

public sealed record WordRingsRoomEventSnapshot(
    long Sequence,
    string Type,
    Guid? PlayerId,
    string PlayerName);

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
    string PlayerName,
    bool IsPending,
    string SubmittedMembership);

public sealed record WordRingsRoomSnapshot(
    string RoomCode,
    long Version,
    string Phase,
    string Outcome,
    int TargetScore,
    bool PartialScoreEnabled,
    double PlayerScore,
    Guid? WinnerPlayerId,
    Guid PlayerId,
    Guid? CurrentPlayerId,
    bool IsHost,
    bool DedicatedHostMode,
    bool CanStart,
    string BlueRuleText,
    string YellowRuleText,
    string RedRuleText,
    IReadOnlyList<WordRingsRoomPlayerSnapshot> Players,
    IReadOnlyList<string> BankWords,
    IReadOnlyList<string> QueuedWords,
    IReadOnlyList<WordRingsRoomPlacementSnapshot> Placements)
{
    public IReadOnlyList<WordRingsRoomEventSnapshot> Events { get; init; } = [];

    [System.Text.Json.Serialization.JsonIgnore]
    public double TeamScore => PlayerScore;
}

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
    bool TurnContinues,
    bool IsPending);

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
    private const int MaximumRoomEvents = 64;

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
        bool partialScoreEnabled,
        string? previousRoomCode = null,
        string? previousPlayerToken = null)
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
            RemoveOwnedRoomCore(previousRoomCode, previousPlayerToken, code);
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
            AddRoomEvent(room, WordRingsRoomEventType.PlayerJoined, player.Id, player.Name);
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
            if (room.Phase == RoomPhase.Playing)
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
            room.WinnerPlayerId = null;
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
            var placementMembership = isCorrect ? actualMembership : expectedMembership;
            var placementX = Math.Clamp(x, 3, 97);
            var placementY = Math.Clamp(y, 3, 97);
            if (!isCorrect)
            {
                (placementX, placementY) = GetCorrectedPlacementAnchor(expectedMembership);
            }

            room.Placements.Add(new PlacementState(
                room.NextPlacementId++,
                actualWord,
                placementMembership,
                placementX,
                placementY,
                isCorrect,
                isPartial,
                points,
                player.Id,
                player.Name,
                false,
                actualMembership));
            if (room.Placements.Count > MaximumSharedPlacements)
            {
                room.Placements.RemoveRange(0, room.Placements.Count - MaximumSharedPlacements);
            }
            AddRoomEvent(room, WordRingsRoomEventType.CheckSubmitted, player.Id, player.Name);

            var turnContinues = isCorrect;
            if (player.Score >= room.TargetScore)
            {
                room.Phase = RoomPhase.Finished;
                room.Outcome = RoomOutcome.Won;
                room.WinnerPlayerId = player.Id;
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
                turnContinues,
                false);
        }
    }

    public WordRingsRoomPlacementResult SubmitHostedPlacement(
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
            if (room.Placements.Any(item => item.IsPending))
            {
                throw new WordRingsRoomException(WordRingsRoomError.InvalidPhase);
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

            player.RemainingWords.Remove(actualWord);
            room.Placements.Add(new PlacementState(
                room.NextPlacementId++,
                actualWord,
                actualMembership,
                Math.Clamp(x, 3, 97),
                Math.Clamp(y, 3, 97),
                false,
                false,
                0,
                player.Id,
                player.Name,
                true,
                actualMembership));
            if (room.Placements.Count > MaximumSharedPlacements)
            {
                room.Placements.RemoveRange(0, room.Placements.Count - MaximumSharedPlacements);
            }
            AddRoomEvent(room, WordRingsRoomEventType.CheckSubmitted, player.Id, player.Name);

            Touch(room, now);
            return new WordRingsRoomPlacementResult(
                CreateSnapshot(room, player),
                actualWord,
                actualMembership,
                actualMembership,
                false,
                false,
                0,
                false,
                true);
        }
    }

    public WordRingsRoomSnapshot MoveHostedPlacement(
        string? roomCode,
        string? playerToken,
        long placementId,
        string? membership,
        double x,
        double y)
    {
        lock (_sync)
        {
            var now = _timeProvider.GetUtcNow();
            var room = GetActiveRoom(roomCode, now);
            var player = GetPlayer(room, playerToken);
            if (room.Phase == RoomPhase.Waiting)
            {
                throw new WordRingsRoomException(WordRingsRoomError.InvalidPhase);
            }

            var normalizedMembership = NormalizeMembership(membership);
            if (normalizedMembership is null)
            {
                throw new WordRingsRoomException(WordRingsRoomError.InvalidPlacement);
            }

            var placementIndex = room.Placements.FindIndex(item => item.Id == placementId);
            if (placementIndex < 0)
            {
                throw new WordRingsRoomException(WordRingsRoomError.InvalidPlacement);
            }

            var placement = room.Placements[placementIndex];
            if (placement.IsPending)
            {
                if (!player.IsHost)
                {
                    throw new WordRingsRoomException(WordRingsRoomError.NotHost);
                }
                if (room.Phase != RoomPhase.Playing)
                {
                    throw new WordRingsRoomException(WordRingsRoomError.InvalidPhase);
                }
            }
            else if (!string.Equals(
                         placement.Membership,
                         normalizedMembership,
                         StringComparison.Ordinal))
            {
                throw new WordRingsRoomException(WordRingsRoomError.InvalidPlacement);
            }

            room.Placements[placementIndex] = placement with
            {
                Membership = placement.IsPending ? normalizedMembership : placement.Membership,
                X = Math.Clamp(x, 3, 97),
                Y = Math.Clamp(y, 3, 97)
            };
            Touch(room, now);
            return CreateSnapshot(room, player);
        }
    }

    public WordRingsRoomPlacementResult ResolveHostedPlacement(
        string? roomCode,
        string? playerToken,
        long placementId)
    {
        lock (_sync)
        {
            var now = _timeProvider.GetUtcNow();
            var room = GetActiveRoom(roomCode, now);
            var host = GetPlayer(room, playerToken);
            if (!host.IsHost)
            {
                throw new WordRingsRoomException(WordRingsRoomError.NotHost);
            }
            if (room.Phase != RoomPhase.Playing)
            {
                throw new WordRingsRoomException(WordRingsRoomError.InvalidPhase);
            }

            var placementIndex = room.Placements.FindIndex(item => item.Id == placementId && item.IsPending);
            if (placementIndex < 0)
            {
                throw new WordRingsRoomException(WordRingsRoomError.InvalidPlacement);
            }

            var placement = room.Placements[placementIndex];
            var player = room.Players.FirstOrDefault(item => item.Id == placement.PlayerId)
                ?? throw new WordRingsRoomException(WordRingsRoomError.InvalidPlayer);
            var submittedMembership = CanonicalMembership(placement.SubmittedMembership);
            var finalMembership = CanonicalMembership(placement.Membership);
            var stayedInSubmittedRegion = string.Equals(
                submittedMembership,
                finalMembership,
                StringComparison.Ordinal);
            var intersectsSubmittedRegion = !stayedInSubmittedRegion &&
                IsPartialPlacement(submittedMembership, finalMembership);

            double points = 0;
            if (stayedInSubmittedRegion)
            {
                if (finalMembership.Length == 0)
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
            else if (intersectsSubmittedRegion)
            {
                points = 0.5;
            }

            player.Score += points;
            room.Placements[placementIndex] = placement with
            {
                IsPending = false,
                IsCorrect = stayedInSubmittedRegion,
                IsPartial = intersectsSubmittedRegion,
                PointsAwarded = points
            };
            AddRoomEvent(
                room,
                stayedInSubmittedRegion ? WordRingsRoomEventType.HostCorrect : WordRingsRoomEventType.HostMoved,
                player.Id,
                player.Name);

            var turnContinues = stayedInSubmittedRegion;
            if (player.Score >= room.TargetScore)
            {
                room.Phase = RoomPhase.Finished;
                room.Outcome = RoomOutcome.Won;
                room.WinnerPlayerId = player.Id;
                turnContinues = false;
            }
            else if (room.Players.All(item => item.RemainingWords.Count == 0))
            {
                room.Phase = RoomPhase.Finished;
                room.Outcome = RoomOutcome.Lost;
                turnContinues = false;
            }
            else if (!stayedInSubmittedRegion || player.RemainingWords.Count == 0)
            {
                AdvanceTurn(room);
                turnContinues = false;
            }

            Touch(room, now);
            return new WordRingsRoomPlacementResult(
                CreateSnapshot(room, host),
                placement.Word,
                submittedMembership,
                finalMembership,
                stayedInSubmittedRegion,
                intersectsSubmittedRegion,
                points,
                turnContinues,
                false);
        }
    }

    public WordRingsRoomSnapshot MovePlacement(
        string? roomCode,
        string? playerToken,
        long placementId,
        string? membership,
        double x,
        double y)
    {
        lock (_sync)
        {
            var now = _timeProvider.GetUtcNow();
            var room = GetActiveRoom(roomCode, now);
            var player = GetPlayer(room, playerToken);
            if (room.Phase == RoomPhase.Waiting)
            {
                throw new WordRingsRoomException(WordRingsRoomError.InvalidPhase);
            }

            var normalizedMembership = NormalizeMembership(membership);
            if (normalizedMembership is null)
            {
                throw new WordRingsRoomException(WordRingsRoomError.InvalidPlacement);
            }

            var placementIndex = room.Placements.FindIndex(item => item.Id == placementId);
            if (placementIndex < 0)
            {
                throw new WordRingsRoomException(WordRingsRoomError.InvalidPlacement);
            }

            var placement = room.Placements[placementIndex];
            if (!string.Equals(
                    placement.Membership,
                    normalizedMembership,
                    StringComparison.Ordinal))
            {
                throw new WordRingsRoomException(WordRingsRoomError.InvalidPlacement);
            }

            room.Placements[placementIndex] = placement with
            {
                X = Math.Clamp(x, 3, 97),
                Y = Math.Clamp(y, 3, 97)
            };
            Touch(room, now);
            return CreateSnapshot(room, player);
        }
    }

    public void LeaveRoom(string? roomCode, string? playerToken)
    {
        lock (_sync)
        {
            var now = _timeProvider.GetUtcNow();
            var room = GetActiveRoom(roomCode, now);
            var player = GetPlayer(room, playerToken);
            if (player.IsHost)
            {
                return;
            }

            var index = room.Players.FindIndex(item => item.Id == player.Id);
            if (index < 0)
            {
                throw new WordRingsRoomException(WordRingsRoomError.InvalidPlayer);
            }

            RemovePlayerAt(room, index);
            AddRoomEvent(room, WordRingsRoomEventType.PlayerLeft, player.Id, player.Name);
            Touch(room, now);
        }
    }

    internal WordRingsRoomSnapshot SetCurrentPlayer(
        string? roomCode,
        string? playerToken,
        Guid playerId,
        bool dedicatedHost)
    {
        lock (_sync)
        {
            var now = _timeProvider.GetUtcNow();
            var room = GetActiveRoom(roomCode, now);
            var host = GetPlayer(room, playerToken);
            if (!host.IsHost)
            {
                throw new WordRingsRoomException(WordRingsRoomError.NotHost);
            }
            if (room.Phase != RoomPhase.Playing)
            {
                throw new WordRingsRoomException(WordRingsRoomError.InvalidPhase);
            }

            var targetIndex = room.Players.FindIndex(item => item.Id == playerId);
            if (targetIndex < 0)
            {
                throw new WordRingsRoomException(WordRingsRoomError.InvalidPlayer);
            }
            var target = room.Players[targetIndex];
            if ((dedicatedHost && target.IsHost) || target.RemainingWords.Count == 0)
            {
                throw new WordRingsRoomException(WordRingsRoomError.InvalidPlayer);
            }

            room.CurrentPlayerIndex = targetIndex;
            room.OutsidePointAwardedThisTurn = false;
            AddRoomEvent(room, WordRingsRoomEventType.TurnTransferred, target.Id, target.Name);
            Touch(room, now);
            return CreateSnapshot(room, host);
        }
    }

    internal WordRingsRoomSnapshot KickPlayer(
        string? roomCode,
        string? playerToken,
        Guid playerId)
    {
        lock (_sync)
        {
            var now = _timeProvider.GetUtcNow();
            var room = GetActiveRoom(roomCode, now);
            var host = GetPlayer(room, playerToken);
            if (!host.IsHost)
            {
                throw new WordRingsRoomException(WordRingsRoomError.NotHost);
            }

            var index = room.Players.FindIndex(item => item.Id == playerId);
            if (index < 0)
            {
                throw new WordRingsRoomException(WordRingsRoomError.InvalidPlayer);
            }
            var removed = room.Players[index];
            if (removed.IsHost)
            {
                throw new InvalidOperationException("CannotKickHost");
            }

            RemovePlayerAt(room, index);
            AddRoomEvent(room, WordRingsRoomEventType.PlayerKicked, removed.Id, removed.Name);
            Touch(room, now);
            return CreateSnapshot(room, host);
        }
    }

    private void RemoveOwnedRoomCore(
        string? roomCode,
        string? playerToken,
        string exceptRoomCode)
    {
        if (string.IsNullOrWhiteSpace(roomCode) || string.IsNullOrWhiteSpace(playerToken))
        {
            return;
        }

        string normalizedCode;
        try
        {
            normalizedCode = NormalizeRoomCode(roomCode);
        }
        catch (WordRingsRoomException)
        {
            return;
        }

        if (string.Equals(normalizedCode, exceptRoomCode, StringComparison.OrdinalIgnoreCase) ||
            !_rooms.TryGetValue(normalizedCode, out var room))
        {
            return;
        }

        var host = room.Players.FirstOrDefault(item => item.IsHost);
        if (host is null ||
            !string.Equals(host.Token, playerToken, StringComparison.Ordinal))
        {
            return;
        }

        _rooms.Remove(normalizedCode);
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

    private static void AddRoomEvent(
        RoomState room,
        string type,
        Guid? playerId = null,
        string? playerName = null)
    {
        room.Events.Add(new RoomEventState(
            room.NextEventSequence++,
            type,
            playerId,
            playerName ?? string.Empty));
        if (room.Events.Count > MaximumRoomEvents)
        {
            room.Events.RemoveRange(0, room.Events.Count - MaximumRoomEvents);
        }
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

    private static void RemovePlayerAt(RoomState room, int index)
    {
        var current = room.CurrentPlayerIndex;
        var removedCurrent = current == index;
        room.Players.RemoveAt(index);

        if (room.Players.Count == 0)
        {
            room.CurrentPlayerIndex = -1;
        }
        else if (removedCurrent)
        {
            room.CurrentPlayerIndex = room.Players.FindIndex(item => item.RemainingWords.Count > 0);
            room.OutsidePointAwardedThisTurn = false;
        }
        else if (current > index)
        {
            room.CurrentPlayerIndex = current - 1;
        }

        if (room.Phase == RoomPhase.Playing && room.Players.All(item => item.RemainingWords.Count == 0))
        {
            room.Phase = RoomPhase.Finished;
            room.Outcome = RoomOutcome.Lost;
            room.WinnerPlayerId = null;
            room.CurrentPlayerIndex = -1;
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
                item.PlayerName,
                item.IsPending,
                item.SubmittedMembership))
            .ToArray();
        var playerOutcome = room.Phase == RoomPhase.Finished && room.WinnerPlayerId is Guid winnerId
            ? (winnerId == player.Id ? RoomOutcome.Won : RoomOutcome.Lost)
            : room.Outcome;

        return new WordRingsRoomSnapshot(
            room.Code,
            room.Version,
            room.Phase.ToString().ToLowerInvariant(),
            playerOutcome.ToString().ToLowerInvariant(),
            room.TargetScore,
            room.PartialScoreEnabled,
            player.Score,
            room.WinnerPlayerId,
            player.Id,
            currentPlayerId,
            player.IsHost,
            false,
            player.IsHost && room.Phase != RoomPhase.Playing && room.Players.Count >= 2,
            room.Puzzle.BlueRuleText,
            room.Puzzle.YellowRuleText,
            room.Puzzle.RedRuleText,
            playerSnapshots,
            bank,
            queued,
            placements)
        {
            Events = room.Events
                .Select(item => new WordRingsRoomEventSnapshot(
                    item.Sequence,
                    item.Type,
                    item.PlayerId,
                    item.PlayerName))
                .ToArray()
        };
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

    private static (double X, double Y) GetCorrectedPlacementAnchor(string membership) =>
        CanonicalMembership(membership) switch
        {
            "A" => (26.5, 27.25),
            "B" => (73.5, 27.25),
            "C" => (50, 84),
            "AB" => (50, 16.75),
            "AC" => (32, 61),
            "BC" => (68, 61),
            "ABC" => (50, 46.25),
            _ => (8, 88)
        };

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
        public List<RoomEventState> Events { get; } = [];
        public RoomPhase Phase { get; set; } = RoomPhase.Waiting;
        public RoomOutcome Outcome { get; set; } = RoomOutcome.None;
        public Guid? WinnerPlayerId { get; set; }
        public int CurrentPlayerIndex { get; set; } = -1;
        public bool OutsidePointAwardedThisTurn { get; set; }
        public long NextPlacementId { get; set; } = 1;
        public long NextEventSequence { get; set; } = 1;
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
        string PlayerName,
        bool IsPending,
        string SubmittedMembership);

    private sealed record RoomEventState(
        long Sequence,
        string Type,
        Guid? PlayerId,
        string PlayerName);

    private sealed record WordCandidate(string Word, string Membership);
}
