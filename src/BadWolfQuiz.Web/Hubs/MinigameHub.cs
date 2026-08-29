using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.SignalR;

namespace BadWolfQuiz.Web.Hubs;

public sealed class MinigameHub(
    MinigameCardSetStore cardSetStore,
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
        return new MinigameCardCatalogSnapshot(
            MinigameRoomStore.MinimumGameCardCount,
            maximum,
            defaultCount);
    }

    public async Task<MinigameRoomConnection> CreateRoom()
    {
        var membership = roomStore.CreateRoom();
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
        string playerToken)
    {
        try
        {
            var state = roomStore.GetState(roomCode, playerToken);
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
        int cardCount)
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
            var state = roomStore.StartNewGame(roomCode, playerToken, cards);
            await BroadcastRoomChanged(state);
            return state;
        }
        catch (MinigameRoomException exception)
        {
            throw CreateHubException(exception);
        }
    }

    public async Task<MinigameRoomSnapshot> ToggleExclusion(
        string roomCode,
        string playerToken,
        string fileName)
    {
        try
        {
            var state = roomStore.ToggleExclusion(roomCode, playerToken, fileName);
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
    int DefaultCardCount);
