using System.Collections;
using System.Reflection;
using BadWolfQuiz.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BadWolfQuiz.Web.Pages;

public sealed class WordRingsRoomTuningApiModel(IWebHostEnvironment environment) : PageModel
{
    private static readonly FieldInfo SyncField = typeof(WordRingsRoomStore)
        .GetField("_sync", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static readonly FieldInfo RoomsField = typeof(WordRingsRoomStore)
        .GetField("_rooms", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private WordRingsRoomStore Store => WordRingsRoomStore.Get(environment);
    private WordRingsRoomHostCoordinator HostCoordinator => WordRingsRoomHostCoordinator.Get(environment);

    public IActionResult OnPostSetTargetScore(string? roomCode, string? playerToken, int targetScore)
    {
        try
        {
            if (targetScore is < 5 or > 20)
            {
                throw new WordRingsRoomException(WordRingsRoomError.InvalidTargetScore);
            }

            var state = HostCoordinator.GetRoomState(roomCode, playerToken);
            if (!state.IsHost)
            {
                throw new WordRingsRoomException(WordRingsRoomError.InvalidPlayer);
            }
            if (!string.Equals(state.Phase, "waiting", StringComparison.Ordinal))
            {
                throw new WordRingsRoomException(WordRingsRoomError.InvalidPhase);
            }

            lock (SyncField.GetValue(Store)!)
            {
                var rooms = (IDictionary)RoomsField.GetValue(Store)!;
                var room = rooms[state.RoomCode]
                    ?? throw new WordRingsRoomException(WordRingsRoomError.RoomNotFound);
                Set(room, "TargetScore", targetScore);
                Set(room, "Version", (long)Get(room, "Version")! + 1);
            }

            return new JsonResult(new
            {
                success = true,
                state = HostCoordinator.GetRoomState(state.RoomCode, playerToken)
            });
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

    private static object? Get(object instance, string property) =>
        instance.GetType().GetProperty(property, BindingFlags.Instance | BindingFlags.Public)!.GetValue(instance);

    private static void Set(object instance, string property, object? value) =>
        instance.GetType().GetProperty(property, BindingFlags.Instance | BindingFlags.Public)!.SetValue(instance, value);
}
