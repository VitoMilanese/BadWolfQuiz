using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BadWolfQuiz.Web.Pages;

public sealed class WordRingsRoomApiModel(IWebHostEnvironment environment) : PageModel
{
    private WordRingsRoomStore Store => WordRingsRoomStore.Get(environment);

    public IActionResult OnPostCreateRoom(
        string? playerName,
        int targetScore,
        bool partialScoreEnabled)
    {
        return Execute(() => new
        {
            success = true,
            connection = Store.CreateRoom(playerName, targetScore, partialScoreEnabled)
        });
    }

    public IActionResult OnPostJoinRoom(string? roomCode, string? playerName)
    {
        return Execute(() => new
        {
            success = true,
            connection = Store.JoinRoom(roomCode, playerName)
        });
    }

    public IActionResult OnPostRoomState(string? roomCode, string? playerToken)
    {
        return Execute(() => new
        {
            success = true,
            state = Store.GetState(roomCode, playerToken)
        });
    }

    public IActionResult OnPostStartRoom(string? roomCode, string? playerToken)
    {
        return Execute(() => new
        {
            success = true,
            state = Store.StartGame(roomCode, playerToken)
        });
    }

    public IActionResult OnPostSubmitRoomWord(
        string? roomCode,
        string? playerToken,
        string? word,
        string? membership,
        double x,
        double y)
    {
        return Execute(() => new
        {
            success = true,
            result = Store.SubmitPlacement(
                roomCode,
                playerToken,
                word,
                membership,
                x,
                y)
        });
    }

    private IActionResult Execute(Func<object> operation)
    {
        try
        {
            return new JsonResult(operation());
        }
        catch (WordRingsRoomException exception)
        {
            return new JsonResult(new
            {
                success = false,
                error = exception.Error.ToString()
            });
        }
    }
}
