using System.Collections.Concurrent;
using System.Data.Common;
using System.Globalization;
using System.Security.Cryptography;
using BadWolfQuiz.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace BadWolfQuiz.Web.Services;

public sealed class MinigameSoloAiService
{
    public const int MinimumAnswerCoveragePercent = 80;

    private static readonly ConcurrentDictionary<string, SoloRoomState> SoloRooms =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly IDbContextFactory<QuizDbContext> _dbFactory;
    private readonly MinigameCatalogStore _catalog;
    private readonly MinigameRoomStore _rooms;

    public MinigameSoloAiService(
        IDbContextFactory<QuizDbContext> dbFactory,
        int defaultCardCount,
        MinigameRoomStore rooms)
    {
        _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
        _rooms = rooms ?? throw new ArgumentNullException(nameof(rooms));
        _catalog = new MinigameCatalogStore(dbFactory, defaultCardCount);
    }

    public async Task<MinigameSoloAiAvailabilitySnapshot> GetAvailabilityAsync(
        CancellationToken cancellationToken = default)
    {
        var counts = await _catalog.GetCountsAsync(cancellationToken);
        var games = await _catalog.GetGamesAsync(cancellationToken);
        var eligibleGameCount = counts.QuestionCount == 0
            ? 0
            : games.Count(game => HasRequiredCoverage(
                game.AssignedAnswerCount,
                counts.QuestionCount));

        return new MinigameSoloAiAvailabilitySnapshot(
            eligibleGameCount,
            MinimumAnswerCoveragePercent,
            eligibleGameCount >= MinigameRoomStore.MinimumGameCardCount);
    }

    public MinigameSoloAiStatusSnapshot GetStatus(
        string roomCode,
        string playerToken)
    {
        var room = _rooms.GetState(roomCode, playerToken, touchActivity: false);
        var isSoloGame = _rooms.IsSoloGame(roomCode, playerToken);
        var hasHumanOpponent = room.PlayerCount == 2 && !isSoloGame;
        var unknownIndexes = Array.Empty<int>();

        if (isSoloGame &&
            SoloRooms.TryGetValue(room.RoomCode, out var solo))
        {
            lock (solo.Sync)
            {
                if (solo.GameNumber == room.GameNumber)
                {
                    unknownIndexes = solo.UnknownAnswerHistoryIndexes
                        .Order()
                        .ToArray();
                }
            }
        }

        return new MinigameSoloAiStatusSnapshot(
            isSoloGame,
            room.PlayerNumber == 1 && !hasHumanOpponent,
            hasHumanOpponent,
            unknownIndexes);
    }

    public async Task<MinigameRoomSnapshot> StartSoloGameAsync(
        string roomCode,
        string playerToken,
        int cardCount,
        IReadOnlyList<string> questions,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(questions);
        if (questions.Count < MinigameQuestionStore.MinimumQuestionCount)
        {
            throw new MinigameRoomException(MinigameRoomError.QuestionsUnavailable);
        }

        var human = _rooms.GetState(roomCode, playerToken, touchActivity: false);
        if (human.PlayerNumber != 1)
        {
            throw new MinigameRoomException(MinigameRoomError.InvalidPlayer);
        }

        var counts = await _catalog.GetCountsAsync(cancellationToken);
        var games = await _catalog.GetGamesAsync(cancellationToken);
        var eligibleGames = counts.QuestionCount == 0
            ? new List<MinigameCatalogGameItem>()
            : games
                .Where(game => HasRequiredCoverage(
                    game.AssignedAnswerCount,
                    counts.QuestionCount))
                .ToList();

        if (cardCount < MinigameRoomStore.MinimumGameCardCount ||
            cardCount > eligibleGames.Count)
        {
            throw new MinigameRoomException(MinigameRoomError.InvalidCardCount);
        }

        Shuffle(eligibleGames);
        var cards = eligibleGames
            .Take(cardCount)
            .Select(game => new MinigameCardDescriptor(
                game.Id.ToString(CultureInfo.InvariantCulture),
                game.Name))
            .ToArray();

        var ai = _rooms.EnsureSoloOpponent(roomCode, playerToken);
        MinigameRoomSnapshot state;
        try
        {
            state = _rooms.StartNewGame(
                roomCode,
                playerToken,
                cards,
                questionCardsEnabled: true,
                questions);
        }
        catch
        {
            _rooms.RemoveSoloOpponent(roomCode, playerToken);
            throw;
        }

        var solo = new SoloRoomState(ai.PlayerToken, state.GameNumber);
        SoloRooms[state.RoomCode] = solo;

        var exclusionChoices = cards.ToList();
        Shuffle(exclusionChoices);
        for (var index = 0; index < state.RequiredExclusionsPerPlayer; index++)
        {
            _rooms.ToggleExclusion(
                state.RoomCode,
                ai.PlayerToken,
                exclusionChoices[index].FileName);
        }

        return _rooms.GetState(state.RoomCode, playerToken, touchActivity: false);
    }

