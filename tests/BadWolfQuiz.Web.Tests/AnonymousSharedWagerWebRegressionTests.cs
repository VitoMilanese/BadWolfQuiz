namespace BadWolfQuiz.Web.Tests;

public sealed class AnonymousSharedWagerWebRegressionTests
{
    [Fact]
    public void Player_surface_suppresses_buzzer_for_entire_shared_wager_lifetime()
    {
        var source = ReadWebFile("wwwroot", "js", "anonymous-shared-wager-player.js");

        Assert.Contains("if (!status.active)", source);
        Assert.Contains("setBuzzerSuppressed(true);", source);
        Assert.Contains("style.setProperty('display', 'none', 'important')", source);
        Assert.Contains("status.phase !== 'collecting'", source);
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
    public void Player_payload_returns_only_own_submission_share_and_selected_percentage()
    {
        var source = ReadWebFile("Pages", "AnonymousSharedWager.cshtml.cs");
        var playerStart = source.IndexOf("private JsonResult PlayerStatus", StringComparison.Ordinal);
        var playerEnd = source.IndexOf("private (GameSessionRegistration", playerStart, StringComparison.Ordinal);
        var playerStatus = source[playerStart..playerEnd];

        Assert.Contains("submitted", playerStatus);
        Assert.Contains("maximumShare", playerStatus);
        Assert.Contains("selectedPercentage = selectedChoice?.Percentage", playerStatus);
        Assert.Contains("state.Choices.SingleOrDefault", playerStatus);
        Assert.DoesNotContain("participants =", playerStatus);
        Assert.DoesNotContain("Choices =", playerStatus);
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
    public void Host_surface_uses_server_slots_and_syncs_full_viewport_layout_state()
    {
        var host = ReadWebFile("wwwroot", "js", "anonymous-shared-wager-host.js");
        var lobby = ReadWebFile("Pages", "Admin", "Games", "Lobby.cshtml");
        var css = ReadWebFile("wwwroot", "css", "site.css");

        Assert.Contains("BadWolfAnonymousSharedWagerHostStarted", host);
        Assert.Contains("[data-anonymous-shared-wager-host-panel]", host);
        Assert.DoesNotContain("document.createElement('section')", host);
        Assert.Contains("data-anonymous-shared-wager-host-panel", lobby);
        Assert.Contains("anonymous-shared-wager-active", lobby);
        Assert.Contains("anonymous-shared-wager-mode", lobby);
        Assert.Contains("currentHostBoard.classList.toggle(\n                    \"anonymous-shared-wager-active\"", lobby);
        Assert.Contains(".host-game-board.anonymous-shared-wager-active", css);
        Assert.Contains("> [data-host-gameplay-view]", css);
    }

    [Fact]
    public void Server_does_not_render_normal_wager_or_judge_controls_for_shared_wager()
    {
        var lobby = ReadWebFile("Pages", "Admin", "Games", "Lobby.cshtml");

        Assert.Contains("!BadWolfQuiz.Game.Definitions.QuestionWagerModes.IsAnonymousShared", lobby);
        Assert.Contains("Model.CurrentQuestion.IsAllPlayerQuestion ||", lobby);
        Assert.Contains("isAnonymousSharedWager", lobby);
    }

    [Fact]
    public void Round_random_wager_count_fields_keep_stable_positions_and_become_read_only()
    {
        var editor = ReadWebFile("Pages", "Admin", "Quizzes", "Editor.cshtml");
        var css = ReadWebFile("wwwroot", "css", "site.css");

        Assert.Contains("setRandomWagerSettingEnabled", editor);
        Assert.Contains("input.readOnly = !enabled", editor);
        Assert.Contains("randomWagerCheckbox?.checked ?? false", editor);
        Assert.Contains("randomAnonymousSharedWagerCheckbox?.checked ?? false", editor);
        Assert.DoesNotContain("setRandomWagerSettingVisible", editor);
        Assert.Contains("#random-anonymous-shared-wager-count-setting", css);
        Assert.Contains(".is-disabled", css);
    }

    [Fact]
    public void Player_percentage_buttons_fill_width_and_show_the_selected_value()
    {
        var player = ReadWebFile("wwwroot", "js", "anonymous-shared-wager-player.js");
        var css = ReadWebFile("wwwroot", "css", "site.css");

        Assert.Contains("aria-pressed", player);
        Assert.Contains("selection.textContent = `Обрано: ${value}%`", player);
        Assert.Contains("Ваш внесок прийнято: ${status.selectedPercentage}%", player);
        Assert.Contains("grid-template-columns: repeat(5, minmax(0, 1fr))", css);
        Assert.Contains(".anonymous-shared-wager-choices .button.is-selected", css);
        Assert.Contains(".anonymous-shared-wager-selection", css);
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
