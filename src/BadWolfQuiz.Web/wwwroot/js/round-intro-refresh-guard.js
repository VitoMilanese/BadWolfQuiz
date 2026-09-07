(() => {
    "use strict";

    if (window.badWolfRoundIntroRefreshGuardInitialized) {
        return;
    }

    window.badWolfRoundIntroRefreshGuardInitialized = true;

    const introSelector =
        "[data-host-gameplay-view] [data-game-intro-page]";
    const summarySelector =
        "[data-host-gameplay-view] .round-summary";
    const introTransitionHandlers = new Set([
        "AdvanceRound",
        "PreviousRound",
        "ReturnToUnfinishedRound",
        "Advance",
        "Previous",
        "ReturnToUnfinished"
    ]);
    const summaryPreparationHandlers = new Set([
        "ForceAdvanceRound",
        "ForceAdvance",
        "PreviousRound",
        "Previous",
        "ReturnToUnfinishedRound",
        "ReturnToUnfinished"
    ]);

    let transitionLocked = false;
    let leavingIntro = false;
    let summaryProbeSequence = 0;

    const isRoundIntroMounted = () =>
        document.querySelector(introSelector) !== null;

    const hasPlayerCards = () =>
        document.querySelector(
            ".host-game-board .scoreboard-player[data-player-id]") !== null;

    const getFormHandler = form => {
        if (!(form instanceof HTMLFormElement) || !form.action) {
            return null;
        }

        try {
            return new URL(form.action, window.location.href)
                .searchParams.get("handler");
        } catch {
            return null;
        }
    };

    const isDirectReturnToIntro = (form, handler) => {
        if (handler !== "ReturnToUnfinishedRound" &&
            handler !== "ReturnToUnfinished") {
            return false;
        }

        return form.querySelector(
            'input[name="skipLeaderboard"][value="true"]') !== null;
    };

    const shouldLockRoundSubmit = (form, handler) => {
        if (!(form instanceof HTMLFormElement) ||
            !handler ||
            !introTransitionHandlers.has(handler)) {
            return false;
        }

        if (form.closest(summarySelector)) {
            return true;
        }

        if (isDirectReturnToIntro(form, handler)) {
            return true;
        }

        return !hasPlayerCards();
    };

    const cancelPendingHostRefresh = () =>
        window.BadWolfHostGameplay?.cancelPending?.();

    const lockRoundTransition = () => {
        transitionLocked = true;
        leavingIntro = false;
        cancelPendingHostRefresh();
    };

    const unlockRoundTransition = () => {
        transitionLocked = false;
        leavingIntro = false;
    };

    const installHostRefreshGuard = () => {
        const hostGameplay = window.BadWolfHostGameplay;
        if (!hostGameplay ||
            hostGameplay.roundIntroRefreshGuardInstalled === true ||
            typeof hostGameplay.refresh !== "function") {
            return false;
        }

        const refresh = hostGameplay.refresh.bind(hostGameplay);

        hostGameplay.refresh = (...args) => {
            if (transitionLocked || isRoundIntroMounted()) {
                hostGameplay.cancelPending?.();
                return Promise.resolve(false);
            }

            return refresh(...args);
        };

        hostGameplay.roundIntroRefreshGuardInstalled = true;
        return true;
    };

    const isLobbyTarget = url => {
        try {
            return new URL(url, window.location.href)
                .pathname
                .toLowerCase()
                .includes("/admin/games/lobby/");
        } catch {
            return false;
        }
    };

    const installHostFlowNavigationGuard = () => {
        const hostFlowNavigation = window.BadWolfHostFlowNavigation;
        if (!hostFlowNavigation ||
            hostFlowNavigation.roundIntroNavigationGuardInstalled === true ||
            typeof hostFlowNavigation.navigate !== "function") {
            return false;
        }

        const navigate = hostFlowNavigation.navigate.bind(hostFlowNavigation);
        hostFlowNavigation.navigate = (...args) => {
            if (transitionLocked &&
                !leavingIntro &&
                isLobbyTarget(args[0])) {
                return Promise.resolve(false);
            }

            return navigate(...args);
        };

        hostFlowNavigation.roundIntroNavigationGuardInstalled = true;
        return true;
    };

    const currentGameId = () =>
        document.querySelector(".host-game-board[data-game-id]")
            ?.dataset.gameId ?? null;

    const delay = milliseconds => new Promise(resolve =>
        window.setTimeout(resolve, milliseconds));

    const mountFastRoundSummary = markup => {
        const parsed = new DOMParser().parseFromString(markup, "text/html");
        const summary = parsed.querySelector("[data-round-transition-summary]");
        const view = document.querySelector("[data-host-gameplay-view]");
        if (!summary || !view || isRoundIntroMounted()) {
            return false;
        }

        if (view.querySelector(".round-summary")) {
            return true;
        }

        view.replaceChildren(document.importNode(summary, true));

        const board = document.querySelector("[data-host-gameplay-board]");
        if (board) {
            board.hidden = true;
        }

        const actionMenu = document.querySelector(".board-action-menu");
        if (actionMenu) {
            actionMenu.hidden = true;
        }

        document.dispatchEvent(new CustomEvent(
            window.BadWolfHostGameplay?.updatedEventName ??
                "badwolf:host-gameplay-updated"));
        return true;
    };

    const probeRoundTransitionSummary = async () => {
        const gameId = currentGameId();
        if (!gameId || !hasPlayerCards()) {
            return false;
        }

        const probeSequence = ++summaryProbeSequence;
        const url = `/Admin/Games/RoundTransitionSummary/${encodeURIComponent(gameId)}`;

        for (let attempt = 0; attempt < 40; attempt += 1) {
            if (probeSequence !== summaryProbeSequence ||
                transitionLocked ||
                isRoundIntroMounted()) {
                return false;
            }

            try {
                const response = await fetch(url, {
                    method: "GET",
                    headers: {
                        "X-Requested-With": "XMLHttpRequest"
                    },
                    cache: "no-store"
                });

                if (response.status === 204) {
                    await delay(35);
                    continue;
                }

                if (!response.ok) {
                    return false;
                }

                if (mountFastRoundSummary(await response.text())) {
                    return true;
                }
            } catch {
                return false;
            }

            await delay(35);
        }

        return false;
    };

    const startSummaryProbeFor = (form, handler) => {
        if (!(form instanceof HTMLFormElement) ||
            form.closest(summarySelector) ||
            !handler ||
            !summaryPreparationHandlers.has(handler)) {
            return;
        }

        void probeRoundTransitionSummary();
    };

    const syncRoundIntroRefreshGuard = () => {
        installHostRefreshGuard();
        installHostFlowNavigationGuard();

        if (isRoundIntroMounted()) {
            transitionLocked = true;
            summaryProbeSequence += 1;
            cancelPendingHostRefresh();
            return;
        }

        if (leavingIntro) {
            unlockRoundTransition();
        }
    };

    window.addEventListener("submit", event => {
        const form = event.target instanceof HTMLFormElement
            ? event.target
            : null;
        const handler = getFormHandler(form);

        startSummaryProbeFor(form, handler);

        if (shouldLockRoundSubmit(form, handler)) {
            summaryProbeSequence += 1;
            lockRoundTransition();
        }
    }, true);

    window.addEventListener("click", event => {
        const target = event.target instanceof Element ? event.target : null;
        if (!target) {
            return;
        }

        if (target.closest("[data-category-preview-url]")) {
            summaryProbeSequence += 1;
            lockRoundTransition();
            return;
        }

        if (target.closest("[data-confirm-force-advance-round]")) {
            if (hasPlayerCards()) {
                void probeRoundTransitionSummary();
            } else {
                summaryProbeSequence += 1;
                lockRoundTransition();
            }
            return;
        }

        const introLink = target.closest(`${introSelector} a[href]`);
        if (!(introLink instanceof HTMLAnchorElement)) {
            return;
        }

        const targetUrl = new URL(introLink.href, window.location.href);
        if (isLobbyTarget(targetUrl.href)) {
            leavingIntro = true;
            summaryProbeSequence += 1;
            cancelPendingHostRefresh();
        }
    }, true);

    syncRoundIntroRefreshGuard();
    document.addEventListener(
        "badwolf:host-gameplay-updated",
        syncRoundIntroRefreshGuard);
    document.addEventListener(
        "badwolf:host-shell-mounted",
        syncRoundIntroRefreshGuard);

    if (!window.badWolfHostContextMenuStabilityLoaderInstalled) {
        window.badWolfHostContextMenuStabilityLoaderInstalled = true;
        const script = document.createElement("script");
        script.src = "/js/host-context-menu-stability.js?v=4";
        script.async = false;
        script.dataset.hostContextMenuStability = "";
        document.head.appendChild(script);
    }
})();
