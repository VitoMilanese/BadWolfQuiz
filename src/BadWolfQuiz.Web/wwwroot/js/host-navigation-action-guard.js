(() => {
    "use strict";

    if (window.badWolfHostNavigationActionGuardInitialized) {
        return;
    }

    window.badWolfHostNavigationActionGuardInitialized = true;

    const resolvedQuestionSelector =
        "[data-host-gameplay-board] a[data-question-resolved]";
    const reviewActionSelector = ".question-review-actions a[href]";
    const historyBackSelector = "[data-answer-history-back-to-game]";
    const guardedLinkSelector = [
        resolvedQuestionSelector,
        reviewActionSelector,
        historyBackSelector
    ].join(",");
    const guardStyleId = "host-navigation-action-guard-style";
    const busyDelayMilliseconds = 250;
    const safetyTimeoutMilliseconds = 15000;

    let activeNavigation = null;
    let busyDelayHandle = 0;
    let safetyHandle = 0;
    let errorObserver = null;
    let busyOverlayOwned = false;
    let lockedControls = [];

    const ensureStyles = () => {
        if (document.getElementById(guardStyleId)) {
            return;
        }

        const style = document.createElement("style");
        style.id = guardStyleId;
        style.textContent = `
[data-navigation-guard-busy="true"] {
    cursor: wait !important;
}

[data-host-gameplay-board][data-navigation-guard-busy="true"] .host-board-question,
.question-review-actions[data-navigation-guard-busy="true"] a {
    pointer-events: none;
}

a[data-navigation-guard-busy="true"],
.question-review-actions[data-navigation-guard-busy="true"] a[aria-busy="true"] {
    opacity: 0.68;
}`;
        document.head.append(style);
    };

    const isPlainNavigationClick = (event, link) =>
        event.button === 0 &&
        !event.altKey &&
        !event.ctrlKey &&
        !event.metaKey &&
        !event.shiftKey &&
        !link.hasAttribute("download") &&
        (!link.target || link.target === "_self");

    const rememberAttribute = (element, name) => ({
        element,
        name,
        value: element.getAttribute(name)
    });

    const rememberButton = button => ({
        element: button,
        disabled: button.disabled
    });

    const restoreLockedControls = () => {
        for (const state of lockedControls) {
            if (!state.element?.isConnected) {
                continue;
            }

            if ("disabled" in state) {
                state.element.disabled = state.disabled;
                continue;
            }

            if (state.value === null) {
                state.element.removeAttribute(state.name);
            } else {
                state.element.setAttribute(state.name, state.value);
            }
        }
        lockedControls = [];
    };

    const lockAnchor = (link, pressed) => {
        lockedControls.push(
            rememberAttribute(link, "aria-disabled"),
            rememberAttribute(link, "aria-busy"),
            rememberAttribute(link, "tabindex"),
            rememberAttribute(link, "data-navigation-guard-busy"));

        link.setAttribute("aria-disabled", "true");
        link.setAttribute("tabindex", "-1");
        link.dataset.navigationGuardBusy = "true";
        if (pressed) {
            link.setAttribute("aria-busy", "true");
        }
    };

    const lockBoard = pressedLink => {
        const board = document.querySelector("[data-host-gameplay-board]");
        if (!(board instanceof HTMLElement)) {
            lockAnchor(pressedLink, true);
            return;
        }

        lockedControls.push(
            rememberAttribute(board, "aria-busy"),
            rememberAttribute(board, "data-navigation-guard-busy"));
        board.setAttribute("aria-busy", "true");
        board.dataset.navigationGuardBusy = "true";

        board.querySelectorAll(".host-board-question").forEach(control => {
            if (control instanceof HTMLButtonElement) {
                lockedControls.push(rememberButton(control));
                control.disabled = true;
                return;
            }

            if (control instanceof HTMLAnchorElement) {
                lockAnchor(control, control === pressedLink);
            }
        });
    };

    const lockReviewActions = pressedLink => {
        const actions = pressedLink.closest(".question-review-actions");
        if (!(actions instanceof HTMLElement)) {
            lockAnchor(pressedLink, true);
            return;
        }

        lockedControls.push(
            rememberAttribute(actions, "data-navigation-guard-busy"));
        actions.dataset.navigationGuardBusy = "true";

        actions.querySelectorAll("a[href]").forEach(link => {
            if (link instanceof HTMLAnchorElement) {
                lockAnchor(link, link === pressedLink);
            }
        });
    };

    const lockHistoryBack = link => {
        lockAnchor(link, true);
    };

    const clearTimersAndObserver = () => {
        if (busyDelayHandle !== 0) {
            window.clearTimeout(busyDelayHandle);
            busyDelayHandle = 0;
        }
        if (safetyHandle !== 0) {
            window.clearTimeout(safetyHandle);
            safetyHandle = 0;
        }
        errorObserver?.disconnect();
        errorObserver = null;
    };

    const releaseNavigation = () => {
        clearTimersAndObserver();
        restoreLockedControls();
        activeNavigation = null;

        if (busyOverlayOwned) {
            busyOverlayOwned = false;
            window.BadWolfBusy?.hide?.();
        }
    };

    const scheduleBusyIndicator = () => {
        busyDelayHandle = window.setTimeout(() => {
            busyDelayHandle = 0;
            busyOverlayOwned = window.BadWolfBusy?.show?.() === true;
        }, busyDelayMilliseconds);
    };

    const observeGameplayErrors = () => {
        const errorTarget = document.getElementById("game-board-error");
        if (!errorTarget) {
            return;
        }

        errorObserver = new MutationObserver(() => {
            if (!errorTarget.hidden && errorTarget.textContent?.trim()) {
                releaseNavigation();
            }
        });
        errorObserver.observe(errorTarget, {
            attributes: true,
            childList: true,
            subtree: true,
            attributeFilter: ["hidden", "class"]
        });
    };

    const beginNavigation = (link, kind, showBusyLater) => {
        if (activeNavigation !== null) {
            return false;
        }

        activeNavigation = {
            link,
            kind,
            href: link.href
        };

        if (kind === "resolved") {
            lockBoard(link);
            observeGameplayErrors();
        } else if (kind === "review") {
            lockReviewActions(link);
            observeGameplayErrors();
        } else {
            lockHistoryBack(link);
        }

        if (showBusyLater) {
            scheduleBusyIndicator();
        }

        safetyHandle = window.setTimeout(
            releaseNavigation,
            safetyTimeoutMilliseconds);
        return true;
    };

    const blockDuplicate = event => {
        event.preventDefault();
        event.stopImmediatePropagation();
    };

    const navigateBackToGame = link => {
        const busyApi = window.BadWolfBusy;
        if (busyApi?.navigate) {
            const wasBusy = busyApi.isBusy === true;
            const accepted = busyApi.navigate(link.href);
            if (!accepted) {
                releaseNavigation();
                return;
            }

            busyOverlayOwned = !wasBusy;
            return;
        }

        try {
            window.location.assign(link.href);
        } catch (error) {
            console.error("Return-to-game navigation failed.", error);
            releaseNavigation();
        }
    };

    document.addEventListener("click", event => {
        const target = event.target instanceof Element
            ? event.target.closest(guardedLinkSelector)
            : null;
        if (!(target instanceof HTMLAnchorElement) ||
            !isPlainNavigationClick(event, target)) {
            return;
        }

        if (activeNavigation !== null) {
            blockDuplicate(event);
            return;
        }

        if (target.matches(historyBackSelector)) {
            blockDuplicate(event);
            if (beginNavigation(target, "history", false)) {
                navigateBackToGame(target);
            }
            return;
        }

        const kind = target.matches(resolvedQuestionSelector)
            ? "resolved"
            : "review";
        beginNavigation(target, kind, true);
        // The first gameplay click is intentionally allowed to continue so the
        // existing soft-navigation handler remains authoritative.
    }, true);

    document.addEventListener("keydown", event => {
        if (event.key !== "Escape" ||
            event.repeat ||
            event.defaultPrevented ||
            event.altKey ||
            event.ctrlKey ||
            event.metaKey ||
            event.shiftKey ||
            document.querySelector("dialog[open]")) {
            return;
        }

        const backToGame = document.querySelector(historyBackSelector);
        if (!(backToGame instanceof HTMLAnchorElement)) {
            return;
        }

        event.preventDefault();
        event.stopImmediatePropagation();
        backToGame.click();
    }, true);

    document.addEventListener("badwolf:host-gameplay-updated", () => {
        if (!activeNavigation || activeNavigation.kind === "history") {
            return;
        }

        if (activeNavigation.kind === "resolved") {
            if (document.querySelector(".question-review-preview")) {
                releaseNavigation();
            }
            return;
        }

        if (!activeNavigation.link.isConnected) {
            releaseNavigation();
        }
    });

    window.addEventListener("pageshow", () => {
        if (activeNavigation) {
            releaseNavigation();
        }
    });

    ensureStyles();
})();
