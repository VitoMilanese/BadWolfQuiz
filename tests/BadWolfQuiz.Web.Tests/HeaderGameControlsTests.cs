namespace BadWolfQuiz.Web.Tests;

public sealed class HeaderGameControlsTests
{
    [Fact]
    public void Gameplay_controls_are_moved_into_the_header_without_duplication()
    {
        var lobby = File.ReadAllText(FindWebFile(
            "Pages",
            "Admin",
            "Games",
            "Lobby.cshtml"));
        var script = File.ReadAllText(FindWebFile(
            "wwwroot",
            "js",
            "discord-media-mute.js"));

        Assert.Equal(1, CountOccurrences(lobby, "class=\"game-side-controls\""));
        Assert.Contains("const moveGameControlsToHeader = () =>", script);
        Assert.Contains("discordSettings.after(controls);", script);
        Assert.Contains("controls.dataset.headerGameControls = \"\";", script);
    }

    [Fact]
    public void Header_controls_preserve_presentation_visibility_rules()
    {
        var script = File.ReadAllText(FindWebFile(
            "wwwroot",
            "js",
            "discord-media-mute.js"));

        Assert.Contains("host-gameplay-presentation-mode", script);
        Assert.Contains("attributeFilter: [\"class\"]", script);
        Assert.Contains("? \"none\"", script);
        Assert.Contains(": \"flex\";", script);
    }

    [Fact]
    public void Header_controls_are_horizontal_evenly_spaced_and_admission_menu_opens_downward()
    {
        var css = File.ReadAllText(FindWebFile(
            "wwwroot",
            "css",
            "player-admission-menu.css"));

        Assert.Contains(".game-side-controls[data-header-game-controls]", css);
        Assert.Contains("flex-direction: row;", css);
        Assert.Contains("gap: 0.75rem;", css);
        Assert.Contains(
            ".game-side-controls[data-header-game-controls] .player-admission-menu-popover",
            css);
        Assert.Contains("top: calc(100% + 8px);", css);
        Assert.Contains("bottom: auto;", css);
    }

    [Fact]
    public void Header_controls_match_the_microphone_button_style_without_requiring_it()
    {
        var script = File.ReadAllText(FindWebFile(
            "wwwroot",
            "js",
            "discord-media-mute.js"));
        var css = File.ReadAllText(FindWebFile(
            "wwwroot",
            "css",
            "player-admission-menu.css"));

        Assert.Contains("const styleHeaderGameControl = button =>", script);
        Assert.Contains(
            ".querySelectorAll(\".game-side-control, .player-join-lock\")",
            script);
        Assert.Contains("\"button\",", script);
        Assert.Contains("\"button-secondary\",", script);
        Assert.Contains("\"icon-button\",", script);
        Assert.Contains("\"game-header-square-button\");", script);
        Assert.Contains("if (discordSettings)", script);
        Assert.Contains("header.append(controls);", script);
        Assert.Contains(
            ":is(.game-side-control, .player-join-lock)",
            css);
        Assert.Contains("border-radius: 10px;", css);
        Assert.Contains("font-size: 1rem;", css);
        Assert.Contains("background: var(--panel);", css);
    }

    [Fact]
    public void Manual_discord_controls_are_only_available_when_voice_control_is_ready()
    {
        var lobby = File.ReadAllText(FindWebFile(
            "Pages",
            "Admin",
            "Games",
            "Lobby.cshtml"));
        var lobbyModel = File.ReadAllText(FindWebFile(
            "Pages",
            "Admin",
            "Games",
            "Lobby.cshtml.cs"));
        var gateway = File.ReadAllText(FindWebFile(
            "Services",
            "DiscordVoiceGateway.cs"));
        var dialogScript = File.ReadAllText(FindWebFile(
            "wwwroot",
            "js",
            "discord-settings-dialog.js"));
        var muteScript = File.ReadAllText(FindWebFile(
            "wwwroot",
            "js",
            "discord-media-mute.js"));

        Assert.Contains("@if (Model.IsDiscordVoiceReady)", lobby);
        Assert.Contains("IsDiscordVoiceReady = DiscordConnection is not null &&", lobbyModel);
        Assert.Contains("DiscordConnection.VoiceChannelId).IsReady;", lobbyModel);
        Assert.Contains(
            "public bool IsReady => settings.Enabled && client.ConnectionState == ConnectionState.Connected;",
            gateway);
        Assert.Contains("const publishVoiceReadiness = () =>", dialogScript);
        Assert.Contains("frameDocument.querySelector(\"[data-discord-test]\") !== null", dialogScript);
        Assert.Contains("badwolfquiz:discord-voice-ready-changed", muteScript);
        Assert.Contains("button.hidden = !ready;", muteScript);
    }

