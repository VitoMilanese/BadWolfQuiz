using System.Collections;
using System.Reflection;

namespace BadWolfQuiz.Web.Services;

public sealed class WordRingsExhaustionWinOverride
{
    private static readonly FieldInfo StoreSyncField = typeof(WordRingsRoomStore)
        .GetField("_sync", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static readonly FieldInfo StoreRoomsField = typeof(WordRingsRoomStore)
        .GetField("_rooms", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private readonly WordRingsRoomStore _store;
    private readonly WordRingsRoomHostCoordinator _host;

    public WordRingsExhaustionWinOverride(IWebHostEnvironment environment)
    {
        _store = WordRingsRoomStore.Get(environment);
        _host = WordRingsRoomHostCoordinator.Get(environment);
    }

    public void ApplyForPlayer(string? roomCode, string? playerToken, Guid playerId)
    {
        var caller = _host.GetRoomState(roomCode, playerToken);
        Apply(caller.RoomCode, playerId);
    }

    public void ApplyForPlacement(string? roomCode, string? playerToken, long placementId)
    {
        var caller = _host.GetRoomState(roomCode, playerToken);
        if (!caller.IsHost)
        {
            throw new WordRingsRoomException(WordRingsRoomError.NotHost);
        }

        Guid? playerId = null;
        lock (StoreSyncField.GetValue(_store)!)
        {
            var room = GetRoom(caller.RoomCode);
            var placement = ((IList)Get(room, "Placements")!).Cast<object>()
                .FirstOrDefault(item => Convert.ToInt64(Get(item, "Id")) == placementId);
            if (placement is not null) playerId = (Guid)Get(placement, "PlayerId")!;
        }
        if (playerId is Guid id) Apply(caller.RoomCode, id);
    }

    private void Apply(string code, Guid playerId)
    {
        lock (StoreSyncField.GetValue(_store)!)
        {
            var room = GetRoom(code);
            var phase = Get(room, "Phase")?.ToString();
            var outcome = Get(room, "Outcome")?.ToString();
            if (!string.Equals(phase, "Playing", StringComparison.Ordinal) &&
                !(string.Equals(phase, "Finished", StringComparison.Ordinal) &&
                  string.Equals(outcome, "Lost", StringComparison.Ordinal)))
            {
                return;
            }

            var player = ((IList)Get(room, "Players")!).Cast<object>()
                .FirstOrDefault(item => (Guid)Get(item, "Id")! == playerId);
            if (player is null || !(bool)Get(player, "IsPlayingParticipant")!) return;
            if (((IList)Get(player, "RemainingWords")!).Count != 0) return;

            SetEnum(room, "Phase", "Finished");
            SetEnum(room, "Outcome", "Won");
            Set(room, "WinnerPlayerId", playerId);
            Set(room, "TurnDeadlineUtc", null);
            Set(room, "PausedTurnSeconds", null);
            Set(room, "Version", Convert.ToInt64(Get(room, "Version")) + 1);
        }
    }

    private object GetRoom(string code)
    {
        var rooms = (IDictionary)StoreRoomsField.GetValue(_store)!;
        if (!rooms.Contains(code)) throw new WordRingsRoomException(WordRingsRoomError.RoomNotFound);
        return rooms[code]!;
    }

    private static object? Get(object instance, string property) =>
        instance.GetType().GetProperty(property, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(instance);

    private static void Set(object instance, string property, object? value) =>
        instance.GetType().GetProperty(property, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.SetValue(instance, value);

    private static void SetEnum(object instance, string property, string value)
    {
        var info = instance.GetType().GetProperty(property, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;
        info.SetValue(instance, Enum.Parse(info.PropertyType, value, ignoreCase: false));
    }
}
