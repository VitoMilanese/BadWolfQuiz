using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.SignalR;

namespace BadWolfQuiz.Web.Hubs;

public sealed class MinigameHub(MinigameCardSetStore cardSetStore) : Hub
{
    public MinigameCardSetSnapshot GetState() => cardSetStore.GetCurrent();

    public async Task<MinigameCardSetSnapshot> Regenerate()
    {
        var state = cardSetStore.Regenerate();
        await Clients.All.SendAsync("cardsRegenerated", state);
        return state;
    }
}