    public void DisableSoloGame(string roomCode, string playerToken)
    {
        if (!_rooms.IsSoloGame(roomCode, playerToken))
        {
            return;
        }

        var state = _rooms.RemoveSoloOpponent(roomCode, playerToken);
        SoloRooms.TryRemove(state.RoomCode, out _);
    }

    public bool IsSoloGame(string roomCode, string playerToken) =>
        _rooms.IsSoloGame(roomCode, playerToken);

    public async Task<MinigameRoomSnapshot> AdvanceAsync(
        string roomCode,
        string playerToken,
        CancellationToken cancellationToken = default)
    {
        var human = _rooms.GetState(roomCode, playerToken, touchActivity: false);
        if (human.PlayerNumber != 1 || !_rooms.IsSoloGame(roomCode, playerToken))
        {
            return human;
        }

        if (!SoloRooms.TryGetValue(human.RoomCode, out var solo))
        {
            var ai = _rooms.EnsureSoloOpponent(roomCode, playerToken);
            solo = new SoloRoomState(ai.PlayerToken, human.GameNumber);
            SoloRooms[human.RoomCode] = solo;
        }

        await solo.Gate.WaitAsync(cancellationToken);
        try
        {
            var aiState = _rooms.GetState(
                human.RoomCode,
                solo.AiPlayerToken,
                touchActivity: false);
            ResetForGameIfNeeded(solo, aiState);

            if (aiState.Phase != MinigameRoomPhase.Playing)
            {
                return _rooms.GetState(
                    human.RoomCode,
                    playerToken,
                    touchActivity: false);
            }

            await ProcessHumanAnswersAsync(solo, aiState, cancellationToken);
            aiState = _rooms.GetState(
                human.RoomCode,
                solo.AiPlayerToken,
                touchActivity: false);

            if (aiState.CurrentPlayerNumber == 1 &&
                aiState.PendingQuestionResponsePlayerNumber == 2 &&
                !string.IsNullOrWhiteSpace(aiState.PendingQuestion))
            {
                var answer = await GetAnswerForGameAsync(
                    aiState.MySecretCardFileName,
                    aiState.PendingQuestion,
                    cancellationToken);
                var responded = _rooms.SubmitQuestionResponse(
                    aiState.RoomCode,
                    solo.AiPlayerToken,
                    answer ?? false);
                if (!answer.HasValue)
                {
                    lock (solo.Sync)
                    {
                        solo.UnknownAnswerHistoryIndexes.Add(
                            responded.QuestionHistory.Count - 1);
                    }
                }

                return _rooms.GetState(
                    aiState.RoomCode,
                    playerToken,
                    touchActivity: false);
            }

            if (aiState.CurrentPlayerNumber != 2)
            {
                return _rooms.GetState(
                    aiState.RoomCode,
                    playerToken,
                    touchActivity: false);
            }

            if (aiState.PendingQuestionResponsePlayerNumber == 1)
            {
                return _rooms.GetState(
                    aiState.RoomCode,
                    playerToken,
                    touchActivity: false);
            }

            if (!aiState.HasSelectedQuestionThisTurn &&
                aiState.MyAvailableQuestions.Count > 0)
            {
                var optionIndex = RandomNumberGenerator.GetInt32(
                    aiState.MyAvailableQuestions.Count);
                _rooms.SelectQuestion(
                    aiState.RoomCode,
                    solo.AiPlayerToken,
                    optionIndex);
                return _rooms.GetState(
                    aiState.RoomCode,
                    playerToken,
                    touchActivity: false);
            }

            if (solo.Candidates.Count == 1)
            {
                _rooms.SubmitGuess(
                    aiState.RoomCode,
                    solo.AiPlayerToken,
                    solo.Candidates.Single());
            }
            else if (aiState.MyAvailableQuestions.Count == 0 &&
                     solo.Candidates.Count > 0)
            {
                var guessIndex = RandomNumberGenerator.GetInt32(solo.Candidates.Count);
                var guess = solo.Candidates.ElementAt(guessIndex);
                var guessed = _rooms.SubmitGuess(
                    aiState.RoomCode,
                    solo.AiPlayerToken,
                    guess);
                if (guessed.Phase != MinigameRoomPhase.Finished)
                {
                    solo.Candidates.Remove(guess);
                }
            }
            else
            {
                _rooms.EndTurn(aiState.RoomCode, solo.AiPlayerToken);
            }

            return _rooms.GetState(
                aiState.RoomCode,
                playerToken,
                touchActivity: false);
        }
        finally
        {
            solo.Gate.Release();
        }
    }

