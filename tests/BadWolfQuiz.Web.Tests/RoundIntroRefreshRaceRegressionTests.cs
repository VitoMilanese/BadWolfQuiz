namespace BadWolfQuiz.Web.Tests;

public sealed class RoundIntroRefreshRaceRegressionTests
{
    [Fact]
    public void Shared_game_bootstrap_loads_the_round_intro_refresh_guard()
    {
        var loader = ReadWebFile("wwwroot", "js", "header-side-menu.js");

        Assert.Contains(
            "/js/round-intro-refresh-guard.js?v=6",
            loader,
            StringComparison.Ordinal);
        Assert.Contains(
            "badWolfRoundIntroRefreshGuardLoaderInstalled",
            loader,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Summary_to_intro_transition_is_locked_before_intro_mounts()
    {
        var guard = ReadWebFile(
            "wwwroot",
            "js",
            "round-intro-refresh-guard.js");

        Assert.Contains("const summarySelector =", guard);
        Assert.Contains("const introTransitionHandlers = new Set([", guard);
        Assert.Contains("\"AdvanceRound\"", guard);
        Assert.Contains("\"PreviousRound\"", guard);
        Assert.Contains("\"ReturnToUnfinishedRound\"", guard);
        Assert.Contains("if (form.closest(summarySelector))", guard);
        Assert.Contains("return true;", guard);
        Assert.Contains("lockRoundTransition();", guard);
        Assert.Contains("transitionLocked = true;", guard);
        Assert.Contains("cancelPendingHostRefresh();", guard);
    }

    [Fact]
    public void Pre_summary_confirmation_keeps_intro_lock_open_when_players_exist()
    {
        var guard = ReadWebFile(
            "wwwroot",
            "js",
            "round-intro-refresh-guard.js");

        Assert.Contains("const hasPlayerCards = () =>", guard);
        Assert.Contains("if (target.closest(\"[data-confirm-force-advance-round]\"))", guard);
        Assert.Contains("if (hasPlayerCards())", guard);
        Assert.Contains("void probeRoundTransitionSummary();", guard);
        Assert.Contains("return !hasPlayerCards();", guard);
    }

    [Fact]
    public void Round_transition_summary_uses_a_lightweight_polling_endpoint()
    {
        var guard = ReadWebFile(
            "wwwroot",
            "js",
            "round-intro-refresh-guard.js");
        var page = ReadWebFile(
            "Pages",
            "Admin",
            "Games",
            "RoundTransitionSummary.cshtml");
        var pageModel = ReadWebFile(
            "Pages",
            "Admin",
            "Games",
            "RoundTransitionSummary.cshtml.cs");

        Assert.Contains(
            "/Admin/Games/RoundTransitionSummary/${encodeURIComponent(gameId)}",
            guard,
            StringComparison.Ordinal);
        Assert.Contains("for (let attempt = 0; attempt < 40; attempt += 1)", guard);
        Assert.Contains("response.status === 204", guard);
        Assert.Contains("mountFastRoundSummary", guard);
        Assert.Contains("data-round-transition-summary", page);
        Assert.Contains("GetCurrentRoundStandings()", pageModel);
        Assert.Contains("return new StatusCodeResult(204);", pageModel);
    }

    [Fact]
    public void Locked_or_mounted_round_intro_rejects_stale_host_refreshes()
    {
        var guard = ReadWebFile(
            "wwwroot",
            "js",
            "round-intro-refresh-guard.js");
        var navigation = ReadWebFile("wwwroot", "js", "site.js");

        Assert.Contains(
            "[data-host-gameplay-view] [data-game-intro-page]",
            guard,
            StringComparison.Ordinal);
        Assert.Contains(
            "const refresh = hostGameplay.refresh.bind(hostGameplay);",
            guard,
            StringComparison.Ordinal);
        Assert.Contains(
            "if (transitionLocked || isRoundIntroMounted())",
            guard,
            StringComparison.Ordinal);
        Assert.Contains(
            "hostGameplay.cancelPending?.();",
            guard,
            StringComparison.Ordinal);
        Assert.Contains(
            "return Promise.resolve(false);",
            guard,
            StringComparison.Ordinal);
        Assert.Contains(
            "window.BadWolfHostGameplay?.cancelPending?.();",
            navigation,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Stale_lobby_redirect_cannot_overwrite_a_newly_started_intro()
    {
        var guard = ReadWebFile(
            "wwwroot",
            "js",
            "round-intro-refresh-guard.js");

        Assert.Contains("const installHostFlowNavigationGuard = () =>", guard);
        Assert.Contains(
            "hostFlowNavigation.navigate = (...args) =>",
            guard,
            StringComparison.Ordinal);
        Assert.Contains(
            "transitionLocked &&",
            guard,
            StringComparison.Ordinal);
        Assert.Contains(
            "!leavingIntro &&",
            guard,
            StringComparison.Ordinal);
        Assert.Contains(
            "isLobbyTarget(args[0])",
            guard,
            StringComparison.Ordinal);
        Assert.Contains(
            "return Promise.resolve(false);",
            guard,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Round_intro_lock_is_released_only_after_returning_to_the_lobby_view()
    {
        var guard = ReadWebFile(
            "wwwroot",
            "js",
            "round-intro-refresh-guard.js");

        Assert.Contains("let leavingIntro = false;", guard);
        Assert.Contains("const introLink = target.closest", guard);
        Assert.Contains("isLobbyTarget(targetUrl.href)", guard);
        Assert.Contains("leavingIntro = true;", guard);
        Assert.Contains("if (leavingIntro)", guard);
        Assert.Contains("unlockRoundTransition();", guard);
        Assert.Contains("badwolf:host-gameplay-updated", guard);
        Assert.Contains("badwolf:host-shell-mounted", guard);
    }

    [Fact]
    public void Round_intro_guard_loads_host_context_menu_stability_asset()
    {
        var guard = ReadWebFile(
            "wwwroot",
            "js",
            "round-intro-refresh-guard.js");

        Assert.Contains(
            "/js/host-context-menu-stability.js?v=4",
            guard,
            StringComparison.Ordinal);
        Assert.Contains(
            "badWolfHostContextMenuStabilityLoaderInstalled",
            guard,
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
