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

    public string? WebcamUrl { get; private set; }

    public bool AvatarFrameEnabled { get; private set; }

    public string? AvatarFrameId { get; private set; }

    public string? AvatarFrameAuthorizedHostId { get; private set; }

    public DateTimeOffset JoinedAtUtc { get; }

    internal void ApplyScore(int points)
    {
        Score = checked(Score + points);
    }

    internal GamePlayerState CaptureState() => new(
        Id,
        Name,
        Score,
        AvatarId,
        UploadedImageDataUrl,
        UsesUploadedImage,
        JoinedAtUtc,
        WebcamUrl);

    internal static GamePlayer Restore(GamePlayerState state)
    {
        var player = new GamePlayer(state.Id, state.Name, state.JoinedAtUtc)
        {
            Score = state.Score,
            AvatarId = state.AvatarId,
            UploadedImageDataUrl = state.UploadedImageDataUrl,
            UsesUploadedImage = state.UsesUploadedImage,
            WebcamUrl = state.WebcamUrl
        };
        return player;
    }

    public void SetAvatar(string avatarId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(avatarId);
        AvatarId = avatarId;
        UsesUploadedImage = false;
        WebcamUrl = null;
    }

    public void SetUploadedImage(string imageDataUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imageDataUrl);
        UploadedImageDataUrl = imageDataUrl;
        UsesUploadedImage = true;
        WebcamUrl = null;
    }

    public void SetWebcamUrl(string webcamUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(webcamUrl);
        WebcamUrl = webcamUrl;
        UsesUploadedImage = false;
    }

    public void SetAvatarFrame(
        bool enabled,
        string frameId,
        string? authorizedHostId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(frameId);
        AvatarFrameEnabled = enabled;
        AvatarFrameId = frameId;
        AvatarFrameAuthorizedHostId = string.IsNullOrWhiteSpace(authorizedHostId)
            ? null
            : authorizedHostId.Trim();
    }

    public void ClearWebcamUrl() => WebcamUrl = null;
}
