namespace BadWolfQuiz.Web.Tests;

public sealed class GameplayRightOverlaySafeGapRegressionTests
{
    [Fact]
    public void Global_gameplay_asset_loads_scrollbar_safe_gap_helper()
    {
        var root = FindRepositoryRoot();
        var layout = Read(
            root,
            "src",
            "BadWolfQuiz.Web",
            "Pages",
            "Shared",
            "_Layout.cshtml");
        var loader = Read(
            root,
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "js",
            "quick-timer-controls.js");

        Assert.Contains(
            "<script src=\"~/js/quick-timer-controls.js\" asp-append-version=\"true\"></script>",
            layout,
            StringComparison.Ordinal);
        Assert.Contains(
            "badWolfGameplayRightOverlaySafeGapLoaderInstalled",
            loader,
            StringComparison.Ordinal);
        Assert.Contains(
            "/js/gameplay-right-overlay-safe-gap.js?v=7",
            loader,
            StringComparison.Ordinal);
        Assert.Contains(
            "script.dataset.gameplayRightOverlaySafeGap = \"\";",
            loader,
            StringComparison.Ordinal);
        Assert.Contains(
            "document.head.appendChild(script);",
            loader,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Safe_gap_tracks_browser_scrollbar_and_persistent_host_navigation()
    {
        var script = ReadHelper();

        Assert.Contains(
            "window.innerWidth - document.documentElement.clientWidth",
            script,
            StringComparison.Ordinal);
        Assert.Contains("const breathingSpace = 8;", script, StringComparison.Ordinal);
        Assert.Contains(
            "--gameplay-right-overlay-safe-gap",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "window.addEventListener(\"resize\", refreshLayout",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "window.addEventListener(\"pageshow\", refreshLayout);",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "window.visualViewport?.addEventListener(",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"badwolf:host-shell-mounted\"",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"badwolf:host-gameplay-updated\"",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "window.requestAnimationFrame",
            script,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Only_requested_right_gameplay_panels_consume_the_safe_gap()
    {
        var script = ReadHelper();

        Assert.Contains(
            ".host-game-board.all-player-question-answering",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            ".current-question-summary .all-player-host-progress",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            ".final-question-host[data-game-status=\"finalwagering\"]",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "right: calc(0.75rem + var(--gameplay-right-overlay-safe-gap, 8px));",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            ".final-question-host[data-game-status=\"finalanswering\"]",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            ".final-question-panel > .final-submission-drawer",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "--final-answering-drawer-right-gap",
            script,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            ".all-player-host-choice-preview",
            script,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            ".board-player-sidebar",
            script,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            ".board-player-list",
            script,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Final_answering_collapses_by_width_without_crossing_the_safe_gap()
    {
        var script = ReadHelper();

        Assert.Contains(
            "const finalAnsweringDrawerClass = \"final-submission-drawer\";",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "const ensureFinalAnsweringDrawer = () =>",
            script,
            StringComparison.Ordinal);
        Assert.Contains("list.before(drawer);", script, StringComparison.Ordinal);
        Assert.Contains("drawer.appendChild(list);", script, StringComparison.Ordinal);
        Assert.Contains("new MutationObserver", script, StringComparison.Ordinal);

        Assert.Contains(
            ".final-question-panel > .final-submission-list {\n        visibility: hidden;",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            ".final-question-panel > .final-submission-drawer > .final-submission-list",
            script,
            StringComparison.Ordinal);
        Assert.Contains("width: 2.75rem;", script, StringComparison.Ordinal);
        Assert.Contains(
            "width: min(24rem, calc(100% - 1rem));",
            script,
            StringComparison.Ordinal);
        Assert.Contains("transform: none;", script, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "transform: translateX(calc(100% - 2.75rem));",
            script,
            StringComparison.Ordinal);
        Assert.Contains("right: 0.75rem;", script, StringComparison.Ordinal);
        Assert.Contains("left: 3.25rem;", script, StringComparison.Ordinal);
        Assert.Contains("padding: 0 1rem 0 0;", script, StringComparison.Ordinal);
        Assert.Contains("overflow-y: auto;", script, StringComparison.Ordinal);
        Assert.Contains("visibility: visible;", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Final_answering_does_not_reserve_a_scrollbar_when_the_list_fits()
    {
        var script = ReadHelper();

        Assert.Contains(
            "overscroll-behavior: contain;\n        scrollbar-gutter: auto;",
            script,
            StringComparison.Ordinal);
        Assert.Equal(
            1,
            script.Split(
                "scrollbar-gutter: stable;",
                StringSplitOptions.None).Length - 1);
    }

    [Fact]
    public void Final_answering_drawer_stays_left_of_question_content_scrollbar()
    {
        var script = ReadHelper();

        Assert.Contains(
            "const finalAnsweringRightGapProperty = \"--final-answering-drawer-right-gap\";",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "const overlayScrollbarReserve = 16;",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "const finalAnsweringContentSelector =",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "\".question-presentation .game-content-blocks\";",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "const applyFinalAnsweringDrawerRightGap = safeGap =>",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "const panelRect = panel.getBoundingClientRect();",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "const contentRect = content.getBoundingClientRect();",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "contentRect.right - overlayScrollbarReserve - breathingSpace",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "drawer.style.setProperty(\n                finalAnsweringRightGapProperty",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "document.querySelector(finalAnsweringDrawerSelector)",
            script,
            StringComparison.Ordinal);
    }

    private static string ReadHelper()
    {
        var root = FindRepositoryRoot();
        return Read(
            root,
            "src",
            "BadWolfQuiz.Web",
            "wwwroot",
            "js",
            "gameplay-right-overlay-safe-gap.js");
    }

    private static string Read(string root, params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { root }.Concat(parts).ToArray()));

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src")) &&
                Directory.Exists(Path.Combine(directory.FullName, "tests")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
