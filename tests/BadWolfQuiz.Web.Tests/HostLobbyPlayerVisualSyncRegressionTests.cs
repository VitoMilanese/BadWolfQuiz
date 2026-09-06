namespace BadWolfQuiz.Web.Tests;

public sealed class HostLobbyPlayerVisualSyncRegressionTests
{
    [Fact]
    public void Host_lobby_rerenders_when_player_switches_between_avatar_and_uploaded_image()
    {
        var hub = File.ReadAllText(FindWebFile("Hubs", "GameHub.cs"));
        var lobby = File.ReadAllText(FindWebFile(
            "Pages",
            "Admin",
            "Games",
            "Lobby.cshtml"));
        var helper = File.ReadAllText(FindWebFile(
            "TagHelpers",
            "GameplayPolishAssetsTagHelper.cs"));
        var adapter = File.ReadAllText(FindWebFile(
            "wwwroot",
            "js",
            "host-lobby-player-visual-contract.js"));

        Assert.Contains("imageDataUrl =", hub, StringComparison.Ordinal);
        Assert.Contains(
            "usesUploadedImage: player.usesUploadedImage",
            lobby,
            StringComparison.Ordinal);
        Assert.Contains(
            "player.uploadedImageDataUrl",
            lobby,
            StringComparison.Ordinal);

        Assert.Contains(
            "data-game-code,data-game-status,data-remove-player-label",
            helper,
            StringComparison.Ordinal);
        Assert.Contains(
            "/js/host-lobby-player-visual-contract.js?v=1",
            helper,
            StringComparison.Ordinal);

        Assert.Contains("PlayersChanged", adapter, StringComparison.Ordinal);
        Assert.Contains("player?.imageDataUrl", adapter, StringComparison.Ordinal);
        Assert.Contains(
            "player.usesUploadedImage = Boolean(imageDataUrl);",
            adapter,
            StringComparison.Ordinal);
        Assert.Contains(
            "player.uploadedImageDataUrl = imageDataUrl;",
            adapter,
            StringComparison.Ordinal);
        Assert.Contains("DOMContentLoaded", adapter, StringComparison.Ordinal);
    }

    private static string FindWebFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                new[]
                {
                    directory.FullName,
                    "src",
                    "BadWolfQuiz.Web"
                }.Concat(parts).ToArray());

            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"Could not locate BadWolfQuiz.Web file: {string.Join('/', parts)}");
    }
}
