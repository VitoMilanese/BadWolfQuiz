using BadWolfQuiz.Web.Services;
using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BadWolfQuiz.Web.Pages;

public sealed class WordRingsRoomApiModel(IWebHostEnvironment environment) : PageModel
{
    private WordRingsRoomStore Store => WordRingsRoomStore.Get(environment);

    public IActionResult OnPostCreateRoom(
        string? playerName,
        int targetScore,
        bool partialScoreEnabled,
        string? previousRoomCode,
        string? previousPlayerToken)
    {
        return Execute(() => new
        {
            success = true,
            connection = Store.CreateRoom(
                playerName,
                targetScore,
                partialScoreEnabled,
                previousRoomCode,
                previousPlayerToken)
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
        string? x,
        string? y)
    {
        return Execute(() => new
        {
            success = true,
            result = Store.SubmitPlacement(
                roomCode,
                playerToken,
                word,
                membership,
                ParseCoordinate(x),
                ParseCoordinate(y))
        });
    }

    public IActionResult OnPostMoveRoomPlacement(
        string? roomCode,
        string? playerToken,
        long placementId,
        string? membership,
        string? x,
        string? y)
    {
        return Execute(() => new
        {
            success = true,
            state = Store.MovePlacement(
                roomCode,
                playerToken,
                placementId,
                membership,
                ParseCoordinate(x),
                ParseCoordinate(y))
        });
    }

    private static double ParseCoordinate(string? value)
    {
        if (!double.TryParse(
                value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var coordinate) ||
            !double.IsFinite(coordinate))
        {
            throw new WordRingsRoomException(WordRingsRoomError.InvalidPlacement);
        }

        return coordinate;
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
