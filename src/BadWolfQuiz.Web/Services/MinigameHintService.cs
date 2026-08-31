using System.Collections.Concurrent;
using System.Globalization;
using BadWolfQuiz.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace BadWolfQuiz.Web.Services;

public sealed class MinigameHintService
{
    public const int MinimumSearchLength = 3;
    public const int SearchPageSize = 20;

    private const int MaximumSearchLength = 100;

    private static readonly ConcurrentDictionary<string, bool> HintsByRoom =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly MinigameCatalogStore _catalog;
    private readonly MinigameRoomStore _roomStore;

    public MinigameHintService(
        IDbContextFactory<QuizDbContext> dbFactory,
        int defaultCardCount,
        MinigameRoomStore roomStore)
    {
        ArgumentNullException.ThrowIfNull(dbFactory);
        ArgumentNullException.ThrowIfNull(roomStore);

        _catalog = new MinigameCatalogStore(dbFactory, defaultCardCount);
        _roomStore = roomStore;
    }

    public void SetEnabled(string roomCode, bool enabled)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomCode);
        HintsByRoom[roomCode.Trim().ToUpperInvariant()] = enabled;
    }

    public bool GetHintsEnabled(string? roomCode, string? playerToken)
    {
        var state = _roomStore.GetState(
            roomCode,
            playerToken,
            touchActivity: false);
        return IsEnabled(state.RoomCode);
    }

    public async Task<MinigameCardHintSnapshot> GetCardHintsAsync(
        string? roomCode,
        string? playerToken,
        string? gameKey,
        CancellationToken cancellationToken = default)
    {
        var state = GetHintState(roomCode, playerToken);
        var card = GetActiveCard(state, gameKey, out var gameId);

        var answers = await GetAnswerMapAsync(gameId, cancellationToken);
        var pinnedQuestions = state.MyAvailableQuestions
            .Take(MinigameQuestionStore.MinimumQuestionCount)
            .ToArray();
        var questionsAskedToOpponent = state.QuestionHistory
            .Where(entry =>
                entry.Kind == MinigameQuestionHistoryKind.Question &&
                entry.PlayerNumber == state.PlayerNumber &&
                !string.IsNullOrWhiteSpace(entry.Value))
            .Reverse()
            .Select(entry => entry.Value!)
            .ToArray();

        return new MinigameCardHintSnapshot(
            card.FileName,
            card.DisplayName,
            BuildRows(pinnedQuestions, answers),
            BuildRows(questionsAskedToOpponent, answers));
    }

    public async Task<MinigameCardHintSearchSnapshot> SearchCardHintsAsync(
        string? roomCode,
        string? playerToken,
        string? gameKey,
        string? query,
        int page,
        CancellationToken cancellationToken = default)
    {
        var state = GetHintState(roomCode, playerToken);
        if (state.QuestionCardsEnabled)
        {
            throw new MinigameRoomException(MinigameRoomError.InvalidPhase);
        }

        var card = GetActiveCard(state, gameKey, out var gameId);
        var normalizedQuery = (query ?? string.Empty).Trim();
        if (normalizedQuery.Length > MaximumSearchLength)
        {
            normalizedQuery = normalizedQuery[..MaximumSearchLength];
        }

        if (normalizedQuery.Length < MinimumSearchLength)
        {
            return new MinigameCardHintSearchSnapshot(
                card.FileName,
                card.DisplayName,
                normalizedQuery,
                1,
                SearchPageSize,
                0,
                0,
                []);
        }

        var matches = (await _catalog.GetAnswerItemsAsync(gameId, cancellationToken))
            .Where(row =>
                row.AnswerYes.HasValue &&
                row.QuestionText.Contains(
                    normalizedQuery,
                    StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var totalCount = matches.Length;
        var totalPages = totalCount == 0
            ? 0
            : (int)Math.Ceiling(totalCount / (double)SearchPageSize);
        var effectivePage = totalPages == 0
            ? 1
            : Math.Clamp(page, 1, totalPages);
        var items = matches
            .Skip((effectivePage - 1) * SearchPageSize)
            .Take(SearchPageSize)
            .Select(row => new MinigameHintAnswerRow(
                row.QuestionText,
                row.AnswerYes!.Value))
            .ToArray();

        return new MinigameCardHintSearchSnapshot(
            card.FileName,
            card.DisplayName,
            normalizedQuery,
            effectivePage,
            SearchPageSize,
            totalCount,
            totalPages,
            items);
    }

    public async Task<MinigameQuestionResponseHintSnapshot> GetQuestionResponseHintAsync(
        string? roomCode,
        string? playerToken,
        CancellationToken cancellationToken = default)
    {
        var state = GetHintState(roomCode, playerToken);
        if (state.PendingQuestionResponsePlayerNumber != state.PlayerNumber ||
            string.IsNullOrWhiteSpace(state.PendingQuestion) ||
            string.IsNullOrWhiteSpace(state.MySecretCardFileName))
        {
            throw new MinigameRoomException(
                MinigameRoomError.QuestionResponseNotPending);
        }

        if (!TryParseGameId(state.MySecretCardFileName, out var gameId))
        {
            throw new MinigameRoomException(MinigameRoomError.CardNotFound);
        }

        var answers = await GetAnswerMapAsync(gameId, cancellationToken);
        answers.TryGetValue(state.PendingQuestion, out var answerYes);
        return new MinigameQuestionResponseHintSnapshot(
            state.PendingQuestion,
            answerYes);
    }

    private MinigameRoomSnapshot GetHintState(
        string? roomCode,
        string? playerToken)
    {
        var state = _roomStore.GetState(
            roomCode,
            playerToken,
            touchActivity: false);
        if (state.Phase != MinigameRoomPhase.Playing ||
            !IsEnabled(state.RoomCode))
        {
            throw new MinigameRoomException(MinigameRoomError.InvalidPhase);
        }

        return state;
    }

    private static MinigameCardDescriptor GetActiveCard(
        MinigameRoomSnapshot state,
        string? gameKey,
        out int gameId)
    {
        var card = state.Cards.FirstOrDefault(item =>
            string.Equals(item.FileName, gameKey, StringComparison.Ordinal));
        if (card is null || !TryParseGameId(card.FileName, out gameId))
        {
            throw new MinigameRoomException(MinigameRoomError.CardNotFound);
        }

        return card;
    }

    private bool IsEnabled(string roomCode) =>
        HintsByRoom.TryGetValue(roomCode, out var enabled) && enabled;

    private async Task<Dictionary<string, bool?>> GetAnswerMapAsync(
        int gameId,
        CancellationToken cancellationToken)
    {
        var rows = await _catalog.GetAnswerItemsAsync(gameId, cancellationToken);
        return rows.ToDictionary(
            row => row.QuestionText,
            row => row.AnswerYes,
            StringComparer.Ordinal);
    }

    private static IReadOnlyList<MinigameHintAnswerRow> BuildRows(
        IEnumerable<string> questions,
        IReadOnlyDictionary<string, bool?> answers) =>
        questions
            .Select(question =>
            {
                answers.TryGetValue(question, out var answerYes);
                return new MinigameHintAnswerRow(question, answerYes);
            })
            .ToArray();

    private static bool TryParseGameId(string? gameKey, out int gameId) =>
        int.TryParse(
            gameKey,
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out gameId) &&
        gameId > 0;
}

public sealed record MinigameHintAnswerRow(
    string Question,
    bool? AnswerYes);

public sealed record MinigameCardHintSnapshot(
    string GameKey,
    string GameName,
    IReadOnlyList<MinigameHintAnswerRow> PinnedQuestions,
    IReadOnlyList<MinigameHintAnswerRow> AskedQuestions);

public sealed record MinigameCardHintSearchSnapshot(
    string GameKey,
    string GameName,
    string Query,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages,
    IReadOnlyList<MinigameHintAnswerRow> Items);

public sealed record MinigameQuestionResponseHintSnapshot(
    string Question,
    bool? AnswerYes);
