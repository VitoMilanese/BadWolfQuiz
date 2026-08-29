using BadWolfQuiz.Web.Services;

namespace BadWolfQuiz.Web.Tests;

public sealed class MinigameRoomStoreTests
{
    [Fact]
    public void Create_room_assigns_player_one_and_unique_room_code()
    {
        var time = new ManualTimeProvider();
        var store = new MinigameRoomStore(time);

        var first = store.CreateRoom();
        var second = store.CreateRoom();

        Assert.Equal(1, first.PlayerNumber);
        Assert.Equal(1, first.State.PlayerCount);
        Assert.False(first.State.HasOpponent);
        Assert.Equal(6, first.RoomCode.Length);
        Assert.NotEqual(first.RoomCode, second.RoomCode);
        Assert.NotEmpty(first.PlayerToken);
        Assert.Equal(2, store.Count);
    }

    [Fact]
    public void Join_room_allows_exactly_one_second_player()
    {
        var store = new MinigameRoomStore(new ManualTimeProvider());
        var owner = store.CreateRoom();

        var secondPlayer = store.JoinRoom(owner.RoomCode.ToLowerInvariant());

        Assert.Equal(2, secondPlayer.PlayerNumber);
        Assert.Equal(2, secondPlayer.State.PlayerCount);
        Assert.True(secondPlayer.State.HasOpponent);
        Assert.Throws<MinigameRoomException>(() =>
            store.JoinRoom(owner.RoomCode));
    }

    [Fact]
    public void Room_state_requires_the_matching_player_token()
    {
        var store = new MinigameRoomStore(new ManualTimeProvider());
        var owner = store.CreateRoom();
        var secondPlayer = store.JoinRoom(owner.RoomCode);

        Assert.Equal(
            1,
            store.GetState(owner.RoomCode, owner.PlayerToken).PlayerNumber);
        Assert.Equal(
            2,
            store.GetState(owner.RoomCode, secondPlayer.PlayerToken).PlayerNumber);

        var exception = Assert.Throws<MinigameRoomException>(() =>
            store.GetState(owner.RoomCode, "invalid-token"));
        Assert.Equal(MinigameRoomError.InvalidPlayer, exception.Error);
    }

    [Fact]
    public void Room_is_removed_after_one_hour_of_inactivity()
    {
        var time = new ManualTimeProvider();
        var store = new MinigameRoomStore(time);
        var owner = store.CreateRoom();

        time.Advance(TimeSpan.FromMinutes(59));
        Assert.Empty(store.RemoveExpired());

        time.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal([owner.RoomCode], store.RemoveExpired());
        Assert.Equal(0, store.Count);

        var exception = Assert.Throws<MinigameRoomException>(() =>
            store.GetState(owner.RoomCode, owner.PlayerToken));
        Assert.Equal(MinigameRoomError.RoomNotFound, exception.Error);
    }

    [Fact]
    public void Page_refresh_or_interaction_extends_room_lifetime()
    {
        var time = new ManualTimeProvider();
        var store = new MinigameRoomStore(time);
        var owner = store.CreateRoom();

        time.Advance(TimeSpan.FromMinutes(50));
        var refreshed = store.GetState(owner.RoomCode, owner.PlayerToken);
        Assert.Equal(
            time.GetUtcNow() + MinigameRoomStore.InactivityTimeout,
            refreshed.ExpiresAtUtc);

        time.Advance(TimeSpan.FromMinutes(50));
        Assert.Empty(store.RemoveExpired());

        var touched = store.TouchRoom(owner.RoomCode, owner.PlayerToken);
        Assert.Equal(
            time.GetUtcNow() + MinigameRoomStore.InactivityTimeout,
            touched.ExpiresAtUtc);

        time.Advance(TimeSpan.FromMinutes(59));
        Assert.Empty(store.RemoveExpired());
        time.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal([owner.RoomCode], store.RemoveExpired());
    }

    [Fact]
    public void Expired_room_is_reported_when_accessed_before_cleanup_tick()
    {
        var time = new ManualTimeProvider();
        var store = new MinigameRoomStore(time);
        var owner = store.CreateRoom();

        time.Advance(TimeSpan.FromHours(1));

        var exception = Assert.Throws<MinigameRoomException>(() =>
            store.GetState(owner.RoomCode, owner.PlayerToken));
        Assert.Equal(MinigameRoomError.RoomExpired, exception.Error);
        Assert.Equal(0, store.Count);
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow =
            new(2026, 8, 29, 8, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan duration) => _utcNow += duration;
    }
}
