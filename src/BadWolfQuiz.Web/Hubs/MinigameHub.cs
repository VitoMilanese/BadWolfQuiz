using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.SignalR;

namespace BadWolfQuiz.Web.Hubs;

public sealed class MinigameHub(
    MinigameCardSetStore cardSetStore,
    MinigameQuestionStore questionStore,
    MinigameRoomStore roomStore) : Hub
{
    public MinigameCardCatalogSnapshot GetCatalog()
    {
        var maximum = cardSetStore.AvailableCardCount;
        var defaultCount = maximum >= MinigameRoomStore.MinimumGameCardCount
            ? Math.Clamp(
                cardSetStore.DefaultCardCount,
                MinigameRoomStore.MinimumGameCardCount,
                maximum)
            : 0;
        var questionCount = questionStore.AvailableQuestionCount;
        return new MinigameCardCatalogSnapshot(
            MinigameRoomStore.MinimumGameCardCount,
            maximum,
            defaultCount,
            questionCount >= MinigameQuestionStore.MinimumQuestionCount,
            questionCount);
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
            var state = roomStore.GetState(
                roomCode,
                playerToken,
                touchActivity);
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
            return roomStore.TouchRoom(roomCode, playerToken);
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
        bool questionCardsEnabled = false)
    {
        try
        {
            var available = cardSetStore.AvailableCardCount;
            if (cardCount < MinigameRoomStore.MinimumGameCardCount ||
                cardCount > available)
            {
                throw new MinigameRoomException(MinigameRoomError.InvalidCardCount);
            }

            var cards = cardSetStore.GenerateCards(cardCount);
            var questions = questionCardsEnabled
                ? questionStore.GetQuestions()
                : [];
            if (questionCardsEnabled &&
                questions.Count < MinigameQuestionStore.MinimumQuestionCount)
            {
                throw new MinigameRoomException(MinigameRoomError.QuestionsUnavailable);
            }

            var state = roomStore.StartNewGame(
                roomCode,
                playerToken,
                cards,
                questionCardsEnabled,
                questions);
            await BroadcastRoomChanged(state);
            return state;
        }
        catch (MinigameRoomException exception)
        {
            throw CreateHubException(exception);
        }
    }

    public async Task<MinigameRoomSnapshot> RestartGame(
        string roomCode,
        string playerToken)
    {
        return await MutateRoom(() =>
            roomStore.RestartGame(roomCode, playerToken));
    }

    public async Task<MinigameRoomSnapshot> ToggleExclusion(
        string roomCode,
        string playerToken,
        string fileName)
    {
        return await MutateRoom(() =>
            roomStore.ToggleExclusion(roomCode, playerToken, fileName));
    }

    public async Task<MinigameRoomSnapshot> SelectQuestion(
        string roomCode,
        string playerToken,
        int optionIndex)
    {
        return await MutateRoom(() =>
            roomStore.SelectQuestion(roomCode, playerToken, optionIndex));
    }

    public async Task<MinigameRoomSnapshot> SubmitQuestionResponse(
        string roomCode,
        string playerToken,
        bool answerYes)
    {
        return await MutateRoom(() =>
            roomStore.SubmitQuestionResponse(roomCode, playerToken, answerYes));
    }

    public async Task<MinigameRoomSnapshot> EndTurn(
        string roomCode,
        string playerToken)
    {
        return await MutateRoom(() =>
            roomStore.EndTurn(roomCode, playerToken));
    }

    public async Task<MinigameRoomSnapshot> ExpireTurn(
        string roomCode,
        string playerToken)
    {
        return await MutateRoom(() =>
            roomStore.ExpireTurn(roomCode, playerToken));
    }

    public async Task<MinigameRoomSnapshot> SubmitGuess(
        string roomCode,
        string playerToken,
        string fileName)
    {
        return await MutateRoom(() =>
            roomStore.SubmitGuess(roomCode, playerToken, fileName));
    }

    private async Task<MinigameRoomSnapshot> MutateRoom(
        Func<MinigameRoomSnapshot> mutation)
    {
        try
        {
            var state = mutation();
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
    int QuestionCount);
