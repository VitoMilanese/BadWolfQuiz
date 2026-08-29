using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.SignalR;

namespace BadWolfQuiz.Web.Hubs;

public sealed class MinigameHub(
    MinigameCardSetStore cardSetStore,
    MinigameRoomStore roomStore) : Hub
{
    // Legacy shared-card endpoints remain available until the room UI is switched
    // over in the next implementation step.
    public MinigameCardSetSnapshot GetState() => cardSetStore.GetCurrent();

    public async Task<MinigameCardSetSnapshot> Regenerate()
    {
        var state = cardSetStore.Regenerate();
        await Clients.All.SendAsync("cardsRegenerated", state);
        return state;
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
            await Clients
                .Group(MinigameRoomStore.GetSignalRGroupName(membership.RoomCode))
                .SendAsync("roomChanged", membership.State.Version);
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

    private Task JoinRoomGroup(string roomCode) =>
        Groups.AddToGroupAsync(
            Context.ConnectionId,
            MinigameRoomStore.GetSignalRGroupName(roomCode));

    private static HubException CreateHubException(
        MinigameRoomException exception) =>
        new($"MINIGAME_ROOM_{exception.Error.ToString().ToUpperInvariant()}");
}