    private async Task ProcessHumanAnswersAsync(
        SoloRoomState solo,
        MinigameRoomSnapshot aiState,
        CancellationToken cancellationToken)
    {
        if (!solo.CandidatesInitialized)
        {
            solo.Candidates.Clear();
            foreach (var card in aiState.Cards)
            {
                if (!string.Equals(
                    card.FileName,
                    aiState.MySecretCardFileName,
                    StringComparison.Ordinal))
                {
                    solo.Candidates.Add(card.FileName);
                }
            }
            solo.CandidatesInitialized = true;
        }

        for (var index = solo.ProcessedHistoryCount;
             index < aiState.QuestionHistory.Count;
             index++)
        {
            var entry = aiState.QuestionHistory[index];
            if (entry.Kind == MinigameQuestionHistoryKind.Question &&
                entry.PlayerNumber == 2 &&
                !string.IsNullOrWhiteSpace(entry.Value))
            {
                solo.PendingAiQuestion = entry.Value;
                continue;
            }

            if (entry.Kind != MinigameQuestionHistoryKind.Answer ||
                entry.PlayerNumber != 1 ||
                !entry.AnswerYes.HasValue ||
                string.IsNullOrWhiteSpace(solo.PendingAiQuestion))
            {
                continue;
            }

            var answers = await GetAssignedAnswersForQuestionAsync(
                solo.PendingAiQuestion,
                cancellationToken);
            solo.Candidates.RemoveWhere(candidate =>
            {
                if (!TryParseGameId(candidate, out var gameId) ||
                    !answers.TryGetValue(gameId, out var storedAnswer))
                {
                    return false;
                }
                return storedAnswer != entry.AnswerYes.Value;
            });
            solo.PendingAiQuestion = null;
        }

        solo.ProcessedHistoryCount = aiState.QuestionHistory.Count;
    }

    private static void ResetForGameIfNeeded(
        SoloRoomState solo,
        MinigameRoomSnapshot state)
    {
        if (solo.GameNumber == state.GameNumber)
        {
            return;
        }

        solo.GameNumber = state.GameNumber;
        solo.ProcessedHistoryCount = 0;
        solo.PendingAiQuestion = null;
        solo.CandidatesInitialized = false;
        solo.Candidates.Clear();
        lock (solo.Sync)
        {
            solo.UnknownAnswerHistoryIndexes.Clear();
        }
    }

    private async Task<bool?> GetAnswerForGameAsync(
        string? gameKey,
        string question,
        CancellationToken cancellationToken)
    {
        if (!TryParseGameId(gameKey, out var gameId))
        {
            return null;
        }

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        await db.Database.OpenConnectionAsync(cancellationToken);
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText =
            """
            SELECT a.AnswerYes
            FROM MinigameCatalogAnswers a
            INNER JOIN MinigameCatalogQuestions q ON q.Id = a.QuestionId
            WHERE a.GameId = $gameId AND q.Text = $question
            LIMIT 1;
            """;
        AddParameter(command, "$gameId", gameId);
        AddParameter(command, "$question", question);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is null or DBNull
            ? null
            : Convert.ToInt32(value, CultureInfo.InvariantCulture) != 0;
    }

    private async Task<Dictionary<int, bool>> GetAssignedAnswersForQuestionAsync(
        string question,
        CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        await db.Database.OpenConnectionAsync(cancellationToken);
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText =
            """
            SELECT a.GameId, a.AnswerYes
            FROM MinigameCatalogAnswers a
            INNER JOIN MinigameCatalogQuestions q ON q.Id = a.QuestionId
            WHERE q.Text = $question;
            """;
        AddParameter(command, "$question", question);

        var result = new Dictionary<int, bool>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result[reader.GetInt32(0)] = reader.GetInt32(1) != 0;
        }
        return result;
    }

    private static void AddParameter(
        DbCommand command,
        string name,
        object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static bool HasRequiredCoverage(int assigned, int total) =>
        total > 0 && assigned * 100 >= total * MinimumAnswerCoveragePercent;

    private static bool TryParseGameId(string? value, out int gameId) =>
        int.TryParse(
            value,
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out gameId) && gameId > 0;

    private static void Shuffle<T>(IList<T> values)
    {
        for (var index = values.Count - 1; index > 0; index--)
        {
            var swapIndex = RandomNumberGenerator.GetInt32(index + 1);
            (values[index], values[swapIndex]) = (values[swapIndex], values[index]);
        }
    }

    private sealed class SoloRoomState(string aiPlayerToken, long gameNumber)
    {
        public string AiPlayerToken { get; } = aiPlayerToken;
        public SemaphoreSlim Gate { get; } = new(1, 1);
        public object Sync { get; } = new();
        public long GameNumber { get; set; } = gameNumber;
        public int ProcessedHistoryCount { get; set; }
        public string? PendingAiQuestion { get; set; }
        public bool CandidatesInitialized { get; set; }
        public HashSet<string> Candidates { get; } = new(StringComparer.Ordinal);
        public HashSet<int> UnknownAnswerHistoryIndexes { get; } = [];
    }
}

public sealed record MinigameSoloAiAvailabilitySnapshot(
    int EligibleGameCount,
    int MinimumAnswerCoveragePercent,
    bool Available);

public sealed record MinigameSoloAiStatusSnapshot(
    bool IsSoloGame,
    bool CanStartSoloGame,
    bool HasHumanOpponent,
    IReadOnlyList<int> UnknownAnswerHistoryIndexes);
