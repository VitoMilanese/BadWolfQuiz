namespace BadWolfQuiz.Web.Tests;

public sealed class HostContextMenuStabilityRegressionTests
{
    [Fact]
    public void Right_click_pointer_sequence_does_not_reach_outside_click_dismissers()
    {
        var script = ReadWebFile(
            "wwwroot",
            "js",
            "host-context-menu-stability.js");

        Assert.Contains("window.addEventListener(\"pointerdown\"", script);
        Assert.Contains("if (event.button === 2)", script);
        Assert.Contains("event.stopPropagation();", script);
        Assert.Contains("#category-context-menu", script);
        Assert.Contains("#question-context-menu", script);
        Assert.Contains("#player-score-context-menu", script);
    }

    [Fact]
    public void Active_menu_visibility_is_enforced_until_explicit_dismissal()
    {
        var script = ReadWebFile(
            "wwwroot",
            "js",
            "host-context-menu-stability.js");

        Assert.Contains("let keepActiveMenuOpen = false;", script);
        Assert.Contains("const ensureActiveMenuVisible = () =>", script);
        Assert.Contains("window.setInterval(", script);
        Assert.Contains("visibilityWatchdogMilliseconds = 25", script);
        Assert.Contains("menu.id === activeMenuId && keepActiveMenuOpen && menu.hidden", script);
        Assert.Contains("attributeFilter: [\"hidden\"]", script);
    }

    [Fact]
    public void Trailing_pointerdown_from_the_opening_gesture_is_ignored()
    {
        var script = ReadWebFile(
            "wwwroot",
            "js",
            "host-context-menu-stability.js");

        Assert.Contains("openingGestureGraceMilliseconds = 500", script);
        Assert.Contains("isOpeningGestureGraceActive", script);
        Assert.Contains("trailing synthetic pointerdown", script);
        Assert.Contains("event.stopPropagation();", script);
    }

    [Fact]
    public void Menu_action_pointerdown_stays_visible_until_the_click_event()
    {
        var script = ReadWebFile(
            "wwwroot",
            "js",
            "host-context-menu-stability.js");

        Assert.Contains("if (target && menu.contains(target))", script);
        Assert.Contains("Keep the menu alive through pointerdown", script);
        Assert.Contains("window.addEventListener(\"click\"", script);
        Assert.Contains("let the existing menu action handler receive this click", script);
        Assert.Contains("releaseActiveMenu();", script);
    }

    [Fact]
    public void Normal_outside_click_or_escape_hides_and_releases_the_active_menu()
    {
        var script = ReadWebFile(
            "wwwroot",
            "js",
            "host-context-menu-stability.js");

        Assert.Contains("const hideAndReleaseActiveMenu = () =>", script);
        Assert.Contains("menu.hidden = true;", script);
        Assert.Contains("if (event.button === 0)", script);
        Assert.Contains("event.key === \"Escape\"", script);
    }

    [Fact]
    public void Blur_or_resize_reasserts_the_active_menu_instead_of_releasing_it()
    {
        var script = ReadWebFile(
            "wwwroot",
            "js",
            "host-context-menu-stability.js");

        Assert.Contains("window.addEventListener(\"blur\"", script);
        Assert.Contains("window.addEventListener(\"resize\"", script);
        Assert.Contains("window.requestAnimationFrame(ensureActiveMenuVisible);", script);
        Assert.DoesNotContain("shouldIgnoreTransientDismiss", script);
    }

    [Fact]
    public void Resolved_question_menu_is_only_stabilized_when_its_controller_is_loaded()
    {
        var script = ReadWebFile(
            "wwwroot",
            "js",
            "host-context-menu-stability.js");

        Assert.Contains(
            ".host-board-question.status-resolved[data-question-resolved]",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "window.badWolfHostQuestionControlsInstalled",
            script,
            StringComparison.Ordinal);
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
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"Could not locate web file: {string.Join('/', parts)}");
    }
}
