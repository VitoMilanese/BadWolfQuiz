(() => {
    if (window.badWolfHostNormalWagerLeftStackInitialized) {
        return;
    }

    window.badWolfHostNormalWagerLeftStackInitialized = true;

    const summarySelector =
        ".host-game-board .current-question-summary.wager-mode:not(.anonymous-shared-wager-mode):has(> .wager-entry-panel)";
    let frameHandle = 0;

    const arrangeSummary = summary => {
        const panel = summary.querySelector(":scope > .wager-entry-panel");
        if (!(panel instanceof HTMLElement)) {
            return;
        }

        let context = panel.querySelector(":scope > .host-normal-wager-context");
        const heading = summary.querySelector(":scope > .current-question-heading") ??
            context?.querySelector(":scope > .current-question-heading");
        const playerSummary = panel.querySelector(":scope > .wager-player-summary") ??
            context?.querySelector(":scope > .wager-player-summary");
        const wagerForm = panel.querySelector(":scope > .question-wager-form");

        if (!(heading instanceof HTMLElement) ||
            !(playerSummary instanceof HTMLElement) ||
            !(wagerForm instanceof HTMLFormElement)) {
            return;
        }

        if (!(context instanceof HTMLElement)) {
            context = document.createElement("div");
            context.className = "host-normal-wager-context";
            panel.insertBefore(context, panel.firstChild);
        }

        if (heading.parentElement !== context) {
            context.appendChild(heading);
        }
        if (playerSummary.parentElement !== context) {
            context.appendChild(playerSummary);
        }
    };

    const arrangeAll = () => {
        frameHandle = 0;
        document.querySelectorAll(summarySelector).forEach(arrangeSummary);
    };

    const scheduleArrange = () => {
        if (frameHandle !== 0) {
            return;
        }

        frameHandle = window.requestAnimationFrame(arrangeAll);
    };

    const observer = typeof MutationObserver === "function"
        ? new MutationObserver(scheduleArrange)
        : null;
    observer?.observe(document.body, {
        childList: true,
        subtree: true
    });

    document.addEventListener("badwolf:host-gameplay-updated", scheduleArrange);
    window.addEventListener("pageshow", scheduleArrange);

    arrangeAll();
})();
