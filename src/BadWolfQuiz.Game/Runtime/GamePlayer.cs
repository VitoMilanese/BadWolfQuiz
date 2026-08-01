namespace BadWolfQuiz.Game.Runtime;

public sealed class GamePlayer
{
    internal GamePlayer(GamePlayerId id, string name, DateTimeOffset joinedAtUtc)
    {
        Id = id;
        Name = name;
        JoinedAtUtc = joinedAtUtc;
    }

    public GamePlayerId Id { get; }

    public string Name { get; }

    public int Score { get; private set; }

    public string? AvatarId { get; private set; }

    public string? UploadedImageDataUrl { get; private set; }

    public bool UsesUploadedImage { get; private set; }

    public DateTimeOffset JoinedAtUtc { get; }

    internal void ApplyScore(int points)
    {
        Score = checked(Score + points);
    }

    public void SetAvatar(string avatarId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(avatarId);
        AvatarId = avatarId;
        UsesUploadedImage = false;
    }

    public void SetUploadedImage(string imageDataUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imageDataUrl);
        UploadedImageDataUrl = imageDataUrl;
        UsesUploadedImage = true;
    }
}
