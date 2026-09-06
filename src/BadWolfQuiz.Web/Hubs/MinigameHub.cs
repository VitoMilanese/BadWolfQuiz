using BadWolfQuiz.Web.Data;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BadWolfQuiz.Web.Hubs;

public sealed class MinigameHub(
    IDbContextFactory<QuizDbContext> dbFactory,
    IOptions<MinigameOptions> options,
    MinigameRoomStore roomStore) : Hub
{
    private static readonly MinigameAiRoomStore AiRooms = new(TimeProvider.System);

    private MinigameCatalogStore Catalog =>
        new(dbFactory, options.Value.CardCount);

    private MinigameAiCatalogStore AiCatalog =>
        new(dbFactory, options.Value.CardCount);

    public async Task<MinigameCardCatalogSnapshot> GetCatalog()
    {
        var counts = await Catalog.GetCountsAsync(Context.ConnectionAborted);
        var maximum = counts.GameCount;
        var defaultCount = maximum >= MinigameRoomStore.MinimumGameCardCount
            ? Math.Clamp(
                Catalog.DefaultCardCount,
                MinigameRoomStore.MinimumGameCardCount,
                maximum)
            : 0;
        var aiMaximum = await AiCatalog.GetEligibleGameCountAsync(
            Context.ConnectionAborted);
        return new MinigameCardCatalogSnapshot(
            MinigameRoomStore.MinimumGameCardCount,
            maximum,
            defaultCount,
            counts.QuestionCount >= MinigameQuestionStore.MinimumQuestionCount,
            counts.QuestionCount,
            aiMaximum);
    }

    public async Task<MinigameRoomConnection> CreateRoom(
        MinigameThemeSnapshot? theme = null)
    {
        var membership = roomStore.CreateRoom(theme);
        await JoinRoomGroup(membership.RoomCode);
        return membership;
    }

    public async Task<MinigameRoomConnection> JoinRoom(string roomCode)
    {
        try
        {
            if (AiRooms.IsActive(roomCode))
            {
                throw new MinigameRoomException(MinigameRoomError.RoomFull);
            }
            var membership = roomStore.JoinRoom(roomCode);
            await JoinRoomGroup(membership.RoomCode);
            await BroadcastRoomChanged(membership.State);
            return membership;
        }
        catch (MinigameRoomException exception)
        {
            throw CreateHubException(exception);
        }
    }

    public async Task<MinigameRoomSnapshot> GetRoomState(
        string roomCode,
        string playerToken,
        bool touchActivity = true)
    {
        try
        {
            MinigameRoomSnapshot state;
            if (AiRooms.IsActive(roomCode))
            {
                if (touchActivity)
                {
                    roomStore.TouchRoom(roomCode, playerToken);
                }
                state = AiRooms.GetState(roomCode, playerToken, touchActivity);
            }
            else
            {
                state = roomStore.GetState(roomCode, playerToken, touchActivity);
            }
            await JoinRoomGroup(state.RoomCode);
            return state;
        }
        catch (MinigameRoomException exception)
        {
            throw CreateHubException(exception);
        }
    }

    public MinigameRoomSnapshot TouchRoom(
        string roomCode,
        string playerToken)
    {
        try
        {
            if (AiRooms.IsActive(roomCode))
            {
                roomStore.TouchRoom(roomCode, playerToken);
                return AiRooms.TouchRoom(roomCode, playerToken);
            }
            return roomStore.TouchRoom(roomCode, playerToken);
        }
        catch (MinigameRoomException exception)
        {
            throw CreateHubException(exception);
        }
    }

    public MinigameAiRoomStatus GetAiStatus(string roomCode, string playerToken)
    {
        try
        {
            return AiRooms.IsActive(roomCode)
                ? AiRooms.GetStatus(roomCode, playerToken)
                : new MinigameAiRoomStatus(false, false);
        }
        catch (MinigameRoomException exception)
        {
            throw CreateHubException(exception);
        }
    }

    public async Task<MinigameRoomSnapshot> StartNewGame(
        string roomCode,
        string playerToken,
        int cardCount,
        bool questionCardsEnabled = false,
        bool playAgainstAi = false)
    {
        try
        {
            MinigameRoomSnapshot state;
            if (playAgainstAi)
            {
                if (AiRooms.IsActive(roomCode))
                {
                    AiRooms.Remove(roomCode, playerToken);
                }
                var baseState = roomStore.GetState(roomCode, playerToken);
                if (baseState.PlayerNumber != 1)
                {
                    throw new MinigameRoomException(MinigameRoomError.InvalidPlayer);
                }
                var aiMaximum = await AiCatalog.GetEligibleGameCountAsync(
                    Context.ConnectionAborted);
                if (cardCount < MinigameRoomStore.MinimumGameCardCount ||
                    cardCount > aiMaximum)
                {
                    throw new HubException("MINIGAME_ROOM_AIUNAVAILABLE");
                }
                var gameData = await AiCatalog.GenerateGameAsync(
                    cardCount,
                    Context.ConnectionAborted);
                state = AiRooms.Start(roomCode, playerToken, baseState, gameData);
            }
            else
            {
                if (AiRooms.IsActive(roomCode))
                {
                    AiRooms.Remove(roomCode, playerToken);
                }
                var counts = await Catalog.GetCountsAsync(Context.ConnectionAborted);
                if (cardCount < MinigameRoomStore.MinimumGameCardCount ||
                    cardCount > counts.GameCount)
                {
                    throw new MinigameRoomException(MinigameRoomError.InvalidCardCount);
                }
                var cards = await Catalog.GenerateCardsAsync(
                    cardCount,
                    Context.ConnectionAborted);
                var questions = questionCardsEnabled
                    ? await Catalog.GetQuestionsAsync(Context.ConnectionAborted)
                    : [];
                if (questionCardsEnabled &&
                    questions.Count < MinigameQuestionStore.MinimumQuestionCount)
                {
                    throw new MinigameRoomException(MinigameRoomError.QuestionsUnavailable);
                }
                state = roomStore.StartNewGame(
                    roomCode,
                    playerToken,
                    cards,
                    questionCardsEnabled,
                    questions);
            }
            await BroadcastRoomChanged(state);
            return state;
        }
        catch (MinigameRoomException exception)
        {
            throw CreateHubException(exception);
        }
    }

    public Task<MinigameRoomSnapshot> RestartGame(
        string roomCode,
        string playerToken) =>
        MutateRoom(
            roomCode,
            playerToken,
            () => roomStore.RestartGame(roomCode, playerToken),
            () => AiRooms.Restart(roomCode, playerToken));

    public Task<MinigameRoomSnapshot> ToggleExclusion(
        string roomCode,
        string playerToken,
        string fileName) =>
        MutateRoom(
            roomCode,
            playerToken,
            () => roomStore.ToggleExclusion(roomCode, playerToken, fileName),
            () => AiRooms.ToggleExclusion(roomCode, playerToken, fileName));

    public Task<MinigameRoomSnapshot> SelectQuestion(
        string roomCode,
        string playerToken,
        int optionIndex) =>
        MutateRoom(
            roomCode,
            playerToken,
            () => roomStore.SelectQuestion(roomCode, playerToken, optionIndex),
            () => AiRooms.SelectQuestion(roomCode, playerToken, optionIndex));

    public Task<MinigameRoomSnapshot> SubmitQuestionResponse(
        string roomCode,
        string playerToken,
        bool? answerYes) =>
        MutateRoom(
            roomCode,
            playerToken,
            () => answerYes is bool multiplayerAnswer
                ? roomStore.SubmitQuestionResponse(roomCode, playerToken, multiplayerAnswer)
                : throw new MinigameRoomException(MinigameRoomError.InvalidQuestion),
            () => AiRooms.SubmitQuestionResponse(roomCode, playerToken, answerYes));

    public Task<MinigameRoomSnapshot> EndTurn(
        string roomCode,
        string playerToken) =>
        MutateRoom(
            roomCode,
            playerToken,
            () => roomStore.EndTurn(roomCode, playerToken),
            () => AiRooms.EndTurn(roomCode, playerToken));

    public Task<MinigameRoomSnapshot> ExpireTurn(
        string roomCode,
        string playerToken) =>
        MutateRoom(
            roomCode,
            playerToken,
            () => roomStore.ExpireTurn(roomCode, playerToken),
            () => AiRooms.ExpireTurn(roomCode, playerToken));

    public Task<MinigameRoomSnapshot> SubmitGuess(
        string roomCode,
        string playerToken,
        string fileName) =>
        MutateRoom(
            roomCode,
            playerToken,
            () => roomStore.SubmitGuess(roomCode, playerToken, fileName),
            () => AiRooms.SubmitGuess(roomCode, playerToken, fileName));

    private async Task<MinigameRoomSnapshot> MutateRoom(
        string roomCode,
        string playerToken,
        Func<MinigameRoomSnapshot> multiplayerMutation,
        Func<MinigameRoomSnapshot> aiMutation)
    {
        try
        {
            MinigameRoomSnapshot state;
            if (AiRooms.IsActive(roomCode))
            {
                roomStore.TouchRoom(roomCode, playerToken);
                state = aiMutation();
            }
            else
            {
                state = multiplayerMutation();
            }
            await BroadcastRoomChanged(state);
            return state;
        }
        catch (MinigameRoomException exception)
        {
            throw CreateHubException(exception);
        }
    }

    private Task JoinRoomGroup(string roomCode) =>
        Groups.AddToGroupAsync(
            Context.ConnectionId,
            MinigameRoomStore.GetSignalRGroupName(roomCode));

    private Task BroadcastRoomChanged(MinigameRoomSnapshot state) =>
        Clients
            .Group(MinigameRoomStore.GetSignalRGroupName(state.RoomCode))
            .SendAsync("roomChanged", state.Version);

    private static HubException CreateHubException(
        MinigameRoomException exception) =>
        new($"MINIGAME_ROOM_{exception.Error.ToString().ToUpperInvariant()}");
}

public sealed record MinigameCardCatalogSnapshot(
    int MinimumCardCount,
    int MaximumCardCount,
    int DefaultCardCount,
    bool QuestionsAvailable,
    int QuestionCount,
    int AiMaximumCardCount);
