namespace BadWolfQuiz.Web.Services;

public sealed partial class MinigameRoomStore
{
    public const int MinimumQuestionSearchLength = 3;
    public const int QuestionSearchPageSize = 10;

    public MinigameRoomSnapshot StartNewGameWithQuestionSearch(
        string? roomCode,
        string? playerToken,
        IReadOnlyList<MinigameCardDescriptor> cards,
        IReadOnlyList<string> questions)
    {
        ArgumentNullException.ThrowIfNull(cards);
        ArgumentNullException.ThrowIfNull(questions);
        if (cards.Count < MinimumGameCardCount ||
            cards.Select(card => card.FileName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count() != cards.Count)
        {
            throw new MinigameRoomException(MinigameRoomError.InvalidCardCount);
        }

        lock (_sync)
        {
            var now = _timeProvider.GetUtcNow();
            var room = GetActiveRoom(roomCode, now);
            var playerNumber = GetPlayerNumber(room, playerToken);

            room.Cards = cards.ToArray();
            room.Player1Excluded.Clear();
            room.Player2Excluded.Clear();
            room.Player1SecretFileName = null;
            room.Player2SecretFileName = null;
            room.CurrentPlayerNumber = null;
            room.TurnDeadlineUtc = null;
            room.Player1TurnsStarted = 0;
            room.Player2TurnsStarted = 0;
            room.WinnerPlayerNumber = null;
            room.QuestionGame = MinigameQuestionGameState.Create(
                questions,
                MinigameQuestionSelectionMode.Search);
            room.Phase = MinigameRoomPhase.ChoosingExclusions;
            room.GameNumber++;
            room.LastActivityUtc = now;
            room.Version++;

            return CreateSnapshot(room, playerNumber);
        }
    }

    public MinigameQuestionSelectionMode GetQuestionSelectionMode(
        string? roomCode,
        string? playerToken)
    {
        lock (_sync)
        {
            var now = _timeProvider.GetUtcNow();
            var room = GetActiveRoom(roomCode, now);
            _ = GetPlayerNumber(room, playerToken);
            return room.QuestionGame?.SelectionMode ?? MinigameQuestionSelectionMode.Cards;
        }
    }

    public MinigameQuestionSearchSnapshot SearchAvailableQuestions(
        string? roomCode,
        string? playerToken,
        string? query,
        int page = 1)
    {
        var normalizedQuery = query?.Trim() ?? string.Empty;
        if (normalizedQuery.Length < MinimumQuestionSearchLength || page < 1)
        {
            throw new MinigameRoomException(MinigameRoomError.InvalidQuestion);
        }

        lock (_sync)
        {
            var now = _timeProvider.GetUtcNow();
            var room = GetActiveRoom(roomCode, now);
            var playerNumber = GetPlayerNumber(room, playerToken);
            if (room.Phase != MinigameRoomPhase.Playing ||
                room.QuestionGame?.SelectionMode != MinigameQuestionSelectionMode.Search)
            {
                throw new MinigameRoomException(MinigameRoomError.InvalidPhase);
            }

            var matches = room.QuestionGame
                .GetRemainingSearchQuestions(playerNumber)
                .Where(question => question.Contains(
                    normalizedQuery,
                    StringComparison.OrdinalIgnoreCase))
                .ToArray();
            var totalCount = matches.Length;
            var totalPages = totalCount == 0
                ? 0
                : (int)Math.Ceiling(totalCount / (double)QuestionSearchPageSize);
            var effectivePage = totalPages == 0
                ? 1
                : Math.Min(page, totalPages);
            var items = matches
                .Skip((effectivePage - 1) * QuestionSearchPageSize)
                .Take(QuestionSearchPageSize)
                .ToArray();

            return new MinigameQuestionSearchSnapshot(
                items,
                effectivePage,
                QuestionSearchPageSize,
                totalCount,
                totalPages);
        }
    }

    public MinigameRoomSnapshot SelectQuestionByText(
        string? roomCode,
        string? playerToken,
        string? question)
    {
        lock (_sync)
        {
            var now = _timeProvider.GetUtcNow();
            var room = GetActiveRoom(roomCode, now);
            var playerNumber = GetPlayerNumber(room, playerToken);
            RequireCurrentTurn(room, playerNumber);

            if (room.QuestionGame?.SelectionMode != MinigameQuestionSelectionMode.Search ||
                string.IsNullOrWhiteSpace(question))
            {
                throw new MinigameRoomException(MinigameRoomError.InvalidQuestion);
            }

            room.QuestionGame.SelectQuestion(playerNumber, question);
            room.LastActivityUtc = now;
            room.Version++;
            return CreateSnapshot(room, playerNumber);
        }
    }

    internal IReadOnlyList<string> GetRemainingSearchQuestions(
        string? roomCode,
        string? playerToken)
    {
        lock (_sync)
        {
            var now = _timeProvider.GetUtcNow();
            var room = GetActiveRoom(roomCode, now);
            var playerNumber = GetPlayerNumber(room, playerToken);
            if (room.QuestionGame?.SelectionMode != MinigameQuestionSelectionMode.Search)
            {
                return [];
            }

            return room.QuestionGame
                .GetRemainingSearchQuestions(playerNumber)
                .ToArray();
        }
    }
}

public sealed record MinigameQuestionSearchSnapshot(
    IReadOnlyList<string> Questions,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
