namespace BadWolfQuiz.Web.Tests;

public sealed class PlayerBuzzerTouchRegressionTests
{
    [Fact]
    public void Player_buzzer_blocks_horizontal_touch_panning()
    {
        var root = FindRepositoryRoot();
        var css = File.ReadAllText(Path.Combine(root, "src", "BadWolfQuiz.Web", "wwwroot", "css", "player-admission-menu.css"));

        Assert.Contains("body:has(.player-buzzer-panel)", css);
        Assert.DoesNotContain(":has(.player-lobby:has(.player-buzzer-panel))", css);
        Assert.Contains("overscroll-behavior-x: none;", css);
        Assert.Contains("touch-action: manipulation;", css);
        Assert.Contains(".player-lobby:has(.player-buzzer-panel) .player-buzzer", css);
        Assert.Contains("touch-action: none;", css);
        Assert.Contains("-webkit-touch-callout: none;", css);
    }

    [Fact]
    public void Gameplay_blocks_double_tap_zoom_without_disabling_pinch_zoom()
    {
        var root = FindRepositoryRoot();
        var css = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "css",
            "busy-indicators.css"));
        var layout = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "Shared",
            "_Layout.cshtml"));

        Assert.Contains("body.gameplay-layout {", css, StringComparison.Ordinal);
        Assert.Contains("touch-action: manipulation;", css, StringComparison.Ordinal);
        Assert.Contains(
            "content=\"width=device-width, initial-scale=1.0\"",
            layout,
            StringComparison.Ordinal);
        Assert.DoesNotContain("user-scalable=no", layout, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("maximum-scale=1", layout, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Player_buzzer_uses_available_mobile_panel_space()
    {
        var root = FindRepositoryRoot();
        var css = File.ReadAllText(Path.Combine(root, "src", "BadWolfQuiz.Web", "wwwroot", "css", "player-admission-menu.css"));

        Assert.Contains("body:has(.player-buzzer-panel) > .page-shell", css);
        Assert.Contains("position: fixed;", css);
        Assert.Contains("height: 100dvh;", css);
        Assert.Contains("flex: 1 1 auto;", css);
        Assert.DoesNotContain("flex: 1 1 0;", css);
        Assert.DoesNotMatch(@"(?<![\w-])height\s*:\s*0\s*;", css);
        Assert.Contains("aspect-ratio: auto;", css);
        Assert.Contains("border-radius: clamp(28px, 9vw, 64px);", css);
    }

    [Fact]
    public void Player_buzzer_page_text_is_not_selectable()
    {
        var root = FindRepositoryRoot();
        var css = File.ReadAllText(Path.Combine(root, "src", "BadWolfQuiz.Web", "wwwroot", "css", "player-admission-menu.css"));

        Assert.Contains(".player-lobby:has(.player-buzzer-panel), .player-lobby:has(.player-buzzer-panel) *", css);
        Assert.Contains("user-select: none;", css);
        Assert.Contains("-webkit-user-select: none;", css);
    }

    [Fact]
    public void Player_buzzer_lobby_allows_vertical_scrolling_for_expanded_settings()
    {
        var root = FindRepositoryRoot();
        var css = File.ReadAllText(Path.Combine(root, "src", "BadWolfQuiz.Web", "wwwroot", "css", "player-admission-menu.css"));

        Assert.Contains("overflow-y: auto;", css);
        Assert.Contains("overscroll-behavior-y: contain;", css);
        Assert.Contains("touch-action: pan-y;", css);
        Assert.Contains("-webkit-overflow-scrolling: touch;", css);
        Assert.Contains(".player-lobby:has(.player-buzzer-panel) .player-buzzer { touch-action: none;", css);
    }

    [Fact]
    public void Player_buzzer_fires_on_primary_pointer_down()
    {
        var root = FindRepositoryRoot();
        var script = File.ReadAllText(Path.Combine(root, "src", "BadWolfQuiz.Web", "wwwroot", "js", "player-admission-menu.js"));

        Assert.Contains("document.getElementById(\"player-buzzer\")", script);
        Assert.Contains("buzzerButton.addEventListener(\"pointerdown\"", script);
        Assert.Contains("event.pointerType !== \"mouse\" || event.button === 0", script);
        Assert.Contains("event.isPrimary", script);
        Assert.Contains("event.preventDefault();", script);
        Assert.Contains("buzzerButton.click();", script);
        Assert.Contains("{ passive: false }", script);
    }

    [Fact]
    public void Player_skip_is_a_proposal_and_does_not_disable_buzzer_eligibility()
    {
        var root = FindRepositoryRoot();
        var page = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "Player",
            "Lobby.cshtml"));
        var hub = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Hubs",
            "GameHub.cs"));
        var styles = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "css",
            "player-regular-gameplay-safe.css"));
        var ukrainian = File.ReadAllText(Path.Combine(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Resources",
            "Localization",
            "SharedResource.uk.resx"));

        Assert.Contains("id=\"player-skip-question\"", page);
        Assert.Contains("@Localizer[\"PlayerGame_SkipQuestion\"]", page);
        var playerNameIndex = page.IndexOf(
            "class=\"player-name-line\"",
            StringComparison.Ordinal);
        var skipControlsIndex = page.IndexOf(
            "id=\"player-skip-controls\"",
            StringComparison.Ordinal);
        var skipButtonIndex = page.IndexOf(
            "id=\"player-skip-question\"",
            StringComparison.Ordinal);
        var moveButtonIndex = page.IndexOf(
            "id=\"player-skip-position-toggle\"",
            StringComparison.Ordinal);
        var actionMenuIndex = page.IndexOf(
            "id=\"player-lobby-action-menu\"",
            StringComparison.Ordinal);
        var buzzerPanelIndex = page.IndexOf(
            "class=\"player-buzzer-panel\"",
            StringComparison.Ordinal);
        Assert.True(playerNameIndex >= 0);
        Assert.True(skipControlsIndex > playerNameIndex);
        Assert.True(skipButtonIndex > skipControlsIndex);
        Assert.True(moveButtonIndex > skipButtonIndex);
        Assert.True(actionMenuIndex > moveButtonIndex);
        Assert.True(buzzerPanelIndex > moveButtonIndex);
        Assert.DoesNotContain("data-question-skipped-label", page);
        Assert.Contains("update.canProposeSkip === true", page);
        Assert.Contains("update.skipProposalPlayerIds?.includes(playerId)", page);
        Assert.Contains("const canBuzz = isOpen && !hasAlreadyAnswered;", page);
        Assert.Contains("!hasProposedSkip", page);
        Assert.Contains(
            "connection.invoke(\"ProposeQuestionSkip\", sourceQuestionId)",
            page);
        Assert.Contains("skipQuestionControls.hidden = !canProposeSkip", page);
        Assert.Contains("skipQuestionControls.hidden = true", page);
        Assert.Contains("id=\"player-skip-position-toggle\"", page);
        Assert.Contains("data-player-skip-position-icon", page);
        Assert.Contains("moveToBottom ? \"↑\" : \"↓\"", page);
        Assert.Contains("buzzerPanel.append(skipQuestionControls)", page);
        Assert.Contains(
            "playerNameLine.insertAdjacentElement(",
            page);
        Assert.Contains("skip-controls-position", page);
        Assert.Contains("localStorage.setItem(", page);
        Assert.Contains("QuestionSkipProposalRejected", page);

        Assert.Contains(
            "public async Task ProposeQuestionSkip(int sourceQuestionId)",
            hub);
        Assert.Contains("skipProposalPlayerIds", hub);
        Assert.Contains("ineligiblePlayerIds", hub);
        Assert.Contains("canProposeSkip", hub);
        Assert.DoesNotContain(".Concat(skipProposalPlayerIds)", hub);

        Assert.Contains(".player-skip-controls[hidden]", styles);
        Assert.Contains("display: none !important;", styles);
        Assert.Contains(".player-skip-controls:not([hidden])", styles);
        Assert.Contains(".player-skip-position-toggle", styles);
        Assert.Contains("width: 44px;", styles);
        Assert.Contains("height: 44px;", styles);
        Assert.Contains(
            "@media (max-width: 900px), (hover: none) and (pointer: coarse)",
            styles);
        Assert.Contains("width: 100% !important;", styles);
        Assert.Contains("justify-self: stretch;", styles);
        Assert.Contains("flex: 1 1 0;", styles);
        Assert.Contains("<value>Пропустити</value>", ukrainian);
        Assert.Contains("<value>Пропонує пропустити питання</value>", ukrainian);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src")) && Directory.Exists(Path.Combine(directory.FullName, "tests")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
