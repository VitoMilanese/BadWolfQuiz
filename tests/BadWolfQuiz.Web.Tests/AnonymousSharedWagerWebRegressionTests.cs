namespace BadWolfQuiz.Web.Tests;

public sealed class AnonymousSharedWagerWebRegressionTests
{
    [Fact]
    public void Player_surface_suppresses_buzzer_for_entire_collection_phase()
    {
        var source = ReadWebFile("wwwroot", "js", "anonymous-shared-wager-player.js");

        Assert.Contains("status.phase !== 'collecting'", source);
        Assert.Contains("setBuzzerSuppressed(true);", source);
        Assert.Contains("buzzerPanel.hidden = suppressed", source);
        Assert.Contains("[0, 25, 50, 75, 100]", source);
        Assert.Contains("maximumShare", source);
    }

    [Fact]
    public void Host_payload_exposes_submission_status_but_not_private_choices()
    {
        var source = ReadWebFile("Pages", "AnonymousSharedWager.cshtml.cs");
        var hostStart = source.IndexOf("public IActionResult OnGetHostStatus", StringComparison.Ordinal);
        var hostEnd = source.IndexOf("public IActionResult OnPostForce", hostStart, StringComparison.Ordinal);
        var hostStatus = source[hostStart..hostEnd];

        Assert.Contains("submitted = state.HasSubmitted(playerId)", hostStatus);
        Assert.Contains("combinedWager = state.IsComplete", hostStatus);
        Assert.DoesNotContain("Percentage", hostStatus);
        Assert.DoesNotContain("Amount", hostStatus);
        Assert.DoesNotContain("Choices", hostStatus);
    }

    [Fact]
    public void Player_payload_returns_only_own_submission_and_share_information()
    {
        var source = ReadWebFile("Pages", "AnonymousSharedWager.cshtml.cs");
        var playerStart = source.IndexOf("private JsonResult PlayerStatus", StringComparison.Ordinal);
        var playerEnd = source.IndexOf("private (GameSessionRegistration", playerStart, StringComparison.Ordinal);
        var playerStatus = source[playerStart..playerEnd];

        Assert.Contains("submitted", playerStatus);
        Assert.Contains("maximumShare", playerStatus);
        Assert.DoesNotContain("participants =", playerStatus);
        Assert.DoesNotContain("Choices", playerStatus);
        Assert.DoesNotContain("Percentage", playerStatus);
    }

    [Fact]
    public void Host_surface_uses_explicit_afk_full_stake_and_zero_sum_settlement_endpoint()
    {
        var host = ReadWebFile("wwwroot", "js", "anonymous-shared-wager-host.js");
        var page = ReadWebFile("Pages", "AnonymousSharedWager.cshtml.cs");

        Assert.Contains("AFK → 100%", host);
        Assert.Contains("api('Force', 'POST'", host);
        Assert.Contains("api('Settle', 'POST'", host);
        Assert.Contains("AnonymousSharedWagerWebStore.ForceFullStake", page);
        Assert.Contains("AnonymousSharedWagerWebStore.Settle", page);
    }

    [Fact]
    public void Gameplay_assets_are_injected_only_on_player_and_host_gameplay_roots()
    {
        var source = ReadWebFile("TagHelpers", "AnonymousSharedWagerAssetsTagHelper.cs");

        Assert.Contains("player-lobby", source);
        Assert.Contains("host-game-board", source);
        Assert.Contains("anonymous-shared-wager-player.js", source);
        Assert.Contains("anonymous-shared-wager-host.js", source);
    }

    private static string ReadWebFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                new[] { directory.FullName, "src", "BadWolfQuiz.Web" }
                    .Concat(parts)
                    .ToArray());
            if (File.Exists(candidate)) return File.ReadAllText(candidate);
            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not locate {Path.Combine(parts)}");
    }
}
