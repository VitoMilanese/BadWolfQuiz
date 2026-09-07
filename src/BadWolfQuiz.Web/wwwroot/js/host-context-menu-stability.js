(() => {
    "use strict";

    if (window.badWolfHostContextMenuStabilityInstalled) {
        return;
    }

    window.badWolfHostContextMenuStabilityInstalled = true;

    const menuSelector =
        "#category-context-menu, #question-context-menu, #player-score-context-menu";
    const openingGestureGraceMilliseconds = 500;
    const visibilityWatchdogMilliseconds = 25;

    let activeMenu = null;
    let activeMenuId = "";
    let activeOpenedAt = 0;
    let keepActiveMenuOpen = false;
    let visibilityWatchdogHandle = null;

    const menuForTarget = target => {
        if (!(target instanceof Element)) {
            return null;
        }

        const category = target.closest("[data-category-context]");
        if (category?.dataset.hasAvailableQuestions === "true") {
            return document.getElementById("category-context-menu");
        }

        if (target.closest(
            ".host-board-question.status-available[data-question-context]")) {
            return document.getElementById("question-context-menu");
        }

        if (target.closest(
            ".host-board-question.status-resolved[data-question-resolved]") &&
            window.badWolfHostQuestionControlsInstalled) {
            return document.getElementById("question-context-menu");
        }

        const player = target.closest(".scoreboard-player[data-player-id]");
        if (player && !player.hasAttribute("data-host-card")) {
            return document.getElementById("player-score-context-menu");
        }

        return null;
    };

    const hideOtherMenus = expectedMenu => {
        for (const menu of document.querySelectorAll(menuSelector)) {
            if (menu !== expectedMenu) {
                menu.hidden = true;
            }
        }
    };

    const stopVisibilityWatchdog = () => {
        if (visibilityWatchdogHandle === null) {
            return;
        }

        window.clearInterval(visibilityWatchdogHandle);
        visibilityWatchdogHandle = null;
    };

    const releaseActiveMenu = () => {
        stopVisibilityWatchdog();
        keepActiveMenuOpen = false;
        activeMenu = null;
        activeMenuId = "";
        activeOpenedAt = 0;
    };

    const isOpeningGestureGraceActive = () =>
        activeMenu !== null &&
        keepActiveMenuOpen &&
        performance.now() - activeOpenedAt <= openingGestureGraceMilliseconds;

    const resolveActiveMenu = () => {
        if (activeMenu?.isConnected) {
            return activeMenu;
        }

        if (!activeMenuId) {
            activeMenu = null;
            return null;
        }

        const replacement = document.getElementById(activeMenuId);
        activeMenu = replacement instanceof HTMLElement ? replacement : null;
        return activeMenu;
    };

    const ensureActiveMenuVisible = () => {
        if (!keepActiveMenuOpen) {
            return;
        }

        const menu = resolveActiveMenu();
        if (!menu) {
            return;
        }

        if (menu.hidden) {
            menu.hidden = false;
        }
    };

    const hideAndReleaseActiveMenu = () => {
        const menu = resolveActiveMenu();
        if (menu) {
            menu.hidden = true;
        }
        releaseActiveMenu();
    };

    const startVisibilityWatchdog = () => {
        stopVisibilityWatchdog();
        visibilityWatchdogHandle = window.setInterval(
            ensureActiveMenuVisible,
            visibilityWatchdogMilliseconds);
    };

    const activateMenu = menu => {
        if (!(menu instanceof HTMLElement)) {
            return;
        }

        activeMenu = menu;
        activeMenuId = menu.id;
        activeOpenedAt = performance.now();
        keepActiveMenuOpen = true;
        hideOtherMenus(menu);
        startVisibilityWatchdog();

        queueMicrotask(ensureActiveMenuVisible);
        window.requestAnimationFrame(ensureActiveMenuVisible);
        window.setTimeout(ensureActiveMenuVisible, 0);
    };

    const observeMenus = () => {
        for (const menu of document.querySelectorAll(menuSelector)) {
            if (menu.dataset.contextMenuStabilityObserved === "true") {
                continue;
            }

            menu.dataset.contextMenuStabilityObserved = "true";
            new MutationObserver(() => {
                if (menu.id === activeMenuId && keepActiveMenuOpen && menu.hidden) {
                    queueMicrotask(ensureActiveMenuVisible);
                }
            }).observe(menu, {
                attributes: true,
                attributeFilter: ["hidden"]
            });
        }

        ensureActiveMenuVisible();
    };

    window.addEventListener("pointerdown", event => {
        const target = event.target instanceof Element ? event.target : null;

        if (event.button === 2) {
            if (menuForTarget(target) || target?.closest(menuSelector)) {
                event.stopPropagation();
            }
            return;
        }

        if (!activeMenu) {
            return;
        }

        const menu = resolveActiveMenu();
        if (!menu) {
            releaseActiveMenu();
            return;
        }

        if (target && menu.contains(target)) {
            // Keep the menu alive through pointerdown. Releasing it here lets
            // legacy outside-click listeners hide the menu before the browser
            // can dispatch the action's click event.
            event.stopPropagation();
            ensureActiveMenuVisible();
            return;
        }

        if (isOpeningGestureGraceActive()) {
            // Some browsers/touchpads emit a trailing synthetic pointerdown after
            // the contextmenu event. Do not let that same gesture reach the
            // legacy outside-click dismissers.
            event.stopPropagation();
            ensureActiveMenuVisible();
            return;
        }

        if (event.button === 0) {
            event.stopPropagation();
            hideAndReleaseActiveMenu();
        }
    }, true);

    window.addEventListener("click", event => {
        if (!activeMenu) {
            return;
        }

        const target = event.target instanceof Element ? event.target : null;
        const menu = resolveActiveMenu();
        if (!menu) {
            releaseActiveMenu();
            return;
        }

        if (target && menu.contains(target)) {
            // The pointer sequence has completed. Stop enforcing visibility now,
            // then let the existing menu action handler receive this click and
            // open its dialog or run its command normally.
            releaseActiveMenu();
            return;
        }

        hideAndReleaseActiveMenu();
    }, true);

    window.addEventListener("contextmenu", event => {
        const target = event.target instanceof Element ? event.target : null;
        const menu = menuForTarget(target);
        if (!menu) {
            hideAndReleaseActiveMenu();
            return;
        }

        activateMenu(menu);
    }, true);

    window.addEventListener("keydown", event => {
        if (event.key === "Escape") {
            hideAndReleaseActiveMenu();
        }
    }, true);

    // Legacy menu controllers still hide on window blur/resize. Those events are
    // not user intent to dismiss the menu, so immediately restore the active menu.
    window.addEventListener("blur", () => {
        if (keepActiveMenuOpen) {
            window.requestAnimationFrame(ensureActiveMenuVisible);
        }
    }, true);

    window.addEventListener("resize", () => {
        if (keepActiveMenuOpen) {
            window.requestAnimationFrame(ensureActiveMenuVisible);
        }
    }, true);

    observeMenus();
    new MutationObserver(observeMenus).observe(document.documentElement, {
        childList: true,
        subtree: true
    });
})();
