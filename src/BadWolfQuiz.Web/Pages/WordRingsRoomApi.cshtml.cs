using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Globalization;

namespace BadWolfQuiz.Web.Pages;

public sealed class WordRingsRoomApiModel(IWebHostEnvironment environment) : PageModel
{
    private WordRingsRoomStore Store => WordRingsRoomStore.Get(environment);
    private WordRingsRoomHostCoordinator HostCoordinator => WordRingsRoomHostCoordinator.Get(environment);

    public IActionResult OnPostCreateRoom(
        string? playerName,
        int targetScore,
        bool partialScoreEnabled,
        bool hostChoosesRules,
        string? previousRoomCode,
        string? previousPlayerToken) =>
        Execute(() => new
        {
            success = true,
            connection = HostCoordinator.CreateRoom(
                playerName,
                targetScore,
                partialScoreEnabled,
                hostChoosesRules,
                previousRoomCode,
                previousPlayerToken)
        });

    public IActionResult OnPostJoinRoom(string? roomCode, string? playerName) =>
        Execute(() => new
        {
            success = true,
            connection = HostCoordinator.JoinRoom(roomCode, playerName)
        });

    public IActionResult OnPostRoomState(string? roomCode, string? playerToken) =>
        Execute(() => new { success = true, state = HostCoordinator.GetRoomState(roomCode, playerToken) });

    public IActionResult OnPostLeaveRoom(string? roomCode, string? playerToken) =>
        Execute(() =>
        {
            HostCoordinator.LeaveRoom(roomCode, playerToken);
            return new { success = true };
        });

    public IActionResult OnPostRoomHostState(string? roomCode, string? playerToken) =>
        Execute(() => new { success = true, state = HostCoordinator.GetHostState(roomCode, playerToken) });

    public IActionResult OnPostStartRoom(string? roomCode, string? playerToken) =>
        Execute(() => new { success = true, state = HostCoordinator.StartGame(roomCode, playerToken) });

    public IActionResult OnPostSelectRoomRule(string? roomCode, string? playerToken, string? ring, Guid ruleId) =>
        Execute(() => new { success = true, state = HostCoordinator.SelectRule(roomCode, playerToken, ring, ruleId) });

    public IActionResult OnPostRefreshRoomRules(string? roomCode, string? playerToken, string? ring) =>
        Execute(() => new { success = true, state = HostCoordinator.RefreshRules(roomCode, playerToken, ring) });

    public IActionResult OnPostSetRoomTurn(string? roomCode, string? playerToken, Guid playerId) =>
        Execute(() => new { success = true, state = HostCoordinator.SetCurrentPlayer(roomCode, playerToken, playerId) });

    public IActionResult OnPostKickRoomPlayer(string? roomCode, string? playerToken, Guid playerId) =>
        Execute(() => new { success = true, state = HostCoordinator.KickPlayer(roomCode, playerToken, playerId) });

    public IActionResult OnPostSetRoomJoinLock(string? roomCode, string? playerToken, bool locked) =>
        Execute(() => new { success = true, state = HostCoordinator.SetJoinLocked(roomCode, playerToken, locked) });

    public IActionResult OnPostSubmitRoomWord(
        string? roomCode,
        string? playerToken,
        string? word,
        string? membership,
        string? x,
        string? y) =>
        Execute(() => new
        {
            success = true,
            result = HostCoordinator.SubmitPlacement(
                roomCode,
                playerToken,
                word,
                membership,
                ParseCoordinate(x),
                ParseCoordinate(y))
        });

    public IActionResult OnPostMoveRoomPlacement(
        string? roomCode,
        string? playerToken,
        long placementId,
        string? membership,
        string? x,
        string? y) =>
        Execute(() => new
        {
            success = true,
            state = HostCoordinator.MovePlacement(
                roomCode,
                playerToken,
                placementId,
                membership,
                ParseCoordinate(x),
                ParseCoordinate(y))
        });

    public IActionResult OnPostResolveRoomPlacement(
        string? roomCode,
        string? playerToken,
        long placementId) =>
        Execute(() => new
        {
            success = true,
            state = HostCoordinator.ResolvePlacement(roomCode, playerToken, placementId)
        });

    private static double ParseCoordinate(string? value)
    {
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var coordinate) ||
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
            return new JsonResult(new { success = false, error = exception.Error.ToString() });
        }
        catch (InvalidOperationException exception)
        {
            return new JsonResult(new { success = false, error = exception.Message });
        }
    }
}