    [Fact]
    public void Manual_discord_controls_are_hydrated_when_voice_becomes_ready_after_page_load()
    {
        var script = File.ReadAllText(FindWebFile(
            "wwwroot",
            "js",
            "discord-media-mute.js"));

        Assert.Contains("const ensureManualMuteControls = async ready =>", script);
        Assert.Contains("const response = await fetch(getLobbyUrl(), { cache: \"no-store\" });", script);
        Assert.Contains(".game-side-controls [data-discord-mute]", script);
        Assert.Contains("document.importNode(sourceButton, true)", script);
        Assert.Contains("styleHeaderGameControl(button);", script);
        Assert.Contains("controls.insertBefore(button, insertionPoint);", script);
        Assert.Contains("void ensureManualMuteControls(event.detail?.ready === true);", script);
        Assert.Contains("event.target.closest(\"[data-discord-mute]\")", script);
    }

    [Fact]
    public void Discord_request_token_is_refreshed_when_voice_becomes_ready_after_setup()
    {
        var script = File.ReadAllText(FindWebFile(
            "wwwroot",
            "js",
            "discord-media-mute.js"));

        Assert.Contains(
            "let token = document.querySelector('input[name=\"__RequestVerificationToken\"]')?.value;",
            script);
        Assert.Contains("const freshToken = parsed.querySelector(", script);
        Assert.Contains("token = freshToken;", script);
        Assert.Contains("headers: token ? { \"RequestVerificationToken\": token } : {}", script);
        Assert.DoesNotContain(
            "const token = document.querySelector('input[name=\"__RequestVerificationToken\"]')?.value;",
            script);
    }

    [Fact]
    public void Discord_operations_target_lobby_handlers_after_soft_navigation()
    {
        var script = File.ReadAllText(FindWebFile(
            "wwwroot",
            "js",
            "discord-media-mute.js"));

        Assert.Contains("const getLobbyUrl = () =>", script);
        Assert.Contains("const getLobbyHandlerUrl = handler =>", script);
        Assert.Contains("const gamesSegment = \"/Admin/Games/\";", script);
        Assert.Contains("Lobby/${encodeURIComponent(gameId)}", script);
        Assert.Contains("?handler=${encodeURIComponent(handler)}", script);
        Assert.Contains("fetch(getLobbyHandlerUrl(handler)", script);
        Assert.DoesNotContain("fetch(`?handler=${handler}`", script);
    }

    [Fact]
    public void Manual_discord_status_is_auto_dismissed_and_malformed_responses_are_sanitized()
    {
        var script = File.ReadAllText(FindWebFile(
            "wwwroot",
            "js",
            "discord-media-mute.js"));

        Assert.Contains("let statusClearTimer = null;", script);
        Assert.Contains("const setOperationStatus = (message, autoClear = false) =>", script);
        Assert.Contains("}, 4000);", script);
        Assert.Contains("const responseText = await response.text();", script);
        Assert.Contains("result = JSON.parse(responseText);", script);
        Assert.Contains("throw new Error(\"Discord voice operation failed.\");", script);
        Assert.Contains("setOperationStatus(result.message, true);", script);
        Assert.Contains("setOperationStatus(error.message, true);", script);
    }

    private static int CountOccurrences(string value, string fragment)
    {
        var count = 0;
        var offset = 0;
        while ((offset = value.IndexOf(fragment, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += fragment.Length;
        }

        return count;
    }

    private static string FindWebFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidateParts = new[]
            {
                directory.FullName,
                "src",
                "BadWolfQuiz.Web"
            }.Concat(parts).ToArray();
            var candidate = Path.Combine(candidateParts);
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"Could not locate BadWolfQuiz.Web file: {Path.Combine(parts)}");
    }
}
