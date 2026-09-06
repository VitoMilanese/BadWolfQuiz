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
    private MinigameCatalogStore Catalog =>
        new(dbFactory, options.Value.CardCount);

    private MinigameQuestionAvailabilityStore QuestionAvailability =>
        new(dbFactory);

    private MinigameHintService Hints =>
        new(dbFactory, options.Value.CardCount, roomStore);

    private MinigameSoloAiService SoloAi =>
        new(dbFactory, options.Value.CardCount, roomStore);

    public async Task<MinigameCardCatalogSnapshot> GetCatalog()
    {
        var counts = await Catalog.GetCountsAsync(Context.ConnectionAborted);
        var enabledQuestionCount = await QuestionAvailability.GetEnabledQuestionCountAsync(
            Context.ConnectionAborted);
        var maximum = counts.GameCount;
        var defaultCount = maximum >= MinigameRoomStore.MinimumGameCardCount
            ? Math.Clamp(
                Catalog.DefaultCardCount,
                MinigameRoomStore.MinimumGameCardCount,
                maximum)
            : 0;
        return new MinigameCardCatalogSnapshot(
            MinigameRoomStore.MinimumGameCardCount,
            maximum,
            defaultCount,
            enabledQuestionCount >= MinigameQuestionStore.MinimumQuestionCount,
            enabledQuestionCount);
    }

    public Task<MinigameSoloAiAvailabilitySnapshot> GetSoloAiAvailability() =>
        SoloAi.GetAvailabilityAsync(Context.ConnectionAborted);

    public MinigameSoloAiStatusSnapshot GetSoloAiStatus(
        string roomCode,
        string playerToken)
    {
        try
        {
            return SoloAi.GetStatus(roomCode, playerToken);
        }
        catch (MinigameRoomException exception)
        {
            throw CreateHubException(exception);
        }
    }

    public async Task<MinigameRoomConnection> CreateRoom(
        MinigameThemeSnapshot? theme = null)
    {
        var membership = roomStore.CreateRoom(theme);
        Hints.SetEnabled(membership.RoomCode, enabled: false);
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

    public Task<MinigameRoomSnapshot> StartNewGame(
        string roomCode,
        string playerToken,
        int cardCount,
        bool questionCardsEnabled = false) =>
        StartNewGameCore(
            roomCode,
            playerToken,
            cardCount,
            questionCardsEnabled,
            hintsEnabled: false,
            soloAi: false);

    public Task<MinigameRoomSnapshot> StartNewGameWithHints(
        string roomCode,
        string playerToken,
        int cardCount,
        bool questionCardsEnabled,
        bool hintsEnabled) =>
        StartNewGameCore(
            roomCode,
            playerToken,
            cardCount,
            questionCardsEnabled,
            hintsEnabled,
            soloAi: false);

    public Task<MinigameRoomSnapshot> StartNewSoloGame(
        string roomCode,
        string playerToken,
        int cardCount,
        bool hintsEnabled) =>
        StartNewGameCore(
            roomCode,
            playerToken,
            cardCount,
            questionCardsEnabled: true,
            hintsEnabled,
            soloAi: true);

    public async Task<MinigameRoomSnapshot> RestartGame(
        string roomCode,
        string playerToken)
    {
        return await MutateRoom(
            roomCode,
            playerToken,
            () => roomStore.RestartGame(roomCode, playerToken));
    }

    public async Task<MinigameRoomSnapshot> ToggleExclusion(
        string roomCode,
        string playerToken,
        string fileName)
    {
        return await MutateRoom(
            roomCode,
            playerToken,
            () => roomStore.ToggleExclusion(roomCode, playerToken, fileName));
    }

    public async Task<MinigameRoomSnapshot> SelectQuestion(
        string roomCode,
        string playerToken,
        int optionIndex)
    {
        return await MutateRoom(
            roomCode,
            playerToken,
            () => roomStore.SelectQuestion(roomCode, playerToken, optionIndex));
    }

    public async Task<MinigameRoomSnapshot> SubmitQuestionResponse(
        string roomCode,
        string playerToken,
        bool answerYes)
    {
        return await MutateRoom(
            roomCode,
            playerToken,
            () => roomStore.SubmitQuestionResponse(roomCode, playerToken, answerYes));
    }

    public async Task<MinigameRoomSnapshot> EndTurn(
        string roomCode,
        string playerToken)
    {
        return await MutateRoom(
            roomCode,
            playerToken,
            () => roomStore.EndTurn(roomCode, playerToken));
    }

    public async Task<MinigameRoomSnapshot> ExpireTurn(
        string roomCode,
        string playerToken)
    {
        return await MutateRoom(
            roomCode,
            playerToken,
            () => roomStore.ExpireTurn(roomCode, playerToken));
    }

    public async Task<MinigameRoomSnapshot> SubmitGuess(
        string roomCode,
        string playerToken,
        string fileName)
    {
        return await MutateRoom(
            roomCode,
            playerToken,
            () => roomStore.SubmitGuess(roomCode, playerToken, fileName));
    }

    public bool GetHintsEnabled(
        string roomCode,
        string playerToken)
    {
        try
        {
            return Hints.GetHintsEnabled(roomCode, playerToken);
        }
        catch (MinigameRoomException exception)
        {
            throw CreateHubException(exception);
        }
    }

    public async Task<MinigameCardHintSnapshot> GetCardHints(
        string roomCode,
        string playerToken,
        string gameKey)
    {
        try
        {
            return await Hints.GetCardHintsAsync(
                roomCode,
                playerToken,
                gameKey,
                Context.ConnectionAborted);
        }
        catch (MinigameRoomException exception)
        {
            throw CreateHubException(exception);
        }
    }

    public async Task<MinigameCardHintSearchSnapshot> SearchCardHints(
        string roomCode,
        string playerToken,
        string gameKey,
        string query,
        int page = 1)
    {
        try
        {
            return await Hints.SearchCardHintsAsync(
                roomCode,
                playerToken,
                gameKey,
                query,
                page,
                Context.ConnectionAborted);
        }
        catch (MinigameRoomException exception)
        {
            throw CreateHubException(exception);
        }
    }

    public async Task<MinigameQuestionResponseHintSnapshot> GetQuestionResponseHint(
        string roomCode,
        string playerToken)
    {
        try
        {
            return await Hints.GetQuestionResponseHintAsync(
                roomCode,
                playerToken,
                Context.ConnectionAborted);
        }
        catch (MinigameRoomException exception)
        {
            throw CreateHubException(exception);
        }
    }

    private async Task<MinigameRoomSnapshot> StartNewGameCore(
        string roomCode,
        string playerToken,
        int cardCount,
        bool questionCardsEnabled,
        bool hintsEnabled,
        bool soloAi)
    {
        try
        {
            var questions = questionCardsEnabled
                ? await QuestionAvailability.GetEnabledQuestionsAsync(Context.ConnectionAborted)
                : [];
            if (questionCardsEnabled &&
                questions.Count < MinigameQuestionStore.MinimumQuestionCount)
            {
                throw new MinigameRoomException(MinigameRoomError.QuestionsUnavailable);
            }

            MinigameRoomSnapshot state;
            if (soloAi)
            {
                state = await SoloAi.StartSoloGameAsync(
                    roomCode,
                    playerToken,
                    cardCount,
                    questions,
                    Context.ConnectionAborted);
            }
            else
            {
                var counts = await Catalog.GetCountsAsync(Context.ConnectionAborted);
                if (cardCount < MinigameRoomStore.MinimumGameCardCount ||
                    cardCount > counts.GameCount)
                {
                    throw new MinigameRoomException(MinigameRoomError.InvalidCardCount);
                }

                var cards = await Catalog.GenerateCardsAsync(
                    cardCount,
                    Context.ConnectionAborted);
                SoloAi.DisableSoloGame(roomCode, playerToken);
                state = roomStore.StartNewGame(
                    roomCode,
                    playerToken,
                    cards,
                    questionCardsEnabled,
                    questions);
            }

            Hints.SetEnabled(state.RoomCode, hintsEnabled);
            await BroadcastRoomChanged(state);
            return state;
        }
        catch (MinigameRoomException exception)
        {
            throw CreateHubException(exception);
        }
    }

    private async Task<MinigameRoomSnapshot> MutateRoom(
        string roomCode,
        string playerToken,
        Func<MinigameRoomSnapshot> mutation)
    {
        try
        {
            var state = mutation();
            if (SoloAi.IsSoloGame(roomCode, playerToken))
            {
                state = await SoloAi.AdvanceAsync(
                    roomCode,
                    playerToken,
                    Context.ConnectionAborted);
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
    int QuestionCount);
